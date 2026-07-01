#!/usr/bin/env python3
"""
MoonHarvest Detection Stream — HSV-First v7c
=============================================
Pipeline: WB+CLAHE → HSV segmentasi (dikalibrasi untuk sawah 15 hari) →
          connected-component regions → ONNX refinement → YOLO-det fusion →
          NMS v7c (per_cls=0.20, cross=0.30, min_area=600) → FHI → JSON stdout

Protocol output (stdout):
  {"type": "frame",     "data": "<base64_jpeg>"}
  {"type": "detection", "data": {"count": N, "summary": "...", "classes": {...}, "fhi": F}}
  {"type": "end",       "data": "Video stream ended"}
  {"type": "error",     "data": "..."}
  {"type": "info",      "data": "..."}

GCS class keys: Healthy / Stress / Drought / Bare Soil
"""

import argparse, base64, json, os, queue, signal, sys, threading, time
import cv2
import numpy as np
from pathlib import Path
from collections import deque

DEMO_MODE = os.environ.get("MOONHARVEST_DEMO", "0") == "1"

# Mapping display label → GCS key (read by C# EdgeModePage / DashboardPage)
DISPLAY_TO_GCS = {
    "Lush Green":              "Healthy",
    "Inconsistent Growth":     "Stress",
    "Drought / Severe Stress": "Drought",
    "Bare Soil / Gap":         "Bare Soil",
}


# ── Warna display ──────────────────────────────────────────────────────────────
COLORS = {
    "Lush Green":              ( 50, 210,  50),
    "Inconsistent Growth":     (  0, 200, 255),
    "Drought / Severe Stress": (  0,  90, 255),
    "Bare Soil / Gap":         (140, 140, 140),
}

SEVERITY = {
    "Lush Green":              0.00,
    "Inconsistent Growth":     0.45,
    "Drought / Severe Stress": 0.80,
    "Bare Soil / Gap":         0.10,
}

# ── Mapping ONNX class → display (v5 model) ────────────────────────────────────
ONNX_MAP = {
    "healthy_crop":  "Lush Green",
    "stressed_crop": "Inconsistent Growth",
    "drought_stress":"Drought / Severe Stress",
    "bare_soil":     "Bare Soil / Gap",
    "lush_green":    "Lush Green",
    "well_irrigated":"Lush Green",
    "inconsistent_growth":"Inconsistent Growth",
    "soil_issues":   "Bare Soil / Gap",
    "disease":       "Inconsistent Growth",
    "pest":          "Inconsistent Growth",
}

# Kelas ONNX yang boleh override label HSV per zona
ONNX_COMPAT = {
    "lush":    {"Lush Green"},
    "stress":  {"Lush Green", "Inconsistent Growth"},
    "drought": {"Drought / Severe Stress", "Inconsistent Growth"},
    "soil":    {"Bare Soil / Gap"},
}

# ── HSV CONFIG dikalibrasi dari 15d.mp4 ────────────────────────────────────────
HSV_CFG = {
    "wb":          True,
    "clahe_clip":  2.0,
    "clahe_grid":  8,
    "shadow_v_max": 35,
    # ExG thresholds — diturunkan dari v3 karena 15d lebih pale
    "exg_veg_thr":     0.004,   # minimum vegetasi (sangat rendah untuk 15d)
    "exg_healthy_min": 0.028,   # dinaikkan dari 0.018 → 0.028 untuk kurangi false lush
    # Zona lush: H dipersempit (30-90), S dinaikkan (25+), exg>=0.028
    "lush":    {"h": [30, 90], "s_lo": 25, "v_lo": 50, "exg_min": 0.028},
    # Zona stress: exg_lo=0.0 agar area pucat (exg 0.000-0.004) tidak jatuh ke drought
    "stress":  {"h": [12, 105], "s_lo":  8, "s_hi": 80, "v_lo": 42, "exg_lo": 0.000, "exg_hi": 0.040},
    # Zona drought: sangat ketat — hanya piksel benar-benar kering tanpa klorofil sama sekali
    # H dipersempit [0,18], V dinaikkan ke 120, exg_hi=0.003, exg_remap=0.006 (per-region fallback)
    "drought": {"h": [ 0,  18], "s_lo": 18, "s_hi": 60, "v_lo": 120, "exg_hi": 0.003,
                "exg_remap_thr": 0.006},
    # Zona soil: S<=15, V>=75, exg<0.008
    "soil":    {"s_hi": 15, "v_lo": 75, "exg_hi": 0.008},
    # Morfologi
    "min_area_frac": 0.006,
    "max_area_frac": 0.20,      # filter box > 20% frame area (cegah lush raksasa)
    "max_regions":   20,
    "morph_k":       7,
    # Temporal EMA
    "ema_alpha":     0.30,
}

