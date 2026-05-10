# Android Camera + ONNX Readiness

This checklist documents the cross-platform requirements from `PIGEON_HARVEST_FULL_IMPLEMENTATION_PLAN.md`.

## Current Desktop Path

- Desktop camera uses `PythonCameraService` + `.venv-camera` + `camera_service.py` for reliable OpenCV device streaming.
- YOLO uses the lightweight `Assets/models/yolov8n.onnx` baseline and class file `classes-yolov8n-coco.txt`.
- Crop-analysis fallback remains available through OpenCV/HSV vegetation analysis.

## Android Target Requirements

1. Replace `PythonCameraService` with Android-native `ICameraService` implementation.
2. Use Android CameraX or platform camera APIs to emit JPEG byte frames into `ICameraService.FrameReceived`.
3. Copy ONNX files as Android assets:
   - `Assets/models/yolov8n.onnx`
   - `Assets/models/classes-yolov8n-coco.txt`
   - optional `Assets/models/yolov8n-agri.onnx`
4. Keep frame throttling enabled before YOLO inference to prevent thermal throttling.
5. Use low input size (640x640) and minimum confidence 0.35 for `yolov8n`.

## Acceptance Criteria

- Camera emits frames through `ICameraService.FrameReceived` on Android.
- Dashboard receives frames and updates `DashboardVideoStream`.
- YOLO option activates `HarvestFunctionalService.IsYoloOptionEnabled`.
- If ONNX inference fails, HSV fallback keeps dashboard/report flow operational.
