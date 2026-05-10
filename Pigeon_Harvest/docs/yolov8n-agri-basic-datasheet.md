# YOLOv8n Agriculture Basic Datasheet

## Runtime Target

- **Model family:** YOLOv8n / YOLOv8 nano
- **Reason:** smallest YOLOv8 variant; best fit for edge/laptop GCS usage and low-latency UAV frame analysis.
- **Input size:** 640 × 640 RGB
- **Model format:** ONNX (`yolov8n-agri.onnx` preferred, fallback `yolov8n.onnx`)
- **Pre/post-processing:** OpenCV (`OpenCvSharp`) image loading, resizing, color conversion, drawing, NMS support path
- **Default confidence:** 0.35
- **Default NMS:** 0.45
- **Execution behavior:** CPU-first safe fallback; GPU/CUDA can be added when runtime supports it.

## Required Files

Place these files in `Pigeon_Uno/Assets/models/` or beside the runtime executable:

1. `yolov8n-agri.onnx` + `classes-yolov8n-agri-basic.txt` for agriculture-specific detection, or
2. `yolov8n.onnx` + `classes-yolov8n-coco.txt` for the included lightweight COCO baseline model.

If `yolov8n-agri.onnx` is not available, the app searches for:

1. `yolov8n.onnx` (included baseline COCO model)
2. `moonharvest-v2.onnx`

When no ONNX model is found, the app still runs with HSV/OpenCV vegetation analysis fallback.

## Included ONNX Model

The project includes `Pigeon_Uno/Assets/models/yolov8n.onnx`, a lightweight YOLOv8 nano baseline model. It uses COCO labels (`classes-yolov8n-coco.txt`) and can detect general objects such as person, car, truck, bird, cow, bottle, chair, potted plant, etc. For agriculture-specific classes such as `crop_stress`, `dry_soil`, and `water_stress`, train/export `yolov8n-agri.onnx` with the agriculture class file.

## Basic Detection Classes

| ID | Class | Purpose |
|---:|---|---|
| 0 | `healthy_crop` | Normal healthy vegetation |
| 1 | `crop_stress` | Early crop stress symptoms |
| 2 | `dry_soil` | Dry soil / low moisture surface |
| 3 | `water_stress` | Water deficit indicators |
| 4 | `pest_damage` | Visible pest damage |
| 5 | `weed` | Weed patches |
| 6 | `disease` | Leaf/plant disease symptoms |
| 7 | `yellowing` | Yellow leaves / chlorosis |
| 8 | `wilting` | Wilted vegetation |
| 9 | `bare_soil` | Bare exposed soil |
| 10 | `irrigation_channel` | Irrigation channel/path |
| 11 | `standing_water` | Standing water / wet area |

## Minimum Dataset Recommendation

For a basic usable model, collect at least:

- 100–200 labeled images per class for prototype testing
- 500+ labeled images per class for more stable field use
- Drone altitude variations: low, medium, high
- Lighting variations: morning, noon, cloudy, late afternoon
- Label format: YOLO bounding box labels

## Acceptance Criteria

- Pressing **Yolo Option** in the sidebar changes state to active.
- Crop Analysis uses YOLO mode when the ONNX model exists.
- If no model exists, Crop Analysis falls back to OpenCV HSV vegetation analysis instead of crashing.
- Reports can export analysis/report results.