# ONNX confidence minimum untuk override HSV — tinggi agar HSV tetap dominan
ONNX_MIN_CONF  = 0.70
DISPLAY_MIN_CONF = 0.28

# ── Pre-processing ─────────────────────────────────────────────────────────────
def _wb(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean()+1e-6, g.mean()+1e-6, r.mean()+1e-6
    k = (mb+mg+mr)/3.0
    return cv2.merge([np.clip(b*(k/mb),0,255), np.clip(g*(k/mg),0,255), np.clip(r*(k/mr),0,255)]).astype(np.uint8)

def _preprocess(bgr, cfg):
    out = _wb(bgr) if cfg["wb"] else bgr.copy()
    hsv = cv2.cvtColor(out, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    cl = cv2.createCLAHE(clipLimit=cfg["clahe_clip"], tileGridSize=(cfg["clahe_grid"], cfg["clahe_grid"]))
    v  = cl.apply(v)
    return out, cv2.merge([h, s, v])

def _exg(bgr_f):
    tot = bgr_f.sum(axis=2) + 1e-6
    return (2*bgr_f[:,:,1] - bgr_f[:,:,0] - bgr_f[:,:,2]) / tot

# ── HSV Segmentasi ─────────────────────────────────────────────────────────────
def segment_regions(frame, cfg=HSV_CFG):
    h, w  = frame.shape[:2]
    scale = 0.5
    small = cv2.resize(frame, (int(w*scale), int(h*scale)))
    sh, sw = small.shape[:2]
    min_px = max(40, int(cfg["min_area_frac"] * sh * sw))

    proc, hsv_img = _preprocess(small, cfg)
    H   = hsv_img[:,:,0].astype(np.int32)
    S   = hsv_img[:,:,1].astype(np.int32)
    V   = hsv_img[:,:,2].astype(np.int32)
    exg = _exg(proc.astype(np.float32))

    shadow = V < cfg["shadow_v_max"]

    c = cfg["lush"]
    lush = ((H>=c["h"][0]) & (H<=c["h"][1]) & (S>=c["s_lo"]) &
            (V>=c["v_lo"]) & (exg>=c["exg_min"]) & ~shadow)

    c = cfg["stress"]
    stress = ((H>=c["h"][0]) & (H<=c["h"][1]) & (S>=c["s_lo"]) & (S<=c["s_hi"]) &
              (V>=c["v_lo"]) & (exg>=c["exg_lo"]) & (exg<c["exg_hi"]) &
              ~lush & ~shadow)

    c = cfg["drought"]
    drought = ((H>=c["h"][0]) & (H<=c["h"][1]) & (S>=c["s_lo"]) & (S<=c["s_hi"]) &
               (V>=c["v_lo"]) & (exg<c["exg_hi"]) & ~lush & ~stress & ~shadow)

    c = cfg["soil"]
    soil = ((S<=c["s_hi"]) & (V>=c["v_lo"]) & (exg<c["exg_hi"]) &
            ~lush & ~stress & ~drought & ~shadow)

    # v6: Untuk konteks sawah 15 hari (tanaman muda), zona coklat/kuning yang secara HSV
    # memenuhi kriteria "drought" lebih tepat dilabel "Inconsistent Growth" daripada
    # "Drought / Severe Stress", karena area tersebut mencerminkan pertumbuhan tidak
    # seragam / lahan belum tertutup tanaman, bukan kekeringan aktual.
    # Label "Drought / Severe Stress" hanya muncul jika secara ONNX dikonfirmasi.
    zones = [
        (lush,    "lush",    "Lush Green"),
        (stress,  "stress",  "Inconsistent Growth"),
        (drought, "drought", "Inconsistent Growth"),   # v6: relabel untuk 15d context
        (soil,    "soil",    "Bare Soil / Gap"),
    ]

    k  = cfg["morph_k"] | 1
    k2 = max(3, k//2) | 1
    ke = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    ks = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k2, k2))

    max_px = int(cfg.get("max_area_frac", 1.0) * sh * sw)
    regions = []
    for mask_bool, zone_key, zone_disp in zones:
        mask = mask_bool.astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,  ks)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, ke)
        n, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = int(stats[i, cv2.CC_STAT_AREA])
            if area < min_px or area > max_px:
                continue
            x  = int(stats[i, cv2.CC_STAT_LEFT]  / scale)
            y  = int(stats[i, cv2.CC_STAT_TOP]   / scale)
            bw = int(stats[i, cv2.CC_STAT_WIDTH]  / scale)
            bh = int(stats[i, cv2.CC_STAT_HEIGHT] / scale)
            x2, y2 = min(x+bw, w-1), min(y+bh, h-1)
            if x2 <= x or y2 <= y:
                continue
            # v5: per-region ExG remap — jika drought tapi rata-rata ExG region > threshold,
            # downgrade ke Inconsistent Growth (masih ada kandungan klorofil = stress, bukan kering total)
            actual_zone_disp = zone_disp
            actual_zone_key  = zone_key
            if zone_key == "drought":
                remap_thr = cfg["drought"].get("exg_remap_thr", 0.006)
                rx1 = int(stats[i, cv2.CC_STAT_LEFT])
                ry1 = int(stats[i, cv2.CC_STAT_TOP])
                rx2 = rx1 + int(stats[i, cv2.CC_STAT_WIDTH])
                ry2 = ry1 + int(stats[i, cv2.CC_STAT_HEIGHT])
                region_mask = (labels[ry1:ry2, rx1:rx2] == i)
                region_exg  = exg[ry1:ry2, rx1:rx2]
                mean_exg    = float(region_exg[region_mask].mean()) if region_mask.any() else 0.0
                if mean_exg > remap_thr:
                    actual_zone_disp = "Inconsistent Growth"
                    actual_zone_key  = "stress"
            regions.append({"bbox":(x,y,x2,y2), "hsv_zone":actual_zone_key,
                             "hsv_display":actual_zone_disp, "area":area})

    regions.sort(key=lambda r: -r["area"])
    return regions[:cfg["max_regions"]]

