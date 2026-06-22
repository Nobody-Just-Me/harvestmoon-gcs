#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
moonharvest_sync.py — Sinkronisasi HSV + YOLO (Pipeline Baru)
==============================================================
Script STANDALONE, tidak mengubah file lain.

Arsitektur (metode_hsv_deteksi_yolo_validasi.md):
  frame -> preprocess
        -> hsv_analyze  -> hsv_regions (bbox + class + confidence)
        -> region_filter (buang region kecil/tipis/tepi/rendah)
        -> crop filtered region -> yolo_validate pada setiap crop
        -> decision_fusion (multi-tier HSV-primary logic)
        -> temporal_smoothing (deque 5 frame)
        -> final_decision (confirmed / hsv-primary / review / negative)
        -> overlay + log

Status deteksi per region:
  confirmed     : HSV kuat + YOLO setuju
  hsv-primary   : HSV positif, YOLO lemah/tidak setuju
  review        : indikasi belum cukup kuat (YOLO positif sendiri atau HSV marginal)
  negative      : keduanya tidak mendukung

Logika keputusan mengacu pada metode_hsv_deteksi_yolo_validasi.md:
  if hsv_conf >= 0.60:
      if yolo_conf >= 0.50 -> confirmed  (0.8*hsv + 0.2*yolo)
      else                 -> hsv-primary (0.9*hsv + 0.1*yolo)
  elif hsv_conf >= 0.40:
      if yolo_conf >= 0.60 -> confirmed  (0.7*hsv + 0.3*yolo)
      else                 -> review     (0.8*hsv + 0.2*yolo)
  else:
      if yolo_conf >= 0.75 -> review     (0.4*hsv + 0.6*yolo)
      else                 -> negative   (0.0)

Penggunaan:
  python3 moonharvest_sync.py -i gabung.mp4
  python3 moonharvest_sync.py -i gabung.mp4 --no-display
  python3 moonharvest_sync.py -i gabung.mp4 --weights path/to/best.pt
  python3 moonharvest_sync.py -i gabung.mp4 --fps 3 --width 1280
