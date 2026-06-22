#!/usr/bin/env python3
"""
UAV Video Detection with Bounding Boxes and Health Classification
Optimized for drone footage with grid-based detection.

DEMO_MODE (MOONHARVEST_DEMO=1): remaps 5 internal classes → 4 proposal classes,
hides bare_soil, merges drought_stress into Stress, uses connected-component
merged bounding boxes instead of per-cell boxes, and sets imgsz=416.
"""

import argparse
import os
import cv2
import numpy as np
import time
from pathlib import Path
from ultralytics import YOLO

# ---------------------------------------------------------------------------
# DEMO_MODE flag — all proposal-alignment transforms are gated on this
# ---------------------------------------------------------------------------
DEMO_MODE = os.environ.get("MOONHARVEST_DEMO", "1") == "1"

# v3 model class names → display labels (Zone Analysis proposal)
DISPLAY_MAP = {
    "lush_green":          "Lush Green",
    "well_irrigated":      "Well Irrigated",
    "inconsistent_growth": "Inconsistent Growth",
    "soil_issues":         "Soil Issues",
    "disease":             "Disease",
    "pest":                "Pest",
}

# v3: tidak ada kelas yang disembunyikan
HIDDEN_CLASSES: set = set()

# Demo colors keyed by display label (BGR)
DEMO_COLORS = {
    "Lush Green":          (50,  205,  50),
    "Well Irrigated":      (200, 150,   2),
    "Inconsistent Growth": (0,   200, 255),
    "Soil Issues":         (55,   64,  93),
    "Disease":             (0,    60, 255),
    "Pest":                (0,   140, 255),
}

# Internal colors (non-demo path) — sama untuk v3
INTERNAL_COLORS = {
    "lush_green":          (50,  205,  50),
    "well_irrigated":      (200, 150,   2),
    "inconsistent_growth": (0,   200, 255),
    "soil_issues":         (55,   64,  93),
    "disease":             (0,    60, 255),
    "pest":                (0,   140, 255),
}

INFERENCE_SIZE = 640  # imgsz — matches proposal Figure 3 (OpenCV Resize 640×640)


# ---------------------------------------------------------------------------
# Utility
# ---------------------------------------------------------------------------

def clamp(value, low, high):
    return max(low, min(high, value))


def scale_box(xyxy, scale, width, height):
    x1, y1, x2, y2 = xyxy
    cx = (x1 + x2) / 2.0
    cy = (y1 + y2) / 2.0
    box_w = (x2 - x1) * scale
    box_h = (y2 - y1) * scale
    return (
        clamp(cx - box_w / 2.0, 0, width - 1),
        clamp(cy - box_h / 2.0, 0, height - 1),
        clamp(cx + box_w / 2.0, 0, width - 1),
        clamp(cy + box_h / 2.0, 0, height - 1),
    )


# ---------------------------------------------------------------------------
# Grid classification
# ---------------------------------------------------------------------------

def create_grid_detections(frame, grid_rows=4, grid_cols=6, classifier=None):
    """Divide frame into grid and classify each cell. Returns raw detections."""
    height, width = frame.shape[:2]
    cell_height = height // grid_rows
    cell_width = width // grid_cols

    detections = []

    for row in range(grid_rows):
        for col in range(grid_cols):
            x1 = col * cell_width
            y1 = row * cell_height
            x2 = x1 + cell_width
            y2 = y1 + cell_height

            cell = frame[y1:y2, x1:x2]

            if classifier:
                results = classifier(cell, verbose=False, imgsz=INFERENCE_SIZE)
                if results and len(results) > 0:
                    probs = results[0].probs
                    class_id = int(probs.top1)
                    confidence = float(probs.top1conf)
                    class_name = results[0].names[class_id]

                    detections.append({
                        "bbox": (x1, y1, x2, y2),
                        "class": class_name,
                        "confidence": confidence,
                        "class_id": class_id,
                    })

    if DEMO_MODE:
        # Apply merge map: drought → stressed, remove bare_soil
        remapped = []
        for det in detections:
            raw_cls = det["class"]
            if raw_cls in HIDDEN_CLASSES:
                continue
            mapped = DISPLAY_MAP.get(raw_cls)
            if mapped is None:
                continue
            # Store display label as class name for rendering
            remapped.append({**det, "class": mapped, "display_label": mapped})
        return remapped

    return detections


# ---------------------------------------------------------------------------
# TASK-05 — Connected-component merging: adjacent same-class cells → one box
# ---------------------------------------------------------------------------

