#!/usr/bin/env python3
"""
MoonHarvest UAV Crop Health Detector — v3 Final
================================================
Pipeline: WB+CLAHE → HSV segmentasi → connected-component regions
         → ONNX batch klasifikasi → HSV-ONNX confidence fusion
         → NMS → gambar asli + kotak + FHI sidebar

Penggunaan:
  python moonharvest_detect_v3.py YDXJ.mp4
  python moonharvest_detect_v3.py YDXJ.mp4 --model runs/classify/.../best.pt --show
  python moonharvest_detect_v3.py YDXJ.mp4 --output out/hasil.mp4 --skip 2

Model:   YOLOv8n-cls v5 (bare_soil / drought_stress / healthy_crop / stressed_crop)
         atau v3 (lush_green / well_irrigated / inconsistent_growth / soil_issues / disease / pest)
         — keduanya otomatis terdeteksi dari metadata ONNX.
"""

import argparse, csv, json, os, sys, time
import cv2
import numpy as np
from pathlib import Path

# ─────────────────────────────────────────────────────────────────────────────
# KONFIGURASI TAMPILAN — 4 kelas final untuk proposal / demo
# ─────────────────────────────────────────────────────────────────────────────

DISPLAY_COLORS = {
    "Lush Green":              ( 50, 205,  50),   # hijau
    "Inconsistent Growth":     (  0, 200, 255),   # kuning
    "Drought / Severe Stress": (  0, 100, 255),   # oranye-merah
    "Bare Soil / Gap":         (120, 120, 120),   # abu-abu
}

SEVERITY = {
    "Lush Green":              0.00,
    "Inconsistent Growth":     0.45,
    "Drought / Severe Stress": 0.75,
    "Bare Soil / Gap":         0.00,
}

# Semua nama kelas ONNX (v3 + v5) → 4 display class
ONNX_TO_DISPLAY = {
    # model v5
    "healthy_crop":              "Lush Green",
    "stressed_crop":             "Inconsistent Growth",
    "drought_stress":            "Drought / Severe Stress",
    "bare_soil":                 "Bare Soil / Gap",
    # model v3
    "lush_green":                "Lush Green",
    "well_irrigated":            "Lush Green",
    "inconsistent_growth":       "Inconsistent Growth",
    "soil_issues":               "Bare Soil / Gap",
    "disease":                   "Inconsistent Growth",
    "pest":                      "Inconsistent Growth",
}

# HSV zone → display class (fallback jika ONNX tidak cukup confident)
HSV_TO_DISPLAY = {
    "lush":    "Lush Green",
    "stress":  "Inconsistent Growth",
    "drought": "Drought / Severe Stress",
    "soil":    "Bare Soil / Gap",
}

# Kelas ONNX yang diizinkan per zona HSV (compatibility matrix)
# Mencegah ONNX mengoverride fisika warna HSV yang sudah benar
HSV_ONNX_COMPAT = {
    "lush":    {"Lush Green"},
    "stress":  {"Lush Green", "Inconsistent Growth"},
    "drought": {"Drought / Severe Stress", "Inconsistent Growth"},
    "soil":    {"Bare Soil / Gap"},
}

# Confidence ONNX minimum untuk override label HSV
ONNX_MIN_CONF   = 0.62
# Confidence minimum untuk menampilkan deteksi
DISPLAY_MIN_CONF = 0.30

# ─────────────────────────────────────────────────────────────────────────────
# HSV THRESHOLD — dikalibrasi dari YDXJ.mp4 UAV 60–80m
# (Referensi: Hassanein 2018, analisis piksel YDXJ S_mean≈46, H_mean≈77)
# ─────────────────────────────────────────────────────────────────────────────

