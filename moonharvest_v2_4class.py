#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MoonHarvest v2 — Detektor 4-Kelas (HSV murni, standalone, TANPA YOLO)
=====================================================================
Program BARU yang ditulis ulang dari nol dengan threshold yang dikalibrasi
langsung dari distribusi warna footage UAV demo (YDXJ0007.mp4, sawah hijau 60-80 m).

4 kelas final yang ditampilkan:
  1. Lush Green               (sehat)        severity 0.00  -> GCS "Healthy"
  2. Inconsistent Growth      (stres ringan) severity 0.45  -> GCS "Stress"
  3. Drought / Severe Stress  (kering/berat) severity 0.75  -> GCS "Drought"
  4. Bare Soil / Gap          (tanah/gap)    severity 0.00  -> GCS "BareSoil"

TIDAK ada klaim penyakit / hama (tidak terbukti pada citra UAV).

Penggunaan:
  python3 moonharvest_v2_4class.py -i demo_videos/YDXJ0007.mp4 -o out/
  python3 moonharvest_v2_4class.py -i frame.jpg --image -o out/
  python3 moonharvest_v2_4class.py -i video.mp4 -o out/ --no-video   (hanya JSON)

Dependensi: hanya opencv-python(-headless) + numpy. Aman dijalankan headless.

Threshold dikalibrasi dari YDXJ0007 (percentile terukur):
  ExG p25=0.006 p50=0.054 p75=0.099 ; veg(ExG>0.02)=68%
  VEG hue p10..p90 = 36..95 ; SOIL S p10=8 ; V p5=88