def merge_grid_to_regions(detections, frame_w, frame_h, grid_rows, grid_cols):
    """BFS connected-component merge of adjacent grid cells into merged boxes."""
    if not detections:
        return []

    cell_h = frame_h // grid_rows
    cell_w = frame_w // grid_cols

    # Build sparse grid map: (row, col) → det
    cell_map = {}
    for det in detections:
        x1, y1, _, _ = det["bbox"]
        col = min(x1 // cell_w, grid_cols - 1)
        row = min(y1 // cell_h, grid_rows - 1)
        cell_map[(row, col)] = det

    visited = set()
    regions = []

    for pos in list(cell_map.keys()):
        if pos in visited:
            continue
        cls = cell_map[pos]["class"]
        # BFS
        queue = [pos]
        cells = []
        while queue:
            r, c = queue.pop()
            if (r, c) in visited:
                continue
            if cell_map.get((r, c), {}).get("class") != cls:
                continue
            visited.add((r, c))
            cells.append(cell_map[(r, c)])
            for dr, dc in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                nb = (r + dr, c + dc)
                if nb not in visited and cell_map.get(nb, {}).get("class") == cls:
                    queue.append(nb)

        if not cells:
            continue

        bx1 = min(d["bbox"][0] for d in cells)
        by1 = min(d["bbox"][1] for d in cells)
        bx2 = max(d["bbox"][2] for d in cells)
        by2 = max(d["bbox"][3] for d in cells)
        avg_conf = sum(d["confidence"] for d in cells) / len(cells)

        regions.append({
            "bbox": (bx1, by1, bx2, by2),
            "class": cls,
            "class_id": cells[0]["class_id"],
            "confidence": avg_conf,
            "cell_count": len(cells),
        })

    return regions


# ---------------------------------------------------------------------------
# NMS — suppress overlapping regional boxes (matches proposal Figure 3)
# ---------------------------------------------------------------------------

def apply_nms(detections, iou_threshold=0.45, min_cells=2):
    """IoU-based NMS on merged regional bounding boxes.

    BFS merge already prevents same-class overlaps; NMS is the final
    pipeline stage matching proposal Figure 3, handling any residual
    cross-class overlaps.
    min_cells: discard isolated single-cell noise regions (default ≥ 2).
    """
    # Filter out single isolated cells — these are noise, not real regions
    detections = [d for d in detections if d.get("cell_count", 1) >= min_cells]

    if len(detections) <= 1:
        return detections

    boxes = []
    scores = []
    for d in detections:
        x1, y1, x2, y2 = d["bbox"]
        boxes.append([int(x1), int(y1), int(x2 - x1), int(y2 - y1)])
        scores.append(float(d["confidence"]))

    indices = cv2.dnn.NMSBoxes(boxes, scores, score_threshold=0.0, nms_threshold=iou_threshold)
    if len(indices) == 0:
        return []

    flat = indices.flatten() if hasattr(indices, "flatten") else list(indices)
    return [detections[i] for i in flat]


# ---------------------------------------------------------------------------
# Drawing
# ---------------------------------------------------------------------------

def draw_detections(frame, detections, min_conf=0.3, box_scale=0.85):
    """Draw bounding boxes and labels on frame (merged or grid depending on DEMO_MODE)."""
    annotated = frame.copy()
    overlay   = frame.copy()   # for semi-transparent fill
    height, width = frame.shape[:2]

    for det in detections:
        if det["confidence"] < min_conf:
            continue

        x1, y1, x2, y2 = det["bbox"]
        class_name = det["class"]
        confidence = det["confidence"]

        if DEMO_MODE:
            sx1, sy1, sx2, sy2 = int(x1), int(y1), int(x2), int(y2)
            color = DEMO_COLORS.get(class_name, (255, 255, 255))
            label = f"{class_name} {confidence:.2f}"
        else:
            sx1, sy1, sx2, sy2 = map(int, scale_box((x1, y1, x2, y2), box_scale, width, height))
            color = INTERNAL_COLORS.get(class_name, (255, 255, 255))
            label = f"{class_name} {confidence:.2f}"

        # Semi-transparent fill inside the box
        cv2.rectangle(overlay, (sx1, sy1), (sx2, sy2), color, -1)

        # Solid border (3px)
        cv2.rectangle(annotated, (sx1, sy1), (sx2, sy2), color, 3)

        (label_w, label_h), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.6, 2)
        cv2.rectangle(annotated, (sx1, sy1 - label_h - 12), (sx1 + label_w + 12, sy1), color, -1)
        cv2.putText(annotated, label, (sx1 + 6, sy1 - 6),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2, cv2.LINE_AA)

    # Blend semi-transparent fill (20% opacity)
    cv2.addWeighted(overlay, 0.20, annotated, 0.80, 0, annotated)
    return annotated


def add_statistics_overlay(frame, detections):
    """Draw detection summary overlay (proposal-aligned labels in DEMO_MODE)."""
    total = len(detections)
    if total == 0:
        return frame

    class_counts = {}
    for det in detections:
        cls = det["class"]
        class_counts[cls] = class_counts.get(cls, 0) + 1

    dominant = max(class_counts, key=class_counts.get)
    overlay = frame.copy()
    num_classes = len(class_counts)
    box_h = 110 + num_classes * 22
    cv2.rectangle(overlay, (10, 10), (320, 10 + box_h), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.55, frame, 0.45, 0, frame)

    y = 35
    cv2.putText(frame, "Detection Summary", (20, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2, cv2.LINE_AA)
    y += 28
    total_cells = sum(d.get("cell_count", 1) for d in detections)
    cv2.putText(frame, f"Regions: {total}  Cells: {total_cells}", (20, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.5, (200, 200, 200), 1, cv2.LINE_AA)
    y += 18
    cv2.putText(frame, f"Dominant: {dominant} ({class_counts[dominant]})", (20, y),
                cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 200), 1, cv2.LINE_AA)
    y += 24

    colors = DEMO_COLORS if DEMO_MODE else INTERNAL_COLORS
    for cls_name in sorted(class_counts, key=lambda c: class_counts[c], reverse=True):
        count = class_counts[cls_name]
        pct = count / total * 100
        color = colors.get(cls_name, (255, 255, 255))
        prefix = ">" if cls_name == dominant else " "
        cv2.putText(frame, f"{prefix}{cls_name}: {count} ({pct:.0f}%)", (20, y),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.45, color, 1, cv2.LINE_AA)
        y += 22

    if DEMO_MODE:
        cv2.putText(frame, f"Inference: {INFERENCE_SIZE}x{INFERENCE_SIZE}", (20, y + 4),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.4, (180, 180, 180), 1, cv2.LINE_AA)

    return frame


def add_legend(frame):
    """Draw proposal-aligned class legend in bottom-right corner."""
    if not DEMO_MODE:
        return frame
    h, w = frame.shape[:2]
    entries = [
        ("Lush Green",          DEMO_COLORS["Lush Green"]),
        ("Well Irrigated",      DEMO_COLORS["Well Irrigated"]),
        ("Inconsistent Growth", DEMO_COLORS["Inconsistent Growth"]),
        ("Soil Issues",         DEMO_COLORS["Soil Issues"]),
        ("Disease",             DEMO_COLORS["Disease"]),
        ("Pest",                DEMO_COLORS["Pest"]),
    ]
    padding, row_h = 8, 20
    legend_h = padding * 2 + len(entries) * row_h
    legend_w = 130
    lx, ly = w - legend_w - 10, h - legend_h - 10
    overlay = frame.copy()
    cv2.rectangle(overlay, (lx, ly), (lx + legend_w, ly + legend_h), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.6, frame, 0.4, 0, frame)
    for i, (name, color) in enumerate(entries):
        iy = ly + padding + i * row_h
        cv2.rectangle(frame, (lx + padding, iy + 2), (lx + padding + 14, iy + 14), color, -1)
        cv2.putText(frame, name, (lx + padding + 20, iy + 13),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)
    return frame


# ---------------------------------------------------------------------------
# Video processing
# ---------------------------------------------------------------------------

def process_video(
    video_path,
    model_path,
    output_path=None,
    show=True,
    grid_rows=5,
    grid_cols=7,
    min_conf=0.3,
    skip_frames=0,
    box_scale=0.85,
):
    print("=" * 60)
    print("UAV CROP HEALTH DETECTION" + (" [DEMO MODE]" if DEMO_MODE else ""))
    print("=" * 60)
    print(f"Video:      {video_path}")
    print(f"Model:      {model_path}")
    print(f"Grid:       {grid_rows}x{grid_cols}  imgsz={INFERENCE_SIZE}")
    print(f"Min Conf:   {min_conf}")
    print(f"Demo Mode:  {DEMO_MODE}")
    print(f"Output:     {output_path}")
    print("=" * 60)

    classifier = YOLO(model_path)

    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        print(f"Error: Cannot open video {video_path}")
        return

    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

    print(f"\nVideo: {width}x{height} @ {fps:.2f} fps  ({total_frames} frames)")

    writer = None
    if output_path:
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        # Try H.264 first (better quality, no block artifacts), fall back to mp4v
        fourcc = cv2.VideoWriter_fourcc(*"avc1")
        writer = cv2.VideoWriter(str(output_path), fourcc, fps, (width, height))
        if not writer.isOpened():
            fourcc = cv2.VideoWriter_fourcc(*"mp4v")
            writer = cv2.VideoWriter(str(output_path), fourcc, fps, (width, height))

    frame_idx = 0
    processed_frames = 0
    start_time = time.time()
    total_class_counts: dict = {}

    print("\nProcessing… (Press 'q' to stop)")

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        frame_idx += 1
        if skip_frames > 0 and (frame_idx % (skip_frames + 1)) != 0:
            continue

        # --- Classify grid cells ---
        detections = create_grid_detections(frame, grid_rows, grid_cols, classifier)

        # --- Merge adjacent same-class cells, then NMS (DEMO_MODE) ---
        if DEMO_MODE:
            merged = merge_grid_to_regions(detections, width, height, grid_rows, grid_cols)
            render_dets = apply_nms(merged, iou_threshold=0.45)
        else:
            render_dets = detections

        for det in render_dets:
            cls = det["class"]
            total_class_counts[cls] = total_class_counts.get(cls, 0) + 1

        # --- Render ---
        annotated = draw_detections(frame, render_dets, min_conf=min_conf, box_scale=box_scale)
        annotated = add_statistics_overlay(annotated, render_dets)
        if DEMO_MODE:
            annotated = add_legend(annotated)

        cv2.putText(annotated, f"Frame {frame_idx}/{total_frames}",
                    (width - 220, height - 20), cv2.FONT_HERSHEY_SIMPLEX, 0.5,
                    (255, 255, 255), 1, cv2.LINE_AA)

        if writer:
            writer.write(annotated)

        if show:
            cv2.imshow("UAV Crop Health Detection", annotated)
            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), 27):
                print("\nStopped by user")
                break

        processed_frames += 1
        if processed_frames % 30 == 0:
            elapsed = time.time() - start_time
            print(f"Processed {processed_frames} frames ({processed_frames/elapsed:.1f} FPS)")

    cap.release()
    if writer:
        writer.release()
    if show:
        cv2.destroyAllWindows()

    elapsed = time.time() - start_time
    total_det = sum(total_class_counts.values())
    print("\n" + "=" * 60)
    print("PROCESSING COMPLETE")
    print(f"Frames: {processed_frames}/{total_frames}  Time: {elapsed:.1f}s  "
          f"Avg FPS: {processed_frames/max(elapsed,0.001):.1f}")
    if total_det > 0:
        for cls in sorted(total_class_counts, key=lambda c: total_class_counts[c], reverse=True):
            pct = total_class_counts[cls] / total_det * 100
            print(f"  {cls:20s} {total_class_counts[cls]:5d} ({pct:5.1f}%)")
    if output_path:
        print(f"Output: {output_path}")
        # Re-encode to H.264 via ffmpeg if available (eliminates mp4v block artifacts)
        import shutil, subprocess
        if shutil.which("ffmpeg"):
            h264_path = str(output_path).replace(".mp4", "_h264.mp4")
            ret = subprocess.run(
                ["ffmpeg", "-y", "-i", str(output_path),
                 "-c:v", "libx264", "-crf", "20", "-preset", "fast", h264_path],
                capture_output=True
            )
            if ret.returncode == 0:
                import os; os.replace(h264_path, str(output_path))
                print(f"Re-encoded to H.264: {output_path}")
    print("=" * 60)


