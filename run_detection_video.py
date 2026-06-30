#!/usr/bin/env python3
"""
UAV Video Detection — ONNX Runtime + NMS
Pipeline: Sliding window multi-scale → ONNX batch inference → per-class NMS → draw boxes.
"""

import argparse
import os
import cv2
import numpy as np
import time
from pathlib import Path

# ---------------------------------------------------------------------------
# ONNX Runtime classifier — 5-6x faster than PyTorch on CPU
# ---------------------------------------------------------------------------

class ONNXClassifier:
    """Drop-in ONNX replacement for YOLO classify inference."""

    def __init__(self, onnx_path, class_names=None):
        import onnxruntime as ort
        self.session = ort.InferenceSession(
            str(onnx_path), providers=["CPUExecutionProvider"]
        )
        self.input_name = self.session.get_inputs()[0].name
        # Read class names from ONNX metadata (YOLOv8 stores them there)
        meta = self.session.get_modelmeta().custom_metadata_map
        if "names" in meta:
            import ast
            raw = ast.literal_eval(meta["names"])
            self.names = raw if isinstance(raw, dict) else {i: v for i, v in enumerate(raw)}
        else:
            self.names = class_names or {}

    def _preprocess(self, crops, sz):
        batch = []
        for c in crops:
            img = cv2.resize(c, (sz, sz)).astype(np.float32) / 255.0
            batch.append(img.transpose(2, 0, 1))          # HWC → CHW
        return np.stack(batch)                             # [N, 3, H, W]

    def infer_batch(self, crops, imgsz=160):
        """Run batch inference; return list of (class_id, confidence, class_name)."""
        batch = self._preprocess(crops, imgsz)
        outputs = self.session.run(None, {self.input_name: batch})
        probs = outputs[0]                                 # [N, num_classes]
        results = []
        for row in probs:
            cid = int(np.argmax(row))
            conf = float(row[cid])
            results.append((cid, conf, self.names.get(cid, str(cid))))
        return results

# ---------------------------------------------------------------------------
# DEMO_MODE flag — all proposal-alignment transforms are gated on this
# ---------------------------------------------------------------------------
DEMO_MODE = os.environ.get("MOONHARVEST_DEMO", "1") == "1"

# v3 model class names → 4-class proposal display labels
# disease  → Drought/Severe Stress (discoloration anomaly, tidak diklaim penyakit)
# pest     → Inconsistent Growth   (damage anomaly, tidak diklaim hama)
# well_irrigated → Lush Green      (healthy alias)
DISPLAY_MAP = {
    "lush_green":          "Lush Green",
    "well_irrigated":      "Lush Green",
    "inconsistent_growth": "Inconsistent Growth",
    "soil_issues":         "Bare Soil / Gap",
    "disease":             "Drought / Severe Stress",
    "pest":                "Inconsistent Growth",
}

HIDDEN_CLASSES: set = set()  # semua kelas dimapping, tidak ada yang disembunyikan

# Demo colors keyed by 4-class display label (BGR) — match moonharvest_detect.py DEMO_PALETTE
DEMO_COLORS = {
    "Lush Green":              ( 50, 205,  50),
    "Inconsistent Growth":     (  0, 200, 255),
    "Drought / Severe Stress": (  0, 100, 255),
    "Bare Soil / Gap":         (120, 120, 120),
}

# Internal colors (non-demo path)
INTERNAL_COLORS = {
    "lush_green":          ( 50, 205,  50),
    "well_irrigated":      ( 50, 205,  50),
    "inconsistent_growth": (  0, 200, 255),
    "soil_issues":         (120, 120, 120),
    "disease":             (  0, 100, 255),
    "pest":                (  0, 200, 255),
}

INFERENCE_SIZE = 160  # imgsz — optimized for CPU real-time

# Minimum region area as fraction of frame — smaller blobs discarded
MIN_REGION_AREA_FRAC = 0.010   # ~1.0% of frame
MAX_REGIONS = 18               # cap per frame to control inference time

# HSV-ONNX compatibility matrix.
# Physics-based HSV zone constrains which ONNX labels are plausible.
# A green pixel (high H, high S, positive ExG) CANNOT be disease/soil regardless
# of what the ONNX model says (it was trained on ground-level images, not UAV).
_HSV_ONNX_COMPAT = {
    "lush_green":    {"lush_green", "well_irrigated"},
    "stressed_crop": {"lush_green", "well_irrigated", "inconsistent_growth", "pest"},
    "drought_stress":{"inconsistent_growth", "pest", "soil_issues"},
    "soil_issues":   {"soil_issues"},
}

