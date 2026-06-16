#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import sys
import time
from contextlib import nullcontext
from pathlib import Path

import cv2
import numpy as np


TRIAL_DIR = Path(__file__).resolve().parent


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run a YOLO model on a video file and save annotated video + detection CSV."
    )
    parser.add_argument(
        "source",
        type=Path,
        help="Input video path, for example /home/fawwazfa/Videos/uav-test.mp4",
    )
    parser.add_argument(
        "--model",
        type=Path,
        default=TRIAL_DIR / "models" / "yolov8n-crop-weed-416.onnx",
        help="YOLO .pt or .onnx model path.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("runs/moonharvest/video_trial/crop_weed_trial.mp4"),
        help="Annotated output video path.",
    )
    parser.add_argument(
        "--csv",
        type=Path,
        default=Path("runs/moonharvest/video_trial/crop_weed_trial.csv"),
        help="Detection CSV output path.",
    )
    parser.add_argument("--imgsz", type=int, default=416, help="YOLO inference image size.")
    parser.add_argument("--conf", type=float, default=0.35, help="Confidence threshold.")
    parser.add_argument("--iou", type=float, default=0.70, help="NMS IoU threshold. Higher keeps more overlapping boxes.")
    parser.add_argument("--max-det", type=int, default=300, help="Maximum detections per frame.")
    parser.add_argument(
        "--box-scale",
        type=float,
        default=1.0,
        help="Scale displayed/exported boxes around their center. Use 0.6-0.8 to make boxes smaller.",
    )
    parser.add_argument(
        "--tile-size",
        type=int,
        default=0,
        help="Run tiled inference with this tile size. 0 disables tiling.",
    )
    parser.add_argument(
        "--tile-overlap",
        type=float,
        default=0.25,
        help="Tile overlap ratio for --tile-size, from 0.0 to 0.8.",
    )
    parser.add_argument(
        "--grid-cols",
        type=int,
        default=0,
        help="Run fixed-grid inference with this many columns. Faster than sliding tiles.",
    )
    parser.add_argument(
        "--grid-rows",
        type=int,
        default=0,
        help="Run fixed-grid inference with this many rows. Faster than sliding tiles.",
    )
    parser.add_argument(
        "--detect-every",
        type=int,
        default=1,
        help="Run YOLO every N frames and reuse detections between updates.",
    )
    parser.add_argument("--device", default="cpu", help="Use cpu or 0 for CUDA when supported.")
    parser.add_argument(
        "--max-frames",
        type=int,
        default=0,
        help="Limit processed frames. 0 means process the whole video.",
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Show an OpenCV preview window while processing. Press q to stop.",
    )
    parser.add_argument(
        "--no-save-video",
        action="store_true",
        help="Skip writing annotated MP4 output to reduce CPU load.",
    )
    parser.add_argument(
        "--no-save-csv",
        action="store_true",
        help="Skip writing detection CSV output to reduce disk/CPU load.",
    )
    parser.add_argument(
        "--window-width",
        type=int,
        default=1280,
        help="Preview window width when --show is enabled.",
    )
    return parser.parse_args()


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def scale_box(xyxy: list[float], scale: float, width: int, height: int) -> list[float]:
    scale = clamp(scale, 0.05, 2.0)
    x1, y1, x2, y2 = xyxy
    cx = (x1 + x2) / 2.0
    cy = (y1 + y2) / 2.0
    box_width = (x2 - x1) * scale
    box_height = (y2 - y1) * scale
    return [
        clamp(cx - box_width / 2.0, 0, width - 1),
        clamp(cy - box_height / 2.0, 0, height - 1),
        clamp(cx + box_width / 2.0, 0, width - 1),
        clamp(cy + box_height / 2.0, 0, height - 1),
    ]


def box_iou(a: list[float], b: list[float]) -> float:
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b
    inter_x1 = max(ax1, bx1)
    inter_y1 = max(ay1, by1)
    inter_x2 = min(ax2, bx2)
    inter_y2 = min(ay2, by2)
    inter_width = max(0.0, inter_x2 - inter_x1)
    inter_height = max(0.0, inter_y2 - inter_y1)
    intersection = inter_width * inter_height
    area_a = max(0.0, ax2 - ax1) * max(0.0, ay2 - ay1)
    area_b = max(0.0, bx2 - bx1) * max(0.0, by2 - by1)
    union = area_a + area_b - intersection
    return intersection / union if union > 0 else 0.0