# ── ONNX Classifier ────────────────────────────────────────────────────────────
class ONNXClassifier:
    def __init__(self, path):
        import onnxruntime as ort, ast
        self.sess  = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        self.iname = self.sess.get_inputs()[0].name
        meta = self.sess.get_modelmeta().custom_metadata_map
        raw  = meta.get("names", "{}")
        parsed = ast.literal_eval(raw)
        self.names = parsed if isinstance(parsed, dict) else {i:v for i,v in enumerate(parsed)}
        print(f"[ONNX] {Path(path).name}  kelas: {self.names}")

    def infer(self, crops, imgsz=160, batch=32):
        if not crops: return []
        batch_list = []
        for c in crops:
            img = cv2.resize(c, (imgsz, imgsz)).astype(np.float32) / 255.0
            batch_list.append(img.transpose(2,0,1))
        out = []
        for s in range(0, len(batch_list), batch):
            arr    = np.stack(batch_list[s:s+batch])
            logits = self.sess.run(None, {self.iname: arr})[0]
            exp    = np.exp(logits - logits.max(axis=1, keepdims=True))
            probs  = exp / exp.sum(axis=1, keepdims=True)
            for row in probs:
                cid  = int(np.argmax(row))
                conf = float(row[cid])
                out.append((self.names.get(cid, str(cid)), conf))
        return out

class PyTorchFallback:
    def __init__(self, path):
        from ultralytics import YOLO
        self.model = YOLO(str(path))
        self.names = self.model.names
        print(f"[PT] {Path(path).name}  kelas: {self.names}")

    def infer(self, crops, imgsz=160, batch=32):
        out = []
        for c in crops:
            r = self.model(c, verbose=False, imgsz=imgsz)[0]
            p = r.probs.data.cpu().numpy()
            i = int(np.argmax(p))
            n = self.names.get(i,str(i)) if isinstance(self.names,dict) else self.names[i]
            out.append((n, float(p[i])))
        return out

def load_classifier(path):
    pt   = Path(path)
    onnx = pt.with_suffix(".onnx")
    if onnx.exists(): return ONNXClassifier(onnx)
    print("[WARN] ONNX tidak ditemukan, pakai PyTorch")
    return PyTorchFallback(pt)

