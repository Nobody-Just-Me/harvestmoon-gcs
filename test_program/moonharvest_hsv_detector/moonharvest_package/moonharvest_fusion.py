#!/usr/bin/env python3
"""
MoonHarvest Fusion — HSV + YOLO per-Region (bukan grid)
=========================================================
Pipeline:
  1. HSV segmentasi per-piksel → connected-component regions (bounding box)
  2. Untuk SETIAP region, potong patch dari frame asli → YOLO classify patch itu
  3. Fusi: alpha*YOLO_prob + (1-alpha)*HSV_soft per kelas
  4. Tampil 3 panel: YOLO | FUSED | HSV  (kotak = region HSV, label berbeda)

Kelas YOLO (alfabetis saat training):
  0=bare_soil  1=disease_stress_vegetation  2=drought_stress
  3=healthy_crop  4=stressed_crop
HSV (moonharvest_hsv.py):
  0=healthy_crop  1=stressed_crop  2=disease_stress_vegetation
  3=drought_stress  4=bare_soil

Pemakaian:
  python3 moonharvest_fusion.py video -i hsvv.mp4 --weights best.pt -o out/
  python3 moonharvest_fusion.py image -i frame.jpg --weights best.pt -o out/
"""
import argparse, csv, json, os, sys, time, threading
import cv2
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import moonharvest_hsv as mh

# ---------------------------------------------------------------------------
# Konfigurasi kelas & pemetaan indeks
# ---------------------------------------------------------------------------
HSV_CLASSES  = mh.CLASSES   # urutan HSV
YOLO_CLASSES = sorted(HSV_CLASSES)          # urutan alfabetis = urutan training
YOLO_TO_HSV  = [HSV_CLASSES.index(c) for c in YOLO_CLASSES]
HSV_IDX      = {c: i for i, c in enumerate(HSV_CLASSES)}

PALETTE  = mh.PALETTE
SEVERITY = mh.SEVERITY

COLOR_AGREE    = (0, 200, 60)
COLOR_DISAGREE = (0, 60, 220)

DEFAULT_WEIGHTS = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "../../../../runs/classify/Pigeon_Harvest/runs/"
    "health_classification/health_train_v1-2/weights/best.pt"
)

# ---------------------------------------------------------------------------
# YOLO classify per region
# ---------------------------------------------------------------------------
def run_yolo_on_regions(frame, yolo_model, regions, max_regions=80):
    """Classify setiap HSV region patch dengan YOLO. Hasil disimpan in-place."""
    n = len(HSV_CLASSES)
    for r in regions[:max_regions]:
        x, y, w, h = r["bbox"]
        # Pastikan patch valid (minimal 16×16)
        if w < 16 or h < 16:
            r["yolo_class"]     = r["class"]
            r["yolo_conf"]      = r["confidence"]
            r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
            continue
        patch = frame[y:y+h, x:x+w]
        if patch.size == 0:
            r["yolo_class"]     = r["class"]
            r["yolo_conf"]      = r["confidence"]
            r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
            continue
        try:
            res  = yolo_model(patch, verbose=False, imgsz=224)[0]
            raw  = res.probs.data.cpu().numpy()         # urutan YOLO
            hsv_order = np.zeros(n, np.float32)
            for yi, hi in enumerate(YOLO_TO_HSV):
                if yi < len(raw):
                    hsv_order[hi] = float(raw[yi])
            top = int(np.argmax(hsv_order))
            r["yolo_class"]     = HSV_CLASSES[top]
            r["yolo_conf"]      = float(hsv_order[top])
            r["yolo_probs_hsv"] = hsv_order.tolist()
        except Exception:
            r["yolo_class"]     = r["class"]
            r["yolo_conf"]      = r["confidence"]
            r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
    # region di luar max_regions: salin HSV saja
    for r in regions[max_regions:]:
        r["yolo_class"]     = r["class"]
        r["yolo_conf"]      = r["confidence"]
        r["yolo_probs_hsv"] = _hsv_soft(HSV_IDX[r["class"]], r["confidence"])
    return regions


def _hsv_soft(cls_id, conf):
    n = len(HSV_CLASSES)
    v = np.full(n, (1 - conf) / max(n - 1, 1), np.float32)
    v[cls_id] = conf
    return v.tolist()