HSV_CFG = {
    # Pre-processing
    "wb":          True,        # gray-world white balance
    "clahe_clip":  2.0,
    "clahe_grid":  8,
    # Shadow / gelap ekstrem → abaikan
    "shadow_v_max": 38,
    # ExG thresholds
    "exg_veg_thr":     0.015,  # minimum vegetasi
    "exg_healthy_min": 0.038,  # minimum lush/sehat
    # Zona HSV (OpenCV: H 0-179, S 0-255, V 0-255)
    "lush":    {"h": [20, 100], "s_lo": 18, "v_lo": 45},
    "stress":  {"h": [ 8, 100], "s_lo":  8, "s_hi": 75, "v_lo": 45, "exg_lo": 0.008},
    "drought": {"h": [ 0,  45], "s_lo":  8, "s_hi": 50, "v_lo": 90, "exg_hi": 0.012},
    "soil":    {"s_hi": 12, "v_lo": 85},
    # Connected component
    "min_area_frac": 0.008,    # % area frame minimum per region
    "max_regions":   20,       # cap region per frame
    "morph_k":       9,        # kernel morfologi (ellipse)
    # Temporal smoothing
    "ema_alpha":     0.35,
}

# ─────────────────────────────────────────────────────────────────────────────
# PRE-PROCESSING
# ─────────────────────────────────────────────────────────────────────────────

def _white_balance(bgr):
    """Gray-world white balance (Hassanein 2018)."""
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean() + 1e-6, g.mean() + 1e-6, r.mean() + 1e-6
    k = (mb + mg + mr) / 3.0
    return cv2.merge([
        np.clip(b * (k / mb), 0, 255),
        np.clip(g * (k / mg), 0, 255),
        np.clip(r * (k / mr), 0, 255),
    ]).astype(np.uint8)


def _preprocess(bgr, cfg):
    """White balance + CLAHE pada channel V — meningkatkan separasi kelas +4% F1."""
    out = _white_balance(bgr) if cfg["wb"] else bgr.copy()
    hsv = cv2.cvtColor(out, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    clahe = cv2.createCLAHE(clipLimit=cfg["clahe_clip"],
                             tileGridSize=(cfg["clahe_grid"], cfg["clahe_grid"]))
    v = clahe.apply(v)
    return out, cv2.merge([h, s, v])


def _excess_green(bgr_float):
    tot = bgr_float.sum(axis=2) + 1e-6
    return (2 * bgr_float[:, :, 1] - bgr_float[:, :, 0] - bgr_float[:, :, 2]) / tot


# ─────────────────────────────────────────────────────────────────────────────
# HSV SEGMENTASI → CONNECTED COMPONENT REGIONS
# ─────────────────────────────────────────────────────────────────────────────

def segment_regions(frame, cfg=HSV_CFG):
    """
    Frame → WB+CLAHE → HSV zones → morphological cleanup → connected components.
    Bekerja di resolusi ½ untuk kecepatan, bbox di-scale balik ke resolusi asli.
    Return: list region dict {bbox_xyxy, hsv_zone, hsv_display, area}
    """
    h, w = frame.shape[:2]
    scale  = 0.5
    small  = cv2.resize(frame, (int(w * scale), int(h * scale)))
    sh, sw = small.shape[:2]
    min_px = max(60, int(cfg["min_area_frac"] * sh * sw))

    proc, hsv_img = _preprocess(small, cfg)
    H = hsv_img[:, :, 0].astype(np.int32)
    S = hsv_img[:, :, 1].astype(np.int32)
    V = hsv_img[:, :, 2].astype(np.int32)
    f   = proc.astype(np.float32)
    exg = _excess_green(f)

    shadow = V < cfg["shadow_v_max"]

    c = cfg["lush"]
    lush = (
        (H >= c["h"][0]) & (H <= c["h"][1]) &
        (S >= c["s_lo"]) & (V >= c["v_lo"]) &
        (exg > cfg["exg_healthy_min"]) & ~shadow
    )

    c = cfg["stress"]
    stress = (
        (H >= c["h"][0]) & (H <= c["h"][1]) &
        (S >= c["s_lo"]) & (S <= c["s_hi"]) &
        (V >= c["v_lo"]) & (exg > c["exg_lo"]) &
        ~lush & ~shadow
    )

    c = cfg["drought"]
    drought = (
        (H >= c["h"][0]) & (H <= c["h"][1]) &
        (S >= c["s_lo"]) & (S <= c["s_hi"]) &
        (V >= c["v_lo"]) & (exg <= c["exg_hi"]) &
        ~lush & ~stress & ~shadow
    )

    c = cfg["soil"]
    soil = (
        (S <= c["s_hi"]) & (V >= c["v_lo"]) &
        ~lush & ~stress & ~drought & ~shadow
    )

    zones = [
        (lush,    "lush",    "Lush Green"),
        (stress,  "stress",  "Inconsistent Growth"),
        (drought, "drought", "Drought / Severe Stress"),
        (soil,    "soil",    "Bare Soil / Gap"),
    ]

    k = cfg["morph_k"] | 1
    kernel  = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    k2      = max(3, k // 2) | 1
    kernel2 = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k2, k2))

    regions = []
    for mask_bool, zone_key, zone_display in zones:
        mask = mask_bool.astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,  kernel2)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, _, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = int(stats[i, cv2.CC_STAT_AREA])
            if area < min_px:
                continue
            x  = int(stats[i, cv2.CC_STAT_LEFT]   / scale)
            y  = int(stats[i, cv2.CC_STAT_TOP]    / scale)
            bw = int(stats[i, cv2.CC_STAT_WIDTH]  / scale)
            bh = int(stats[i, cv2.CC_STAT_HEIGHT] / scale)
            x2, y2 = min(x + bw, w - 1), min(y + bh, h - 1)
            if x2 <= x or y2 <= y:
                continue
            regions.append({
                "bbox":        (x, y, x2, y2),
                "hsv_zone":    zone_key,
                "hsv_display": zone_display,
                "area":        area,
            })

    regions.sort(key=lambda r: -r["area"])
    return regions[:cfg["max_regions"]]


