# Tutorial Training YOLOv8n Untuk Vision MoonHarvest

Tutorial ini untuk training model vision Harvestmoon GCS sesuai proposal: deteksi crop stress/pest/disease/weed/dry soil dari dataset YOLOv8, lalu export ke ONNX agar bisa dipakai oleh aplikasi.

Rekomendasi untuk dataset yang kamu tunjuk:

- Image size download: `resize-416x416`
- Annotation export: `YOLOv8`
- Base model: `yolov8n.pt`
- Export runtime: `ONNX`

## 1. Siapkan Environment Training

Dari root project:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
python3 -m venv .venv-yolo
source .venv-yolo/bin/activate
pip install -U pip
pip install -U ultralytics onnx onnxruntime
```

Cek instalasi:

```bash
yolo checks
```

## 2. Download Dataset

Dari Roboflow atau sumber dataset lain:

1. Pilih ukuran: `resize-416x416`
2. Pilih format: `YOLOv8`
3. Download dan unzip, contoh:

```text
/home/fawwazfa/Datasets/moonharvest-vision/
  train/
    images/
    labels/
  valid/
    images/
    labels/
  test/
    images/
    labels/
  data.yaml
```

Roboflow biasanya memakai folder `valid`, bukan `val`. Itu tidak masalah selama `data.yaml` menunjuk ke folder yang benar.

## 3. Cek `data.yaml`

Untuk model MVP 4 kelas yang cocok dengan `VegetationYoloAnalyzer.cs`, isi class idealnya:

```yaml
names:
  0: green_healthy
  1: yellow_stress
  2: brown_drought
  3: soil_crack
```

Untuk model proposal 12 kelas:

```yaml
names:
  0: healthy_crop
  1: crop_stress
  2: dry_soil
  3: water_stress
  4: pest_damage
  5: weed
  6: disease
  7: yellowing
  8: wilting
  9: bare_soil
  10: irrigation_channel
  11: standing_water
```

Kalau dataset dari Roboflow punya nama class berbeda, jangan langsung diubah sembarangan. Mapping class harus sama dengan label `.txt`. Kalau mau mengganti nama class, ganti hanya nama di `data.yaml` dengan urutan ID yang tetap sama.

## 4. Training Cepat Untuk Demo

Gunakan ini kalau hanya ingin model cepat untuk percobaan:

```bash
source .venv-yolo/bin/activate
yolo detect train \
  model=yolov8n.pt \
  data=/home/fawwazfa/Datasets/moonharvest-vision/data.yaml \
  imgsz=416 \
  epochs=30 \
  batch=8 \
  project=runs/moonharvest \
  name=yolov8n_demo_416
```

Jika laptop tidak kuat, pakai:

```bash
yolo detect train model=yolov8n.pt data=/home/fawwazfa/Datasets/moonharvest-vision/data.yaml imgsz=416 epochs=30 batch=4 device=cpu project=runs/moonharvest name=yolov8n_demo_416
```

## 5. Training Lebih Serius

Untuk hasil yang lebih layak:

```bash
yolo detect train \
  model=yolov8n.pt \
  data=/home/fawwazfa/Datasets/moonharvest-vision/data.yaml \
  imgsz=416 \
  epochs=100 \
  batch=8 \
  patience=25 \
  project=runs/moonharvest \
  name=yolov8n_agri_416
```

Output penting:

```text
runs/moonharvest/yolov8n_agri_416/weights/best.pt
runs/moonharvest/yolov8n_agri_416/results.png
runs/moonharvest/yolov8n_agri_416/confusion_matrix.png
```

## 6. Validasi Model

```bash
yolo detect val \
  model=runs/moonharvest/yolov8n_agri_416/weights/best.pt \
  data=/home/fawwazfa/Datasets/moonharvest-vision/data.yaml \
  imgsz=416
```

Yang dicek:

- `mAP50` makin tinggi makin baik.
- Precision tinggi berarti false detection lebih sedikit.
- Recall tinggi berarti objek penting jarang terlewat.
- Confusion matrix jangan terlalu banyak class tertukar.

Untuk demo awal, target realistis:

| Metrik | Target awal |
| --- | ---: |
| mAP50 | 0.50+ |
| Precision | 0.60+ |
| Recall | 0.50+ |

## 7. Tes Prediksi Pada Gambar/Video

Tes pada gambar:

```bash
yolo detect predict \
  model=runs/moonharvest/yolov8n_agri_416/weights/best.pt \
  source=/home/fawwazfa/Datasets/moonharvest-vision/test/images \
  imgsz=416 \
  conf=0.35 \
  save=True