"""

import argparse
import csv
import json
import os
import sys
import time
from collections import deque

import cv2
import numpy as np

# ─────────────────────────────────────────────────────────────────────────────
# KONSTANTA GLOBAL
# ─────────────────────────────────────────────────────────────────────────────

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_WEIGHTS = os.path.join(
    _SCRIPT_DIR,
    "runs/classify/Pigeon_Harvest/runs/health_classification/"
    "health_train_v1-2/weights/best.pt",
)

# Nama kelas (harus sama urutan dengan yang ditraining YOLO v1)
HSV_CLASSES = [
    "healthy_crop",
    "stressed_crop",
    "disease_stress_vegetation",
    "drought_stress",
    "bare_soil",
]

SEVERITY = {
    "healthy_crop":              0.00,
    "stressed_crop":             0.45,
    "disease_stress_vegetation": 1.00,
    "drought_stress":            0.75,
    "bare_soil":                 0.00,
}

# Peta tampilan label ke nama proposal
DISPLAY_LABEL = {
    "healthy_crop":              "Lush Green",
    "stressed_crop":             "Inconsistent Growth",
    "disease_stress_vegetation": "Disease",
    "drought_stress":            "Soil Issues",
    "bare_soil":                 "Bare Soil",
}

# Warna BGR per kelas (overlay region)
CLASS_COLOR = {
    "healthy_crop":              ( 50, 205,  50),
    "stressed_crop":             (  0, 200, 255),
    "disease_stress_vegetation": (  0,  60, 255),
    "drought_stress":            ( 55,  64,  93),
    "bare_soil":                 (110, 120, 140),
}

# Warna BGR per status — sesuai metode_hsv_deteksi_yolo_validasi.md
STATUS_COLOR = {
    "confirmed":    (  0, 200,  60),   # hijau
    "hsv-primary":  (  0, 180, 255),   # kuning-oranye
    "review":       (180, 100,  30),   # biru tua
    "negative":     ( 80,  80,  80),   # abu
}

IGNORE_VALS = {"background": 250, "shadow": 251, "unknown": 255}

# ─────────────────────────────────────────────────────────────────────────────
# KONFIGURASI HSV (dikalibrasi dari gabung.mp4 — TIDAK DIUBAH)
# ─────────────────────────────────────────────────────────────────────────────
HSV_CFG = {
    "white_balance":        True,
    "clahe_clip":           2.0,
    "clahe_grid":           8,
    "shadow_v_max":         45,
    "exg_veg_thr":          0.0213,
    "exg_healthy_min":      0.0693,
    "bg_h":                 [100, 140],
    "bg_s_min":             34,
    "healthy":  {"h": [30, 100], "s_lo": 15, "v": [69, 255]},
    "stressed": {"h": [15,  46], "s_lo": 15, "v": [80, 255]},
    "drought":  {"h": [ 8,  16], "s_lo": 65, "v": [80, 235]},
    "disease":  {"h1": [0, 10], "h2": [168, 179], "s_lo": 45, "v": [25, 215]},
    "soil":     {"s_max": 18, "v": [110, 240]},
    "texture_win":          9,
    "disease_texture_min":  16.0,
    "morph_kernel":         5,
    "min_region_area_frac": 0.0015,
    "dark_as_soil":         True,
    "dark_v_max":           55,      # was 62 — turun agar bayangan tanaman tidak ikut
    "exg_soil_guard":       0.012,   # ExG min agar pixel gelap tidak diklasifikasi soil
    "suppress_structures":  True,
    "road_open_kernel":     11,
    "struct_min_area_frac": 0.0008,
    "stressed_to_healthy":  True,
    "yellow_h":             [18, 36],
    "yellow_exg_max":       0.085,
    "yellow_s_min":         36,
}

# ─────────────────────────────────────────────────────────────────────────────
# PARAMETER FUSION — sesuai metode_hsv_deteksi_yolo_validasi.md
# ─────────────────────────────────────────────────────────────────────────────
IOU_MIN_MATCH    = 0.25   # IoU minimum agar dianggap spasial cocok
TEMPORAL_WIN     = 5      # jumlah frame untuk temporal smoothing
YOLO_MIN_PATCH   = 48     # patch crop minimal (pixel) agar YOLO dijalankan
CROP_PAD_RATIO   = 0.15   # padding tambahan pada crop region (15% dari bbox)

# Threshold logika keputusan multi-tier (dari dokumentasi)
HSV_CONF_HIGH    = 0.60   # HSV confidence tinggi
HSV_CONF_MED     = 0.40   # HSV confidence menengah
YOLO_CONF_HIGH   = 0.50   # YOLO confidence tinggi (digunakan saat HSV tinggi)
YOLO_CONF_MED    = 0.60   # YOLO confidence menengah (digunakan saat HSV menengah)
YOLO_CONF_LOW    = 0.75   # YOLO confidence minimal untuk review saat HSV rendah

# Filtrasi region sebelum dikirim ke YOLO
MIN_REGION_AREA_FRAC_YOLO = 0.0025   # area minimal (fraksi frame) untuk YOLO
MIN_REGION_ASPECT_RATIO   = 0.20     # rasio min(w,h)/max(w,h) minimal
MARGIN_EDGE_FRAC          = 0.01     # margin pinggir frame (fraksi)
HSV_MIN_FOR_YOLO          = 0.35     # HSV confidence minimal agar dikirim ke YOLO

# ─────────────────────────────────────────────────────────────────────────────
# HSV PIPELINE (disalin dan disederhanakan — tidak import moonharvest_detect)
# ─────────────────────────────────────────────────────────────────────────────

def _gray_world_wb(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean() + 1e-6, g.mean() + 1e-6, r.mean() + 1e-6
    k = (mb + mg + mr) / 3.0
    return cv2.merge([
        np.clip(b * k / mb, 0, 255),
        np.clip(g * k / mg, 0, 255),
        np.clip(r * k / mr, 0, 255),
    ]).astype(np.uint8)


def _preprocess(bgr):
    wb  = _gray_world_wb(bgr)
    hsv = cv2.cvtColor(wb, cv2.COLOR_BGR2HSV)
    h, s, v = cv2.split(hsv)
    clahe = cv2.createCLAHE(clipLimit=HSV_CFG["clahe_clip"],
                             tileGridSize=(HSV_CFG["clahe_grid"],) * 2)
    v = clahe.apply(v)
    exg_bgr = wb.astype(np.float32)
    b_, g_, r_ = cv2.split(exg_bgr)
    tot = b_ + g_ + r_ + 1e-6
    exg = (2 * g_ - r_ - b_) / tot
    texture = _local_std(cv2.cvtColor(wb, cv2.COLOR_BGR2GRAY), HSV_CFG["texture_win"])
    return cv2.merge([h, s, v]), exg, texture


def _local_std(gray, win):
    g = gray.astype(np.float32)
    mean = cv2.boxFilter(g, -1, (win, win))
    sq   = cv2.boxFilter(g * g, -1, (win, win))
    return np.sqrt(np.clip(sq - mean * mean, 0, None))


def _classify_pixels(hsv_img, exg, texture):
    cfg = HSV_CFG
    H, S, V = hsv_img[:, :, 0], hsv_img[:, :, 1], hsv_img[:, :, 2]
    h, w    = H.shape
    label   = np.full((h, w), IGNORE_VALS["unknown"], np.uint8)
    conf    = np.zeros((h, w), np.float32)
    done    = np.zeros((h, w), bool)

    def commit(mask, code, score):
        m = mask & ~done
        label[m] = code
        conf[m]  = np.clip(score[m] if isinstance(score, np.ndarray) else score, 0, 1)
        done.__ior__(m)

    sat  = np.clip(S / 180.0, 0, 1)
    exg_ = np.clip((exg + 0.05) / 0.3, 0, 1)
    tex_ = np.clip(texture / 40.0, 0, 1)

    # Hitung vegetasi DULU sebelum soil classification
    cs   = cfg["soil"]
    veg0 = exg > cfg["exg_veg_thr"]           # pixel bervegetasi (ExG > 0.0213)
    veg_weak = exg > cfg.get("exg_soil_guard", 0.012)  # guard lebih longgar untuk dark area

    # shadow & dark-as-soil — tambahkan guard ExG agar bayangan tanaman tidak jadi soil
    commit(V < cfg["shadow_v_max"], IGNORE_VALS["shadow"], 0.5)
    if cfg.get("dark_as_soil"):
        commit(
            (V >= cfg["shadow_v_max"]) & (V < cfg["dark_v_max"]) & ~veg_weak,
            4, 0.40,
        )

    commit((S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & ~veg0,
           4, 0.45 + 0.3 * (1 - sat))

    commit((H >= cfg["bg_h"][0]) & (H <= cfg["bg_h"][1]) & (S >= cfg["bg_s_min"]),
           IGNORE_VALS["background"], 0.5)

    c = cfg["healthy"]
    commit((H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) &
           (V >= c["v"][0]) & (exg >= cfg["exg_healthy_min"]),
           0, 0.45 + 0.25 * sat + 0.3 * exg_)

    c   = cfg["disease"]
    red = (((H >= c["h1"][0]) & (H <= c["h1"][1])) |
           ((H >= c["h2"][0]) & (H <= c["h2"][1]))) & (S >= c["s_lo"])
    commit(red & ((texture >= cfg["disease_texture_min"]) | (S >= 80)) &
           (V >= c["v"][0]) & (V <= c["v"][1]),
           2, 0.4 + 0.6 * tex_)

    c = cfg["stressed"]
    commit((H >= c["h"][0]) & (H <= c["h"][1]) & (S >= c["s_lo"]) &
           (V >= c["v"][0]) & (exg >= 0.02),
           1, 0.4 + 0.4 * sat)

    c = cfg["drought"]
    commit((H >= c["h"][0]) & (H < c["h"][1]) & (S >= c["s_lo"]) &
           (V >= c["v"][0]),
           3, 0.45 + 0.35 * sat)

    # stressed → healthy re-assignment
    if cfg.get("stressed_to_healthy"):
        yh = cfg.get("yellow_h", [20, 34])
        truly_yellow = (
            (H >= yh[0]) & (H <= yh[1]) &
            (exg < cfg.get("yellow_exg_max", 0.08)) &
            (S >= cfg.get("yellow_s_min", 55))
        )
        label[(label == 1) & ~truly_yellow] = 0

    # second-pass soil
    veg2  = exg > cfg["exg_veg_thr"]
    soil2 = (S <= cs["s_max"]) & (V >= cs["v"][0]) & (V <= cs["v"][1]) & ~veg2
    commit(soil2, 4, 0.4 + 0.3 * (1 - sat))

    commit(~done, IGNORE_VALS["background"], 0.3)
    return label, conf


def _extract_hsv_regions(label, conf, frame_h, frame_w):
    """Ekstrak region dari label HSV, return list DetectionResult (HSV side)."""
    min_area = int(HSV_CFG["min_region_area_frac"] * frame_h * frame_w)
    ksz      = HSV_CFG["morph_kernel"] | 1
    kernel   = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ksz, ksz))
    regions  = []
    for idx, name in enumerate(HSV_CLASSES):
        mask = (label == idx).astype(np.uint8)
        if mask.sum() == 0:
            continue
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,  kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, lab, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = int(stats[i, cv2.CC_STAT_AREA])
            if area < min_area:
                continue
            x, y   = int(stats[i, cv2.CC_STAT_LEFT]),  int(stats[i, cv2.CC_STAT_TOP])
            ww, hh = int(stats[i, cv2.CC_STAT_WIDTH]), int(stats[i, cv2.CC_STAT_HEIGHT])
            c_avg  = float(conf[lab == i].mean())
            regions.append({
                "class":      name,
                "bbox":       [x, y, ww, hh],   # x,y,w,h
                "confidence": round(c_avg, 3),
                "area":       area,
            })
    regions.sort(key=lambda r: -r["area"])
    return regions


def _field_health(label):
    total = label.size
    valid = sum(int((label == i).sum()) for i in range(len(HSV_CLASSES)))
    pct   = {n: 100.0 * int((label == i).sum()) / max(valid, 1)
             for i, n in enumerate(HSV_CLASSES)}
    fhi   = max(0.0, 100.0 - sum(SEVERITY[k] * pct[k] for k in HSV_CLASSES))
    return round(fhi, 1), pct


# ─────────────────────────────────────────────────────────────────────────────
# IoU HELPER
# ─────────────────────────────────────────────────────────────────────────────

def _iou(b1, b2):
    """b1, b2: [x, y, w, h]"""
    x1, y1, w1, h1 = b1
    x2, y2, w2, h2 = b2
    ix = max(0, min(x1+w1, x2+w2) - max(x1, x2))
    iy = max(0, min(y1+h1, y2+h2) - max(y1, y2))
    inter = ix * iy
    union = w1*h1 + w2*h2 - inter
    return inter / max(union, 1)


# ─────────────────────────────────────────────────────────────────────────────
# NMS — Non-Maximum Suppression untuk region HSV
# ─────────────────────────────────────────────────────────────────────────────

def _apply_nms(regions, iou_thr=0.30, min_area_frac=0.004, frame_h=720, frame_w=1280):
    """
    NMS dua tahap pada region HSV:
      1. Filter region terlalu kecil (min_area_frac × luas frame)
      2. Per-kelas NMS: hapus box yang terlalu tumpang tindih dalam kelas yang sama
      3. Cross-class NMS: hapus box kelas berbeda yang overlap > iou_thr,
         simpan yang area-nya lebih besar

    iou_thr   — threshold IoU untuk suppress (0.25–0.40 agresif, 0.50 konservatif)
    min_area_frac — fraksi minimum area region terhadap frame
    """
    min_area = int(min_area_frac * frame_h * frame_w)

    # ── 1. filter kecil ──────────────────────────────────────────────────────
    regions = [r for r in regions if r["area"] >= min_area]
    if len(regions) <= 1:
        return regions

    # ── 2. per-kelas NMS ─────────────────────────────────────────────────────
    by_class = {}
    for r in regions:
        by_class.setdefault(r["class"], []).append(r)

    after_cls_nms = []
    for cls_regions in by_class.values():
        # urutkan area desc → besar diprioritaskan
        cls_regions.sort(key=lambda r: -r["area"])
        kept = []
        for r in cls_regions:
            if all(_iou(r["bbox"], k["bbox"]) <= iou_thr for k in kept):
                kept.append(r)
        after_cls_nms.extend(kept)

    # ── 3. cross-class NMS ───────────────────────────────────────────────────
    # Prioritas: tanaman (crop classes) > soil. Soil tidak boleh suppress tanaman.
    CROP_CLASSES = {"healthy_crop", "stressed_crop", "disease_stress_vegetation", "drought_stress"}
    SOIL_CLASS   = "bare_soil"

    def _priority(r):
        # 0 = prioritas tertinggi (crop), 1 = soil
        return 0 if r["class"] in CROP_CLASSES else 1

    # Urutkan: crop dulu (priority 0), lalu soil, dalam tier yang sama urutkan area desc
    after_cls_nms.sort(key=lambda r: (_priority(r), -r["area"]))
    final = []
    for r in after_cls_nms:
        suppress = False
        for k in final:
            if _iou(r["bbox"], k["bbox"]) > iou_thr:
                # Soil tidak suppress crop — hanya suppress sesama kelas atau sesama prioritas
                if _priority(r) >= _priority(k):
                    suppress = True
                    break
        if not suppress:
            final.append(r)

    return final


# ─────────────────────────────────────────────────────────────────────────────
# TEMPORAL SMOOTHER
# ─────────────────────────────────────────────────────────────────────────────

class TemporalSmoother:
    """Simpan status N frame terakhir per cell-ID dan voting majority."""

    def __init__(self, window=TEMPORAL_WIN):
        self.window  = window
        self._buffer = {}   # key → deque of status strings

    def update(self, key, status):
        if key not in self._buffer:
            self._buffer[key] = deque(maxlen=self.window)
        self._buffer[key].append(status)

    def smooth(self, key, current_status):
        self.update(key, current_status)
        hist = list(self._buffer[key])
        # voting: ambil status yang paling sering muncul
        counts = {}
        for s in hist:
            counts[s] = counts.get(s, 0) + 1
        return max(counts, key=counts.get)


# ─────────────────────────────────────────────────────────────────────────────
# FILTER REGION SEBELUM YOLO — sesuai metode_hsv_deteksi_yolo_validasi.md
# ─────────────────────────────────────────────────────────────────────────────

def _filter_regions_for_yolo(regions, frame_h, frame_w):
    """
    Saring region HSV sebelum dikirim ke YOLO.
    Region yang sangat kecil, terlalu tipis, terlalu dekat pinggir frame,
    atau memiliki skor HSV sangat rendah dibuang.

    Returns: list region yang lobos filter (referensi ke list asli).
    """
    min_area_yolo  = int(MIN_REGION_AREA_FRAC_YOLO * frame_h * frame_w)
    margin_edge    = int(MARGIN_EDGE_FRAC * min(frame_h, frame_w))
    filtered = []
    for reg in regions:
        x, y, rw, rh = reg["bbox"]

        # Buang region terlalu kecil
        if rw * rh < min_area_yolo:
            continue

        # Buang region terlalu tipis (rasio aspect < threshold)
        aspect = min(rw, rh) / max(rw, rh, 1)
        if aspect < MIN_REGION_ASPECT_RATIO:
            continue

        # Buang region terlalu dekat pinggir frame
        if (x < margin_edge or y < margin_edge or
                x + rw > frame_w - margin_edge or
                y + rh > frame_h - margin_edge):
            continue

        # Buang region dengan HSV confidence sangat rendah
        if reg["confidence"] < HSV_MIN_FOR_YOLO:
            continue

        filtered.append(reg)

    return filtered


# ─────────────────────────────────────────────────────────────────────────────
# FUSION LOGIC — sesuai metode_hsv_deteksi_yolo_validasi.md
# ─────────────────────────────────────────────────────────────────────────────

def _decide_status(hsv_conf, yolo_conf):
    """
    Logika keputusan multi-tier dari dokumentasi.

    Returns: (status_str, final_score)
    """
    if hsv_conf >= HSV_CONF_HIGH:           # HSV confidence tinggi (>= 0.60)
        if yolo_conf >= YOLO_CONF_HIGH:     # YOLO setuju (>= 0.50)
            status = "confirmed"
            final_score = 0.8 * hsv_conf + 0.2 * yolo_conf
        else:
            status = "hsv-primary"
            final_score = 0.9 * hsv_conf + 0.1 * yolo_conf
    elif hsv_conf >= HSV_CONF_MED:          # HSV confidence menengah (0.40 - 0.60)
        if yolo_conf >= YOLO_CONF_MED:      # YOLO cukup yakin (>= 0.60)
            status = "confirmed"
            final_score = 0.7 * hsv_conf + 0.3 * yolo_conf
        else:
            status = "review"
            final_score = 0.8 * hsv_conf + 0.2 * yolo_conf
    else:                                    # HSV confidence rendah (< 0.40)
        if yolo_conf >= YOLO_CONF_LOW:      # YOLO sangat yakin (>= 0.75)
            status = "review"
            final_score = 0.4 * hsv_conf + 0.6 * yolo_conf
        else:
            status = "negative"
            final_score = 0.0

    return status, round(final_score, 3)


def fuse_regions(hsv_regions, yolo_model, bgr_frame, smoother):
    """
    Jalankan YOLO pada setiap region HSV (setelah difilter), lakukan fusion.

    Returns list of result dicts.
    """
    h, w   = bgr_frame.shape[:2]
    results = []

    # ── Filter region sebelum dikirim ke YOLO ──────────────────────────────
    filtered_regions = _filter_regions_for_yolo(hsv_regions, h, w)

    for reg_idx, reg in enumerate(hsv_regions):
        x, y, rw, rh = reg["bbox"]

        # ── padding proporsional (15% dari bbox) ─────────────────────────
        pad_x = max(4, int(CROP_PAD_RATIO * rw))
        pad_y = max(4, int(CROP_PAD_RATIO * rh))
        x0    = max(0, x - pad_x)
        y0    = max(0, y - pad_y)
        x1    = min(w, x + rw + pad_x)
        y1    = min(h, y + rh + pad_y)
        crop  = bgr_frame[y0:y1, x0:x1]

        yolo_class = None
        yolo_conf  = 0.0
        yolo_bbox  = [x, y, rw, rh]   # sama dengan HSV (spasial identik)

        # ── Jalankan YOLO hanya jika region lulus filter ────────────────
        run_yolo = (
            reg in filtered_regions and
            crop.shape[0] >= YOLO_MIN_PATCH and
            crop.shape[1] >= YOLO_MIN_PATCH and
            yolo_model is not None
        )
        if run_yolo:
            try:
                res    = yolo_model(crop, verbose=False, device=0)[0]
                probs  = res.probs
                top_i  = int(probs.top1)
                top_c  = float(probs.top1conf)
                # YOLO classes diurutkan alfabet saat training
                yolo_cls_sorted = sorted(HSV_CLASSES)
                yolo_class = yolo_cls_sorted[top_i]
                yolo_conf  = top_c
            except Exception:
                pass

        hsv_conf      = reg["confidence"]
        classes_agree = (yolo_class == reg["class"]) if yolo_class else False

        # IoU — karena kita crop dari bbox yang sama, IoU = 1.0 secara konsep.
        iou_val = _iou(reg["bbox"], yolo_bbox)
        spatial_ok = iou_val >= IOU_MIN_MATCH

        # ── Gunakan decision logic multi-tier ──────────────────────────
        # Jika kelas tidak setuju, turunkan yolo_conf untuk decision logic
        yolo_conf_effective = yolo_conf if (classes_agree and spatial_ok) else 0.0
        raw_status, final_score = _decide_status(hsv_conf, yolo_conf_effective)

        # ── Temporal smoothing ──────────────────────────────────────────
        key    = f"{reg['class']}_{reg_idx % 8}"
        status = smoother.smooth(key, raw_status)

        results.append({
            "hsv_class":     reg["class"],
            "yolo_class":    yolo_class,
            "hsv_conf":      hsv_conf,
            "yolo_conf":     round(yolo_conf, 3),
            "final_score":   round(final_score, 3),
            "classes_agree": classes_agree,
            "iou":           round(iou_val, 3),
            "status":        status,
            "bbox":          reg["bbox"],
            "area":          reg["area"],
            "yolo_skipped":  not run_yolo,
        })

    return results


# ─────────────────────────────────────────────────────────────────────────────
# OVERLAY RENDERER
# ─────────────────────────────────────────────────────────────────────────────

def _fhi_status_label(fhi):
    if fhi >= 75:
        return "BAIK",      ( 50, 200,  50)
    elif fhi >= 50:
        return "PERHATIAN", (  0, 200, 255)
    else:
        return "KRITIS",    (  0,  60, 255)


def render_overlay(bgr_frame, fused_results, fhi, frame_no, t_sec, hsv_display=False):
    """
    Gambar bounding box + label + status bar + sidebar.
    hsv_display=True  → warna per kelas HSV (CLASS_COLOR), semua region ditampilkan
    hsv_display=False → warna per status fusion (STATUS_COLOR), negative disembunyikan
    Return: frame annotated (ukuran sama dengan input).
    """
    out = bgr_frame.copy()
    h, w = out.shape[:2]

    # ── bounding boxes ───────────────────────────────────────────────────────
    for r in fused_results:
        if not hsv_display and r["status"] == "negative":
            continue
        x, y, bw, bh = r["bbox"]
        col = CLASS_COLOR.get(r["hsv_class"], (180, 180, 180)) if hsv_display \
              else STATUS_COLOR[r["status"]]
        cv2.rectangle(out, (x, y), (x + bw, y + bh), col, 2)

        if bw >= 50 and bh >= 32:
            disp  = DISPLAY_LABEL.get(r["hsv_class"], r["hsv_class"])
            if hsv_display:
                label = f"{disp}  {r['hsv_conf']:.2f}"
            else:
                label = f"{disp}  {r['final_score']:.2f}  [{r['status']}]"
            (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.38, 1)
            ly = max(th + 6, y)
            cv2.rectangle(out, (x, ly - th - 4), (x + tw + 6, ly + 2), col, -1)
            cv2.putText(out, label, (x + 3, ly - 2),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.38, (255, 255, 255), 1, cv2.LINE_AA)

    # ── sidebar kiri ─────────────────────────────────────────────────────────
    sw      = 210
    overlay = out.copy()
    cv2.rectangle(overlay, (0, 0), (sw, h), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.58, out, 0.42, 0, out)

    status_lbl, status_col = _fhi_status_label(fhi)
    mode_txt = "HSV Display" if hsv_display else "HSV + YOLO validate"
    cv2.putText(out, "MoonHarvest SYNC", (8, 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.48, (200, 200, 200), 1, cv2.LINE_AA)
    cv2.putText(out, mode_txt, (8, 36),
                cv2.FONT_HERSHEY_SIMPLEX, 0.38, (140, 140, 140), 1, cv2.LINE_AA)

    cv2.rectangle(out, (6, 46), (sw - 6, 100), status_col, -1)
    cv2.putText(out, f"FHI  {fhi:.1f}", (11, 74),
                cv2.FONT_HERSHEY_SIMPLEX, 0.78, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(out, status_lbl, (11, 96),
                cv2.FONT_HERSHEY_SIMPLEX, 0.50, (255, 255, 255), 1, cv2.LINE_AA)

    y_cur = 112
    if hsv_display:
        # Sidebar: hitung per kelas HSV
        cls_counts = {c: 0 for c in HSV_CLASSES}
        for r in fused_results:
            cls_counts[r["hsv_class"]] = cls_counts.get(r["hsv_class"], 0) + 1
        for cls_name, col in CLASS_COLOR.items():
            cv2.rectangle(out, (8, y_cur + 2), (16, y_cur + 10), col, -1)
            disp = DISPLAY_LABEL.get(cls_name, cls_name)
            cv2.putText(out, f"{disp[:14]}: {cls_counts.get(cls_name, 0)}",
                        (20, y_cur + 11),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.35, (200, 200, 200), 1, cv2.LINE_AA)
            y_cur += 16
    else:
        counts = {"confirmed": 0, "hsv-primary": 0, "review": 0, "negative": 0}
        for r in fused_results:
            counts[r["status"]] = counts.get(r["status"], 0) + 1
        for st, col in STATUS_COLOR.items():
            cv2.rectangle(out, (8, y_cur + 2), (16, y_cur + 10), col, -1)
            cv2.putText(out, f"{st}: {counts.get(st, 0)}", (20, y_cur + 11),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.37, (200, 200, 200), 1, cv2.LINE_AA)
            y_cur += 16

    y_cur += 6
    cv2.putText(out, f"frame {frame_no}  t={t_sec:.1f}s", (8, y_cur),
                cv2.FONT_HERSHEY_SIMPLEX, 0.36, (130, 130, 130), 1, cv2.LINE_AA)

    # ── status bar bawah ─────────────────────────────────────────────────────
    if hsv_display:
        line1 = f"Field Health {status_lbl}  |  FHI: {fhi:.1f}  |  regions: {len(fused_results)}"
        line2 = ("HSV klasifikasi  |  "
                 "HIJAU=Lush Green  KUNING=Inconsistent  MERAH=Disease  ABU=Soil")
    else:
        counts = {"confirmed": 0, "hsv-primary": 0, "review": 0, "negative": 0}
        for r in fused_results:
            counts[r["status"]] = counts.get(r["status"], 0) + 1
        ok    = counts["confirmed"]
        n     = max(len(fused_results), 1)
        agree = f"{100*ok//n}%"
        line1 = (f"Field Health {status_lbl}  |  FHI: {fhi:.1f}  "
                 f"|  confirmed: {ok}/{n} ({agree})")
        line2 = ("HSV-primary + YOLO-validate (multi-tier)  |  "
                 "HIJAU=confirmed  KUNING=hsv-primary  BIRU=review  ABU=negative")

    bar_h = 48
    ov2   = out.copy()
    cv2.rectangle(ov2, (0, h - bar_h), (w, h), (0, 0, 0), -1)
    cv2.addWeighted(ov2, 0.70, out, 0.30, 0, out)
    cv2.putText(out, line1, (sw + 8, h - 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.44, status_col, 1, cv2.LINE_AA)
    cv2.putText(out, line2, (sw + 8, h - 12),
                cv2.FONT_HERSHEY_SIMPLEX, 0.36, (130, 130, 130), 1, cv2.LINE_AA)

    return out


# ─────────────────────────────────────────────────────────────────────────────
# MAIN VIDEO LOOP
# ─────────────────────────────────────────────────────────────────────────────

def run_video(args):
    # ── muat model YOLO ──────────────────────────────────────────────────────
    yolo_model = None
    if not args.hsv_only:
        try:
            import torch
            from ultralytics import YOLO
            yolo_model = YOLO(args.weights)
            yolo_model.to("cuda" if torch.cuda.is_available() else "cpu")
            _dev = "GPU (CUDA)" if torch.cuda.is_available() else "CPU"
            print(f"[sync] model  : {args.weights}  [{_dev}]")
        except Exception as e:
            print(f"[sync] YOLO tidak bisa dimuat ({e}), jalankan mode HSV-only")

    # ── buka video ───────────────────────────────────────────────────────────
    cap = cv2.VideoCapture(args.input)
    if not cap.isOpened():
        sys.exit(f"[error] tidak bisa membuka video: {args.input}")

    fps_src = cap.get(cv2.CAP_PROP_FPS) or 25.0
    w_src   = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h_src   = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    step    = max(1, int(round(fps_src / args.fps)))

    out_w   = args.width
    out_h   = int(out_w * h_src / w_src)
    print(f"[sync] video  : {args.input}  {w_src}x{h_src} @ {fps_src:.1f}fps")
    print(f"[sync] proses : setiap {step} frame  ({args.fps:.1f} fps efektif)")
    print(f"[sync] output : {out_w}x{out_h}")
    print(f"[sync] tekan 'q' untuk berhenti\n")

    # ── siapkan output ───────────────────────────────────────────────────────
    os.makedirs("sync_out", exist_ok=True)
    base    = os.path.splitext(os.path.basename(args.input))[0]
    mp4_tmp = f"sync_out/{base}_sync_tmp.mp4"
    mp4_out = f"sync_out/{base}_sync.mp4"
    csv_out = f"sync_out/{base}_sync_log.csv"
    json_out= f"sync_out/{base}_sync_summary.json"

    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
    writer = cv2.VideoWriter(mp4_tmp, fourcc, args.fps, (out_w, out_h))

    # ── inisialisasi state ───────────────────────────────────────────────────
    smoother  = TemporalSmoother(TEMPORAL_WIN)
    csv_rows  = []
    frame_no  = 0
    proc_no   = 0
    t_start   = time.time()
    fhi_history = []

    while True:
        ret, bgr = cap.read()
        if not ret:
            break
        frame_no += 1
        if (frame_no - 1) % step != 0:
            continue
        proc_no += 1
        t_vid = (frame_no - 1) / fps_src

        # ── resize input ────────────────────────────────────────────────────
        bgr_r = cv2.resize(bgr, (out_w, out_h))

        # ── HSV pipeline ────────────────────────────────────────────────────
        hsv_pp, exg, texture = _preprocess(bgr_r)
        label, conf          = _classify_pixels(hsv_pp, exg, texture)
        fhi, _pct            = _field_health(label)
        hsv_regions          = _extract_hsv_regions(label, conf, out_h, out_w)

        # ── NMS: hapus box kecil dan tumpang tindih ──────────────────────────
        hsv_regions = _apply_nms(
            hsv_regions,
            iou_thr=args.nms_iou,
            min_area_frac=args.nms_min_area,
            frame_h=out_h,
            frame_w=out_w,
        )

        # ── fusion ──────────────────────────────────────────────────────────
        fused = fuse_regions(hsv_regions, yolo_model, bgr_r, smoother)
        fhi_history.append(fhi)

        # ── overlay ─────────────────────────────────────────────────────────
        annotated = render_overlay(bgr_r, fused, fhi, proc_no, t_vid,
                                   hsv_display=args.hsv_display)
        writer.write(annotated)

        # ── CSV log ─────────────────────────────────────────────────────────
        counts = {"confirmed": 0, "hsv-primary": 0, "review": 0, "negative": 0}
        for r in fused:
            counts[r["status"]] += 1
        csv_rows.append({
            "frame": frame_no,
            "t_sec": round(t_vid, 2),
            "fhi":   fhi,
            "regions": len(fused),
            "confirmed":  counts["confirmed"],
            "hsv_primary": counts["hsv-primary"],
            "review":     counts["review"],
            "negative":   counts["negative"],
        })

        # ── display ─────────────────────────────────────────────────────────
        if not args.no_display:
            cv2.imshow("MoonHarvest SYNC", annotated)
            if cv2.waitKey(args.delay) & 0xFF == ord("q"):
                print("\n[sync] dihentikan oleh pengguna")
                break

        # ── console log ─────────────────────────────────────────────────────
        if proc_no % 5 == 0:
            ok = counts["confirmed"]
            n  = max(len(fused), 1)
            print(f"  frame {frame_no:5d}  t={t_vid:.1f}s"
                  f"  regions={len(fused)}"
                  f"  confirmed={ok}/{n}"
                  f"  review={counts['review']}"
                  f"  FHI={fhi:.1f}")

    # ── cleanup ──────────────────────────────────────────────────────────────
    cap.release()
    writer.release()
    if not args.no_display:
        cv2.destroyAllWindows()

    proc_sec = round(time.time() - t_start, 1)

    # ── re-encode H.264 ──────────────────────────────────────────────────────
    if os.path.exists(mp4_tmp) and os.path.getsize(mp4_tmp) > 0:
        ret_ff = os.system(
            f'ffmpeg -y -i "{mp4_tmp}" -c:v libx264 -crf 22 -preset fast '
            f'"{mp4_out}" -loglevel error'
        )
        if ret_ff == 0:
            os.remove(mp4_tmp)
            print(f"\n  Video H.264 -> {mp4_out}")
        else:
            os.rename(mp4_tmp, mp4_out)

    # ── tulis CSV ────────────────────────────────────────────────────────────
    if csv_rows:
        with open(csv_out, "w", newline="") as f:
            writer_csv = csv.DictWriter(f, fieldnames=csv_rows[0].keys())
            writer_csv.writeheader()
            writer_csv.writerows(csv_rows)
        print(f"  CSV         -> {csv_out}")

    # ── tulis JSON summary ───────────────────────────────────────────────────
    avg_fhi = round(sum(fhi_history) / max(len(fhi_history), 1), 1)
    total_confirmed  = sum(r["confirmed"] for r in csv_rows)
    total_regions    = sum(r["regions"]   for r in csv_rows)
    summary = {
        "video":        os.path.basename(args.input),
        "frames_proc":  proc_no,
        "avg_fhi":      avg_fhi,
        "confirmed_rate_pct": round(100 * total_confirmed / max(total_regions, 1), 1),
        "total_regions": total_regions,
        "total_confirmed": total_confirmed,
        "proc_seconds": proc_sec,
        "mode": "hsv-only" if args.hsv_only else "hsv+yolo-validate",
        "decision_logic": "multi-tier (metode_hsv_deteksi_yolo_validasi.md)",
        "temporal_window": TEMPORAL_WIN,
        "thresholds": {
            "hsv_conf_high": HSV_CONF_HIGH,
            "hsv_conf_med":  HSV_CONF_MED,
            "yolo_conf_high": YOLO_CONF_HIGH,
            "yolo_conf_med":  YOLO_CONF_MED,
            "yolo_conf_low":  YOLO_CONF_LOW,
        },
    }
    with open(json_out, "w") as f:
        json.dump(summary, f, indent=2)
    print(f"  JSON        -> {json_out}")

    print()
    print(json.dumps(summary, indent=2))


# ─────────────────────────────────────────────────────────────────────────────
# CLI
# ─────────────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(
        description="MoonHarvest SYNC — HSV deteksi utama + YOLO validasi tahap kedua"
    )
    ap.add_argument("-i", "--input",    required=True, help="path video input")
    ap.add_argument("-w", "--weights",  default=DEFAULT_WEIGHTS,
                    help="path model YOLO (.pt)")
    ap.add_argument("--fps",       type=float, default=3.0,
                    help="frame rate diproses (default 3)")
    ap.add_argument("--width",     type=int,   default=1280,
                    help="lebar output (default 1280)")
    ap.add_argument("--no-display", action="store_true",
                    help="jangan tampilkan window OpenCV")
    ap.add_argument("--hsv-only",    action="store_true",
                    help="jalankan tanpa YOLO (HSV saja)")
    ap.add_argument("--hsv-display", action="store_true",
                    help="tampilkan overlay warna kelas HSV (bukan status fusion)")
    ap.add_argument("--nms-iou",     type=float, default=0.30,
                    help="threshold IoU untuk NMS (default 0.30, makin kecil makin agresif)")
    ap.add_argument("--nms-min-area",type=float, default=0.004,
                    help="area minimum region sbg fraksi frame (default 0.004)")
    ap.add_argument("--delay",     type=int,   default=300,
                    help="jeda antar frame di window (ms, default 300)")
    args = ap.parse_args()

    if not os.path.isfile(args.input):
        sys.exit(f"[error] video tidak ditemukan: {args.input}")

    run_video(args)


if __name__ == "__main__":
    main()