# Minimum ONNX confidence to override HSV zone's implied class
_ONNX_OVERRIDE_CONF = 0.60   # below this → trust HSV label


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
# HSV region extractor — tight bounding boxes from connected components
# ---------------------------------------------------------------------------

def _gray_world_wb(bgr):
    """Gray-world white balance — konsisten dengan moonharvest_detect.py."""
    b, g, r = cv2.split(bgr.astype(np.float32))
    mb, mg, mr = b.mean()+1e-6, g.mean()+1e-6, r.mean()+1e-6
    k = (mb + mg + mr) / 3.0
    b = np.clip(b*(k/mb), 0, 255)
    g = np.clip(g*(k/mg), 0, 255)
    r = np.clip(r*(k/mr), 0, 255)
    return cv2.merge([b, g, r]).astype(np.uint8)


def _hsv_segment_regions(frame, min_area_frac=MIN_REGION_AREA_FRAC, morph_k=9):
    """
    Pre-process (WB + CLAHE) → HSV pixel classification → connected components.
    Thresholds calibrated from YDXJ pixel analysis and moonharvest_detect tuning.
    """
    h, w = frame.shape[:2]
    scale = 0.5
    small = cv2.resize(frame, (int(w * scale), int(h * scale)))
    sh, sw = small.shape[:2]
    min_area_px = max(50, int(min_area_frac * sh * sw))

    # Pre-processing: white balance + CLAHE (meningkatkan pemisahan kelas +4% F1, Jintasuttisak 2025)
    small = _gray_world_wb(small)
    hsv_pre = cv2.cvtColor(small, cv2.COLOR_BGR2HSV)
    h_ch, s_ch, v_ch = cv2.split(hsv_pre)
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    v_ch  = clahe.apply(v_ch)
    small_hsv = cv2.merge([h_ch, s_ch, v_ch])

    hsv = small_hsv
    H, S, V = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    f = small.astype(np.float32)
    exg = (2*f[:,:,1] - f[:,:,0] - f[:,:,2]) / (f.sum(axis=2) + 1e-6)

    # Piksel sangat gelap (bayangan/shadow) → jangan diklasifikasi sebagai apapun
    shadow = V < 35

    # 4-zone HSV — diselaraskan dengan tuning moonharvest_detect.py
    # ExG thresholds: veg_thr=0.015 (any vegetation), healthy_min=0.038 (lush)
    lush    = (H >= 20) & (H <= 100) & (S >= 18) & (V >= 45)  & (exg > 0.038) & ~shadow
    stress  = (H >= 8)  & (H <= 100) & (S >= 8)  & (S <= 75)  & (V >= 45) & (exg > 0.008) & ~lush & ~shadow
    drought = (H <= 45) & (S >= 8)   & (S <= 50)  & (V >= 90)  & (exg <= 0.012) & ~lush & ~stress & ~shadow
    soil    = (S <= 12) & (V >= 85)  & ~lush & ~stress & ~drought & ~shadow

    zones = [
        (lush,    "lush_green",    "Lush Green"),
        (stress,  "stressed_crop", "Inconsistent Growth"),
        (drought, "drought_stress","Drought / Severe Stress"),
        (soil,    "soil_issues",   "Bare Soil / Gap"),
    ]

    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (morph_k, morph_k))
    regions = []
    for mask_bool, raw_cls, hsv_display in zones:
        mask = mask_bool.astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        n, _, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
        for i in range(1, n):
            area = int(stats[i, cv2.CC_STAT_AREA])
            if area < min_area_px:
                continue
            # Scale bbox back to original frame
            x = int(stats[i, cv2.CC_STAT_LEFT]  / scale)
            y = int(stats[i, cv2.CC_STAT_TOP]   / scale)
            bw = int(stats[i, cv2.CC_STAT_WIDTH] / scale)
            bh = int(stats[i, cv2.CC_STAT_HEIGHT]/ scale)
            x2 = min(x + bw, w - 1)
            y2 = min(y + bh, h - 1)
            if x2 <= x or y2 <= y:
                continue
            regions.append({
                "bbox_xyxy": (x, y, x2, y2),
                "hsv_class": raw_cls,
                "hsv_display": hsv_display,
                "area": area,
            })

    # Largest regions first, cap to MAX_REGIONS
    regions.sort(key=lambda r: -r["area"])
    return regions[:MAX_REGIONS]


