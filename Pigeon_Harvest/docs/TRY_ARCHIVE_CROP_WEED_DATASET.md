# Cara Mencoba Dataset `/home/fawwazfa/Program/datasheet/archive`

Dataset ini sudah cocok untuk YOLOv8n, tetapi class-nya hanya:

```text
0 crop
1 weed
```

Jadi dataset ini cocok untuk bagian proposal:

- `healthy_crop` / tanaman
- `weed` / gulma
- demo live YOLO bounding box

Dataset ini belum cukup untuk:

- `disease`
- `dry_soil`
- `water_stress`
- `soil_crack`
- `standing_water`

## Status Dataset

Saya sudah konversi dataset flat dari:

```text
/home/fawwazfa/Program/datasheet/archive/agri_data/data
```

menjadi struktur YOLOv8 di:

```text
/home/fawwazfa/Program/datasheet/yolo_crop_weed
```

Hasil split:

| Split | Images | Boxes |
| --- | ---: | ---: |
| train | 1040 | 1619 |
| val | 195 | 325 |
| test | 65 | 128 |

File training:

```text
/home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml
```

## Training Cepat

Dari root project:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
```

Training cepat 30 epoch CPU:

```bash
IMGSZ=416 EPOCHS=30 BATCH=4 DEVICE=cpu \
scripts/train_yolov8n_vision.sh /home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml crop_weed_demo_416
```

Kalau ada GPU NVIDIA/CUDA, coba:

```bash
IMGSZ=416 EPOCHS=100 BATCH=8 \
scripts/train_yolov8n_vision.sh /home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml crop_weed_416
```

## Hasil Training

Model PyTorch:

```text
runs/moonharvest/crop_weed_demo_416/weights/best.pt
```

Model ONNX:

```text
runs/moonharvest/crop_weed_demo_416/weights/best.onnx
```

## Coba Predict

```bash
source .venv-yolo/bin/activate
yolo detect predict \
  model=runs/moonharvest/crop_weed_demo_416/weights/best.pt \
  source=/home/fawwazfa/Program/datasheet/yolo_crop_weed/test/images \
  imgsz=416 \
  conf=0.35 \
  save=True
```

Hasil gambar prediksi biasanya muncul di:

```text
runs/detect/predict
```

## Pakai Di Harvestmoon GCS

Copy ONNX:

```bash
cp runs/moonharvest/crop_weed_demo_416/weights/best.onnx \
  HarvestmoonGCS/Assets/models/yolov8n-crop-weed-416.onnx
```

Buat class file:

```bash
cat > HarvestmoonGCS/Assets/models/classes-crop-weed.txt <<'EOF'
crop
weed
EOF
```

Jalankan app dengan model ini:

```bash
HARVESTMOON_YOLO_MODEL=HarvestmoonGCS/Assets/models/yolov8n-crop-weed-416.onnx \
HARVESTMOON_YOLO_CLASSES=HarvestmoonGCS/Assets/models/classes-crop-weed.txt \
scripts/run_desktop_trial.sh
```

## Kesimpulan

Untuk proposal kamu, dataset ini **cocok sebagai demo YOLO agriculture awal**, terutama untuk mendeteksi tanaman dan gulma. Untuk vision yang penuh sesuai proposal, nanti perlu tambah dataset penyakit, kekeringan, soil crack, dan water stress.