```

Tes pada video:

```bash
yolo detect predict \
  model=runs/moonharvest/yolov8n_agri_416/weights/best.pt \
  source=/path/video-uav.mp4 \
  imgsz=416 \
  conf=0.35 \
  save=True
```

Kalau hasil bounding box sudah masuk akal, lanjut export ONNX.

## 8. Export Ke ONNX

Untuk desktop:

```bash
yolo export \
  model=runs/moonharvest/yolov8n_agri_416/weights/best.pt \
  format=onnx \
  imgsz=416 \
  opset=12 \
  simplify=True
```

Untuk Android/tablet yang lebih ringan:

```bash
yolo export \
  model=runs/moonharvest/yolov8n_agri_416/weights/best.pt \
  format=onnx \
  imgsz=320 \
  opset=12 \
  simplify=True
```

Hasil export biasanya:

```text
runs/moonharvest/yolov8n_agri_416/weights/best.onnx
```

## 9. Masukkan Ke Harvestmoon GCS

Copy model ONNX:

```bash
cp runs/moonharvest/yolov8n_agri_416/weights/best.onnx \
  HarvestmoonGCS/Assets/models/yolov8n-agri-416.onnx
```

Buat class file dari `data.yaml`. Untuk 12 kelas proposal:

```bash
cat > HarvestmoonGCS/Assets/models/classes-yolov8n-agri-custom.txt <<'EOF'
healthy_crop
crop_stress
dry_soil
water_stress
pest_damage
weed
disease
yellowing
wilting
bare_soil
irrigation_channel
standing_water
EOF
```

Jalankan app dengan model custom:

```bash
HARVESTMOON_YOLO_MODEL=HarvestmoonGCS/Assets/models/yolov8n-agri-416.onnx \
HARVESTMOON_YOLO_CLASSES=HarvestmoonGCS/Assets/models/classes-yolov8n-agri-custom.txt \
scripts/run_desktop_trial.sh
```

## 10. Troubleshooting

### Error path dataset

Cek `data.yaml`. Path harus benar. Kalau dataset ada di:

```text
/home/fawwazfa/Datasets/moonharvest-vision
```

maka `data.yaml` bisa memakai:

```yaml
path: /home/fawwazfa/Datasets/moonharvest-vision
train: train/images
val: valid/images
test: test/images
```

### Training sangat lambat

Gunakan:

```bash
imgsz=320 batch=4 epochs=30 device=cpu
```

### Model mendeteksi class yang salah

Penyebab umum:

- urutan class di `data.yaml` tidak sama dengan label,
- dataset terlalu sedikit,
- class mirip secara visual,
- label bounding box tidak konsisten,
- terlalu banyak gambar duplikat dari video yang sama.

### Model jalan di Python tapi tidak di GCS

Cek:

- file `.onnx` ada,
- class file ada,
- jumlah class di model dan class file sesuai,
- env `HARVESTMOON_YOLO_MODEL` dan `HARVESTMOON_YOLO_CLASSES` benar,
- gunakan `imgsz=320` atau `416` agar lebih ringan.

## 11. Urutan Paling Aman Untuk Kamu

1. Download dataset `resize-416x416`.
2. Export annotation `YOLOv8`.
3. Unzip ke `/home/fawwazfa/Datasets/moonharvest-vision`.
4. Jalankan training demo 30 epoch.
5. Tes predict pada folder `test/images`.
6. Export ONNX.
7. Copy ONNX ke `HarvestmoonGCS/Assets/models`.
8. Jalankan Harvestmoon GCS dengan env model custom.

## Referensi Resmi

- Ultralytics install/quickstart: https://docs.ultralytics.com/quickstart/
- Ultralytics train mode: https://docs.ultralytics.com/modes/train/
- Ultralytics object detection dataset format: https://docs.ultralytics.com/datasets/detect/
- Ultralytics export mode: https://docs.ultralytics.com/modes/export/
- Ultralytics ONNX integration: https://docs.ultralytics.com/integrations/onnx/