def create_hsv_yolo_detections(frame, classifier, min_conf=0.25):
    """
    HSV connected-component regions → ONNX classify each region → HSV-ONNX fusion.

    Fusion rules:
    1. HSV zone defines which ONNX classes are physically plausible (_HSV_ONNX_COMPAT).
       A green pixel cannot be 'disease' — HSV wins over model bias.
    2. If ONNX predicts a compatible class with confidence >= _ONNX_OVERRIDE_CONF → use ONNX.
    3. Otherwise → trust HSV zone label directly.
    This corrects the domain mismatch: ONNX trained on ground-level images, inference on UAV.
    """
    regions = _hsv_segment_regions(frame)
    if not regions:
        return []

    crops = []
    for r in regions:
        x1, y1, x2, y2 = r["bbox_xyxy"]
        crop = frame[y1:y2, x1:x2]
        crops.append(crop if crop.size > 0 else np.zeros((8, 8, 3), np.uint8))

    detections = []
    if classifier and crops:
        infer_results = classifier.infer_batch(crops, imgsz=INFERENCE_SIZE)
        for i, (class_id, confidence, raw_cls) in enumerate(infer_results):
            r = regions[i]
            hsv_zone  = r["hsv_class"]
            compat    = _HSV_ONNX_COMPAT.get(hsv_zone, set())

            # ONNX label accepted only when: compatible with HSV zone AND confidence high enough
            if raw_cls in compat and confidence >= _ONNX_OVERRIDE_CONF and DISPLAY_MAP.get(raw_cls):
                display_cls = DISPLAY_MAP[raw_cls]
                final_conf  = confidence
            else:
                # Fall back to HSV zone label — physics-based, reliable for UAV color
                display_cls = r["hsv_display"]
                final_conf  = max(confidence, 0.55)

            x1, y1, x2, y2 = r["bbox_xyxy"]
            detections.append({
                "bbox":      (x1, y1, x2, y2),
                "class":     display_cls,
                "confidence": final_conf,
                "class_id":  class_id,
                "cell_count": 1,
            })
    else:
        for r in regions:
            x1, y1, x2, y2 = r["bbox_xyxy"]
            detections.append({
                "bbox":      (x1, y1, x2, y2),
                "class":     r["hsv_display"],
                "confidence": 0.75,
                "class_id":  0,
                "cell_count": 1,
            })

    return detections


# ---------------------------------------------------------------------------
# Sliding-window detection — ONNX batch inference, non-grid-aligned
# ---------------------------------------------------------------------------

def _collect_windows(frame):
    """Generate all sliding window crops and their bounding boxes."""
    h, w = frame.shape[:2]
    cells, bboxes = [], []
    for wh_f, ww_f, sh_f, sw_f in SLIDING_CONFIGS:
        win_h = int(h * wh_f)
        win_w = int(w * ww_f)
        stride_h = max(1, int(h * sh_f))
        stride_w = max(1, int(w * sw_f))
        y = 0
        while y + win_h <= h:
            x = 0
            while x + win_w <= w:
                cells.append(frame[y:y + win_h, x:x + win_w])
                bboxes.append((x, y, x + win_w, y + win_h))
                x += stride_w
            y += stride_h
    return cells, bboxes


def create_sliding_detections(frame, classifier, min_conf=0.25):
    """Multi-scale sliding window + ONNX batch inference → raw detections."""
    cells, bboxes = _collect_windows(frame)
    if not cells or not classifier:
        return []

    # ONNX batch inference — one forward pass for all windows
    infer_results = classifier.infer_batch(cells, imgsz=INFERENCE_SIZE)

    detections = []
    for i, (class_id, confidence, raw_cls) in enumerate(infer_results):
        if confidence < min_conf:
            continue
        if raw_cls in HIDDEN_CLASSES:
            continue
        mapped = DISPLAY_MAP.get(raw_cls)
        if mapped is None:
            continue
        x1, y1, x2, y2 = bboxes[i]
        detections.append({
            "bbox": (x1, y1, x2, y2),
            "class": mapped,
            "confidence": confidence,
            "class_id": class_id,
            "cell_count": 1,
        })
    return detections


# ---------------------------------------------------------------------------
# NMS — per-class + optional cross-class suppression
# ---------------------------------------------------------------------------

def apply_nms_per_class(detections, iou_threshold=0.08, score_threshold=0.25):
    """Per-class NMS: merge adjacent same-class windows into one best box."""
    if not detections:
        return []
    by_class = {}
    for d in detections:
        by_class.setdefault(d["class"], []).append(d)

    result = []
    for cls_dets in by_class.values():
        if len(cls_dets) == 1:
            result.extend(cls_dets)
            continue
        boxes = [[int(d["bbox"][0]), int(d["bbox"][1]),
                  int(d["bbox"][2] - d["bbox"][0]), int(d["bbox"][3] - d["bbox"][1])]
                 for d in cls_dets]
        scores = [float(d["confidence"]) for d in cls_dets]
        idx = cv2.dnn.NMSBoxes(boxes, scores, score_threshold, iou_threshold)
        if len(idx) > 0:
            flat = idx.flatten() if hasattr(idx, "flatten") else list(idx)
            result.extend(cls_dets[i] for i in flat)
    return result