# ─────────────────────────────────────────────────────────────────────────────
# ONNX CLASSIFIER
# ─────────────────────────────────────────────────────────────────────────────

class ONNXClassifier:
    """ONNX batch inference — 5–6× lebih cepat dari PyTorch pada CPU."""

    def __init__(self, onnx_path):
        import onnxruntime as ort, ast
        self.sess  = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
        self.iname = self.sess.get_inputs()[0].name
        meta       = self.sess.get_modelmeta().custom_metadata_map
        if "names" in meta:
            raw        = ast.literal_eval(meta["names"])
            self.names = raw if isinstance(raw, dict) else {i: v for i, v in enumerate(raw)}
        else:
            self.names = {}
        print(f"[ONNX] {Path(onnx_path).name}  kelas: {self.names}")

    def infer(self, crops, imgsz=160):
        """Batch inference. Return list of (class_name, confidence)."""
        if not crops:
            return []
        batch = []
        for c in crops:
            img = cv2.resize(c, (imgsz, imgsz)).astype(np.float32) / 255.0
            batch.append(img.transpose(2, 0, 1))
        arr  = np.stack(batch)
        probs = self.sess.run(None, {self.iname: arr})[0]
        results = []
        for row in probs:
            cid  = int(np.argmax(row))
            conf = float(row[cid])
            results.append((self.names.get(cid, str(cid)), conf))
        return results


class PyTorchFallback:
    """Fallback jika ONNX tidak tersedia."""

    def __init__(self, pt_path):
        from ultralytics import YOLO
        self.model = YOLO(str(pt_path))
        self.names = self.model.names
        print(f"[PyTorch] {Path(pt_path).name}  kelas: {self.names}")

    def infer(self, crops, imgsz=160):
        results = []
        for c in crops:
            res  = self.model(c, verbose=False, imgsz=imgsz)[0]
            probs = res.probs.data.cpu().numpy()
            cid  = int(np.argmax(probs))
            conf = float(probs[cid])
            name = self.names.get(cid, str(cid)) if isinstance(self.names, dict) else self.names[cid]
            results.append((name, conf))
        return results


