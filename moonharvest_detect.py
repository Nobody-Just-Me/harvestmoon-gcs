#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MoonHarvest Standalone Detector — HSV + YOLO Fusion
=====================================================
Satu file lengkap: HSV segmentasi + YOLO v1 per-region fusion.

Penggunaan:
  python3 moonharvest_detect.py video  -i gabung.mp4  -o out/
  python3 moonharvest_detect.py video  -i gabung.mp4  -o out/ --no-display
  python3 moonharvest_detect.py image  -i frame.jpg   -o out/
  python3 moonharvest_detect.py hsv    -i gabung.mp4  -o out/   [HSV saja, tanpa YOLO]

Model YOLO default:
  runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt

Kelas v1 (5 kelas):
  0=bare_soil  1=disease_stress_vegetation  2=drought_stress
  3=healthy_crop  4=stressed_crop
"""
import argparse, json, os, sys, time, threading, csv
import cv2
import numpy as np

# =============================================================================
# KONFIGURASI HSV (dikalibrasi dari gabung.mp4 UAV footage)
# =============================================================================
HSV_CLASSES = [
    "healthy_crop",
    "stressed_crop",
    "disease_stress_vegetation",
    "drought_stress",
    "bare_soil",
]

PALETTE = {
    "healthy_crop":              ( 60, 200,  60),
    "stressed_crop":             ( 40, 220, 230),
    "disease_stress_vegetation": ( 40,  40, 230),
    "drought_stress":            ( 30, 140, 250),
    "bare_soil":                 (110, 120, 140),
    "background":                (200, 160,  90),
    "shadow":                    ( 50,  50,  50),
}

SEVERITY = {
    "healthy_crop": 0.0,
    "stressed_crop": 0.45,
    "drought_stress": 0.75,
    "disease_stress_vegetation": 1.0,
    "bare_soil": 0.0,
}

# Threshold HSV — dikalibrasi dari gabung.mp4
DEFAULT_CFG = {
    "white_balance": True,
    "clahe_clip": 2.0,
    "clahe_grid": 8,
    "shadow_v_max": 45,
    "exg_veg_thr": 0.0213,
    "exg_healthy_min": 0.0693,
    "bg_h": [100, 140],
    "bg_s_min": 34,
    "healthy":  {"h": [30, 100], "s_lo": 15, "v": [69, 255]},
    "stressed": {"h": [15,  46], "s_lo": 15, "v": [80, 255]},
    "drought":  {"h": [ 8,  16], "s_lo": 65, "v": [80, 235]},
    "disease":  {"h1": [0, 10], "h2": [168, 179], "s_lo": 45, "v": [25, 215]},
    "soil":     {"s_max": 18, "v": [110, 240]},
    "texture_win": 9,
    "disease_texture_min": 16.0,
    "morph_kernel": 5,
    "label_median": 7,
    "min_region_area_frac": 0.0015,
    "suppress_structures": True,
    "road_open_kernel": 11,
    "struct_min_area_frac": 0.0008,
    "stat_reject_maha": 90.0,
    "stressed_to_healthy": True,
    "yellow_h": [18, 36],
    "yellow_exg_max": 0.085,
    "yellow_s_min": 36,
    "dark_as_soil": True,
    "dark_v_max": 62,
    "ema_alpha": 0.4,
}

IGNORE = {"background": 250, "shadow": 251, "unknown": 255}

# =============================================================================
# KONFIGURASI FUSION
# =============================================================================
YOLO_CLASSES  = sorted(HSV_CLASSES)          # urutan alfabet = urutan training
YOLO_TO_HSV   = [HSV_CLASSES.index(c) for c in YOLO_CLASSES]
HSV_IDX       = {c: i for i, c in enumerate(HSV_CLASSES)}

YOLO_MIN_CONF  = 0.40
YOLO_MIN_PATCH = 48
CONF_AGREE_GAP = 0.25

CLASS_ALPHA_YOLO = {
    "healthy_crop":              0.20,
    "stressed_crop":             0.55,
    "disease_stress_vegetation": 0.45,
    "drought_stress":            0.50,
    "bare_soil":                 0.45,
}

HSV_WINS_CONFLICT = {
    ("healthy_crop",  "stressed_crop"),
    ("healthy_crop",  "disease_stress_vegetation"),
    ("stressed_crop", "disease_stress_vegetation"),
}

COLOR_AGREE    = (  0, 200,  60)   # hijau
COLOR_DISAGREE = (200,  60,   0)   # biru (BGR)

# Label display (internal → nama proposal)
DISPLAY_MAP = {
    "healthy_crop":              "Lush Green",
    "stressed_crop":             "Inconsistent Growth",
    "disease_stress_vegetation": "Disease",
    "drought_stress":            "Soil Issues",
    "bare_soil":                 None,   # disembunyikan
}
DEMO_PALETTE = {
    "Lush Green":          ( 50, 205,  50),
    "Inconsistent Growth": (  0, 200, 255),
    "Disease":             (  0,  60, 255),
    "Soil Issues":         ( 55,  64,  93),
}

# Status Field Health Index
def _fhi_status(fhi):
    """Return (label, warna BGR) berdasarkan nilai FHI."""
    if fhi >= 75:
        return "BAIK",      ( 50, 200,  50)
    elif fhi >= 50:
        return "PERHATIAN", (  0, 200, 255)
    else:
        return "KRITIS",    (  0,  60, 255)

# Path model default
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_WEIGHTS = os.path.join(
    _SCRIPT_DIR,
    "runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt"
)


# =============================================================================
# HSV PIPELINE
# =============================================================================

def _gray_world_wb(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean()+1e-6, g.mean()+1e-6, r.mean()+1e-6
    k = (mb + mg + mr) / 3.0
    b = np.clip(b * (k/mb), 0, 255)
    g = np.clip(g * (k/mg), 0, 255)
    r = np.clip(r * (k/mr), 0, 255)
    return cv2.merge([b, g, r]).astype(np.uint8)


def _preprocess(bgr, cfg):
    out = _gray_world_wb(bgr) if cfg["white_balance"] else bgr.copy()
    hsv = cv2.cvtColor(out, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    clahe = cv2.createCLAHE(clipLimit=cfg["clahe_clip"],
                            tileGridSize=(cfg["clahe_grid"], cfg["clahe_grid"]))
    v = clahe.apply(v)
    return out, cv2.merge([h, s, v])


def _excess_green(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    tot = b + g + r + 1e-6
    return (2*g - r - b) / tot


def _local_std(gray, win):
    g = gray.astype(np.float32)
    mean = cv2.boxFilter(g, -1, (win, win))
    sq   = cv2.boxFilter(g*g, -1, (win, win))
    return np.sqrt(np.clip(sq - mean*mean, 0, None))


def _classify_pixels(hsv, exg, texture, cfg):
    H, S, V = hsv[:,:,0], hsv[:,:,1], hsv[:,:,2]
    h, w = H.shape
    label    = np.full((h, w), IGNORE["unknown"], np.uint8)
    conf     = np.zeros((h, w), np.float32)
    assigned = np.zeros((h, w), bool)

    def commit(mask, code, score):
        m = mask & ~assigned
        label[m] = code
        conf[m] = np.clip(score[m] if isinstance(score, np.ndarray) else score, 0, 1)
        assigned.__ior__(m)

    sat_score = np.clip(S / 180.0, 0, 1)
    exg_score = np.clip((exg + 0.05) / 0.3, 0, 1)
    tex_score = np.clip(texture / 40.0, 0, 1)

    commit(V < cfg["shadow_v_max"], IGNORE["shadow"], 0.5)

    cs = cfg["soil"]
    veg0 = exg > cfg["exg_veg_thr"]
    soil = (S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & (~veg0)
    commit(soil, 4, 0.45 + 0.3*(1 - np.clip(S/180.0, 0, 1)))

    bg = (H >= cfg["bg_h"][0]) & (H <= cfg["bg_h"][1]) & (S >= cfg["bg_s_min"])
    commit(bg, IGNORE["background"], 0.5)

    c = cfg["healthy"]
    healthy = ((H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) &
               (V >= c["v"][0]) & (exg >= cfg["exg_healthy_min"]))
    commit(healthy, 0, 0.45 + 0.25*sat_score + 0.3*exg_score)

    c = cfg["disease"]
    redish = (((H >= c["h1"][0]) & (H <= c["h1"][1])) |
              ((H >= c["h2"][0]) & (H <= c["h2"][1]))) & (S >= c["s_lo"])
    high_tex = texture >= cfg["disease_texture_min"]
    disease = redish & (high_tex | (S >= 80)) & (V >= c["v"][0]) & (V <= c["v"][1])
    commit(disease, 2, 0.4 + 0.6*tex_score)

    c = cfg["stressed"]
    stressed = ((H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) &
                (V >= c["v"][0]) & (exg >= 0.02))
    commit(stressed, 1, 0.4 + 0.4*sat_score)

    c = cfg["drought"]
    drought = ((H >= c["h"][0]) & (H < c["h"][1]) & (S >= c["s_lo"]) & (V >= c["v"][0]))
    commit(drought, 3, 0.45 + 0.35*sat_score)

    cs = cfg["soil"]
    veg = exg > cfg["exg_veg_thr"]
    soil2 = (S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & (~veg)
    commit(soil2, 4, 0.4 + 0.3*(1-sat_score))

    commit(~assigned, IGNORE["background"], 0.3)
    return label, conf


def _reassign_stressed(label, hsv, exg, cfg):
    if not cfg.get("stressed_to_healthy", False):
        return label
    H = hsv[:,:,0].astype(np.int16)
    S = hsv[:,:,1].astype(np.int16)
    yh = cfg.get("yellow_h", [20, 34])
    truly_yellow = (
        (H >= yh[0]) & (H <= yh[1])
        & (exg < cfg.get("yellow_exg_max", 0.08))
        & (S >= cfg.get("yellow_s_min", 55))
    )
    out = label.copy()
    out[(label == 1) & (~truly_yellow)] = 0
    return out


def _suppress_structures(label, cfg):
    if not cfg.get("suppress_structures", False):
        return label
    bare = (label == 4).astype(np.uint8)
    if bare.sum() == 0:
        return label
    k = cfg["road_open_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    opened = cv2.morphologyEx(bare, cv2.MORPH_OPEN, kernel)
    label[(bare > 0) & (opened == 0)] = IGNORE["background"]
    n, lab, stats, _ = cv2.connectedComponentsWithStats(opened, 8)
    min_area = int(cfg["struct_min_area_frac"] * label.size)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < min_area:
            label[lab == i] = IGNORE["background"]
    return label


def _extract_regions(label, conf, cfg):
    h, w = label.shape
    min_area = int(cfg["min_region_area_frac"] * h * w)
    ksz = cfg["morph_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ksz, ksz))
    regions = []
    for idx, name in enumerate(HSV_CLASSES):
        mask = (label == idx).astype(np.uint8)
        if mask.sum() == 0:
            continue
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, lab, stats, cent = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = stats[i, cv2.CC_STAT_AREA]
            if area < min_area:
                continue
            x = stats[i, cv2.CC_STAT_LEFT]
            y = stats[i, cv2.CC_STAT_TOP]
            ww = stats[i, cv2.CC_STAT_WIDTH]
            hh = stats[i, cv2.CC_STAT_HEIGHT]
            c = float(conf[lab == i].mean())
            regions.append({
                "class": name,
                "bbox": [int(x), int(y), int(ww), int(hh)],
                "area": int(area),
                "confidence": round(c, 3),
                "centroid": [round(float(cent[i][0]),1), round(float(cent[i][1]),1)],
            })
    regions.sort(key=lambda r: -r["area"])
    return regions


def _class_distribution(label):
    total = label.size
    dist = {}
    valid = 0
    for idx, name in enumerate(HSV_CLASSES):
        c = int((label == idx).sum())
        dist[name] = c
        valid += c
    pct = {k: round(100.0 * v / max(valid, 1), 2) for k, v in dist.items()}
    field_health = 100.0 - sum(SEVERITY[k] * pct[k] for k in HSV_CLASSES)
    return pct, round(max(field_health, 0.0), 1)


def _colorize(label):
    h, w = label.shape
    out = np.zeros((h, w, 3), np.uint8)
    for idx, name in enumerate(HSV_CLASSES):
        out[label == idx] = PALETTE[name]
    out[label == IGNORE["background"]] = PALETTE["background"]
    out[label == IGNORE["shadow"]] = PALETTE["shadow"]
    return out


def _draw_summary_sidebar(frame, fhi, pct):
    """
    Tambahkan sidebar kiri berisi FHI + status + bar distribusi kelas.
    Lebar sidebar 220px, background semi-transparan hitam.
    """
    h, w = frame.shape[:2]
    sw = 220
    out = frame.copy()
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (sw, h), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.60, out, 0.40, 0, out)

    status_lbl, status_col = _fhi_status(fhi)

    # Judul
    cv2.putText(out, "MoonHarvest", (8, 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.52, (200, 200, 200), 1, cv2.LINE_AA)
    cv2.putText(out, "Crop Monitor", (8, 38),
                cv2.FONT_HERSHEY_SIMPLEX, 0.45, (140, 140, 140), 1, cv2.LINE_AA)

    # FHI besar + status
    cv2.rectangle(out, (6, 48), (sw-6, 106), status_col, -1)
    cv2.putText(out, f"FHI  {fhi:.1f}", (12, 78),
                cv2.FONT_HERSHEY_SIMPLEX, 0.80, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(out, status_lbl, (12, 100),
                cv2.FONT_HERSHEY_SIMPLEX, 0.48, (255, 255, 255), 1, cv2.LINE_AA)

    # Bar distribusi kelas (hanya yang ada di DISPLAY_MAP dan nilainya > 0)
    y_cur = 118
    cv2.putText(out, "Distribusi Kelas:", (8, y_cur),
                cv2.FONT_HERSHEY_SIMPLEX, 0.38, (170, 170, 170), 1, cv2.LINE_AA)
    y_cur += 14

    bar_max_w = sw - 16
    for internal, display in DISPLAY_MAP.items():
        if display is None:
            continue
        val = pct.get(internal, 0.0)
        col = DEMO_PALETTE.get(display, (160, 160, 160))
        bar_w = int(bar_max_w * val / 100.0)
        if bar_w > 0:
            cv2.rectangle(out, (8, y_cur), (8 + bar_w, y_cur + 12), col, -1)
        cv2.putText(out, f"{display[:14]}: {val:.1f}%", (8, y_cur + 11),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.34, (240, 240, 240), 1, cv2.LINE_AA)
        y_cur += 20

    return out


def hsv_process_frame(bgr, cfg, ema_state=None):
    """Proses satu frame dengan pipeline HSV murni. Return (result_dict, overlay)."""
    proc, hsv = _preprocess(bgr, cfg)
    exg  = _excess_green(proc)
    gray = cv2.cvtColor(proc, cv2.COLOR_BGR2GRAY)
    tex  = _local_std(gray, cfg["texture_win"])

    label, conf = _classify_pixels(hsv, exg, tex, cfg)
    label = _reassign_stressed(label, hsv, exg, cfg)
    if cfg.get("dark_as_soil"):
        label[hsv[:,:,2] < cfg.get("dark_v_max", 55)] = 4

    label = cv2.medianBlur(label, cfg["label_median"] | 1)
    label = _suppress_structures(label, cfg)

    regions = _extract_regions(label, conf, cfg)
    pct, health = _class_distribution(label)

    if ema_state is not None:
        a = cfg["ema_alpha"]
        for k in pct:
            ema_state[k] = a * pct[k] + (1-a) * ema_state.get(k, pct[k])
        health = 100.0 - sum(SEVERITY[k] * ema_state[k] for k in HSV_CLASSES)
        health = round(max(health, 0.0), 1)

    # Buat overlay
    color = _colorize(label)
    overlay = cv2.addWeighted(proc, 0.55, color, 0.45, 0)
    for r in regions[:15]:   # batasi 15 region terbesar
        x, y, w, h = r["bbox"]
        display = DISPLAY_MAP.get(r["class"])
        if display is None:
            continue
        col = DEMO_PALETTE.get(display, PALETTE[r["class"]])
        cv2.rectangle(overlay, (x, y), (x+w, y+h), col, 2)
        lbl = f"{display} {r['confidence']:.2f}"
        (tw, th), _ = cv2.getTextSize(lbl, cv2.FONT_HERSHEY_SIMPLEX, 0.42, 1)
        cv2.rectangle(overlay, (x, max(0, y-th-6)), (x+tw+4, y), col, -1)
        cv2.putText(overlay, lbl, (x+2, max(th, y-4)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255,255,255), 1, cv2.LINE_AA)
    overlay = _draw_summary_sidebar(overlay, health, pct)

    result = {
        "field_health": round(health, 1),
        "class_pct": pct,
        "n_regions": len(regions),
        "regions": regions,
    }
    return result, overlay


def _hsv_soft(cls_id, conf):
    n = len(HSV_CLASSES)
    v = np.full(n, (1-conf) / max(n-1, 1), np.float32)
    v[cls_id] = conf
    return v.tolist()


def _run_yolo_on_regions(frame, yolo_model, regions, max_regions=80):
    n = len(HSV_CLASSES)

    def fallback(r):
        r["yolo_class"]     = r["class"]
        r["yolo_conf"]      = r["confidence"]
        r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
        r["yolo_valid"]     = False

    for r in regions[:max_regions]:
        x, y, w, h = r["bbox"]
        if w < YOLO_MIN_PATCH or h < YOLO_MIN_PATCH:
            fallback(r); continue
        patch = frame[y:y+h, x:x+w]
        if patch.size == 0:
            fallback(r); continue
        try:
            res = yolo_model(patch, verbose=False, imgsz=224, device=0)[0]
            raw = res.probs.data.cpu().numpy()
            hsv_order = np.zeros(n, np.float32)
            for yi, hi in enumerate(YOLO_TO_HSV):
                if yi < len(raw):
                    hsv_order[hi] = float(raw[yi])
            top = int(np.argmax(hsv_order))
            top_conf = float(hsv_order[top])
            if top_conf < YOLO_MIN_CONF:
                fallback(r); continue
            r["yolo_class"]     = HSV_CLASSES[top]
            r["yolo_conf"]      = top_conf
            r["yolo_probs_hsv"] = hsv_order.tolist()
            r["yolo_valid"]     = True
        except Exception:
            fallback(r)

    for r in regions[max_regions:]:
        fallback(r)
    return regions


def _fuse_region(region, alpha=0.55):
    """Confidence-adaptive fusion. Lihat komentar di moonharvest_fusion.py."""
    hsv_cls  = region["class"]
    yolo_cls = region["yolo_class"]
    agree    = yolo_cls == hsv_cls

    if not region.get("yolo_valid", False):
        return hsv_cls, region["confidence"], agree

    yolo_conf = region["yolo_conf"]
    hsv_conf  = region["confidence"]
    yp = np.array(region["yolo_probs_hsv"], np.float32)
    hp = np.array(_hsv_soft(HSV_IDX[hsv_cls], hsv_conf), np.float32)
    yp /= yp.sum() + 1e-9
    hp /= hp.sum() + 1e-9

    if (hsv_cls, yolo_cls) in HSV_WINS_CONFLICT:
        return hsv_cls, round(hsv_conf, 3), False

    if agree:
        cls_alpha = CLASS_ALPHA_YOLO.get(hsv_cls, alpha)
        fused = cls_alpha * yp + (1-cls_alpha) * hp
        fconf = min(1.0, max(yolo_conf, hsv_conf) * 1.05)
        return HSV_CLASSES[int(np.argmax(fused))], round(fconf, 3), True

    gap = abs(yolo_conf - hsv_conf)
    if gap > CONF_AGREE_GAP:
        if yolo_conf > hsv_conf:
            return yolo_cls, round(yolo_conf, 3), False
        return hsv_cls, round(hsv_conf, 3), False

    cls_alpha = CLASS_ALPHA_YOLO.get(hsv_cls, alpha)
    total  = yolo_conf + hsv_conf + 1e-9
    w_conf = yolo_conf / total
    eff_y  = 0.5*cls_alpha + 0.5*w_conf
    fused  = eff_y * yp + (1-eff_y) * hp
    fused /= fused.sum() + 1e-9
    fi = int(np.argmax(fused))
    return HSV_CLASSES[fi], round(float(fused[fi]), 3), False


def _field_health_from_regions(regions, cls_key):
    area_by = {c: 0 for c in HSV_CLASSES}
    for r in regions:
        cls = r.get(cls_key, r["class"])
        if cls in area_by:
            area_by[cls] += r["area"]
    total = sum(area_by.values()) or 1
    pct = {k: 100*v/total for k, v in area_by.items()}
    return round(max(100.0 - sum(SEVERITY[k]*pct[k] for k in HSV_CLASSES), 0.0), 1)


def _draw_panel(base_frame, regions, cls_key, conf_key, title,
                orig_w, orig_h, show_agree=False, max_regions=15):
    out = base_frame.copy()
    ph, pw = out.shape[:2]
    sx, sy = pw/orig_w, ph/orig_h
    for r in regions[:max_regions]:
        x, y, w, h = r["bbox"]
        cls_name = r.get(cls_key, r["class"])
        conf     = r.get(conf_key, r["confidence"])
        display  = DISPLAY_MAP.get(cls_name)
        if display is None:      # sembunyikan bare_soil
            continue
        col     = DEMO_PALETTE.get(display, PALETTE.get(cls_name, (180,180,180)))
        px0, py0 = int(x*sx), int(y*sy)
        px1, py1 = int((x+w)*sx), int((y+h)*sy)
        box_col  = (COLOR_AGREE if r.get("agree",True) else COLOR_DISAGREE) if show_agree else col
        cv2.rectangle(out, (px0, py0), (px1, py1), box_col, 2)
        # Hanya tampilkan label jika box cukup besar (mengurangi clutter)
        if (px1 - px0) >= 45 and (py1 - py0) >= 30:
            lbl = f"{display} {conf:.2f}"
            (tw, th), _ = cv2.getTextSize(lbl, cv2.FONT_HERSHEY_SIMPLEX, 0.42, 1)
            ly = max(th + 6, py0)
            cv2.rectangle(out, (px0, ly-th-4), (px0+tw+6, ly+2), col, -1)
            cv2.putText(out, lbl, (px0+3, ly-2),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255,255,255), 1, cv2.LINE_AA)
    cv2.rectangle(out, (0, ph-22), (pw, ph), (0,0,0), -1)
    _, title_col = _fhi_status(float(title.split("=")[-1]) if "=" in title else 50)
    cv2.putText(out, title, (6, ph-6),
                cv2.FONT_HERSHEY_SIMPLEX, 0.52, title_col, 1, cv2.LINE_AA)
    return out


def _stats_bar(regions, fh_yolo, fh_hsv, fh_fused, total_w):
    bar = np.zeros((80, total_w, 3), np.uint8)
    n   = len(regions)
    ok  = sum(1 for r in regions if r.get("agree", False))
    agree_pct = 100*ok/max(n,1)
    _, fused_col = _fhi_status(fh_fused)
    _, hsv_col   = _fhi_status(fh_hsv)

    # Bar background untuk FHI fused
    cv2.rectangle(bar, (0, 0), (total_w, 80), (20, 20, 20), -1)

    fused_status, _ = _fhi_status(fh_fused)
    lines = [
        (f"Field Health {fused_status}  |  YOLO: {fh_yolo:.1f}   HSV: {fh_hsv:.1f}   FUSED: {fh_fused:.1f}",
         fused_col),
        (f"Agreement: {agree_pct:.0f}%  ({ok}/{n} regions)   |   Kotak HIJAU=setuju  BIRU=tidak setuju",
         COLOR_AGREE if agree_pct >= 50 else COLOR_DISAGREE),
        ("Fusion: confidence-adaptive HSV+YOLO v1  |  MoonHarvest UAV Crop Monitor",
         (110, 110, 110)),
    ]
    for i, (txt, col) in enumerate(lines):
        cv2.putText(bar, txt, (10, 20+i*22),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.48, col, 1, cv2.LINE_AA)
    return bar


def fusion_process_frame(bgr, cfg, yolo_model, ema_state=None, panel_w=480):
    """
    Proses satu frame: HSV segmentasi → YOLO per-region → fusion.
    Return (metrics_dict, display_3panel, regions).
    """
    # 1. HSV
    result_hsv, _ = hsv_process_frame(bgr, cfg, ema_state)
    regions = result_hsv["regions"]
    fh_hsv  = result_hsv["field_health"]

    # 2. YOLO classify tiap region
    regions = _run_yolo_on_regions(bgr, yolo_model, regions)

    # 3. Fusi
    for r in regions:
        fc, fconf, agree = _fuse_region(r)
        r["fused_class"] = fc
        r["fused_conf"]  = fconf
        r["agree"]       = agree

    fh_yolo  = _field_health_from_regions(regions, "yolo_class")
    fh_fused = _field_health_from_regions(regions, "fused_class")

    # 4. Render 3-panel
    orig_h, orig_w = bgr.shape[:2]
    ph      = int(orig_h * panel_w / orig_w)
    resized = cv2.resize(bgr, (panel_w, ph))

    p_yolo  = _draw_panel(resized, regions, "yolo_class",  "yolo_conf",
                          f"YOLO   FH={fh_yolo:.1f}", orig_w, orig_h)
    p_fused = _draw_panel(resized, regions, "fused_class", "fused_conf",
                          f"FUSED  FH={fh_fused:.1f}", orig_w, orig_h, show_agree=True)
    p_hsv   = _draw_panel(resized, regions, "class",       "confidence",
                          f"HSV    FH={fh_hsv:.1f}", orig_w, orig_h)

    combo   = np.hstack([p_yolo, p_fused, p_hsv])
    stats   = _stats_bar(regions, fh_yolo, fh_hsv, fh_fused, combo.shape[1])
    display = np.vstack([combo, stats])

    agree_pct = 100 * sum(1 for r in regions if r.get("agree")) / max(len(regions), 1)
    metrics = {
        "field_health_yolo":  fh_yolo,
        "field_health_hsv":   fh_hsv,
        "field_health_fused": fh_fused,
        "agreement_pct":      round(agree_pct, 1),
        "n_regions":          len(regions),
    }
    return metrics, display, regions


# =============================================================================
# VIDEO WRITER HELPER
# =============================================================================

def _make_writer(path, fps, w, h):
    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
    wrt = cv2.VideoWriter(path, fourcc, fps, (w, h))
    if not wrt.isOpened():
        print(f"[warn] VideoWriter gagal: {path}")
        return None
    return wrt


def _ffmpeg_reencode(path):
    import shutil, subprocess
    if not shutil.which("ffmpeg"):
        return
    tmp = path.replace(".mp4", "_h264.mp4")
    ret = subprocess.run(
        ["ffmpeg", "-y", "-i", path, "-c:v", "libx264", "-crf", "22", "-preset", "fast", tmp],
        capture_output=True
    )
    if ret.returncode == 0:
        os.replace(tmp, path)
        print(f"  Re-encoded H.264 -> {path}")


# =============================================================================
# SUBCOMMAND: fusion video
# =============================================================================

def cmd_video(args):
    try:
        from ultralytics import YOLO
    except ImportError:
        sys.exit("pip install ultralytics")

    cfg = json.loads(json.dumps(DEFAULT_CFG))
    if args.config and os.path.exists(args.config):
        with open(args.config) as f:
            cfg.update(json.load(f))

    os.makedirs(args.output, exist_ok=True)
    print(f"[fusion] model : {args.weights}")
    import torch as _torch
    yolo_model = YOLO(args.weights)
    yolo_model.to("cuda" if _torch.cuda.is_available() else "cpu")
    print(f"[fusion] kelas : {yolo_model.names}")
    print(f"[fusion] video : {args.input}")
    print(f"[fusion] tekan 'q' untuk berhenti\n")

    base = os.path.splitext(os.path.basename(args.input))[0]
    t0   = time.time()

    shared = {"display": None, "done": False, "log_rows": [], "timeline": []}
    lock   = threading.Lock()
    wr_3panel = [None]
    wr_fused  = [None]

    def compute_loop():
        cap = cv2.VideoCapture(args.input)
        if not cap.isOpened():
            with lock: shared["done"] = True
            return
        src_fps = cap.get(cv2.CAP_PROP_FPS) or 30
        step    = max(1, int(round(src_fps / max(args.fps, 0.1))))
        ema = {}
        idx = 0
        while True:
            ok, frame = cap.read()
            if not ok: break
            if idx % step == 0:
                if args.width and frame.shape[1] > args.width:
                    fh = int(frame.shape[0] * args.width / frame.shape[1])
                    frame = cv2.resize(frame, (args.width, fh))

                t = round(idx / src_fps, 2)
                metrics, display, regions = fusion_process_frame(
                    frame, cfg, yolo_model, ema, panel_w=args.panel_w)
                disp_c = np.ascontiguousarray(display)

                if not args.no_video:
                    if wr_3panel[0] is None:
                        wr_3panel[0] = _make_writer(
                            os.path.join(args.output, f"{base}_fused.mp4"),
                            args.fps, disp_c.shape[1], disp_c.shape[0])
                    if wr_3panel[0]: wr_3panel[0].write(disp_c)

                    # panel fused-only (2x ukuran panel)
                    orig_h2, orig_w2 = frame.shape[:2]
                    fw = args.panel_w * 2
                    fh2 = int(orig_h2 * fw / orig_w2)
                    fused_only = _draw_panel(
                        cv2.resize(frame, (fw, fh2)),
                        regions, "fused_class", "fused_conf",
                        f"FUSED  FH={metrics['field_health_fused']:.1f}",
                        orig_w2, orig_h2)
                    fo_c = np.ascontiguousarray(fused_only)
                    if wr_fused[0] is None:
                        wr_fused[0] = _make_writer(
                            os.path.join(args.output, f"{base}_fused_only.mp4"),
                            args.fps, fo_c.shape[1], fo_c.shape[0])
                    if wr_fused[0]: wr_fused[0].write(fo_c)

                rows = []
                for r in regions:
                    rows.append({
                        "t": t, "area": r["area"],
                        "hsv_class":   r["class"],
                        "hsv_conf":    round(r["confidence"], 3),
                        "yolo_class":  r.get("yolo_class", r["class"]),
                        "yolo_conf":   round(r.get("yolo_conf", 0.0), 3),
                        "fused_class": r.get("fused_class", r["class"]),
                        "fused_conf":  round(r.get("fused_conf", 0.0), 3),
                        "agree":       int(r.get("agree", False)),
                    })

                with lock:
                    shared["display"] = disp_c
                    shared["timeline"].append({"t": t, **metrics})
                    shared["log_rows"].extend(rows)

                n = len(shared["timeline"])
                if n % 5 == 0:
                    print(f"  frame {n:4d}  t={t:.2f}s  regions={metrics['n_regions']}  "
                          f"agree={metrics['agreement_pct']:.0f}%  "
                          f"FH yolo={metrics['field_health_yolo']:.1f}  "
                          f"hsv={metrics['field_health_hsv']:.1f}  "
                          f"fused={metrics['field_health_fused']:.1f}", flush=True)
            idx += 1

        cap.release()
        if wr_3panel[0]: wr_3panel[0].release()
        if wr_fused[0]:  wr_fused[0].release()
        with lock: shared["done"] = True

    t_compute = threading.Thread(target=compute_loop, daemon=True)
    t_compute.start()

    WIN = "MoonHarvest — YOLO | FUSED | HSV"
    if not args.no_display:
        cv2.namedWindow(WIN, cv2.WINDOW_KEEPRATIO)

    while True:
        with lock:
            done = shared["done"]
            disp = shared["display"]
        if not args.no_display:
            if disp is not None:
                cv2.imshow(WIN, disp)
            k = cv2.waitKey(33) & 0xFF
            if k in (ord("q"), 27):
                print("  [dihentikan pengguna]")
                break
        else:
            time.sleep(0.033)
        if done: break

    if not args.no_display:
        cv2.destroyAllWindows()
    t_compute.join(timeout=60)

    with lock:
        log_rows = list(shared["log_rows"])
        timeline = list(shared["timeline"])

    if log_rows:
        csv_path = os.path.join(args.output, f"{base}_log.csv")
        with open(csv_path, "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(log_rows[0].keys()))
            w.writeheader(); w.writerows(log_rows)
        print(f"\n  CSV  -> {csv_path}")

    if timeline:
        avg = lambda k: round(float(np.mean([r[k] for r in timeline])), 1)
        summary = {
            "video":           os.path.basename(args.input),
            "frames":          len(timeline),
            "avg_field_health": {
                "yolo":  avg("field_health_yolo"),
                "hsv":   avg("field_health_hsv"),
                "fused": avg("field_health_fused"),
            },
            "avg_agreement_pct": avg("agreement_pct"),
            "proc_seconds":      round(time.time()-t0, 1),
        }
        json_path = os.path.join(args.output, f"{base}_summary.json")
        with open(json_path, "w") as f:
            json.dump(summary, f, indent=2)
        print(f"  JSON -> {json_path}")
        print(json.dumps(summary, indent=2))

    # Re-encode ke H.264 agar bisa diputar di semua player
    for fn in [f"{base}_fused.mp4", f"{base}_fused_only.mp4"]:
        p = os.path.join(args.output, fn)
        if os.path.exists(p):
            _ffmpeg_reencode(p)


# =============================================================================
# SUBCOMMAND: hsv saja (tanpa YOLO)
# =============================================================================

def cmd_hsv(args):
    cfg = json.loads(json.dumps(DEFAULT_CFG))
    if args.config and os.path.exists(args.config):
        with open(args.config) as f:
            cfg.update(json.load(f))

    os.makedirs(args.output, exist_ok=True)
    cap = cv2.VideoCapture(args.input)
    if not cap.isOpened():
        sys.exit(f"Tidak bisa membuka: {args.input}")
    src_fps = cap.get(cv2.CAP_PROP_FPS) or 30
    step    = max(1, int(round(src_fps / max(args.fps, 0.1))))
    base = os.path.splitext(os.path.basename(args.input))[0]

    writer = None
    timeline = []
    ema = {}
    idx = 0
    t0 = time.time()

    while True:
        ok, frame = cap.read()
        if not ok: break
        if idx % step == 0:
            if args.width and frame.shape[1] > args.width:
                fh = int(frame.shape[0] * args.width / frame.shape[1])
                frame = cv2.resize(frame, (args.width, fh))
            result, overlay = hsv_process_frame(frame, cfg, ema)
            t = round(idx / src_fps, 2)
            timeline.append({"t": t, "field_health": result["field_health"],
                              **result["class_pct"]})
            if not args.no_video:
                if writer is None:
                    writer = _make_writer(
                        os.path.join(args.output, f"{base}_hsv.mp4"),
                        args.fps, overlay.shape[1], overlay.shape[0])
                if writer: writer.write(overlay)
            if not args.no_display:
                cv2.imshow("MoonHarvest HSV", overlay)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break
            if len(timeline) % 25 == 0:
                print(f"  frame {len(timeline):4d}  t={t:.2f}s  "
                      f"FH={result['field_health']:.1f}", flush=True)
        idx += 1

    cap.release()
    if writer: writer.release()
    cv2.destroyAllWindows()

    agg = {k: round(float(np.mean([r[k] for r in timeline])), 2) for k in HSV_CLASSES}
    summary = {
        "video": os.path.basename(args.input),
        "frames": len(timeline),
        "avg_class_pct": agg,
        "avg_field_health": round(float(np.mean([r["field_health"] for r in timeline])), 1),
        "proc_seconds": round(time.time()-t0, 1),
    }
    json_path = os.path.join(args.output, f"{base}_hsv_summary.json")
    with open(json_path, "w") as f:
        json.dump(summary, f, indent=2)
    print(json.dumps(summary, indent=2))
    if os.path.exists(os.path.join(args.output, f"{base}_hsv.mp4")):
        _ffmpeg_reencode(os.path.join(args.output, f"{base}_hsv.mp4"))


def cmd_image(args):
    try:
        from ultralytics import YOLO
    except ImportError:
        sys.exit("pip install ultralytics")

    cfg = json.loads(json.dumps(DEFAULT_CFG))
    if args.config and os.path.exists(args.config):
        with open(args.config) as f:
            cfg.update(json.load(f))

    os.makedirs(args.output, exist_ok=True)
    bgr = cv2.imread(args.input)
    if bgr is None:
        sys.exit(f"Tidak bisa membaca: {args.input}")
    if args.width and bgr.shape[1] > args.width:
        bh = int(bgr.shape[0] * args.width / bgr.shape[1])
        bgr = cv2.resize(bgr, (args.width, bh))

    import torch as _torch
    yolo_model = YOLO(args.weights)
    yolo_model.to("cuda" if _torch.cuda.is_available() else "cpu")
    metrics, display, _ = fusion_process_frame(bgr, cfg, yolo_model, panel_w=args.panel_w)

    base = os.path.splitext(os.path.basename(args.input))[0]
    out_path = os.path.join(args.output, f"{base}_fused.jpg")
    cv2.imwrite(out_path, display)
    print(f"Tersimpan: {out_path}")
    print(json.dumps(metrics, indent=2))
    if not args.no_display:
        cv2.imshow("MoonHarvest Fusion", display)
        cv2.waitKey(0)
        cv2.destroyAllWindows()


# =============================================================================
# MAIN
# =============================================================================

def main():
    p = argparse.ArgumentParser(
        description="MoonHarvest Detector — HSV + YOLO Fusion (standalone)")
    p.add_argument("--weights", default=DEFAULT_WEIGHTS,
                   help="Path model YOLO .pt")
    p.add_argument("--config",  default=None,
                   help="Path hsv_config.json (opsional, override threshold)")
    sub = p.add_subparsers(dest="cmd", required=True)

    # --- video (fusion) ---
    pv = sub.add_parser("video", help="Deteksi fusion HSV+YOLO pada video")
    pv.add_argument("-i", "--input",   required=True)
    pv.add_argument("-o", "--output",  default="fusion_out")
    pv.add_argument("--fps",     type=float, default=2.0)
    pv.add_argument("--width",   type=int,   default=1280)
    pv.add_argument("--panel-w", type=int,   default=480)
    pv.add_argument("--no-video",   action="store_true")
    pv.add_argument("--no-display", action="store_true")
    pv.set_defaults(func=cmd_video)

    # --- image (fusion) ---
    pi = sub.add_parser("image", help="Deteksi fusion HSV+YOLO pada gambar")
    pi.add_argument("-i", "--input",   required=True)
    pi.add_argument("-o", "--output",  default="fusion_out")
    pi.add_argument("--width",   type=int, default=960)
    pi.add_argument("--panel-w", type=int, default=480)
    pi.add_argument("--no-display", action="store_true")
    pi.set_defaults(func=cmd_image)

    # --- hsv (tanpa YOLO) ---
    ph = sub.add_parser("hsv", help="Deteksi HSV murni (tanpa YOLO)")
    ph.add_argument("-i", "--input",   required=True)
    ph.add_argument("-o", "--output",  default="hsv_out")
    ph.add_argument("--fps",     type=float, default=2.0)
    ph.add_argument("--width",   type=int,   default=1280)
    ph.add_argument("--no-video",   action="store_true")
    ph.add_argument("--no-display", action="store_true")
    ph.set_defaults(func=cmd_hsv)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
