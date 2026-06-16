#!/usr/bin/env python3
"""
MoonHarvest - Perbandingan HSV vs YOLO + bootstrap dataset YOLO dari mask HSV.

Program HSV (moonharvest_hsv.py) TIDAK butuh YOLO. Script ini OPSIONAL, untuk:
  1) auto-label : ubah mask HSV -> label YOLO-seg (bootstrap dataset, tanpa
                  anotasi manual). Ini cara tercepat menyiapkan data YOLO.
  2) compare    : jalankan YOLO terlatih + HSV pada gambar/video yang sama,
                  lalu tempel berdampingan untuk perbandingan visual & angka.

YOLO (ultralytics) hanya diperlukan untuk subcommand 'compare'. 'auto-label'
murni HSV + OpenCV.

Contoh:
  python3 yolo_compare.py auto-label -i frames/ -o yolo_dataset --model model.json
  python3 yolo_compare.py compare -i clip.mp4 --weights best.pt --model model.json -o cmp
"""
import argparse, os, sys, glob, json, time
import numpy as np
import cv2

import moonharvest_hsv as mh

CLASSES = mh.CLASSES  # 5 kelas, indeks sama dengan label YOLO


# ---------------------------------------------------------------------------
# 1) AUTO-LABEL: mask HSV -> anotasi YOLO segmentation (polygon ternormalisasi)
# ---------------------------------------------------------------------------
def mask_to_yolo_polygons(label, min_area_frac=0.002, eps_frac=0.01):
    """Tiap komponen kelas -> 1 baris YOLO-seg: 'cls x1 y1 x2 y2 ...' (0-1)."""
    h, w = label.shape
    lines = []
    for cls_idx in range(len(CLASSES)):
        m = (label == cls_idx).astype(np.uint8)
        if m.sum() == 0:
            continue
        cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for c in cnts:
            if cv2.contourArea(c) < min_area_frac * w * h:
                continue
            eps = eps_frac * cv2.arcLength(c, True)
            poly = cv2.approxPolyDP(c, eps, True).reshape(-1, 2).astype(np.float32)
            if len(poly) < 3:
                continue
            poly[:, 0] /= w
            poly[:, 1] /= h
            coords = " ".join(f"{v:.6f}" for v in poly.flatten())
            lines.append(f"{cls_idx} {coords}")
    return lines


def run_autolabel(args):
    cfg = mh.load_cfg(args.config)
    model = json.load(open(args.model)) if args.model else None
    engine = "stat" if model else "rule"
    imgs = []
    if os.path.isdir(args.input):
        for ext in ("*.jpg", "*.png", "*.jpeg"):
            imgs += glob.glob(os.path.join(args.input, ext))
    else:
        imgs = [args.input]
    imgs.sort()
    img_dir = os.path.join(args.output, "images")
    lbl_dir = os.path.join(args.output, "labels")
    os.makedirs(img_dir, exist_ok=True)
    os.makedirs(lbl_dir, exist_ok=True)
    n = 0
    for p in imgs:
        bgr = cv2.imread(p)
        if bgr is None:
            continue
        if args.width and bgr.shape[1] > args.width:
            hh = int(bgr.shape[0] * args.width / bgr.shape[1])
            bgr = cv2.resize(bgr, (args.width, hh))
        _, _, _, label = mh.process_frame(bgr, cfg, engine=engine, model=model)
        base = os.path.splitext(os.path.basename(p))[0]
        cv2.imwrite(os.path.join(img_dir, base + ".jpg"), bgr)
        lines = mask_to_yolo_polygons(label)
        with open(os.path.join(lbl_dir, base + ".txt"), "w") as f:
            f.write("\n".join(lines))
        n += 1
    # data.yaml siap latih YOLOv8-seg
    with open(os.path.join(args.output, "data.yaml"), "w") as f:
        f.write("path: .\ntrain: images\nval: images\n")
        f.write(f"nc: {len(CLASSES)}\n")
        f.write("names: [" + ", ".join(CLASSES) + "]\n")
    print(json.dumps({"dataset": args.output, "images": n,
                      "note": "latih: yolo segment train data=data.yaml model=yolov8n-seg.pt epochs=100"},
                     indent=2))