# ── YOLO Detector (moonharvest-uav-det.onnx) ──────────────────────────────────
# Mapping kelas YOLO-det → display label
YOLO_DET_MAP = {
    "crop":                       "Lush Green",
    "crop_row":                   "Lush Green",
    "weed":                       "Inconsistent Growth",
    # disease_stress_vegetation di-map ke Inconsistent Growth, bukan Drought
    # karena di video sawah 15d area ini umumnya masih ada kandungan hijau
    "disease_stress_vegetation":  "Inconsistent Growth",
}

class YOLODetector:
    """
    Wrapper untuk model deteksi YOLOv8 ONNX (output shape [1, 4+nc, anchors]).
    Hanya mengambil box + kelas dengan conf >= threshold, kemudian scale kembali
    ke koordinat frame asli.

    YOLO-det bersifat SUPPLEMENTARY — confidence-nya dibatasi di bawah HSV (0.60)
    agar tidak menekan deteksi HSV dalam cross-class NMS.
    conf_thr dinaikkan ke 0.45 untuk mengurangi false positive disease_stress.
    """
    def __init__(self, path, conf_thr=0.45):
        import onnxruntime as ort, ast
        self.sess     = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        inp           = self.sess.get_inputs()[0]
        self.iname    = inp.name
        # Input shape: [1, 3, H, W] — gunakan ukuran dinamis atau default 640
        shp           = inp.shape
        self.imgsz    = (shp[2] if isinstance(shp[2], int) and shp[2] > 0 else 640,
                         shp[3] if isinstance(shp[3], int) and shp[3] > 0 else 640)
        meta          = self.sess.get_modelmeta().custom_metadata_map
        raw           = meta.get("names", "{}")
        try:
            parsed    = ast.literal_eval(raw)
            self.names = parsed if isinstance(parsed, dict) else {i:v for i,v in enumerate(parsed)}
        except Exception:
            self.names = {0:"crop", 1:"weed", 2:"crop_row", 3:"disease_stress_vegetation"}
        self.conf_thr = conf_thr
        self.nc       = len(self.names)
        print(f"[YOLO-DET] {Path(path).name}  kelas: {self.names}  imgsz={self.imgsz}")

    def detect(self, frame):
        """
        Jalankan deteksi pada satu frame BGR.
        Return: list of {"bbox":(x1,y1,x2,y2), "class": display_label,
                          "conf": float, "area": int, "source": "yolo-det"}
        """
        oh, ow = frame.shape[:2]
        iw, ih = self.imgsz
        img    = cv2.resize(frame, (iw, ih)).astype(np.float32) / 255.0
        inp    = img.transpose(2, 0, 1)[None]          # [1,3,H,W]

        out    = self.sess.run(None, {self.iname: inp})[0]  # [1, 4+nc, anchors]
        out    = out[0]                                  # [4+nc, anchors]

        # YOLOv8 detect output: rows = [cx, cy, w, h, cls0, cls1, ...]
        # Transpose so each row is one anchor
        if out.shape[0] < out.shape[1]:
            preds = out.T                                # [anchors, 4+nc]
        else:
            preds = out                                  # already [anchors, 4+nc]

        dets = []
        for pred in preds:
            box_raw    = pred[:4]
            cls_scores = pred[4:4+self.nc]
            cid        = int(np.argmax(cls_scores))
            conf       = float(cls_scores[cid])
            if conf < self.conf_thr:
                continue

            # cx,cy,w,h in normalised [0,1] space (imgsz coords → normalise)
            cx, cy, bw, bh = box_raw
            # If values > 1, they are in pixel space of imgsz
            if cx > 1 or cy > 1:
                cx /= iw; cy /= ih; bw /= iw; bh /= ih

            x1 = int((cx - bw/2) * ow);  y1 = int((cy - bh/2) * oh)
            x2 = int((cx + bw/2) * ow);  y2 = int((cy + bh/2) * oh)
            x1, y1 = max(0, x1), max(0, y1)
            x2, y2 = min(ow-1, x2), min(oh-1, y2)
            if x2 <= x1 or y2 <= y1:
                continue

            cname  = self.names.get(cid, str(cid))
            label  = YOLO_DET_MAP.get(cname, "Inconsistent Growth")
            area   = (x2-x1) * (y2-y1)
            # YOLO-det confidence dibatasi < 0.58 agar HSV (0.60) tetap dominan
            # dalam cross-class NMS. Ini mencegah YOLO menindas deteksi HSV.
            yolo_conf = round(min(0.57, conf * 0.85), 3)
            dets.append({"bbox":(x1,y1,x2,y2), "class":label,
                         "conf": yolo_conf,
                         "area": area, "source": "yolo-det"})
        return dets