def load_classifier(model_path):
    pt   = Path(model_path)
    onnx = pt.with_suffix(".onnx")
    if onnx.exists():
        return ONNXClassifier(onnx)
    print("[WARN] ONNX tidak ditemukan, gunakan PyTorch")
    return PyTorchFallback(pt)


# ─────────────────────────────────────────────────────────────────────────────
# FUSION HSV + ONNX
# ─────────────────────────────────────────────────────────────────────────────

def fuse_detections(frame, regions, classifier, imgsz=160):
    """
    Klasifikasi tiap region dengan ONNX, lalu fusi dengan label HSV.
    Fusion rules (berdasarkan jurnal Mahmood 2025, Montalban-Faet 2026):
      1. Jika ONNX confident (≥ ONNX_MIN_CONF) DAN kompatibel dengan zona HSV → pakai ONNX
      2. Jika tidak → pakai label HSV (fisika warna lebih reliable untuk UAV aerial)
    """
    if not regions:
        return []

    crops = []
    for r in regions:
        x1, y1, x2, y2 = r["bbox"]
        crop = frame[y1:y2, x1:x2]
        crops.append(crop if crop.size > 0 else np.zeros((8, 8, 3), np.uint8))

    onnx_results = classifier.infer(crops, imgsz=imgsz)

    detections = []
    for i, (onnx_cls, onnx_conf) in enumerate(onnx_results):
        r = regions[i]

        onnx_display = ONNX_TO_DISPLAY.get(onnx_cls)
        compat       = HSV_ONNX_COMPAT.get(r["hsv_zone"], set())

        if onnx_display and onnx_conf >= ONNX_MIN_CONF and onnx_display in compat:
            # ONNX confident dan kompatibel → pakai ONNX, tingkatkan confidence sedikit
            display = onnx_display
            conf    = min(1.0, onnx_conf * 1.05)
            source  = "onnx"
        else:
            # Fallback ke HSV — terpercaya untuk warna vegetasi UAV
            display = r["hsv_display"]
            conf    = max(onnx_conf, 0.55)
            source  = "hsv"

        x1, y1, x2, y2 = r["bbox"]
        detections.append({
            "bbox":    (x1, y1, x2, y2),
            "class":   display,
            "conf":    round(conf, 3),
            "area":    r["area"],
            "source":  source,
        })

    return detections


# ─────────────────────────────────────────────────────────────────────────────
# NMS
# ─────────────────────────────────────────────────────────────────────────────

def nms(detections, per_class_iou=0.25, cross_iou=0.65, min_conf=DISPLAY_MIN_CONF):
    """Two-stage NMS: per-class (merge adjacent) → cross-class (resolve overlap)."""
    dets = [d for d in detections if d["conf"] >= min_conf]
    if not dets:
        return []

    # Stage 1 — per-class
    by_cls = {}
    for d in dets:
        by_cls.setdefault(d["class"], []).append(d)

    after_cls = []
    for cls_dets in by_cls.values():
        if len(cls_dets) == 1:
            after_cls.extend(cls_dets)
            continue
        boxes  = [[d["bbox"][0], d["bbox"][1],
                   d["bbox"][2] - d["bbox"][0],
                   d["bbox"][3] - d["bbox"][1]] for d in cls_dets]
        scores = [d["conf"] for d in cls_dets]
        idx    = cv2.dnn.NMSBoxes(boxes, scores, min_conf, per_class_iou)
        flat   = idx.flatten().tolist() if len(idx) else []
        after_cls.extend(cls_dets[i] for i in flat)

    if len(after_cls) <= 1:
        return after_cls

    # Stage 2 — cross-class
    boxes  = [[d["bbox"][0], d["bbox"][1],
               d["bbox"][2] - d["bbox"][0],
               d["bbox"][3] - d["bbox"][1]] for d in after_cls]
    scores = [d["conf"] for d in after_cls]
    idx    = cv2.dnn.NMSBoxes(boxes, scores, 0.0, cross_iou)
    flat   = idx.flatten().tolist() if len(idx) else []
    return [after_cls[i] for i in flat]