# ---------------------------------------------------------------------------
# Fusi per region
# ---------------------------------------------------------------------------
def fuse_region(region, alpha=0.55):
    """
    Gabungkan vektor probabilitas YOLO (HSV-order) dengan HSV soft-label.
    Return: fused_class(str), fused_conf(float), agree(bool)
    """
    n   = len(HSV_CLASSES)
    yp  = np.array(region.get("yolo_probs_hsv", [1/n]*n), np.float32)
    hp  = np.array(_hsv_soft(HSV_IDX[region["class"]], region["confidence"]), np.float32)
    yp /= yp.sum() + 1e-9
    hp /= hp.sum() + 1e-9
    fused    = alpha * yp + (1 - alpha) * hp
    fused_id = int(np.argmax(fused))
    agree    = region["yolo_class"] == region["class"]
    return HSV_CLASSES[fused_id], float(fused[fused_id]), agree


# ---------------------------------------------------------------------------
# Render panel
# ---------------------------------------------------------------------------
def _draw_panel_regions(base_frame, regions, cls_key, conf_key, title,
                        orig_w, orig_h, show_agreement=False):
    """
    Gambar bounding box dari regions HSV pada panel.
    cls_key  : kunci nama kelas di region dict ("class" / "yolo_class" / "fused_class")
    conf_key : kunci confidence ("confidence" / "yolo_conf" / "fused_conf")
    """
    out = base_frame.copy()
    ph, pw = out.shape[:2]
    sx = pw / orig_w
    sy = ph / orig_h

    for r in regions:
        x, y, w, h = r["bbox"]
        cls_name = r.get(cls_key, r["class"])
        conf     = r.get(conf_key, r["confidence"])
        col      = PALETTE.get(cls_name, (180, 180, 180))

        # Skalakan ke ukuran panel
        px0, py0 = int(x * sx), int(y * sy)
        px1, py1 = int((x + w) * sx), int((y + h) * sy)

        box_col = (COLOR_AGREE if r.get("agree", True) else COLOR_DISAGREE) \
                  if show_agreement else col

        cv2.rectangle(out, (px0, py0), (px1, py1), box_col, 2)

        # Label: nama kelas pendek + confidence
        short = cls_name.replace("_stress_vegetation", "").replace("_stress", "").replace("_", " ")
        label = f"{short} {conf:.2f}"
        (tw, th), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.38, 1)
        ly = max(0, py0 - 2)
        cv2.rectangle(out, (px0, ly - th - 4), (px0 + tw + 4, ly + 2), col, -1)
        cv2.putText(out, label, (px0 + 2, ly - 2),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.38, (255, 255, 255), 1, cv2.LINE_AA)

    # Judul panel (baris bawah)
    cv2.rectangle(out, (0, ph - 22), (pw, ph), (0, 0, 0), -1)
    cv2.putText(out, title, (6, ph - 6),
                cv2.FONT_HERSHEY_SIMPLEX, 0.52, (255, 255, 255), 1, cv2.LINE_AA)
    return out


def _stats_bar(regions, fh_yolo, fh_hsv, fh_fused, total_w):
    bar = np.zeros((72, total_w, 3), np.uint8)
    n   = len(regions)
    ok  = sum(1 for r in regions if r.get("agree", False))
    pct = 100 * ok / max(n, 1)
    lines = [
        (f"Agreement: {pct:.0f}%  ({ok}/{n} regions setuju)",
         COLOR_AGREE if pct >= 60 else COLOR_DISAGREE),
        (f"Field Health  —  YOLO: {fh_yolo:.1f}   HSV: {fh_hsv:.1f}   FUSED: {fh_fused:.1f}",
         (200, 200, 200)),
        ("Kotak HIJAU = setuju  |  Kotak MERAH = tidak setuju  |  Fusion: 55% YOLO + 45% HSV",
         (130, 130, 130)),
    ]
    for i, (txt, col) in enumerate(lines):
        cv2.putText(bar, txt, (10, 18 + i * 22),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.48, col, 1, cv2.LINE_AA)
    return bar