def nms_detections(detections: list[dict], iou_threshold: float, max_det: int) -> list[dict]:
    kept: list[dict] = []
    for detection in sorted(detections, key=lambda item: item["confidence"], reverse=True):
        overlaps_existing = any(
            detection["class_id"] == existing["class_id"]
            and box_iou(detection["xyxy"], existing["xyxy"]) > iou_threshold
            for existing in kept
        )
        if not overlaps_existing:
            kept.append(detection)
        if len(kept) >= max_det:
            break
    return kept


def detections_from_result(result, x_offset: int = 0, y_offset: int = 0) -> list[dict]:
    detections: list[dict] = []
    names = result.names
    if result.boxes is None:
        return detections

    for box in result.boxes:
        xyxy = box.xyxy[0].tolist()
        class_id = int(box.cls[0].item())
        detections.append(
            {
                "xyxy": [
                    xyxy[0] + x_offset,
                    xyxy[1] + y_offset,
                    xyxy[2] + x_offset,
                    xyxy[3] + y_offset,
                ],
                "class_id": class_id,
                "class_name": str(names.get(class_id, class_id)),
                "confidence": float(box.conf[0].item()),
            }
        )
    return detections


def predict_frame(model, frame, args) -> list[dict]:
    height, width = frame.shape[:2]
    if args.grid_cols > 0 and args.grid_rows > 0:
        detections: list[dict] = []
        grid_cols = max(1, args.grid_cols)
        grid_rows = max(1, args.grid_rows)
        for row in range(grid_rows):
            y1 = int(row * height / grid_rows)
            y2 = int((row + 1) * height / grid_rows)
            for col in range(grid_cols):
                x1 = int(col * width / grid_cols)
                x2 = int((col + 1) * width / grid_cols)
                crop = frame[y1:y2, x1:x2]
                if crop.size == 0:
                    continue
                results = model.predict(
                    crop,
                    imgsz=args.imgsz,
                    conf=args.conf,
                    iou=args.iou,
                    max_det=args.max_det,
                    device=args.device,
                    verbose=False,
                )
                detections.extend(detections_from_result(results[0], x1, y1))
        return nms_detections(detections, args.iou, args.max_det)

    if args.tile_size <= 0:
        results = model.predict(
            frame,
            imgsz=args.imgsz,
            conf=args.conf,
            iou=args.iou,
            max_det=args.max_det,
            device=args.device,
            verbose=False,
        )
        return detections_from_result(results[0])

    tile_size = max(64, args.tile_size)
    overlap = clamp(args.tile_overlap, 0.0, 0.8)
    stride = max(1, int(tile_size * (1.0 - overlap)))
    detections: list[dict] = []

    y_positions = list(range(0, max(1, height - tile_size + 1), stride))
    x_positions = list(range(0, max(1, width - tile_size + 1), stride))
    if not y_positions or y_positions[-1] != max(0, height - tile_size):
        y_positions.append(max(0, height - tile_size))
    if not x_positions or x_positions[-1] != max(0, width - tile_size):
        x_positions.append(max(0, width - tile_size))

    for y in y_positions:
        for x in x_positions:
            crop = frame[y : min(y + tile_size, height), x : min(x + tile_size, width)]
            if crop.size == 0:
                continue
            results = model.predict(
                crop,
                imgsz=args.imgsz,
                conf=args.conf,
                iou=args.iou,
                max_det=args.max_det,
                device=args.device,
                verbose=False,
            )
            detections.extend(detections_from_result(results[0], x, y))

    return nms_detections(detections, args.iou, args.max_det)


def draw_detections(frame, detections: list[dict], box_scale: float) -> np.ndarray:
    annotated = frame.copy()
    height, width = frame.shape[:2]
    palette = {
        0: (34, 197, 94),
        1: (239, 68, 68),
    }
    for detection in detections:
        xyxy = scale_box(detection["xyxy"], box_scale, width, height)
        x1, y1, x2, y2 = [int(round(value)) for value in xyxy]
        color = palette.get(detection["class_id"], (59, 130, 246))
        label = f'{detection["class_name"]} {detection["confidence"]:.2f}'
        cv2.rectangle(annotated, (x1, y1), (x2, y2), color, 2)
        label_size, baseline = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        label_y1 = max(0, y1 - label_size[1] - baseline - 6)
        label_y2 = label_y1 + label_size[1] + baseline + 6
        cv2.rectangle(annotated, (x1, label_y1), (x1 + label_size[0] + 8, label_y2), color, -1)
        cv2.putText(
            annotated,
            label,
            (x1 + 4, label_y2 - baseline - 3),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (255, 255, 255),
            1,
            cv2.LINE_AA,
        )
    return annotated