# ── Fusion HSV + ONNX ──────────────────────────────────────────────────────────
def fuse(frame, regions, classifier, imgsz=160):
    """
    HSV sebagai primary. ONNX hanya override jika:
    - confidence >= ONNX_MIN_CONF (0.70)
    - label ONNX kompatibel dengan zona HSV
    Jika tidak memenuhi syarat, label HSV dipertahankan dengan confidence HSV.
    """
    if not regions: return []
    crops = []
    for r in regions:
        x1,y1,x2,y2 = r["bbox"]
        c = frame[y1:y2, x1:x2]
        crops.append(c if c.size > 0 else np.zeros((8,8,3), np.uint8))

    onnx_res = classifier.infer(crops, imgsz=imgsz)
    dets = []
    for i, (oc, oconf) in enumerate(onnx_res):
        r = regions[i]
        od = ONNX_MAP.get(oc)
        compat = ONNX_COMPAT.get(r["hsv_zone"], set())

        if od and oconf >= ONNX_MIN_CONF and od in compat:
            # ONNX confident & kompatibel → pakai ONNX, sedikit boost
            label  = od
            conf   = min(1.0, oconf * 1.03)
            source = "onnx-confirmed"
        elif od and oconf >= 0.55 and od in compat:
            # ONNX moderat & kompatibel → blend HSV + ONNX
            label  = r["hsv_display"]
            conf   = max(0.52, oconf * 0.85)
            source = "hsv+onnx-blend"
        else:
            # HSV wins — lebih terpercaya secara fisika warna
            label  = r["hsv_display"]
            conf   = 0.60  # baseline confidence HSV
            source = "hsv-primary"

        x1,y1,x2,y2 = r["bbox"]
        dets.append({"bbox":(x1,y1,x2,y2), "class":label, "conf":round(conf,3),
                     "area":r["area"], "source":source})
    return dets

# ── NMS ────────────────────────────────────────────────────────────────────────
def _iou_matrix(boxes):
    """Hitung matriks IoU untuk semua pasangan box."""
    x1=boxes[:,0]; y1=boxes[:,1]; x2=boxes[:,2]; y2=boxes[:,3]
    a = np.maximum(0, x2-x1) * np.maximum(0, y2-y1)
    ix1=np.maximum(x1[:,None], x1[None,:]); iy1=np.maximum(y1[:,None], y1[None,:])
    ix2=np.minimum(x2[:,None], x2[None,:]); iy2=np.minimum(y2[:,None], y2[None,:])
    inter = np.maximum(0, ix2-ix1) * np.maximum(0, iy2-iy1)
    return inter / (a[:,None] + a[None,:] - inter + 1e-6)

def _iou_pair(b1, b2):
    """IoU antara dua box tunggal (tuple/list x1,y1,x2,y2)."""
    ix1 = max(b1[0], b2[0]); iy1 = max(b1[1], b2[1])
    ix2 = min(b1[2], b2[2]); iy2 = min(b1[3], b2[3])
    inter = max(0, ix2-ix1) * max(0, iy2-iy1)
    a1 = max(0, b1[2]-b1[0]) * max(0, b1[3]-b1[1])
    a2 = max(0, b2[2]-b2[0]) * max(0, b2[3]-b2[1])
    return inter / (a1 + a2 - inter + 1e-6)

def _hard_nms(dets, iou_thr):
    """Standard greedy NMS — hapus box ber-IoU > iou_thr dengan box conf lebih tinggi."""
    if not dets: return []
    sc   = np.array([d["conf"] for d in dets], np.float32)
    order = np.argsort(-sc)
    iou  = _iou_matrix(np.array([d["bbox"] for d in dets], np.float32))
    suppressed = np.zeros(len(dets), bool)
    keep = []
    for i in order:
        if suppressed[i]: continue
        keep.append(i)
        suppressed |= iou[i] > iou_thr
        suppressed[i] = False
    return [dets[i] for i in keep]

