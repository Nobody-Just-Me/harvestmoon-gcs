#!/usr/bin/env python3
"""
MoonHarvest Detection Stream
==============================
Streaming JSON protocol untuk dashboard — HSV full pipeline + YOLO per-region fusion
(identik dengan moonharvest_fusion.py di test_program, diadaptasi untuk streaming).

Pipeline:
  Frame → HSV classify_pixels → reassign_stressed_to_healthy → extract_regions
        → [YOLO classify per-region patch] → fuse_region (adaptive confidence-gated)
        → draw bounding boxes + HSV overlay → base64 JPEG → JSON stdout

Protocol output (stdout):
  {"type": "frame",     "data": "<base64_jpeg>"}
  {"type": "detection", "data": {"count": N, "summary": "...", "classes": {...}}}
  {"type": "end",       "data": "Video ended"}
  {"type": "error",     "data": "..."}
"""

import argparse, base64, json, os, queue, signal, sys, threading, time
import cv2
import numpy as np

# ---------------------------------------------------------------------------
# YOLO (opsional — lazy import supaya frame pertama tidak menunggu ultralytics)
# ---------------------------------------------------------------------------
_YOLO = None
YOLO_AVAILABLE = None


def ensure_yolo_available():
    global _YOLO, YOLO_AVAILABLE
    if YOLO_AVAILABLE is not None:
        return YOLO_AVAILABLE
    try:
        from ultralytics import YOLO as yolo_cls
        _YOLO = yolo_cls
        YOLO_AVAILABLE = True
    except ImportError:
        YOLO_AVAILABLE = False
    return YOLO_AVAILABLE

# ---------------------------------------------------------------------------
# DEMO MODE
# ---------------------------------------------------------------------------
DEMO_MODE = os.environ.get("MOONHARVEST_DEMO", "0") == "1"

DISPLAY_MAP = {
    "healthy_crop":              "Lush Green",
    "stressed_crop":             "Inconsistent Growth",
    "disease_stress_vegetation": "Drought/Severe Stress",
    "drought_stress":            "Drought/Severe Stress",
    "bare_soil":                 "Bare Soil / Gap",
}
DEMO_PALETTE = {
    "Lush Green":          (50,  205,  50),   # BGR green
    "Inconsistent Growth": (0,   200, 255),   # yellow
    "Drought/Severe Stress": (0, 140, 255),   # orange
    "Bare Soil / Gap":     (55,   64,  93),   # brown
}

# ---------------------------------------------------------------------------
# KONSTANTA KELAS
# ---------------------------------------------------------------------------
CLASSES = ["healthy_crop", "stressed_crop", "disease_stress_vegetation",
           "drought_stress", "bare_soil"]
IGNORE  = {"background": 250, "shadow": 251, "unknown": 255}
SEVERITY = {
    "healthy_crop": 0.0, "stressed_crop": 0.45,
    "drought_stress": 0.75, "disease_stress_vegetation": 1.0, "bare_soil": 0.0,
}
PALETTE = {
    "healthy_crop":              (60, 200, 60),
    "stressed_crop":             (40, 220, 230),
    "disease_stress_vegetation": (40,  40, 230),
    "drought_stress":            (30, 140, 250),
    "bare_soil":                 (110,120, 140),
    "background":                (200,160,  90),
    "shadow":                    (50,  50,  50),
}

# ---------------------------------------------------------------------------
# YOLO FUSION CONSTANTS (port dari moonharvest_fusion.py)
# ---------------------------------------------------------------------------
# YOLO dilatih dengan nama kelas diurutkan alfabetis; HSV pakai urutan custom
YOLO_CLASSES = sorted(CLASSES)
YOLO_TO_HSV  = [CLASSES.index(c) for c in YOLO_CLASSES]
HSV_IDX      = {c: i for i, c in enumerate(CLASSES)}