# ---------------------------------------------------------------------------
# 2) COMPARE: YOLO terlatih vs HSV, berdampingan
# ---------------------------------------------------------------------------
def run_compare(args):
    try:
        from ultralytics import YOLO
    except Exception:
        sys.exit("ultralytics belum terpasang. `pip install ultralytics` lalu ulangi. "
                 "(HSV tetap jalan tanpa ini lewat moonharvest_hsv.py)")
    cfg = mh.load_cfg(args.config)
    model = json.load(open(args.model)) if args.model else None
    engine = "stat" if model else "rule"
    yolo = YOLO(args.weights)
    os.makedirs(args.output, exist_ok=True)

    def process(bgr):
        _, hsv_overlay, _, _ = mh.process_frame(bgr, cfg, mh.parse_grid("10x6"),
                                                engine=engine, model=model)
        res = yolo.predict(bgr, verbose=False)[0]
        yolo_overlay = res.plot()
        h = min(hsv_overlay.shape[0], yolo_overlay.shape[0])
        a = cv2.resize(hsv_overlay, (int(hsv_overlay.shape[1] * h / hsv_overlay.shape[0]), h))
        b = cv2.resize(yolo_overlay, (int(yolo_overlay.shape[1] * h / yolo_overlay.shape[0]), h))
        cv2.putText(a, "HSV", (12, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        cv2.putText(b, "YOLO", (12, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        return np.hstack([a, b])

    if args.input.lower().endswith((".mp4", ".mov", ".avi")):
        cap = cv2.VideoCapture(args.input)
        fps = cap.get(cv2.CAP_PROP_FPS) or 30
        step = max(1, int(round(fps / max(args.fps, 0.1))))
        writer = None
        idx = 0
        while True:
            ok, fr = cap.read()
            if not ok:
                break
            if idx % step == 0:
                if args.width and fr.shape[1] > args.width:
                    hh = int(fr.shape[0] * args.width / fr.shape[1])
                    fr = cv2.resize(fr, (args.width, hh))
                combo = process(fr)
                if writer is None:
                    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
                    writer = cv2.VideoWriter(os.path.join(args.output, "compare.mp4"),
                                             fourcc, args.fps, (combo.shape[1], combo.shape[0]))
                writer.write(combo)
            idx += 1
        cap.release()
        if writer:
            writer.release()
        print("tersimpan:", os.path.join(args.output, "compare.mp4"))
    else:
        bgr = cv2.imread(args.input)
        combo = process(bgr)
        out = os.path.join(args.output, "compare.jpg")
        cv2.imwrite(out, combo)
        print("tersimpan:", out)


def main():
    p = argparse.ArgumentParser(description="HSV vs YOLO")
    sub = p.add_subparsers(dest="cmd", required=True)

    pa = sub.add_parser("auto-label", help="mask HSV -> dataset YOLO-seg")
    pa.add_argument("-i", "--input", required=True, help="gambar atau folder")
    pa.add_argument("-o", "--output", default="yolo_dataset")
    pa.add_argument("--model", default=None, help="model.json (engine stat)")
    pa.add_argument("--config", default=None)
    pa.add_argument("--width", type=int, default=960)
    pa.set_defaults(func=run_autolabel)

    pc = sub.add_parser("compare", help="YOLO terlatih vs HSV berdampingan")
    pc.add_argument("-i", "--input", required=True)
    pc.add_argument("--weights", required=True, help="bobot YOLO .pt")
    pc.add_argument("--model", default=None, help="model.json (engine stat)")
    pc.add_argument("--config", default=None)
    pc.add_argument("--fps", type=float, default=2.0)
    pc.add_argument("--width", type=int, default=960)
    pc.add_argument("-o", "--output", default="cmp")
    pc.set_defaults(func=run_compare)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