def main():
    parser = argparse.ArgumentParser(description="UAV video detection with bounding boxes")
    parser.add_argument("video", type=str)
    parser.add_argument("--model", type=str,
                        default="runs/classify/health_train_v3-20260621/weights/best.pt")
    parser.add_argument("--output", type=str, default=None)
    parser.add_argument("--show", action="store_true")
    parser.add_argument("--grid-rows", type=int, default=5)
    parser.add_argument("--grid-cols", type=int, default=7)
    parser.add_argument("--min-conf", type=float, default=0.3)
    parser.add_argument("--skip-frames", type=int, default=0)
    parser.add_argument("--box-scale", type=float, default=0.85)
    parser.add_argument("--demo", action="store_true",
                        help="Force demo mode regardless of MOONHARVEST_DEMO env var")
    args = parser.parse_args()

    if args.demo:
        global DEMO_MODE
        DEMO_MODE = True

    if args.output is None and not args.show:
        args.output = "runs/detection_output/" + Path(args.video).stem + "_detected.mp4"

    process_video(
        video_path=args.video,
        model_path=args.model,
        output_path=args.output,
        show=args.show,
        grid_rows=args.grid_rows,
        grid_cols=args.grid_cols,
        min_conf=args.min_conf,
        skip_frames=args.skip_frames,
        box_scale=args.box_scale,
    )


if __name__ == "__main__":
    main()