# ---------------------------------------------------------------------------
# Field Health dari region (area-weighted)
# ---------------------------------------------------------------------------
def _field_health_regions(regions, cls_key):
    area_by = {c: 0 for c in HSV_CLASSES}
    for r in regions:
        cls = r.get(cls_key, r["class"])
        if cls in area_by:
            area_by[cls] += r["area"]
    total = sum(area_by.values()) or 1
    pct   = {k: 100 * v / total for k, v in area_by.items()}
    return round(max(100.0 - sum(SEVERITY[k] * pct[k] for k in HSV_CLASSES), 0.0), 1)


# ---------------------------------------------------------------------------
# Proses satu frame
# ---------------------------------------------------------------------------
def process_fusion_frame(frame, cfg, yolo_model, alpha=0.55, ema_state=None,
                         panel_w=480):
    # 1. HSV: segmentasi + ekstrak regions
    result_hsv, _, _, label = mh.process_frame(frame, cfg, None, ema_state)
    regions = result_hsv["regions"]          # sorted by -area, sudah ada bbox
    fh_hsv  = result_hsv["field_health"]

    # 2. YOLO classify tiap region patch
    regions = run_yolo_on_regions(frame, yolo_model, regions)

    # 3. Fusi per region
    for r in regions:
        fc, fconf, agree = fuse_region(r, alpha)
        r["fused_class"] = fc
        r["fused_conf"]  = fconf
        r["agree"]       = agree

    # 4. Field Health
    fh_yolo  = _field_health_regions(regions, "yolo_class")
    fh_fused = _field_health_regions(regions, "fused_class")

    # 5. Render 3 panel
    orig_h, orig_w = frame.shape[:2]
    ph      = int(orig_h * panel_w / orig_w)
    resized = cv2.resize(frame, (panel_w, ph))

    p_yolo  = _draw_panel_regions(resized, regions, "yolo_class",  "yolo_conf",
                                  f"YOLO   FH={fh_yolo:.1f}", orig_w, orig_h)
    p_fused = _draw_panel_regions(resized, regions, "fused_class", "fused_conf",
                                  f"FUSED  FH={fh_fused:.1f}", orig_w, orig_h,
                                  show_agreement=True)
    p_hsv   = _draw_panel_regions(resized, regions, "class",       "confidence",
                                  f"HSV    FH={fh_hsv:.1f}", orig_w, orig_h)

    combo   = np.hstack([p_yolo, p_fused, p_hsv])
    stats   = _stats_bar(regions, fh_yolo, fh_hsv, fh_fused, combo.shape[1])
    display = np.vstack([combo, stats])

    agree_pct = 100 * sum(1 for r in regions if r.get("agree")) / max(len(regions), 1)
    metrics   = {
        "field_health_yolo" : fh_yolo,
        "field_health_hsv"  : fh_hsv,
        "field_health_fused": fh_fused,
        "agreement_pct"     : round(agree_pct, 1),
        "n_regions"         : len(regions),
    }
    return metrics, display, regions