def _soft_nms(dets, iou_thr, sigma=0.5, min_conf=DISPLAY_MIN_CONF):
    """
    Soft-NMS (Gaussian decay) — daripada menghapus box langsung, turunkan
    confidence-nya secara eksponensial berdasarkan overlap. Cocok untuk cross-class
    suppression agar box di area perbatasan tidak hilang sama sekali.
    """
    if not dets: return []
    boxes = [dict(d) for d in dets]  # copy agar tidak ubah original
    result = []
    while boxes:
        # Ambil box conf tertinggi
        idx = int(np.argmax([b["conf"] for b in boxes]))
        best = boxes.pop(idx)
        result.append(best)
        # Decay conf box lain berdasarkan overlap
        remaining = []
        for b in boxes:
            iou = _iou_pair(best["bbox"], b["bbox"])
            if iou > iou_thr:
                b = dict(b)
                b["conf"] = float(b["conf"]) * np.exp(-(iou**2) / sigma)
            if b["conf"] >= min_conf:
                remaining.append(b)
        boxes = remaining
    return result

def nms(dets, per_cls_iou=0.20, cross_iou=0.30, min_conf=DISPLAY_MIN_CONF,
        min_area=600):
    """
    NMS dua tahap (v7c):
    1. Hard NMS per kelas (IoU=0.20) — sama seperti v6, ketat untuk kelas sama
    2. Hard NMS cross kelas (IoU=0.30) — sama seperti v6
    3. min_area=600px² — filter noise box kecil (baru, v6=0)

    v7b (per=0.30, cross=0.35, area=400) lebih buruk dari v6:
    FHI 82.1 vs 84.5, dets 11.2 vs ~9, IncGrowth 28.4% vs 21.6%.
    Penyebab: per_cls=0.30 terlalu longgar → duplikat per-kelas tidak disuppresi.
    v7c: kembali ke threshold v6 yang terbukti bagus, tambah min_area=600 saja.
    """
    # Filter confidence dan area minimum
    dets = [d for d in dets if d["conf"] >= min_conf and d.get("area", 0) >= min_area]
    if not dets: return []

    # Tahap 1: Hard NMS per kelas
    by_cls = {}
    for d in dets:
        by_cls.setdefault(d["class"], []).append(d)
    after_per_cls = []
    for lst in by_cls.values():
        after_per_cls.extend(_hard_nms(lst, per_cls_iou))

    # Tahap 2: Hard NMS cross kelas
    return _hard_nms(after_per_cls, cross_iou)

# ── FHI ────────────────────────────────────────────────────────────────────────
def compute_fhi(dets, frame_area):
    if not dets: return 50.0
    total_w = sum(d["area"] for d in dets) + 1e-6
    base = 0.0
    for d in dets:
        w = d["area"] / total_w
        sev = SEVERITY.get(d["class"], 0.5)
        base += w * (1.0 - sev)
    return round(base * 100.0, 1)

# ── EMA temporal smoothing ─────────────────────────────────────────────────────
class EMASmooth:
    def __init__(self, alpha=0.30):
        self.alpha = alpha
        self.state = {}

    def update(self, key, val):
        if key not in self.state:
            self.state[key] = val
        else:
            self.state[key] = self.alpha*val + (1-self.alpha)*self.state[key]
        return self.state[key]

# ── Visualisasi ────────────────────────────────────────────────────────────────
def draw_stream(frame, dets, fhi):
    """Render bounding boxes onto frame. FHI sidebar dihapus."""
    out = frame.copy()
    for d in dets:
        x1, y1, x2, y2 = d["bbox"]
        col = COLORS.get(d["class"], (200, 200, 200))
        thick = 3 if d.get("source", "").startswith("yolo") else 2
        cv2.rectangle(out, (x1, y1), (x2, y2), col, thick)
        label = f"{d['class'][:18]} {d['conf']:.2f}"
        (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.45, 1)
        cv2.rectangle(out, (x1, y1 - th - 6), (x1 + tw + 4, y1), col, -1)
        cv2.putText(out, label, (x1 + 2, y1 - 3),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 0, 0), 1, cv2.LINE_AA)
    return out

# ── YOLO-det wrapper (ONNX detect model, supplementary) ─────────────────────────
YOLO_DET_MAP = {
    "crop":                      "Lush Green",
    "crop_row":                  "Lush Green",
    "weed":                      "Inconsistent Growth",
    "disease_stress_vegetation": "Inconsistent Growth",
}

