# Trial YOLO Dari File Video

Gunakan ini kalau ingin mencoba model vision memakai video, bukan kamera live.

## Model Default

Script default memakai model crop/weed yang sudah dibuat:

```text
HarvestmoonGCS/Assets/models/yolov8n-crop-weed-416.onnx
```

Class:

```text
crop
weed
```

## Cara Jalan

Dari root project:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
source .venv-yolo/bin/activate
```

Jalankan pada video:

```bash
scripts/trial_yolo_video.py /path/ke/video-uav.mp4
```

Contoh dengan output custom:

```bash
scripts/trial_yolo_video.py /home/fawwazfa/Videos/uav-test.mp4 \
  --model HarvestmoonGCS/Assets/models/yolov8n-crop-weed-416.onnx \
  --output runs/moonharvest/video_trial/uav-test-annotated.mp4 \
  --csv runs/moonharvest/video_trial/uav-test-detections.csv \
  --imgsz 416 \
  --conf 0.35 \
  --device cpu
```

## Trial Cepat 300 Frame

```bash
scripts/trial_yolo_video.py /home/fawwazfa/Videos/uav-test.mp4 --max-frames 300
```

## Output

Script menghasilkan:

```text
runs/moonharvest/video_trial/crop_weed_trial.mp4
runs/moonharvest/video_trial/crop_weed_trial.csv
```

CSV berisi:

- nomor frame
- waktu video
- class id
- class name
- confidence
- bounding box `x1,y1,x2,y2`

## Catatan GPU

Training sudah memakai RTX 3050. Untuk trial ONNX, default `--device cpu` lebih aman karena ONNX Runtime GPU di mesin ini membutuhkan dependency CUDA tambahan seperti `libcublasLt.so.12`.

Kalau ingin mencoba PyTorch `.pt` dengan GPU:

```bash
scripts/trial_yolo_video.py /home/fawwazfa/Videos/uav-test.mp4 \
  --model runs/detect/runs/moonharvest/crop_weed_416_gpu/weights/best.pt \
  --device 0
```