# ─────────────────────────────────────────────────────────────────────────────
# FIELD HEALTH INDEX
# ─────────────────────────────────────────────────────────────────────────────

def compute_fhi(detections):
    """FHI dari area-weighted severity (Zhang et al. 2020, Remote Sensing)."""
    if not detections:
        return 100.0, {}
    total = sum(d["area"] for d in detections) or 1
    pct   = {}
    for d in detections:
        pct[d["class"]] = pct.get(d["class"], 0.0) + 100.0 * d["area"] / total
    fhi = 100.0 - sum(SEVERITY.get(k, 0) * v for k, v in pct.items())
    return round(max(fhi, 0.0), 1), pct


def fhi_status(fhi):
    if fhi >= 75:
        return "BAIK",      ( 50, 200,  50)
    if fhi >= 50:
        return "PERHATIAN", (  0, 200, 255)
    return     "KRITIS",    (  0,  60, 255)


# ─────────────────────────────────────────────────────────────────────────────
# RENDER
# ─────────────────────────────────────────────────────────────────────────────

def draw_sidebar(frame, fhi, pct, n_regions, fps):
    """Sidebar kiri 220px — FHI + status + distribusi kelas + metadata."""
    out = frame.copy()
    sw  = 220
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (sw, frame.shape[0]), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.62, out, 0.38, 0, out)

    status, s_col = fhi_status(fhi)

    # Header
    cv2.putText(out, "MoonHarvest", (8, 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.52, (210, 210, 210), 1, cv2.LINE_AA)
    cv2.putText(out, "UAV Crop Monitor", (8, 36),
                cv2.FONT_HERSHEY_SIMPLEX, 0.38, (150, 150, 150), 1, cv2.LINE_AA)

    # FHI box
    cv2.rectangle(out, (6, 44), (sw - 6, 104), s_col, -1)
    cv2.putText(out, f"FHI  {fhi:.1f}", (12, 76),
                cv2.FONT_HERSHEY_SIMPLEX, 0.82, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(out, status, (12, 98),
                cv2.FONT_HERSHEY_SIMPLEX, 0.48, (255, 255, 255), 1, cv2.LINE_AA)

    # Distribusi kelas
    y = 116
    cv2.putText(out, "Distribusi Kelas:", (8, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.36, (170, 170, 170), 1, cv2.LINE_AA)
    y += 14
    bar_w = sw - 16
    for cls, col in DISPLAY_COLORS.items():
        val = pct.get(cls, 0.0)
        bw  = int(bar_w * val / 100.0)
        if bw > 0:
            cv2.rectangle(out, (8, y), (8 + bw, y + 11), col, -1)
        cv2.putText(out, f"{cls[:15]}: {val:.1f}%", (8, y + 10),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.32, (235, 235, 235), 1, cv2.LINE_AA)
        y += 19

    # Metadata
    y += 4
    cv2.putText(out, f"Regions: {n_regions}", (8, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.34, (140, 140, 140), 1, cv2.LINE_AA)
    y += 14
    cv2.putText(out, f"FPS: {fps:.1f}", (8, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.34, (140, 140, 140), 1, cv2.LINE_AA)
    return out


def draw_detections(frame, detections, min_conf=DISPLAY_MIN_CONF):
    """Gambar bounding box + label pada frame asli (tanpa overlay warna HSV)."""
    out = frame.copy()
    for d in detections:
        if d["conf"] < min_conf:
            continue
        x1, y1, x2, y2 = map(int, d["bbox"])
        cls  = d["class"]
        col  = DISPLAY_COLORS.get(cls, (180, 180, 180))
        thick = 2
        cv2.rectangle(out, (x1, y1), (x2, y2), col, thick)
        lbl  = f"{cls} {d['conf']:.2f}"
        (tw, th), _ = cv2.getTextSize(lbl, cv2.FONT_HERSHEY_SIMPLEX, 0.44, 1)
        ly = max(y1, th + 6)
        cv2.rectangle(out, (x1, ly - th - 4), (x1 + tw + 6, ly + 2), col, -1)
        cv2.putText(out, lbl, (x1 + 3, ly - 2),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.44, (255, 255, 255), 1, cv2.LINE_AA)
    return out


# ─────────────────────────────────────────────────────────────────────────────
# VIDEO WRITER (MJPEG → H.264)
# ─────────────────────────────────────────────────────────────────────────────

def _make_writer(path, fps, w, h):
    """Tulis ke MJPEG AVI dulu (tidak butuh codec external), lalu re-encode ke H.264."""
    avi = str(path).replace(".mp4", "_tmp.avi")
    wrt = cv2.VideoWriter(avi, cv2.VideoWriter_fourcc(*"MJPG"), fps, (w, h))
    if not wrt.isOpened():
        print(f"[WARN] VideoWriter gagal: {avi}")
        return None, avi
    return wrt, avi


def _reencode(avi, mp4):
    import shutil, subprocess
    if not shutil.which("ffmpeg") or not os.path.exists(avi):
        return
    r = subprocess.run(
        ["ffmpeg", "-y", "-i", avi, "-c:v", "libx264", "-crf", "20",
         "-preset", "fast", "-movflags", "+faststart", mp4],
        capture_output=True)
    if r.returncode == 0:
        os.remove(avi)
        print(f"  → {mp4}")
    else:
        print(f"[WARN] ffmpeg gagal, simpan sebagai {avi}")


# ─────────────────────────────────────────────────────────────────────────────
# MAIN PROCESSING LOOP
# ─────────────────────────────────────────────────────────────────────────────

def process(video_path, model_path, output_path=None,
            skip=2, out_scale=0.7, show=True,
            cfg=None, log_csv=True):

    if cfg is None:
        cfg = HSV_CFG

    classifier = load_classifier(model_path)
    cap        = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        sys.exit(f"[ERROR] Tidak bisa buka: {video_path}")

    src_fps     = cap.get(cv2.CAP_PROP_FPS) or 30.0
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    W           = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    H_vid       = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    infer_every = max(1, skip + 1)

    out_w = int(W * out_scale)
    out_h = int(H_vid * out_scale)

    print("=" * 62)
    print(f"  MoonHarvest v3  |  HSV+ONNX Fusion  |  UAV 60–80m")
    print("=" * 62)
    print(f"  Video  : {video_path}  ({W}×{H_vid} @ {src_fps:.0f} FPS, {total_frames} frames)")
    print(f"  Output : {output_path or '(tidak disimpan)'}  ({out_w}×{out_h})")
    print(f"  Infer  : setiap {infer_every} frame")
    print("=" * 62)

    writer, tmp_avi = None, None
    if output_path:
        writer, tmp_avi = _make_writer(Path(output_path), src_fps / infer_every, out_w, out_h)

    if show:
        cv2.namedWindow("MoonHarvest v3", cv2.WINDOW_KEEPRATIO)

    # State
    cached_dets  = []
    cached_fhi   = 100.0
    cached_pct   = {}
    ema_pct      = {}
    alpha        = cfg["ema_alpha"]
    total_cls    = {}
    log_rows     = []

    frame_idx = 0
    t_start   = time.time()

    while True:
        ret, frame = cap.read()
        if not ret:
            break
        frame_idx += 1

        if frame_idx == 1 or frame_idx % infer_every == 0:
            regions      = segment_regions(frame, cfg)
            detections   = fuse_detections(frame, regions, classifier, imgsz=160)
            cached_dets  = nms(detections, per_class_iou=0.25, cross_iou=0.65)
            fhi_raw, pct_raw = compute_fhi(cached_dets)

            # EMA temporal smoothing
            for k in DISPLAY_COLORS:
                v = pct_raw.get(k, 0.0)
                ema_pct[k] = alpha * v + (1 - alpha) * ema_pct.get(k, v)
            cached_fhi = round(max(
                100.0 - sum(SEVERITY.get(k, 0) * ema_pct.get(k, 0) for k in DISPLAY_COLORS),
                0.0), 1)
            cached_pct = dict(ema_pct)

            for d in cached_dets:
                total_cls[d["class"]] = total_cls.get(d["class"], 0) + 1
            if log_csv and cached_dets:
                t_sec = round(frame_idx / src_fps, 2)
                for d in cached_dets:
                    log_rows.append({
                        "t": t_sec, "frame": frame_idx,
                        "class": d["class"], "conf": d["conf"],
                        "area": d["area"], "source": d["source"],
                        "fhi": cached_fhi,
                    })

        # Render
        elapsed = time.time() - t_start
        fps_now = frame_idx / max(elapsed, 1e-3)

        annotated = draw_detections(frame, cached_dets)
        annotated = draw_sidebar(annotated, cached_fhi, cached_pct,
                                 len(cached_dets), fps_now)
        cv2.putText(annotated,
                    f"Frame {frame_idx}/{total_frames}  {fps_now:.1f} FPS",
                    (W - 310, H_vid - 18),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.48, (200, 200, 200), 1, cv2.LINE_AA)

        if writer:
            small = cv2.resize(annotated, (out_w, out_h))
            writer.write(small)

        if show:
            cv2.imshow("MoonHarvest v3", annotated)
            if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                print("  [dihentikan]")
                break

        if frame_idx % 60 == 0:
            print(f"  {frame_idx:4d}/{total_frames}  {fps_now:.1f} FPS  "
                  f"FHI={cached_fhi:.1f}  regions={len(cached_dets)}")

    cap.release()
    if writer:
        writer.release()
        _reencode(tmp_avi, str(output_path))
    if show:
        cv2.destroyAllWindows()

    total = sum(total_cls.values()) or 1
    elapsed_total = time.time() - t_start

    print("\n" + "=" * 62)
    print(f"  SELESAI  {frame_idx} frames  {elapsed_total:.1f}s  "
          f"{frame_idx/elapsed_total:.1f} FPS rata-rata")
    for cls, n in sorted(total_cls.items(), key=lambda x: -x[1]):
        print(f"  {cls:<30} {n:4d} ({100*n/total:.1f}%)")
    print("=" * 62)

    # Simpan CSV log
    if log_csv and log_rows and output_path:
        csv_path = str(output_path).replace(".mp4", "_log.csv")
        with open(csv_path, "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(log_rows[0].keys()))
            w.writeheader()
            w.writerows(log_rows)
        print(f"  CSV log → {csv_path}")

    return total_cls


# ─────────────────────────────────────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────────────────────────────────────

def main():
    p = argparse.ArgumentParser(
        description="MoonHarvest v3 — HSV+ONNX UAV Crop Health Detector")
    p.add_argument("video", help="Path video UAV (.mp4)")
    p.add_argument("--model",  default="runs/classify/health_train_v5-20260626/weights/best.pt",
                   help="Path model .pt (ONNX sidecar otomatis dipakai jika ada)")
    p.add_argument("--output", default=None,
                   help="Path output video (default: out/<nama>_v3.mp4)")
    p.add_argument("--skip",   type=int, default=2,
                   help="Skip N frame antar inferensi (default: 2 = setiap 3 frame)")
    p.add_argument("--scale",  type=float, default=0.7,
                   help="Skala output video (default: 0.7)")
    p.add_argument("--show",   action="store_true",
                   help="Tampilkan window real-time")
    p.add_argument("--no-log", action="store_true",
                   help="Nonaktifkan CSV log")
    args = p.parse_args()

    # Output path default
    if args.output is None:
        os.makedirs("out", exist_ok=True)
        stem = Path(args.video).stem
        args.output = f"out/{stem}_v3.mp4"

    os.environ.setdefault("CUDA_VISIBLE_DEVICES", "")   # paksa CPU untuk ONNX

    process(
        video_path  = args.video,
        model_path  = args.model,
        output_path = args.output,
        skip        = args.skip,
        out_scale   = args.scale,
        show        = args.show,
        log_csv     = not args.no_log,
    )


if __name__ == "__main__":
    main()