class YOLODetector:
    def __init__(self, path, conf_thr=0.45):
        import onnxruntime as ort, ast
        self.sess  = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        inp        = self.sess.get_inputs()[0]
        self.iname = inp.name
        shp        = inp.shape
        self.imgsz = (shp[2] if isinstance(shp[2], int) and shp[2] > 0 else 640,
                      shp[3] if isinstance(shp[3], int) and shp[3] > 0 else 640)
        meta       = self.sess.get_modelmeta().custom_metadata_map
        raw        = meta.get("names", "{}")
        try:
            parsed     = ast.literal_eval(raw)
            self.names = parsed if isinstance(parsed, dict) else {i: v for i, v in enumerate(parsed)}
        except Exception:
            self.names = {0: "crop", 1: "weed", 2: "crop_row", 3: "disease_stress_vegetation"}
        self.conf_thr = conf_thr
        self.nc       = len(self.names)

    def detect(self, frame):
        oh, ow = frame.shape[:2]
        iw, ih = self.imgsz
        img = cv2.resize(frame, (iw, ih)).astype(np.float32) / 255.0
        inp = img.transpose(2, 0, 1)[None]
        out = self.sess.run(None, {self.iname: inp})[0][0]
        preds = out.T if out.shape[0] < out.shape[1] else out
        dets = []
        for pred in preds:
            cls_scores = pred[4:4 + self.nc]
            cid  = int(np.argmax(cls_scores))
            conf = float(cls_scores[cid])
            if conf < self.conf_thr:
                continue
            cx, cy, bw, bh = pred[:4]
            if cx > 1 or cy > 1:
                cx /= iw; cy /= ih; bw /= iw; bh /= ih
            x1 = max(0, int((cx - bw / 2) * ow))
            y1 = max(0, int((cy - bh / 2) * oh))
            x2 = min(ow - 1, int((cx + bw / 2) * ow))
            y2 = min(oh - 1, int((cy + bh / 2) * oh))
            if x2 <= x1 or y2 <= y1:
                continue
            cname = self.names.get(cid, str(cid))
            label = YOLO_DET_MAP.get(cname, "Inconsistent Growth")
            area  = (x2 - x1) * (y2 - y1)
            dets.append({"bbox": (x1, y1, x2, y2), "class": label,
                         "conf": round(min(0.57, conf * 0.85), 3),
                         "area": area, "source": "yolo-det"})
        return dets

# ── Build GCS detection summary ────────────────────────────────────────────────
def build_gcs_counts(dets, fhi):
    counts = {"Healthy": 0.0, "Stress": 0.0, "Drought": 0.0, "Bare Soil": 0.0}
    total_area = sum(d["area"] for d in dets) or 1
    for d in dets:
        gcs_key = DISPLAY_TO_GCS.get(d["class"])
        if gcs_key:
            counts[gcs_key] += d["area"] / total_area * 100.0
    visible = {k: round(v, 1) for k, v in counts.items() if v > 0.5}
    parts   = sorted(visible.items(), key=lambda x: -x[1])
    summary = " | ".join(f"{k}: {v:.0f}%" for k, v in parts)
    return visible, summary

# ── Emit helper ────────────────────────────────────────────────────────────────
def emit(payload):
    print(json.dumps(payload), flush=True)

