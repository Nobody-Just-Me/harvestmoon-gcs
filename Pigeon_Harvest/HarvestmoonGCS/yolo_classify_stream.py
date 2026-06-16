#!/usr/bin/env python3
"""
Grid-based YOLO classification streaming for MoonHarvest GCS.

Reads video frames, divides into grid, classifies each cell via YOLO,
draws scaled bounding boxes + summary overlay, outputs base64 JPEG JSON to stdout.

Protocol (same as camera_service.py):
  {"type": "frame", "data": "<base64_jpeg>"}     -- annotated frame
  {"type": "detection", "data": {"count": N, "summary": "...", "classes": {...}}}  -- per-frame metadata
  {"type": "error", "data": "..."}                 -- non-fatal error
"""

import argparse
import base64
import json
import signal
import sys
import time

import cv2
import numpy as np
from ultralytics import YOLO

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

COLORS = {
    "healthy_crop": (0, 255, 0),  # Green
    "stressed_crop": (0, 165, 255),  # Orange
    "disease_stress_vegetation": (0, 0, 255),  # Red
    "drought_stress": (0, 255, 255),  # Yellow
    "bare_soil": (128, 128, 128),  # Gray
}

SHORT_LABELS = {
    "healthy_crop": "Healthy",
    "stressed_crop": "Stressed",
    "disease_stress_vegetation": "Disease",
    "drought_stress": "Drought",
    "bare_soil": "Bare Soil",
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def emit(payload: dict) -> None:
    """Write a JSON line to stdout and flush immediately."""
    print(json.dumps(payload), flush=True)


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def scale_box(x1: int, y1: int, x2: int, y2: int, scale: float, w: int, h: int):
    """Scale a box around its centre so adjacent boxes don't touch."""
    cx = (x1 + x2) / 2.0
    cy = (y1 + y2) / 2.0
    bw = (x2 - x1) * scale
    bh = (y2 - y1) * scale
    return (
        int(clamp(cx - bw / 2.0, 0, w - 1)),
        int(clamp(cy - bh / 2.0, 0, h - 1)),
        int(clamp(cx + bw / 2.0, 0, w - 1)),
        int(clamp(cy + bh / 2.0, 0, h - 1)),
    )


# ---------------------------------------------------------------------------
# Grid classification
# ---------------------------------------------------------------------------


def classify_grid(frame, model, grid_rows: int, grid_cols: int, min_conf: float):
    """Divide frame into grid cells and classify each one.

    Returns (detections, total_count, class_counts).
    """
    height, width = frame.shape[:2]
    cell_h = height // grid_rows
    cell_w = width // grid_cols

    detections = []
    class_counts: dict[str, int] = {}

    for row in range(grid_rows):
        for col in range(grid_cols):
            x1 = col * cell_w
            y1 = row * cell_h
            x2 = x1 + cell_w
            y2 = y1 + cell_h

            cell = frame[y1:y2, x1:x2]
            results = model(cell, verbose=False)
            if not results or len(results) == 0:
                continue

            probs = results[0].probs
            class_id = int(probs.top1)
            conf = float(probs.top1conf)
            class_name = results[0].names[class_id]

            if conf < min_conf:
                continue

            detections.append(
                {
                    "bbox": (x1, y1, x2, y2),
                    "class": class_name,
                    "class_id": class_id,
                    "confidence": conf,
                }
            )
            class_counts[class_name] = class_counts.get(class_name, 0) + 1

    total = sum(class_counts.values())
    return detections, total, class_counts


# ---------------------------------------------------------------------------
# Drawing
# ---------------------------------------------------------------------------


def draw_detections(frame, detections, box_scale: float):
    """Draw scaled boxes with labels on the frame."""
    annotated = frame.copy()
    height, width = frame.shape[:2]

    for det in detections:
        x1, y1, x2, y2 = det["bbox"]
        class_name = det["class"]
        conf = det["confidence"]
        color = COLORS.get(class_name, (255, 255, 255))

        sx1, sy1, sx2, sy2 = scale_box(x1, y1, x2, y2, box_scale, width, height)
        cv2.rectangle(annotated, (sx1, sy1), (sx2, sy2), color, 2)

        label = f"{class_name} {conf:.2f}"
        (lw, lh), baseline = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        cv2.rectangle(
            annotated,
            (sx1, sy1 - lh - 10),
            (sx1 + lw + 10, sy1),
            color,
            -1,
        )
        cv2.putText(
            annotated,
            label,
            (sx1 + 5, sy1 - 5),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (255, 255, 255),
            1,
            cv2.LINE_AA,
        )

    return annotated


def add_statistics_overlay(frame, detections):
    """Draw detection summary overlay at top-left."""
    total = len(detections)
    if total == 0:
        return frame

    class_counts: dict[str, int] = {}
    for det in detections:
        cls = det["class"]
        class_counts[cls] = class_counts.get(cls, 0) + 1

    dominant = max(class_counts, key=class_counts.get)

    overlay = frame.copy()
    n = len(class_counts)
    box_h = 100 + n * 22
    cv2.rectangle(overlay, (10, 10), (340, 10 + box_h), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.55, frame, 0.45, 0, frame)

    y = 35
    cv2.putText(
        frame,
        "Detection Summary",
        (20, y),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.65,
        (255, 255, 255),
        2,
        cv2.LINE_AA,
    )
    y += 28
    cv2.putText(
        frame,
        f"Total: {total} cells",
        (20, y),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.5,
        (200, 200, 200),
        1,
        cv2.LINE_AA,
    )
    y += 18
    cv2.putText(
        frame,
        f"Dominant: {SHORT_LABELS.get(dominant, dominant)} ({class_counts[dominant]})",
        (20, y),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.5,
        (255, 255, 200),
        1,
        cv2.LINE_AA,
    )
    y += 24

    for cls_name in sorted(class_counts, key=lambda c: class_counts[c], reverse=True):
        count = class_counts[cls_name]
        pct = count / total * 100
        color = COLORS.get(cls_name, (255, 255, 255))
        short = SHORT_LABELS.get(cls_name, cls_name)
        prefix = ">" if cls_name == dominant else " "
        cv2.putText(
            frame,
            f"{prefix}{short}: {count} ({pct:.0f}%)",
            (20, y),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.45,
            color,
            1,
            cv2.LINE_AA,
        )
        y += 22

    return frame


# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------


def main():
    parser = argparse.ArgumentParser(
        description="Grid-based YOLO health classification streaming for GCS"
    )
    parser.add_argument("--source", type=str, required=True, help="Video source (path or camera index)")
    parser.add_argument("--model", type=str, required=True, help="Path to YOLO classification model (.pt)")
    parser.add_argument("--grid-rows", type=int, default=5, help="Number of grid rows (default: 5)")
    parser.add_argument("--grid-cols", type=int, default=8, help="Number of grid columns (default: 8)")
    parser.add_argument("--min-conf", type=float, default=0.3, help="Minimum confidence threshold (default: 0.3)")
    parser.add_argument("--box-scale", type=float, default=0.85, help="Box scale factor for gaps (default: 0.85)")
    parser.add_argument("--max-fps", type=float, default=15.0, help="Maximum output FPS (default: 15)")
    args = parser.parse_args()

    signal.signal(signal.SIGINT, lambda s, f: sys.exit(0))
    signal.signal(signal.SIGTERM, lambda s, f: sys.exit(0))

    # Load model
    try:
        model = YOLO(args.model)
    except Exception as exc:
        emit({"type": "error", "data": f"Failed to load model: {exc}"})
        return 1

    # Open video
    source = args.source
    if source.isdigit():
        cap = cv2.VideoCapture(int(source))
    else:
        cap = cv2.VideoCapture(source)

    if not cap.isOpened():
        emit({"type": "error", "data": f"Failed to open video source: {source}"})
        return 1

    min_frame_interval = 1.0 / args.max_fps
    last_frame_time = 0.0

    try:
        while True:
            ret, frame = cap.read()
            if not ret or frame is None:
                # End of video — emit an end marker and exit
                emit({"type": "end", "data": "Video stream ended"})
                break

            # Rate-limit
            now = time.time()
            if now - last_frame_time < min_frame_interval:
                continue
            last_frame_time = now

            # Classify grid
            detections, total, class_counts = classify_grid(
                frame, model, args.grid_rows, args.grid_cols, args.min_conf
            )

            # Draw boxes
            annotated = draw_detections(frame, detections, args.box_scale)

            # Draw summary overlay
            annotated = add_statistics_overlay(annotated, detections)

            # Encode as JPEG
            ok, buf = cv2.imencode(".jpg", annotated, [cv2.IMWRITE_JPEG_QUALITY, 80])
            if not ok:
                emit({"type": "error", "data": "JPEG encoding failed"})
                continue

            b64 = base64.b64encode(buf).decode("utf-8")
            emit({"type": "frame", "data": b64})

            # Build summary string for detection metadata
            if total > 0:
                summary_parts = []
                for cls_name in sorted(class_counts, key=lambda c: class_counts[c], reverse=True):
                    short = SHORT_LABELS.get(cls_name, cls_name)
                    summary_parts.append(f"{short}: {class_counts[cls_name]}")
                summary_str = " | ".join(summary_parts)
            else:
                summary_str = "No detections"

            emit(
                {
                    "type": "detection",
                    "data": {
                        "count": total,
                        "summary": summary_str,
                        "classes": class_counts,
                    },
                }
            )

    except (BrokenPipeError, EOFError):
        # Parent closed pipe — normal shutdown
        pass
    except KeyboardInterrupt:
        pass
    except Exception as exc:
        emit({"type": "error", "data": str(exc)})
        return 1
    finally:
        cap.release()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