# ---------------------------------------------------------------------------
# Subcommand VIDEO  (compute thread + display main thread → tidak flicker)
# ---------------------------------------------------------------------------
def run_video(args):
    try:
        from ultralytics import YOLO
    except ImportError:
        sys.exit("ultralytics belum terpasang. pip install ultralytics")

    cfg   = mh.load_cfg(args.config)
    alpha = args.alpha
    os.makedirs(args.output, exist_ok=True)

    print(f"[fusion] YOLO model : {args.weights}")
    yolo_model = YOLO(args.weights)
    print(f"[fusion] kelas YOLO : {yolo_model.names}")
    print(f"[fusion] video      : {args.input}")
    print(f"[fusion] alpha      : {alpha:.2f} YOLO + {1-alpha:.2f} HSV")
    print(f"[fusion] tekan 'q' untuk berhenti\n")

    WIN  = "MoonHarvest Fusion — YOLO | FUSED | HSV"
    base = os.path.splitext(os.path.basename(args.input))[0]
    t0   = time.time()

    # Shared state antara compute thread dan display (main) thread
    shared = {
        "display"  : None,   # frame terakhir yang siap ditampilkan
        "done"     : False,
        "log_rows" : [],
        "timeline" : [],
    }
    lock   = threading.Lock()
    writer = [None]          # list agar bisa diubah dari dalam fungsi

    # ----------------------------------------------------------------
    # COMPUTE THREAD: baca video, jalankan HSV+YOLO, update shared
    # ----------------------------------------------------------------
    def compute_loop():
        cap = cv2.VideoCapture(args.input)
        if not cap.isOpened():
            with lock:
                shared["done"] = True
            return
        src_fps = cap.get(cv2.CAP_PROP_FPS) or 30
        step    = max(1, int(round(src_fps / max(args.fps, 0.1))))
        ema     = {}
        idx     = 0

        while True:
            ok, frame = cap.read()
            if not ok:
                break
            if idx % step == 0:
                if args.width and frame.shape[1] > args.width:
                    fh = int(frame.shape[0] * args.width / frame.shape[1])
                    frame = cv2.resize(frame, (args.width, fh))

                t = round(idx / src_fps, 2)
                metrics, display, regions = process_fusion_frame(
                    frame, cfg, yolo_model, alpha, ema, panel_w=args.panel_w)
                disp_c = np.ascontiguousarray(display)

                # Simpan ke video
                if not args.no_video:
                    if writer[0] is None:
                        fourcc = cv2.VideoWriter_fourcc(*"mp4v")
                        writer[0] = cv2.VideoWriter(
                            os.path.join(args.output, f"{base}_fused.mp4"),
                            fourcc, args.fps,
                            (disp_c.shape[1], disp_c.shape[0]))
                    writer[0].write(disp_c)

                # Update shared state
                rows = []
                for r in regions:
                    rows.append({
                        "t": t, "area": r["area"],
                        "cx": round(r["centroid"][0], 1),
                        "cy": round(r["centroid"][1], 1),
                        "hsv_class"  : r["class"],
                        "hsv_conf"   : round(r["confidence"], 3),
                        "yolo_class" : r.get("yolo_class", r["class"]),
                        "yolo_conf"  : round(r.get("yolo_conf", 0.0), 3),
                        "fused_class": r.get("fused_class", r["class"]),
                        "fused_conf" : round(r.get("fused_conf", 0.0), 3),
                        "agree"      : int(r.get("agree", False)),
                    })
                with lock:
                    shared["display"] = disp_c
                    shared["timeline"].append({"t": t, **metrics})
                    shared["log_rows"].extend(rows)

                n = len(shared["timeline"])
                if n % 5 == 0:
                    print(f"  frame {n:4d}  t={t}s  "
                          f"regions={metrics['n_regions']}  "
                          f"agree={metrics['agreement_pct']:.0f}%  "
                          f"FH yolo={metrics['field_health_yolo']:.1f}  "
                          f"hsv={metrics['field_health_hsv']:.1f}  "
                          f"fused={metrics['field_health_fused']:.1f}", flush=True)
            idx += 1

        cap.release()
        if writer[0]:
            writer[0].release()
        with lock:
            shared["done"] = True

    # Mulai compute thread
    t_compute = threading.Thread(target=compute_loop, daemon=True)
    t_compute.start()

    # ----------------------------------------------------------------
    # DISPLAY LOOP (main thread): refresh ~30fps, selalu tampilkan frame terbaru
    # ----------------------------------------------------------------
    if not args.no_display:
        cv2.namedWindow(WIN, cv2.WINDOW_KEEPRATIO)

    while True:
        with lock:
            done  = shared["done"]
            disp  = shared["display"]

        if not args.no_display:
            if disp is not None:
                cv2.imshow(WIN, disp)
            k = cv2.waitKey(33) & 0xFF   # ~30fps
            if k == ord("q") or k == 27:
                print("  [dihentikan pengguna]")
                break
        else:
            time.sleep(0.033)

        if done:
            # Tunggu sebentar agar frame terakhir terlihat
            if not args.no_display:
                for _ in range(60):
                    cv2.imshow(WIN, disp)
                    if cv2.waitKey(33) & 0xFF in (ord("q"), 27):
                        break
            break

    if not args.no_display:
        cv2.destroyAllWindows()
    t_compute.join(timeout=30)

    # Ambil data final
    with lock:
        log_rows = list(shared["log_rows"])
        timeline = list(shared["timeline"])

    # ---- CSV ----
    if log_rows:
        csv_path = os.path.join(args.output, f"{base}_fusion_log.csv")
        with open(csv_path, "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(log_rows[0].keys()))
            w.writeheader(); w.writerows(log_rows)
        print(f"\n  CSV log   -> {csv_path}")

    # ---- Summary ----
    if timeline:
        avg = lambda k: round(float(np.mean([r[k] for r in timeline])), 1)
        cls_agree = {}
        for cls in HSV_CLASSES:
            rows_cls = [r for r in log_rows if r["hsv_class"] == cls or r["yolo_class"] == cls]
            both     = [r for r in rows_cls if r["agree"] == 1]
            cls_agree[cls] = round(100 * len(both) / max(len(rows_cls), 1), 1)

        summary = {
            "video"                  : os.path.basename(args.input),
            "frames_analyzed"        : len(timeline),
            "alpha_yolo"             : alpha,
            "avg_agreement_pct"      : avg("agreement_pct"),
            "min_agreement_pct"      : round(float(np.min([r["agreement_pct"] for r in timeline])), 1),
            "per_class_agreement_pct": cls_agree,
            "avg_field_health"       : {
                "yolo" : avg("field_health_yolo"),
                "hsv"  : avg("field_health_hsv"),
                "fused": avg("field_health_fused"),
            },
            "avg_regions_per_frame"  : avg("n_regions"),
            "proc_seconds"           : round(time.time() - t0, 1),
        }
        json_path = os.path.join(args.output, f"{base}_fusion_summary.json")
        with open(json_path, "w") as f:
            json.dump(summary, f, indent=2)
        print(f"  Summary   -> {json_path}")
        print(json.dumps(summary, indent=2))


# ---------------------------------------------------------------------------
# Subcommand IMAGE
# ---------------------------------------------------------------------------
def run_image(args):
    try:
        from ultralytics import YOLO
    except ImportError:
        sys.exit("ultralytics belum terpasang. pip install ultralytics")

    cfg  = mh.load_cfg(args.config)
    os.makedirs(args.output, exist_ok=True)

    print(f"[fusion] YOLO model : {args.weights}")
    yolo_model = YOLO(args.weights)

    bgr = cv2.imread(args.input)
    if bgr is None:
        sys.exit(f"Tidak bisa membaca: {args.input}")
    if args.width and bgr.shape[1] > args.width:
        bh = int(bgr.shape[0] * args.width / bgr.shape[1])
        bgr = cv2.resize(bgr, (args.width, bh))

    metrics, display, _ = process_fusion_frame(bgr, cfg, yolo_model, args.alpha,
                                               panel_w=args.panel_w)
    base    = os.path.splitext(os.path.basename(args.input))[0]
    out_img = os.path.join(args.output, f"{base}_fused.jpg")
    cv2.imwrite(out_img, display)
    print(f"Tersimpan: {out_img}")
    print(json.dumps(metrics, indent=2))
    cv2.imshow("MoonHarvest Fusion", display)
    cv2.waitKey(0)
    cv2.destroyAllWindows()


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def main():
    p = argparse.ArgumentParser(description="MoonHarvest Fusion: HSV region + YOLO classify")
    sub = p.add_subparsers(dest="cmd", required=True)

    pv = sub.add_parser("video")
    pv.add_argument("-i", "--input",   required=True)
    pv.add_argument("-o", "--output",  default="fusion_out")
    pv.add_argument("--weights", default=DEFAULT_WEIGHTS)
    pv.add_argument("--fps",     type=float, default=2.0)
    pv.add_argument("--width",   type=int,   default=960)
    pv.add_argument("--panel-w", type=int,   default=480)
    pv.add_argument("--alpha",   type=float, default=0.55)
    pv.add_argument("--delay",   type=int,   default=300)
    pv.add_argument("--no-video",   action="store_true")
    pv.add_argument("--no-display", action="store_true")
    pv.add_argument("--config",  default=None)
    pv.set_defaults(func=run_video)

    pi = sub.add_parser("image")
    pi.add_argument("-i", "--input",   required=True)
    pi.add_argument("-o", "--output",  default="fusion_out")
    pi.add_argument("--weights", default=DEFAULT_WEIGHTS)
    pi.add_argument("--width",   type=int,   default=960)
    pi.add_argument("--panel-w", type=int,   default=480)
    pi.add_argument("--alpha",   type=float, default=0.55)
    pi.add_argument("--config",  default=None)
    pi.set_defaults(func=run_image)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