# ── Main streaming loop ────────────────────────────────────────────────────────
def main():
    global DEMO_MODE

    parser = argparse.ArgumentParser(description="MoonHarvest HSV-First stream v7c")
    parser.add_argument("--source",       required=True, help="Video path or camera index")
    parser.add_argument("--model",        default="",    help="ONNX/PT classifier (opsional)")
    parser.add_argument("--det-model",    default="",    help="YOLO-det ONNX (opsional)")
    parser.add_argument("--max-fps",      type=float, default=15.0)
    parser.add_argument("--playback-rate",type=float, default=1.0)
    parser.add_argument("--demo",         action="store_true")
    args = parser.parse_args()

    if args.demo:
        DEMO_MODE = True

    signal.signal(signal.SIGINT,  lambda s, f: sys.exit(0))
    signal.signal(signal.SIGTERM, lambda s, f: sys.exit(0))

    src = int(args.source) if args.source.isdigit() else args.source
    cap = cv2.VideoCapture(src)
    if not cap.isOpened():
        emit({"type": "error", "data": f"Cannot open: {args.source}"})
        return 1

    # Send preview frame immediately before loading any heavy models
    ret_preview, preview = cap.read()
    if ret_preview and preview is not None:
        ok, buf = cv2.imencode(".jpg", preview, [cv2.IMWRITE_JPEG_QUALITY, 75])
        if ok:
            emit({"type": "frame", "data": base64.b64encode(buf).decode()})
        if not isinstance(src, int):
            cap.set(cv2.CAP_PROP_POS_FRAMES, 0)

    # Load ONNX classifier
    classifier = None
    if args.model and os.path.isfile(args.model):
        try:
            classifier = load_classifier(args.model)
            emit({"type": "info", "data": f"Classifier: {Path(args.model).name}"})
        except Exception as e:
            emit({"type": "info", "data": f"Classifier load error ({e}), HSV-only"})
    else:
        emit({"type": "info", "data": "HSV-only mode (no classifier)"})

    # Load YOLO-det
    detector = None
    det_path = getattr(args, "det_model", "")
    if det_path and os.path.isfile(det_path):
        try:
            detector = YOLODetector(det_path)
            emit({"type": "info", "data": f"YOLO-det: {Path(det_path).name}"})
        except Exception as e:
            emit({"type": "info", "data": f"YOLO-det load error ({e})"})

    src_fps     = cap.get(cv2.CAP_PROP_FPS) or 30.0
    target_fps  = min(float(args.max_fps), src_fps)
    step        = max(1, int(round(src_fps / target_fps)))
    playback_rate = max(0.25, min(2.0, float(args.playback_rate)))
    frame_delay = (step / src_fps) / playback_rate

    PROC_W = 640
    _emit_q: queue.Queue = queue.Queue(maxsize=1)
    _stop   = threading.Event()

    def compute_loop():
        ema    = EMASmooth(HSV_CFG["ema_alpha"])
        fidx   = 0
        eof_retries = 0

        while not _stop.is_set():
            ret, frame = cap.read()
            if not ret or frame is None:
                if DEMO_MODE and not isinstance(src, int):
                    cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                    fidx = 0
                    eof_retries += 1
                    if eof_retries <= 3:
                        continue
                _emit_q.put(None)
                break
            eof_retries = 0
            fidx += 1
            if fidx % step != 0:
                continue

            fh, fw = frame.shape[:2]
            if fw > PROC_W:
                frame = cv2.resize(frame, (PROC_W, int(fh * PROC_W / fw)))

            try:
                regions = segment_regions(frame, HSV_CFG)
                if classifier and regions:
                    dets = fuse(frame, regions, classifier)
                else:
                    dets = [{"bbox": r["bbox"], "class": r["hsv_display"],
                             "conf": 0.60, "area": r["area"], "source": "hsv-only"}
                            for r in regions]
                if detector:
                    dets = dets + detector.detect(frame)
                dets = nms(dets)
                fhi_raw = compute_fhi(dets, frame.shape[0] * frame.shape[1])
                fhi     = ema.update("fhi", fhi_raw)
                vis     = draw_stream(frame, dets, fhi)
            except Exception as exc:
                emit({"type": "error", "data": str(exc)})
                continue

            ok, buf = cv2.imencode(".jpg", vis, [cv2.IMWRITE_JPEG_QUALITY, 75])
            if not ok:
                continue

            visible, summary = build_gcs_counts(dets, fhi)
            payload = {
                "jpg":     base64.b64encode(buf).decode(),
                "count":   len(dets),
                "summary": summary,
                "classes": visible,
                "fhi":     round(fhi, 1),
            }
            try:
                _emit_q.put_nowait(payload)
            except queue.Full:
                pass

    t_compute = threading.Thread(target=compute_loop, daemon=True)
    t_compute.start()

    t_last = time.time()
    try:
        while True:
            try:
                item = _emit_q.get(timeout=5.0)
            except queue.Empty:
                if not t_compute.is_alive():
                    break
                continue

            if item is None:
                emit({"type": "end", "data": "Video stream ended"})
                break

            emit({"type": "frame",     "data": item["jpg"]})
            emit({"type": "detection", "data": {
                "count":   item["count"],
                "summary": item["summary"],
                "classes": item["classes"],
                "fhi":     item["fhi"],
            }})

            elapsed = time.time() - t_last
            sleep_t = frame_delay - elapsed
            if sleep_t > 0.004:
                time.sleep(sleep_t)
            t_last = time.time()

    except Exception as exc:
        emit({"type": "error", "data": str(exc)})
    finally:
        _stop.set()
        cap.release()
        t_compute.join(timeout=2.0)

    return 0


if __name__ == "__main__":
    sys.exit(main())
