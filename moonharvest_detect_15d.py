#!/usr/bin/env python3
"""
MoonHarvest detector khusus video 15d.

Tujuan:
- tidak mengubah perilaku `moonharvest_detect_v3.py`
- mengecilkan region HSV agar box tidak terlalu "menyapu" satu petak besar
- lebih memprioritaskan prediksi model dibanding fallback HSV mentah
- fallback HSV dibuat lebih konservatif
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path

import moonharvest_detect_v3 as base


# Konfigurasi khusus video 15d: region lebih kecil, smoothing lebih ringan.
CFG_15D = dict(base.HSV_CFG)
CFG_15D.update({
    "exg_healthy_min": 0.032,
    "min_area_frac": 0.0035,
    "max_regions": 28,
    "morph_k": 5,
    "ema_alpha": 0.50,
})


def fuse_detections_15d(frame, regions, classifier, imgsz=192):
    """
    Versi lebih konservatif:
    - ONNX dipakai lebih sering
    - HSV hanya jadi fallback lemah
    - kalau ONNX dan HSV tidak cocok, tapi confidence ONNX tinggi, tetap pakai ONNX
    """
    if not regions:
        return []

    crops = []
    for r in regions:
        x1, y1, x2, y2 = r["bbox"]
        crop = frame[y1:y2, x1:x2]
        crops.append(crop if crop.size > 0 else base.np.zeros((8, 8, 3), base.np.uint8))

    onnx_results = classifier.infer(crops, imgsz=imgsz)
    detections = []

    for i, (onnx_cls, onnx_conf) in enumerate(onnx_results):
        r = regions[i]
        onnx_display = base.ONNX_TO_DISPLAY.get(onnx_cls)
        compat = base.HSV_ONNX_COMPAT.get(r["hsv_zone"], set())
        area_ratio = ((r["bbox"][2] - r["bbox"][0]) * (r["bbox"][3] - r["bbox"][1])) / max(1.0, frame.shape[0] * frame.shape[1])

        if onnx_display and onnx_conf >= 0.58 and onnx_display in compat:
            display = onnx_display
            conf = min(0.96, onnx_conf * 1.06)
            source = "onnx-compat"
        elif onnx_display and onnx_conf >= 0.70:
            display = onnx_display
            conf = min(0.94, onnx_conf * 1.02)
            source = "onnx-strong"
        elif onnx_display and onnx_conf >= 0.50 and area_ratio <= 0.18:
            display = onnx_display
            conf = min(0.88, max(onnx_conf, 0.50))
            source = "onnx-soft"
        else:
            display = r["hsv_display"]
            # fallback HSV tidak lagi terlalu percaya diri
            base_conf = 0.38 if area_ratio < 0.10 else 0.44
            conf = max(min(onnx_conf, 0.49), base_conf)
            source = "hsv-fallback"

        x1, y1, x2, y2 = r["bbox"]
        detections.append({
            "bbox": (x1, y1, x2, y2),
            "class": display,
            "conf": round(conf, 3),
            "area": r["area"],
            "source": source,
        })

    return detections


def main():
    parser = argparse.ArgumentParser(
        description="MoonHarvest detector khusus video 15d dengan fusion lebih konservatif"
    )
    parser.add_argument("video", help="Path video UAV (.mp4)")
    parser.add_argument("--model", default="runs/classify/health_train_v5-20260626/weights/best.pt")
    parser.add_argument("--output", default=None)
    parser.add_argument("--skip", type=int, default=1,
                        help="Skip 1 = infer setiap 2 frame, lebih rapat dari v3")
    parser.add_argument("--scale", type=float, default=0.7)
    parser.add_argument("--show", action="store_true")
    parser.add_argument("--no-log", action="store_true")
    args = parser.parse_args()

    if args.output is None:
        os.makedirs("out", exist_ok=True)
        stem = Path(args.video).stem.replace("(", "").replace(")", "")
        args.output = f"out/{stem}_15d.mp4"

    os.environ.setdefault("CUDA_VISIBLE_DEVICES", "")

    base.fuse_detections = fuse_detections_15d
    base.ONNX_MIN_CONF = 0.58
    base.DISPLAY_MIN_CONF = 0.36

    base.process(
        video_path=args.video,
        model_path=args.model,
        output_path=args.output,
        skip=args.skip,
        out_scale=args.scale,
        show=args.show,
        cfg=CFG_15D,
        log_csv=not args.no_log,
    )


if __name__ == "__main__":
    main()