"""
import argparse, json, os, sys, time
import cv2
import numpy as np

# =============================================================================
# DEFINISI 4 KELAS
# =============================================================================
CLASSES = ["lush_green", "inconsistent_growth", "drought_severe_stress", "bare_soil_gap"]
L_LUSH, L_INC, L_DR, L_BARE = 0, 1, 2, 3
L_SHADOW, L_BG = 250, 251          # diabaikan dari FHI

DISPLAY = {
    "lush_green":            "Lush Green",
    "inconsistent_growth":   "Inconsistent Growth",
    "drought_severe_stress": "Drought / Severe Stress",
    "bare_soil_gap":         "Bare Soil / Gap",
}
GCS_KEY = {                         # untuk dikirim ke GCS C# (DashboardPage)
    "lush_green":            "Healthy",
    "inconsistent_growth":   "Stress",
    "drought_severe_stress": "Drought",
    "bare_soil_gap":         "BareSoil",
}
SEVERITY = {
    "lush_green":            0.00,
    "inconsistent_growth":   0.45,
    "drought_severe_stress": 0.75,
    "bare_soil_gap":         0.00,
}
PALETTE = {                         # warna overlay (BGR)
    "lush_green":            ( 50, 205,  50),
    "inconsistent_growth":   (  0, 200, 255),
    "drought_severe_stress": (  0, 100, 255),
    "bare_soil_gap":         (120, 120, 120),
}
CODE_TO_KEY = {L_LUSH: "lush_green", L_INC: "inconsistent_growth",
               L_DR: "drought_severe_stress", L_BARE: "bare_soil_gap"}
COL_SHADOW = (45, 45, 45)
COL_BG     = (80, 80, 80)

# =============================================================================
# THRESHOLD BARU (dikalibrasi dari YDXJ0007)
# =============================================================================
CFG = {
    # Gray-world WB merusak adegan UAV yang didominasi hijau (asumsi rata-rata
    # abu-abu tidak berlaku) -> dimatikan default. Aktifkan via --wb bila perlu.
    "white_balance":   False,
    "clahe_clip":      2.0,
    "clahe_grid":      8,
    "shadow_v_max":    70,        # V < 70  -> bayangan / terlalu gelap
    "veg_exg_thr":     0.02,      # batas vegetasi vs tanah
    "healthy_exg_min": 0.090,     # ExG kuat = hijau subur (p75 video)
    "green_h":         [33, 100], # rentang hue hijau (UAV, OpenCV 0-179)
    "yellow_h":        [18, 40],  # hijau-kekuningan = stres
    "drought_h":       [8, 20],   # oranye-tan kering
    "drought_s_min":   45,
    "drought_v_min":   80,
    "soil_s_max":      22,        # tanah = saturasi rendah
    "soil_v":          [90, 220],
    "texture_win":     9,
    "morph_kernel":    5,
    "label_median":    7,
    "min_region_area_frac": 0.0015,
    "ema_alpha":       0.40,
    # Auto-kalibrasi: sesuaikan ambang hijau-subur ke percentile tiap video,
    # supaya program generalisasi ke footage lain (bukan hanya YDXJ0007).
    "auto_calibrate":  True,
    "auto_exg_pctl":   55,        # ExG persentil-55 frame -> ambang lush (FHI demo >80)
    "auto_exg_min":    0.055,     # batas bawah aman
    "auto_exg_max":    0.130,     # batas atas aman
}


# =============================================================================
# UTILITAS CITRA
# =============================================================================
def _gray_world_wb(bgr):
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean() + 1e-6, g.mean() + 1e-6, r.mean() + 1e-6
    k = (mb + mg + mr) / 3.0
    b = np.clip(b * (k / mb), 0, 255)
    g = np.clip(g * (k / mg), 0, 255)
    r = np.clip(r * (k / mr), 0, 255)
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
    return (2 * g - r - b) / tot


def _auto_healthy_exg(exg, cfg):
    """Adaptif: ambil percentile ExG frame, clamp ke rentang aman."""
    if not cfg.get("auto_calibrate"):
        return cfg["healthy_exg_min"]
    veg = exg[exg > cfg["veg_exg_thr"]]
    if veg.size < 100:
        return cfg["healthy_exg_min"]
    val = float(np.percentile(veg, cfg["auto_exg_pctl"]))
    return float(np.clip(val, cfg["auto_exg_min"], cfg["auto_exg_max"]))


# =============================================================================
# KLASIFIKASI 4-KELAS (prioritas: shadow -> lush -> drought -> stres -> tanah)
# =============================================================================
def classify_pixels(hsv, exg, cfg):
    H, S, V = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    h, w = H.shape
    label    = np.full((h, w), L_BG, np.uint8)
    assigned = np.zeros((h, w), bool)

    def commit(mask, code):
        m = mask & ~assigned
        label[m] = code
        assigned[m] = True

    # 1. Bayangan / terlalu gelap
    commit(V < cfg["shadow_v_max"], L_SHADOW)

    veg = exg > cfg["veg_exg_thr"]
    he  = _auto_healthy_exg(exg, cfg)

    # 2. Lush Green: vegetasi hijau + ExG kuat
    green_hue = (H >= cfg["green_h"][0]) & (H <= cfg["green_h"][1])
    lush = veg & green_hue & (exg >= he)
    commit(lush, L_LUSH)

    # 3. Drought / Severe Stress: oranye-tan kering, saturasi sedang-tinggi, non-veg
    dh = cfg["drought_h"]
    drought = (~veg) & (H >= dh[0]) & (H <= dh[1]) & \
              (S >= cfg["drought_s_min"]) & (V >= cfg["drought_v_min"])
    commit(drought, L_DR)

    # 4. Inconsistent Growth: sisa vegetasi (ExG lemah) atau hijau-kekuningan
    yh = cfg["yellow_h"]
    yellowish = (H >= yh[0]) & (H <= yh[1])
    inc = (veg & ~assigned) | (yellowish & (exg > cfg["veg_exg_thr"] * 0.5) & ~assigned)
    commit(inc, L_INC)

    # 5. Bare Soil / Gap: saturasi rendah, non-veg, terang cukup
    sv = cfg["soil_v"]
    soil = (~veg) & (S <= cfg["soil_s_max"]) & (V >= sv[0]) & (V <= sv[1])
    commit(soil, L_BARE)

    # 6. Sisa non-vegetasi yang belum terklasifikasi -> tanah (gap)
    commit(~veg & ~assigned, L_BARE)
    # sisanya background
    return label


def _clean_label(label, cfg):
    label = cv2.medianBlur(label, cfg["label_median"] | 1)
    return label


def _distribution(label):
    """pct atas 4 kelas (shadow/bg dikecualikan) + FHI."""
    counts = {k: int((label == code).sum())
              for code, k in CODE_TO_KEY.items()}
    valid = sum(counts.values())
    pct = {k: round(100.0 * v / max(valid, 1), 2) for k, v in counts.items()}
    fhi = 100.0 - sum(SEVERITY[k] * pct[k] for k in CLASSES)
    return pct, counts, round(max(fhi, 0.0), 1)


def _fhi_status(fhi):
    if fhi >= 75:  return "BAIK",      ( 50, 200,  50)
    if fhi >= 50:  return "PERHATIAN", (  0, 200, 255)
    return "KRITIS", (  0, 60, 255)


# =============================================================================
# VISUAL
# =============================================================================
def _colorize(label):
    h, w = label.shape
    out = np.zeros((h, w, 3), np.uint8)
    for code, key in CODE_TO_KEY.items():
        out[label == code] = PALETTE[key]
    out[label == L_SHADOW] = COL_SHADOW
    out[label == L_BG]     = COL_BG
    return out


def _extract_regions(label, cfg):
    h, w = label.shape
    min_area = int(cfg["min_region_area_frac"] * h * w)
    ksz = cfg["morph_kernel"] | 1
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (ksz, ksz))
    regions = []
    for code, key in CODE_TO_KEY.items():
        mask = (label == code).astype(np.uint8)
        if mask.sum() == 0:
            continue
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, lab, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = int(stats[i, cv2.CC_STAT_AREA])
            if area < min_area:
                continue
            regions.append({
                "class": key,
                "bbox": [int(stats[i, cv2.CC_STAT_LEFT]), int(stats[i, cv2.CC_STAT_TOP]),
                         int(stats[i, cv2.CC_STAT_WIDTH]), int(stats[i, cv2.CC_STAT_HEIGHT])],
                "area": area,
            })
    regions.sort(key=lambda r: -r["area"])
    return regions


def _sidebar(frame, fhi, pct):
    h, w = frame.shape[:2]
    sw = 230
    out = frame.copy()
    ov = frame.copy()
    cv2.rectangle(ov, (0, 0), (sw, h), (0, 0, 0), -1)
    cv2.addWeighted(ov, 0.60, out, 0.40, 0, out)
    cv2.putText(out, "MoonHarvest v2", (8, 22), cv2.FONT_HERSHEY_SIMPLEX, 0.55,
                (210, 210, 210), 1, cv2.LINE_AA)
    cv2.putText(out, "4-Class Crop Monitor", (8, 40), cv2.FONT_HERSHEY_SIMPLEX,
                0.42, (140, 140, 140), 1, cv2.LINE_AA)
    lbl, col = _fhi_status(fhi)
    cv2.rectangle(out, (6, 50), (sw - 6, 108), col, -1)
    cv2.putText(out, f"FHI  {fhi:.1f}", (12, 82), cv2.FONT_HERSHEY_SIMPLEX, 0.82,
                (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(out, lbl, (12, 102), cv2.FONT_HERSHEY_SIMPLEX, 0.48,
                (255, 255, 255), 1, cv2.LINE_AA)
    y = 128
    cv2.putText(out, "Distribusi Kelas:", (8, y), cv2.FONT_HERSHEY_SIMPLEX, 0.40,
                (170, 170, 170), 1, cv2.LINE_AA)
    y += 16
    bar_max = sw - 16
    for key in CLASSES:
        val = pct.get(key, 0.0)
        bw = int(bar_max * val / 100.0)
        if bw > 0:
            cv2.rectangle(out, (8, y), (8 + bw, y + 13), PALETTE[key], -1)
        cv2.putText(out, f"{DISPLAY[key][:15]}: {val:.1f}%", (8, y + 11),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.34, (240, 240, 240), 1, cv2.LINE_AA)
        y += 22
    return out


def process_frame(bgr, cfg, ema=None, draw=True):
    proc, hsv = _preprocess(bgr, cfg)
    exg = _excess_green(proc)
    label = classify_pixels(hsv, exg, cfg)
    label = _clean_label(label, cfg)
    pct, counts, fhi = _distribution(label)

    if ema is not None:
        a = cfg["ema_alpha"]
        for k in pct:
            ema[k] = a * pct[k] + (1 - a) * ema.get(k, pct[k])
        fhi = round(max(100.0 - sum(SEVERITY[k] * ema[k] for k in CLASSES), 0.0), 1)
        pct = {k: round(ema[k], 2) for k in CLASSES}

    overlay = None
    regions = []
    if draw:
        color = _colorize(label)
        if cfg.get("color_overlay", True):
            overlay = cv2.addWeighted(proc, 0.55, color, 0.45, 0)
        else:
            overlay = proc.copy()   # gambar asli, tanpa warna HSV
        regions = _extract_regions(label, cfg)
        for r in regions[:15]:
            x, y, ww, hh = r["bbox"]
            key = r["class"]
            cv2.rectangle(overlay, (x, y), (x + ww, y + hh), PALETTE[key], 2)
            txt = DISPLAY[key]
            (tw, th), _ = cv2.getTextSize(txt, cv2.FONT_HERSHEY_SIMPLEX, 0.42, 1)
            cv2.rectangle(overlay, (x, max(0, y - th - 6)), (x + tw + 4, y), PALETTE[key], -1)
            cv2.putText(overlay, txt, (x + 2, max(th, y - 4)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)
        overlay = _sidebar(overlay, fhi, pct)

    result = {
        "field_health": fhi,
        "class_pct": pct,
        "gcs_counts": {GCS_KEY[k]: counts[k] for k in CLASSES},
        "n_regions": len(regions),
    }
    return result, overlay


def _make_writer(path, fps, w, h):
    for cc in ("mp4v", "avc1", "XVID"):
        wr = cv2.VideoWriter(path, cv2.VideoWriter_fourcc(*cc), fps, (w, h))
        if wr.isOpened():
            return wr
    return None


# =============================================================================
# CLI
# =============================================================================
def run_image(args, cfg):
    img = cv2.imread(args.input)
    if img is None:
        sys.exit(f"Tidak bisa membuka gambar: {args.input}")
    if args.width and img.shape[1] > args.width:
        h = int(img.shape[0] * args.width / img.shape[1])
        img = cv2.resize(img, (args.width, h))
    res, overlay = process_frame(img, cfg, ema=None, draw=True)
    base = os.path.splitext(os.path.basename(args.input))[0]
    cv2.imwrite(os.path.join(args.output, f"{base}_v2.jpg"), overlay)
    with open(os.path.join(args.output, f"{base}_v2.json"), "w") as f:
        json.dump(res, f, indent=2)
    print(json.dumps(res, indent=2, ensure_ascii=False))


def run_video(args, cfg):
    cap = cv2.VideoCapture(args.input)
    if not cap.isOpened():
        sys.exit(f"Tidak bisa membuka video: {args.input}")
    src_fps = cap.get(cv2.CAP_PROP_FPS) or 30
    step = max(1, int(round(src_fps / max(args.fps, 0.1))))
    base = os.path.splitext(os.path.basename(args.input))[0]
    writer = None
    ema = {}
    timeline = []
    idx = 0
    t0 = time.time()
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        if idx % step == 0:
            if args.width and frame.shape[1] > args.width:
                h = int(frame.shape[0] * args.width / frame.shape[1])
                frame = cv2.resize(frame, (args.width, h))
            res, overlay = process_frame(frame, cfg, ema=ema, draw=not args.no_video)
            t = round(idx / src_fps, 2)
            row = {"t": t, "field_health": res["field_health"]}
            row.update(res["class_pct"])
            timeline.append(row)
            if not args.no_video and overlay is not None:
                if writer is None:
                    writer = _make_writer(os.path.join(args.output, f"{base}_v2.mp4"),
                                          args.fps, overlay.shape[1], overlay.shape[0])
                if writer:
                    writer.write(overlay)
            if len(timeline) % 25 == 0:
                print(f"  frame {len(timeline):4d}  t={t:.2f}s  FHI={res['field_health']:.1f}",
                      flush=True)
        idx += 1
    cap.release()
    if writer:
        writer.release()

    # ringkasan agregat
    n = max(len(timeline), 1)
    keys = CLASSES
    avg_pct = {k: round(sum(r.get(k, 0.0) for r in timeline) / n, 2) for k in keys}
    avg_fhi = round(sum(r["field_health"] for r in timeline) / n, 1)
    dominant = max(avg_pct, key=avg_pct.get) if timeline else None
    summary = {
        "video": os.path.basename(args.input),
        "frames_sampled": len(timeline),
        "duration_s": round(idx / src_fps, 1),
        "avg_field_health": avg_fhi,
        "status": _fhi_status(avg_fhi)[0],
        "avg_class_pct": avg_pct,
        "avg_class_display": {DISPLAY[k]: avg_pct[k] for k in keys},
        "dominant_class": DISPLAY.get(dominant) if dominant else None,
        "gcs_avg": {GCS_KEY[k]: avg_pct[k] for k in keys},
        "elapsed_s": round(time.time() - t0, 1),
    }
    with open(os.path.join(args.output, f"{base}_v2_summary.json"), "w") as f:
        json.dump({"summary": summary, "timeline": timeline}, f, indent=2)
    print("\n===== RINGKASAN =====")
    print(json.dumps(summary, indent=2, ensure_ascii=False))


def main():
    p = argparse.ArgumentParser(description="MoonHarvest v2 — detektor 4-kelas HSV")
    p.add_argument("-i", "--input", required=True)
    p.add_argument("-o", "--output", default="v2_out")
    p.add_argument("--image", action="store_true", help="input gambar, bukan video")
    p.add_argument("--fps", type=float, default=2.0, help="sampling fps video")
    p.add_argument("--width", type=int, default=1280)
    p.add_argument("--no-video", action="store_true", help="tidak menulis video, hanya JSON")
    p.add_argument("--no-auto", action="store_true", help="matikan auto-kalibrasi ExG")
    p.add_argument("--wb", action="store_true", help="aktifkan gray-world white balance")
    p.add_argument("--no-overlay", action="store_true",
                   help="gambar asli sebagai background (tanpa warna HSV), kotak+FHI tetap tampil")
    args = p.parse_args()

    cfg = dict(CFG)
    if args.no_auto:
        cfg["auto_calibrate"] = False
    if args.wb:
        cfg["white_balance"] = True
    if args.no_overlay:
        cfg["color_overlay"] = False
    os.makedirs(args.output, exist_ok=True)
    if args.image:
        run_image(args, cfg)
    else:
        run_video(args, cfg)


if __name__ == "__main__":
    main()