# Bobot YOLO per kelas — healthy: HSV dominan (YOLO over-detect stress di hijau)
CLASS_ALPHA_YOLO = {
    "healthy_crop":              0.20,
    "stressed_crop":             0.55,
    "disease_stress_vegetation": 0.45,
    "drought_stress":            0.50,
    "bare_soil":                 0.45,
}

# Konflik di mana HSV selalu menang tanpa syarat
HSV_WINS_CONFLICT = {
    ("healthy_crop",  "stressed_crop"),
    ("healthy_crop",  "disease_stress_vegetation"),
    ("stressed_crop", "disease_stress_vegetation"),
}

YOLO_MIN_CONF  = 0.40   # YOLO di bawah ini → fallback ke HSV
YOLO_MIN_PATCH = 40     # Patch lebih kecil dari ini → YOLO tidak reliable
CONF_AGREE_GAP = 0.25   # Selisih confidence besar → percaya yang lebih yakin

# ---------------------------------------------------------------------------
# CONFIG (identik dengan DEFAULT_CFG di moonharvest_hsv.py)
# ---------------------------------------------------------------------------
CFG = {
    "white_balance": True,
    "clahe_clip": 2.0, "clahe_grid": 8,
    "shadow_v_max": 45,
    "exg_veg_thr": 0.04,
    "exg_healthy_min": 0.11,
    "bg_h": [84, 140], "bg_s_min": 34,
    "healthy":  {"h": [40, 82],  "s_lo": 48,  "v": [80,  255]},
    # Stressed diperlebar: H 15–55 mencakup kuning-jingga-hijau-kuning
    # reassign_stressed_to_healthy menyaring kembali pixel non-kuning → healthy
    "stressed": {"h": [15, 55],  "s_lo": 40,  "v": [75,  255]},
    "drought":  {"h": [10, 20],  "s_lo": 28,  "v": [90,  255]},
    "disease":  {"h1": [0, 10],  "h2": [168, 179], "s_lo": 45, "v": [25, 215]},
    "soil":     {"s_max": 30,    "v": [110, 240]},
    "texture_win": 9,
    "disease_texture_min": 24.0,
    "morph_kernel": 5,
    "label_median": 7,
    "min_region_area_frac": 0.004,
    "max_region_area_frac": 0.40,
    "suppress_structures": True,
    "road_open_kernel": 11,
    "struct_min_area_frac": 0.0008,
    "stat_reject_maha": 90.0,
    "stressed_to_healthy": True,
    # yellow_h diperlebar [15,42] + S min 40 agar stress kuning-kehijauan juga tertangkap
    "yellow_h": [15, 42],
    "yellow_exg_max": 0.10,
    "yellow_s_min": 40,
    "dark_as_soil": True,
    "dark_v_max": 55,
    "ema_alpha": 0.6,
}

# CLAHE instance di-cache — dibuat sekali, bukan setiap frame
_CLAHE = cv2.createCLAHE(clipLimit=CFG["clahe_clip"],
                          tileGridSize=(CFG["clahe_grid"], CFG["clahe_grid"]))