def apply_nms_cross_class(detections, iou_threshold=0.60):
    """Cross-class NMS: suppress lower-confidence box when two classes heavily overlap."""
    if len(detections) <= 1:
        return detections
    boxes  = [[int(d["bbox"][0]), int(d["bbox"][1]),
               int(d["bbox"][2]-d["bbox"][0]), int(d["bbox"][3]-d["bbox"][1])]
              for d in detections]
    scores = [float(d["confidence"]) for d in detections]
    idx = cv2.dnn.NMSBoxes(boxes, scores, 0.0, iou_threshold)
    if len(idx) == 0:
        return []
    flat = idx.flatten() if hasattr(idx, "flatten") else list(idx)
    return [detections[i] for i in flat]


def run_nms(detections, per_class_iou=0.08, cross_iou=0.60, score_thr=0.25):
    """Full NMS pipeline: per-class merge → cross-class dedup."""
    dets = apply_nms_per_class(detections, iou_threshold=per_class_iou,
                               score_threshold=score_thr)
    dets = apply_nms_cross_class(dets, iou_threshold=cross_iou)
    return dets


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

def apply_nms(detections, iou_threshold=0.45, min_cells=1):
    """IoU-based NMS on merged regional bounding boxes."""
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
    """Draw detection boxes on original frame — border only, no fill overlay."""
    annotated = frame.copy()
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
        else:
            sx1, sy1, sx2, sy2 = map(int, scale_box((x1, y1, x2, y2), box_scale, width, height))
            color = INTERNAL_COLORS.get(class_name, (255, 255, 255))

        label = f"{class_name} {confidence:.2f}"

        # Box border only — no fill
        cv2.rectangle(annotated, (sx1, sy1), (sx2, sy2), color, 2)

        # Label background + text
        (label_w, label_h), _ = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.55, 2)
        cv2.rectangle(annotated, (sx1, sy1 - label_h - 8), (sx1 + label_w + 8, sy1), color, -1)
        cv2.putText(annotated, label, (sx1 + 4, sy1 - 4),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2, cv2.LINE_AA)

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
        ("Lush Green",              DEMO_COLORS["Lush Green"]),
        ("Inconsistent Growth",     DEMO_COLORS["Inconsistent Growth"]),
        ("Drought / Severe Stress", DEMO_COLORS["Drought / Severe Stress"]),
        ("Bare Soil / Gap",         DEMO_COLORS["Bare Soil / Gap"]),
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
    grid_rows=3,
    grid_cols=5,
    min_conf=0.3,
    skip_frames=0,
    box_scale=0.85,
    output_scale=0.5,
    mode="sliding",
):
    print("=" * 60)
    print("UAV CROP HEALTH DETECTION" + (" [DEMO MODE]" if DEMO_MODE else ""))
    print("=" * 60)
    print(f"Video:      {video_path}")
    print(f"Model:      {model_path}")
    print(f"Mode:       {mode}  imgsz={INFERENCE_SIZE}")
    if mode == "grid":
        print(f"Grid:       {grid_rows}x{grid_cols}")
    print(f"Min Conf:   {min_conf}")
    print(f"Demo Mode:  {DEMO_MODE}")
    print(f"Output:     {output_path}  (scale={output_scale})")
    print("=" * 60)

    # Load ONNX classifier (try .onnx sidecar first, fall back to PyTorch wrapper)
    onnx_path = Path(model_path).with_suffix(".onnx")
    if onnx_path.exists():
        print(f"[ONNX] Loading {onnx_path.name}")
        classifier = ONNXClassifier(onnx_path)
    else:
        print("[WARN] ONNX model not found, falling back to PyTorch")
        from ultralytics import YOLO as _YOLO

        class _PyTorchWrapper:
            def __init__(self, path):
                self._m = _YOLO(path)
                self.names = self._m.names
            def infer_batch(self, crops, imgsz=160):
                res = self._m(crops, verbose=False, imgsz=imgsz)
                out = []
                for r in res:
                    cid = int(r.probs.top1)
                    out.append((cid, float(r.probs.top1conf), self.names[cid]))
                return out
        classifier = _PyTorchWrapper(model_path)

    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        print(f"Error: Cannot open video {video_path}")
        return

    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

    out_w = int(width * output_scale)
    out_h = int(height * output_scale)
    print(f"\nVideo: {width}x{height} @ {fps:.2f} fps  ({total_frames} frames)")
    if output_scale != 1.0:
        print(f"Output res: {out_w}x{out_h}")

    writer = None
    avi_path = None
    if output_path:
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        # Write MJPEG .avi first (fast intra-frame encode), then convert to H.264 mp4
        avi_path = output_path.with_suffix(".avi")
        fourcc = cv2.VideoWriter_fourcc(*"MJPG")
        writer = cv2.VideoWriter(str(avi_path), fourcc, fps, (out_w, out_h))
        if not writer.isOpened():
            fourcc = cv2.VideoWriter_fourcc(*"mp4v")
            writer = cv2.VideoWriter(str(output_path), fourcc, fps, (out_w, out_h))
            avi_path = None

    frame_idx = 0
    processed_frames = 0
    start_time = time.time()
    total_class_counts: dict = {}
    cached_render_dets = []   # last inference result — reused on skipped frames
    infer_every = max(1, skip_frames + 1)  # run inference every N frames

    print("\nProcessing… (Press 'q' to stop)")

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        frame_idx += 1

        # Run inference on frame 1 and every infer_every frames after
        if frame_idx == 1 or frame_idx % infer_every == 0:
            # HSV connected-component regions → ONNX classify → NMS
            detections = create_hsv_yolo_detections(frame, classifier, min_conf=min_conf)
            cached_render_dets = run_nms(detections,
                                         per_class_iou=0.30,
                                         cross_iou=0.60,
                                         score_thr=min_conf)

            for det in cached_render_dets:
                cls = det["class"]
                total_class_counts[cls] = total_class_counts.get(cls, 0) + 1

        render_dets = cached_render_dets

        # --- Render ---
        annotated = draw_detections(frame, render_dets, min_conf=min_conf, box_scale=box_scale)
        annotated = add_statistics_overlay(annotated, render_dets)
        if DEMO_MODE:
            annotated = add_legend(annotated)

        elapsed_now = time.time() - start_time
        display_fps = frame_idx / max(elapsed_now, 0.001)
        cv2.putText(annotated, f"Frame {frame_idx}/{total_frames}  {display_fps:.1f} FPS",
                    (width - 300, height - 20), cv2.FONT_HERSHEY_SIMPLEX, 0.5,
                    (255, 255, 255), 1, cv2.LINE_AA)

        if writer:
            write_frame = cv2.resize(annotated, (out_w, out_h)) if output_scale != 1.0 else annotated
            writer.write(write_frame)

        if show:
            cv2.imshow("UAV Crop Health Detection", annotated)
            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), 27):
                print("\nStopped by user")
                break

        processed_frames += 1
        if processed_frames % 60 == 0:
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
        import shutil, subprocess
        src = avi_path if (avi_path and avi_path.exists()) else output_path
        if shutil.which("ffmpeg") and src:
            ret = subprocess.run(
                ["ffmpeg", "-y", "-i", str(src),
                 "-c:v", "libx264", "-crf", "20", "-preset", "fast", str(output_path)],
                capture_output=True
            )
            if ret.returncode == 0:
                if avi_path and avi_path.exists():
                    avi_path.unlink()
                print(f"Output: {output_path}")
            else:
                print(f"Output (raw): {src}")
        else:
            print(f"Output: {src}")
    print("=" * 60)


def main():
    parser = argparse.ArgumentParser(description="UAV video detection with bounding boxes")
    parser.add_argument("video", type=str)
    parser.add_argument("--model", type=str,
                        default="runs/classify/health_train_v3-20260621/weights/best.pt")
    parser.add_argument("--output", type=str, default=None)
    parser.add_argument("--show", action="store_true")
    parser.add_argument("--grid-rows", type=int, default=3)
    parser.add_argument("--grid-cols", type=int, default=5)
    parser.add_argument("--min-conf", type=float, default=0.3)
    parser.add_argument("--skip-frames", type=int, default=0)
    parser.add_argument("--box-scale", type=float, default=0.85)
    parser.add_argument("--output-scale", type=float, default=0.5,
                        help="Resize output video (0.5=half res, faster write)")
    parser.add_argument("--mode", type=str, default="sliding",
                        choices=["sliding", "grid"],
                        help="sliding: YOLO-detect style (default); grid: fixed grid")
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
        mode=args.mode,
        box_scale=args.box_scale,
        output_scale=args.output_scale,
    )


if __name__ == "__main__":
    main()