def require_ultralytics():
    try:
        from ultralytics import YOLO  # type: ignore
    except Exception as exc:
        print(
            "Ultralytics is not installed in this Python environment.\n"
            "Run:\n"
            "  cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest\n"
            "  python3 -m venv .venv-yolo\n"
            "  source .venv-yolo/bin/activate\n"
            "  pip install -U ultralytics onnx onnxruntime opencv-python\n",
            file=sys.stderr,
        )
        raise SystemExit(2) from exc
    return YOLO


def main() -> int:
    args = parse_args()
    if not args.source.is_file():
        print(f"Video not found: {args.source}", file=sys.stderr)
        return 1
    if not args.model.is_file():
        print(f"Model not found: {args.model}", file=sys.stderr)
        return 1

    if not args.no_save_video:
        args.output.parent.mkdir(parents=True, exist_ok=True)
    if not args.no_save_csv:
        args.csv.parent.mkdir(parents=True, exist_ok=True)

    YOLO = require_ultralytics()
    model = YOLO(str(args.model), task="detect")

    capture = cv2.VideoCapture(str(args.source))
    if not capture.isOpened():
        print(f"Failed to open video: {args.source}", file=sys.stderr)
        return 1

    fps = capture.get(cv2.CAP_PROP_FPS)
    if fps <= 0:
        fps = 25.0
    width = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))

    writer = None
    if not args.no_save_video:
        writer = cv2.VideoWriter(
            str(args.output),
            cv2.VideoWriter_fourcc(*"mp4v"),
            fps,
            (width, height),
        )
        if not writer.isOpened():
            capture.release()
            print(f"Failed to create output video: {args.output}", file=sys.stderr)
            return 1

    frame_index = 0
    detection_count = 0
    cached_detections: list[dict] = []
    started = time.perf_counter()
    window_name = "MoonHarvest YOLO Video Trial"

    if args.show:
        cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(window_name, max(320, args.window_width), int(max(320, args.window_width) * height / max(width, 1)))

    csv_context = nullcontext(None) if args.no_save_csv else args.csv.open("w", newline="")
    with csv_context as csv_file:
        csv_writer = None
        if csv_file is not None:
            csv_writer = csv.writer(csv_file)
            csv_writer.writerow(
                [
                    "frame",
                    "time_seconds",
                    "class_id",
                    "class_name",
                    "confidence",
                    "x1",
                    "y1",
                    "x2",
                    "y2",
                ]
            )

        while True:
            ok, frame = capture.read()
            if not ok:
                break
            frame_index += 1

            should_detect = (
                frame_index == 1
                or args.detect_every <= 1
                or (frame_index - 1) % args.detect_every == 0
            )
            if should_detect:
                cached_detections = predict_frame(model, frame, args)
            detections = cached_detections
            annotated = draw_detections(frame, detections, args.box_scale)
            if writer is not None:
                writer.write(annotated)

            if args.show:
                cv2.imshow(window_name, annotated)
                key = cv2.waitKey(1) & 0xFF
                if key == ord("q") or key == 27:
                    print("Preview stopped by user")
                    break

            if csv_writer is None:
                detection_count += len(detections)
            else:
                for detection in detections:
                    xyxy = scale_box(detection["xyxy"], args.box_scale, width, height)
                    detection_count += 1
                    csv_writer.writerow(
                        [
                            frame_index,
                            f"{frame_index / fps:.3f}",
                            detection["class_id"],
                            detection["class_name"],
                            f'{detection["confidence"]:.4f}',
                            f"{xyxy[0]:.1f}",
                            f"{xyxy[1]:.1f}",
                            f"{xyxy[2]:.1f}",
                            f"{xyxy[3]:.1f}",
                        ]
                    )

            if args.max_frames > 0 and frame_index >= args.max_frames:
                break

            if frame_index % 30 == 0:
                elapsed = max(time.perf_counter() - started, 1e-6)
                print(
                    f"Processed {frame_index} frames, "
                    f"{detection_count} detections, "
                    f"{frame_index / elapsed:.1f} FPS"
                )

    capture.release()
    if writer is not None:
        writer.release()
    if args.show:
        cv2.destroyWindow(window_name)
    elapsed = max(time.perf_counter() - started, 1e-6)

    print("Video trial complete")
    print(f"Frames:     {frame_index}")
    print(f"Detections: {detection_count}")
    print(f"Speed:      {frame_index / elapsed:.1f} FPS")
    print(f"Video:      {'skipped' if args.no_save_video else args.output}")
    print(f"CSV:        {'skipped' if args.no_save_csv else args.csv}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