# ---------------------------------------------------------------------------
# PRE-PROSES
# ---------------------------------------------------------------------------
def gray_world_wb(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean() + 1e-6, g.mean() + 1e-6, r.mean() + 1e-6
    k = (mb + mg + mr) / 3.0
    b = np.clip(b * (k / mb), 0, 255)
    g = np.clip(g * (k / mg), 0, 255)
    r = np.clip(r * (k / mr), 0, 255)
    return cv2.merge([b, g, r]).astype(np.uint8)


def preprocess(bgr, cfg):
    out = gray_world_wb(bgr) if cfg["white_balance"] else bgr.copy()
    hsv = cv2.cvtColor(out, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    v = _CLAHE.apply(v)   # gunakan instance ter-cache
    return out, cv2.merge([h, s, v])


def excess_green(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    tot = b + g + r + 1e-6
    return (2 * g - r - b) / tot


def local_std(gray, win):
    g = gray.astype(np.float32)
    mean = cv2.boxFilter(g, -1, (win, win))
    sq   = cv2.boxFilter(g * g, -1, (win, win))
    return np.sqrt(np.clip(sq - mean * mean, 0, None))


# ---------------------------------------------------------------------------
# KLASIFIKASI PER-PIKSEL
# ---------------------------------------------------------------------------
def classify_pixels(hsv, exg, texture, cfg):
    H, S, V = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    h, w = H.shape
    label    = np.full((h, w), IGNORE["unknown"], np.uint8)
    conf     = np.zeros((h, w), np.float32)
    assigned = np.zeros((h, w), bool)

    def commit(mask, code, score):
        nonlocal assigned
        m = mask & ~assigned
        label[m] = code
        conf[m]  = np.clip(score[m] if isinstance(score, np.ndarray) else score, 0, 1)
        assigned |= m

    sat_score = np.clip(S / 180.0, 0, 1)
    exg_score = np.clip((exg + 0.05) / 0.3, 0, 1)
    tex_score = np.clip(texture / 40.0, 0, 1)

    commit(V < cfg["shadow_v_max"], IGNORE["shadow"], 0.5)

    cs   = cfg["soil"]
    veg0 = exg > cfg["exg_veg_thr"]
    soil = (S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & (~veg0)
    commit(soil, 4, 0.45 + 0.3 * (1 - np.clip(S / 180.0, 0, 1)))

    bg = (H >= cfg["bg_h"][0]) & (H <= cfg["bg_h"][1]) & (S >= cfg["bg_s_min"])
    commit(bg, IGNORE["background"], 0.5)

    c = cfg["healthy"]
    healthy = (H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) & \
              (V >= c["v"][0]) & (exg >= cfg["exg_healthy_min"])
    commit(healthy, 0, 0.45 + 0.25 * sat_score + 0.3 * exg_score)

    # Disease: wajib vegetasi (veg0) — mencegah false positive di tanah bajak cokelat
    c        = cfg["disease"]
    redish   = (((H >= c["h1"][0]) & (H <= c["h1"][1])) |
                ((H >= c["h2"][0]) & (H <= c["h2"][1]))) & (S >= c["s_lo"])
    high_tex = texture >= cfg["disease_texture_min"]
    disease  = redish & veg0 & (high_tex | (S >= 80)) & (V >= c["v"][0]) & (V <= c["v"][1])
    commit(disease, 2, 0.4 + 0.6 * tex_score)

    # Stressed: exg_veg_thr (bukan 0.02) agar tidak match tanah kuning pucat
    c = cfg["stressed"]
    stressed = (H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) & \
               (V >= c["v"][0]) & (exg >= cfg["exg_veg_thr"])
    commit(stressed, 1, 0.4 + 0.4 * sat_score)

    # Drought: wajib vegetasi — mencegah false positive di lahan pucat tak bervegetasi
    c = cfg["drought"]
    drought = (H >= c["h"][0]) & (H < c["h"][1]) & (S >= c["s_lo"]) & \
              (V >= c["v"][0]) & veg0
    commit(drought, 3, 0.45 + 0.35 * sat_score)

    veg   = exg > cfg["exg_veg_thr"]
    soil2 = (S <= cs["s_max"] + 6) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & (~veg)
    commit(soil2, 4, 0.4 + 0.3 * (1 - sat_score))

    commit(~assigned, IGNORE["background"], 0.3)
    return label, conf


def reassign_stressed_to_healthy(label, hsv, exg, cfg):
    if not cfg.get("stressed_to_healthy", False):
        return label
    H = hsv[:, :, 0].astype(np.int16)
    S = hsv[:, :, 1].astype(np.int16)
    yh = cfg.get("yellow_h", [20, 34])
    truly_yellow = (
        (H >= yh[0]) & (H <= yh[1])
        & (exg < cfg.get("yellow_exg_max", 0.08))
        & (S >= cfg.get("yellow_s_min", 55))
    )
    out = label.copy()
    out[(label == 1) & (~truly_yellow)] = 0
    return out


def suppress_structures(label, cfg):
    if not cfg.get("suppress_structures", False):
        return label
    bare = (label == 4).astype(np.uint8)
    if bare.sum() == 0:
        return label
    k      = cfg["road_open_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    opened = cv2.morphologyEx(bare, cv2.MORPH_OPEN, kernel)
    label[(bare > 0) & (opened == 0)] = IGNORE["background"]
    n, lab, stats, _ = cv2.connectedComponentsWithStats(opened, 8)
    min_area = int(cfg["struct_min_area_frac"] * label.size)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < min_area:
            label[lab == i] = IGNORE["background"]
    return label


def smooth_labels(label, cfg):
    return cv2.medianBlur(label, cfg["label_median"] | 1)


def extract_regions(label, conf, cfg):
    h, w      = label.shape
    min_area  = int(cfg["min_region_area_frac"] * h * w)
    max_area  = int(cfg.get("max_region_area_frac", 0.40) * h * w)
    ksz       = cfg["morph_kernel"] | 1
    kernel    = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ksz, ksz))
    regions   = []
    for idx, name in enumerate(CLASSES):
        mask = (label == idx).astype(np.uint8)
        if mask.sum() == 0:
            continue
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,  kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, lab, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = stats[i, cv2.CC_STAT_AREA]
            if area < min_area or area > max_area:
                continue
            x, y, ww, hh = (stats[i, cv2.CC_STAT_LEFT], stats[i, cv2.CC_STAT_TOP],
                             stats[i, cv2.CC_STAT_WIDTH], stats[i, cv2.CC_STAT_HEIGHT])
            c = float(conf[lab == i].mean())
            regions.append({"class": name, "bbox": [int(x), int(y), int(ww), int(hh)],
                            "area": int(area), "confidence": round(c, 3)})
    regions.sort(key=lambda r: -r["area"])
    return regions


def class_distribution(label):
    valid = 0
    dist  = {}
    for idx, name in enumerate(CLASSES):
        c = int((label == idx).sum())
        dist[name] = c
        valid += c
    pct = {k: round(100.0 * v / max(valid, 1), 2) for k, v in dist.items()}
    return pct, valid / label.size


def colorize(label):
    h, w = label.shape
    out = np.zeros((h, w, 3), np.uint8)
    for idx, name in enumerate(CLASSES):
        out[label == idx] = PALETTE[name]
    out[label == IGNORE["background"]] = PALETTE["background"]
    out[label == IGNORE["shadow"]]     = PALETTE["shadow"]
    return out


# ---------------------------------------------------------------------------
# YOLO FUSION (port dari moonharvest_fusion.py)
# ---------------------------------------------------------------------------
def _hsv_soft(cls_id, conf):
    n = len(CLASSES)
    v = np.full(n, (1 - conf) / max(n - 1, 1), np.float32)
    v[cls_id] = conf
    return v.tolist()


def run_yolo_on_regions(frame, model, regions, max_regions=80):
    """Classify tiap HSV region dengan YOLO; fallback ke HSV jika patch kecil/conf rendah."""
    n = len(CLASSES)

    def _fallback(r):
        r["yolo_class"]     = r["class"]
        r["yolo_conf"]      = r["confidence"]
        r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
        r["yolo_valid"]     = False

    for r in regions[:max_regions]:
        x, y, w, h = r["bbox"]
        if w < YOLO_MIN_PATCH or h < YOLO_MIN_PATCH:
            _fallback(r)
            continue
        patch = frame[y:y + h, x:x + w]
        if patch.size == 0:
            _fallback(r)
            continue
        try:
            res      = model(patch, verbose=False, imgsz=224, device=0)[0]
            raw      = res.probs.data.cpu().numpy()
            hsv_order = np.zeros(n, np.float32)
            for yi, hi in enumerate(YOLO_TO_HSV):
                if yi < len(raw):
                    hsv_order[hi] = float(raw[yi])
            top      = int(np.argmax(hsv_order))
            top_conf = float(hsv_order[top])
            if top_conf < YOLO_MIN_CONF:
                _fallback(r)
                continue
            r["yolo_class"]     = CLASSES[top]
            r["yolo_conf"]      = top_conf
            r["yolo_probs_hsv"] = hsv_order.tolist()
            r["yolo_valid"]     = True
        except Exception:
            _fallback(r)

    for r in regions[max_regions:]:
        _fallback(r)

    return regions


def fuse_region(r, alpha=0.55):
    """
    Adaptive confidence-gated fusion (5 kasus, prioritas dari atas):
      1. YOLO tidak valid (patch kecil/conf rendah) → pakai HSV
      2. HSV_WINS_CONFLICT                          → HSV menang
      3. Keduanya setuju                            → boost confidence
      4. Selisih confidence besar                   → pakai yang lebih yakin
      5. Konflik kecil                              → CLASS_ALPHA_YOLO blend
    """
    hsv_cls  = r["class"]
    yolo_cls = r["yolo_class"]

    if not r.get("yolo_valid", False):
        return hsv_cls, r["confidence"], True

    yolo_conf = r["yolo_conf"]
    hsv_conf  = r["confidence"]

    yp = np.array(r["yolo_probs_hsv"], np.float32)
    hp = np.array(_hsv_soft(HSV_IDX[hsv_cls], hsv_conf), np.float32)
    yp /= yp.sum() + 1e-9
    hp /= hp.sum() + 1e-9

    if (hsv_cls, yolo_cls) in HSV_WINS_CONFLICT:
        return hsv_cls, round(hsv_conf, 3), False

    agree = yolo_cls == hsv_cls
    if agree:
        cls_alpha  = CLASS_ALPHA_YOLO.get(hsv_cls, alpha)
        fused      = cls_alpha * yp + (1 - cls_alpha) * hp
        fused_id   = int(np.argmax(fused))
        fused_conf = min(1.0, max(yolo_conf, hsv_conf) * 1.05)
        return CLASSES[fused_id], round(fused_conf, 3), True

    gap = abs(yolo_conf - hsv_conf)
    if gap > CONF_AGREE_GAP:
        if yolo_conf > hsv_conf:
            return yolo_cls, round(yolo_conf, 3), False
        else:
            return hsv_cls, round(hsv_conf, 3), False

    cls_alpha = CLASS_ALPHA_YOLO.get(hsv_cls, alpha)
    total     = yolo_conf + hsv_conf + 1e-9
    w_conf    = yolo_conf / total
    eff_yolo  = 0.5 * cls_alpha + 0.5 * w_conf
    eff_hsv   = 1.0 - eff_yolo
    fused     = eff_yolo * yp + eff_hsv * hp
    fused_id  = int(np.argmax(fused))
    return CLASSES[fused_id], round(float(fused[fused_id]), 3), False


# ---------------------------------------------------------------------------
# NMS — hilangkan bounding box yang tumpang tindih (sesuai proposal Figure 3)
# ---------------------------------------------------------------------------
def apply_nms(regions, iou_threshold=0.25, score_threshold=0.20):
    """
    Two-pass NMS untuk meminimalkan box tumpang tindih:
    Pass 1 — per-class NMS agresif (IoU 0.25)
    Pass 2 — cross-class containment: hapus box A jika >60% luas A ada di dalam
              box B yang lebih besar (kelas apapun)
    """
    if len(regions) <= 1:
        return regions

    # --- Pass 1: per-class NMS ---
    kept = []
    for cls_name in CLASSES:
        cls_reg = [r for r in regions if r["class"] == cls_name]
        if not cls_reg:
            continue
        if len(cls_reg) == 1:
            kept.extend(cls_reg)
            continue
        boxes  = [[r["bbox"][0], r["bbox"][1], r["bbox"][2], r["bbox"][3]] for r in cls_reg]
        scores = [r["confidence"] for r in cls_reg]
        try:
            idx = cv2.dnn.NMSBoxes(boxes, scores, score_threshold, iou_threshold)
            if len(idx) > 0:
                kept.extend(cls_reg[i] for i in idx.flatten())
        except Exception:
            kept.extend(cls_reg)

    # Urutkan: box terbesar (area) → terkecil
    kept.sort(key=lambda r: -r["area"])

    # --- Pass 2: cross-class containment suppression ---
    # Hapus box A jika >60% luasnya tercakup oleh box B yang lebih besar
    final = []
    for i, a in enumerate(kept):
        ax1, ay1, aw, ah = a["bbox"]
        ax2, ay2 = ax1 + aw, ay1 + ah
        absorbed = False
        for b in kept:
            if b is a:
                continue
            if b["area"] <= a["area"]:
                continue   # hanya cek box yang lebih besar
            bx1, by1, bw, bh = b["bbox"]
            bx2, by2 = bx1 + bw, by1 + bh
            ix = max(0, min(ax2, bx2) - max(ax1, bx1))
            iy = max(0, min(ay2, by2) - max(ay1, by1))
            if ix == 0 or iy == 0:
                continue
            inter = ix * iy
            if inter / (aw * ah + 1e-6) > 0.60:
                absorbed = True
                break
        if not absorbed:
            final.append(a)

    # Batasi maksimum 15 box paling besar+confident
    final.sort(key=lambda r: -(r["area"] * r["confidence"]))
    return final[:15]


# ---------------------------------------------------------------------------
# RENDER
# ---------------------------------------------------------------------------
def draw_detections(frame, label, regions, alpha=0.35):
    """Bounding boxes saja (tanpa HSV overlay) menggunakan fused_class/fused_conf."""
    out = frame.copy()

    for r in regions[:40]:
        cls     = r.get("fused_class", r["class"])
        conf    = r.get("fused_conf",  r["confidence"])
        display = DISPLAY_MAP.get(cls)
        if display is None:
            continue
        col  = DEMO_PALETTE.get(display, (200, 200, 200))
        x, y, w, h = r["bbox"]
        cv2.rectangle(out, (x, y), (x + w, y + h), col, 2)

        txt = f"{display} {conf:.2f}"
        (tw, th), bl = cv2.getTextSize(txt, cv2.FONT_HERSHEY_SIMPLEX, 0.50, 1)
        ly = max(y - 4, th + 4)
        cv2.rectangle(out, (x, ly - th - 3), (x + tw + 6, ly + bl), col, -1)
        cv2.putText(out, txt, (x + 3, ly), cv2.FONT_HERSHEY_SIMPLEX, 0.50,
                    (255, 255, 255), 1, cv2.LINE_AA)

    return out


# ---------------------------------------------------------------------------
# PROCESS FRAME — HSV pipeline + YOLO fusion (jika model tersedia)
# ---------------------------------------------------------------------------
def process_frame(bgr, cfg, ema_state=None, model=None):
    proc, hsv = preprocess(bgr, cfg)
    exg       = excess_green(proc)
    gray      = cv2.cvtColor(proc, cv2.COLOR_BGR2GRAY)
    tex       = local_std(gray, cfg["texture_win"])

    label, conf = classify_pixels(hsv, exg, tex, cfg)
    label = reassign_stressed_to_healthy(label, hsv, exg, cfg)
    if cfg.get("dark_as_soil"):
        label[hsv[:, :, 2] < cfg.get("dark_v_max", 55)] = 4
    label   = smooth_labels(label, cfg)
    label   = suppress_structures(label, cfg)
    regions = extract_regions(label, conf, cfg)
    regions = apply_nms(regions)   # hapus bounding box tumpang tindih (NMS per kelas)

    # YOLO per-region fusion (jika model tersedia)
    if model is not None:
        regions = run_yolo_on_regions(proc, model, regions)
        for r in regions:
            fc, fconf, _ = fuse_region(r)
            r["fused_class"] = fc
            r["fused_conf"]  = fconf
    else:
        for r in regions:
            r["fused_class"] = r["class"]
            r["fused_conf"]  = r["confidence"]

    pct, _ = class_distribution(label)

    if ema_state is not None:
        a = cfg["ema_alpha"]
        for k in pct:
            ema_state[k] = a * pct[k] + (1 - a) * ema_state.get(k, pct[k])
        smooth_pct = ema_state
    else:
        smooth_pct = pct

    return regions, label, smooth_pct, proc


# ---------------------------------------------------------------------------
# STREAMING SUMMARY
# ---------------------------------------------------------------------------
def build_demo_counts(regions, pct):
    """
    Hitung distribusi kelas dari fused_class → key v5 yang dibaca GCS C#:
    "Healthy", "Stress", "Drought", "Bare Soil"
    """
    # Pemetaan display label → key GCS
    DISPLAY_TO_GCS = {
        "Lush Green":              "Healthy",
        "Inconsistent Growth":     "Stress",
        "Drought/Severe Stress":   "Drought",
        "Bare Soil / Gap":         "Bare Soil",
    }

    if regions:
        counts = {"Healthy": 0.0, "Stress": 0.0, "Drought": 0.0, "Bare Soil": 0.0}
        total_area = sum(r["area"] for r in regions
                        if DISPLAY_MAP.get(r.get("fused_class", r["class"])) is not None)
        if total_area > 0:
            for r in regions:
                cls     = r.get("fused_class", r["class"])
                display = DISPLAY_MAP.get(cls)
                if display is None:
                    continue
                gcs_key = DISPLAY_TO_GCS.get(display)
                if gcs_key:
                    counts[gcs_key] += r["area"] / total_area * 100.0
            return {k: round(v, 1) for k, v in counts.items() if v > 0}

    # fallback: pixel-based percentage
    h = round(pct.get("healthy_crop", 0), 1)
    s = round(pct.get("stressed_crop", 0), 1)
    d = round(pct.get("drought_stress", 0) + pct.get("disease_stress_vegetation", 0), 1)
    b = round(pct.get("bare_soil", 0), 1)
    return {"Healthy": h, "Stress": s, "Drought": d, "Bare Soil": b}


# ---------------------------------------------------------------------------
# MAIN
# ---------------------------------------------------------------------------
def emit(payload):
    print(json.dumps(payload), flush=True)


def main():
    global DEMO_MODE

    parser = argparse.ArgumentParser()
    parser.add_argument("--source",     required=True)
    parser.add_argument("--model",      default=None)
    parser.add_argument("--max-fps",    type=float, default=15.0)
    parser.add_argument("--playback-rate", type=float, default=1.0,
                        help="Playback speed multiplier. 0.75 = 25%% slower, 1.0 = normal.")
    parser.add_argument("--demo",       action="store_true")
    parser.add_argument("--no-overlay", action="store_true")
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

    # Kirim frame preview secepat mungkin. YOLO/ultralytics bisa lambat saat import,
    # jadi UI tidak perlu menunggu model siap hanya untuk menampilkan video.
    ret_preview, preview = cap.read()
    if ret_preview and preview is not None:
        ok, buf = cv2.imencode(".jpg", preview, [cv2.IMWRITE_JPEG_QUALITY, 75])
        if ok:
            emit({"type": "frame", "data": base64.b64encode(buf).decode()})
        if not isinstance(src, int):
            cap.set(cv2.CAP_PROP_POS_FRAMES, 0)

    # --- Load YOLO (opsional) setelah preview keluar ---
    yolo_model = None
    if args.model and os.path.isfile(args.model):
        if ensure_yolo_available():
            try:
                import torch as _torch
                yolo_model = _YOLO(args.model)
                yolo_model.to("cuda" if _torch.cuda.is_available() else "cpu")
                _dev = "GPU" if _torch.cuda.is_available() else "CPU"
                emit({"type": "info", "data": f"YOLO fusion aktif: {os.path.basename(args.model)} [{_dev}]"})
            except Exception as e:
                emit({"type": "info", "data": f"YOLO load gagal ({e}), HSV-only mode"})
        else:
            emit({"type": "info", "data": "ultralytics tidak tersedia, HSV-only mode"})
    else:
        emit({"type": "info", "data": "HSV-only mode"})

    src_fps     = cap.get(cv2.CAP_PROP_FPS) or 30.0
    target_fps  = min(float(args.max_fps), src_fps)
    step        = max(1, int(round(src_fps / target_fps)))
    playback_rate = max(0.25, min(2.0, float(args.playback_rate)))
    frame_delay = (step / src_fps) / playback_rate

    # 640px: ~32ms/frame → 31fps ceiling, lebih cepat dari 720px (44ms)
    PROC_W     = 640
    YOLO_EVERY = 30

    # Antrian antar compute thread dan emit thread — max 1 item (frame drop alami)
    _emit_q: queue.Queue = queue.Queue(maxsize=1)
    _stop   = threading.Event()

    # -----------------------------------------------------------------------
    # COMPUTE THREAD — baca frame, proses HSV, taruh hasil ke queue
    # -----------------------------------------------------------------------
    def compute_loop():
        yolo_cache = {}
        emit_count = 0
        frame_idx  = 0
        ema_state  = {}
        eof_retries = 0

        while not _stop.is_set():
            ret, frame = cap.read()
            if not ret or frame is None:
                if DEMO_MODE and not isinstance(src, int):
                    cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                    frame_idx = 0
                    eof_retries += 1
                    if eof_retries <= 3:
                        continue
                _emit_q.put(None)   # sinyal selesai
                break
            eof_retries = 0
            frame_idx += 1
            if frame_idx % step != 0:
                continue

            fh, fw = frame.shape[:2]
            if fw > PROC_W:
                frame = cv2.resize(frame, (PROC_W, int(fh * PROC_W / fw)))

            emit_count += 1
            use_yolo = (yolo_model is not None) and (emit_count % YOLO_EVERY == 1)

            try:
                regions, label, smooth_pct, _ = process_frame(
                    frame, CFG, ema_state,
                    model=yolo_model if use_yolo else None)
            except Exception as exc:
                emit({"type": "error", "data": str(exc)})
                continue

            if use_yolo:
                for r in regions:
                    yolo_cache[r["class"]] = {
                        "fused_class": r.get("fused_class", r["class"]),
                        "fused_conf":  r.get("fused_conf",  r["confidence"]),
                    }
            else:
                for r in regions:
                    c = yolo_cache.get(r["class"])
                    if c:
                        r["fused_class"] = c["fused_class"]
                        r["fused_conf"]  = c["fused_conf"]

            annotated = draw_detections(frame, label, regions)

            counts  = build_demo_counts(regions, smooth_pct)
            visible = {k: v for k, v in counts.items() if v > 0}
            parts   = sorted(visible.items(), key=lambda x: -x[1])
            summary = " | ".join(f"{lbl}: {cnt:.0f}%" for lbl, cnt in parts)
            n_boxes = sum(1 for r in regions
                         if DISPLAY_MAP.get(r.get("fused_class", r["class"])) is not None)

            ok, buf = cv2.imencode(".jpg", annotated, [cv2.IMWRITE_JPEG_QUALITY, 75])
            if not ok:
                continue

            payload = {
                "jpg":     base64.b64encode(buf).decode(),
                "count":   n_boxes,
                "summary": summary,
                "classes": visible,
            }
            # put_nowait: jika antrian penuh (emit thread lambat), drop frame ini
            try:
                _emit_q.put_nowait(payload)
            except queue.Full:
                pass   # frame drop — emit thread masih sibuk, tidak apa-apa

    # -----------------------------------------------------------------------
    # EMIT THREAD (main thread) — baca dari queue, kirim ke stdout
    # -----------------------------------------------------------------------
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
            }})

            # Sleep agar video berjalan di kecepatan sumber
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
