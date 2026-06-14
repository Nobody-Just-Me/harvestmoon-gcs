# Vision Trial Folder

Folder ini berisi paket mandiri untuk mencoba model vision MoonHarvest memakai video file, bukan kamera.

## Isi Folder

```text
vision_trial/
  README.md
  run_video_trial.sh
  trial_yolo_video.py
  train_yolov8n_vision.sh
  prepare_flat_yolo_dataset.py
  models/
    yolov8n-crop-weed-416.onnx
    classes-crop-weed.txt
  configs/
    crop_weed_data.yaml
  output/
```

## Fungsi

- `run_video_trial.sh`: cara paling mudah untuk test video.
- `trial_yolo_video.py`: program utama untuk inference video + simpan CSV.
- `train_yolov8n_vision.sh`: training YOLOv8n dari `data.yaml`.
- `prepare_flat_yolo_dataset.py`: konversi dataset flat gambar+label menjadi struktur YOLOv8.
- `models/yolov8n-crop-weed-416.onnx`: model crop/weed hasil training.
- `models/classes-crop-weed.txt`: class model, yaitu `crop` dan `weed`.
- `output/`: hasil video anotasi dan CSV.

## Cara Trial Video

Dari root project:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
vision_trial/run_video_trial.sh /path/ke/video-uav.mp4
```

Contoh trial cepat 300 frame:

```bash
vision_trial/run_video_trial.sh /home/fawwazfa/Videos/uav-test.mp4 --max-frames 300
```

Tampilkan window preview saat proses:

```bash
vision_trial/run_video_trial.sh vision_trial/testkamera.mp4 --show
```

Tekan `q` atau `Esc` untuk menghentikan preview.

Mode deteksi lebih banyak dengan kotak lebih kecil:

```bash
vision_trial/run_video_trial.sh vision_trial/testkamera.mp4 --show --dense
```

Mode `--dense` sekarang memakai fixed-grid inference dan cache deteksi agar lebih ringan:

```text
imgsz=416
conf=0.22
iou=0.75
max-det=120
box-scale=0.60
grid-cols=3
grid-rows=2
detect-every=5
```

Mode paling ringan untuk prosesor lemah:

```bash
vision_trial/run_video_trial.sh vision_trial/testkamera.mp4 --show --fast
```

Mode `--fast` memakai:

```text
imgsz=416
conf=0.30
max-det=80
box-scale=0.75
detect-every=5
```

Catatan: model ONNX di folder ini diexport fixed input `416x416`, jadi preset bawaan memakai `imgsz=416`. Jika ingin mode `320x320`, export ONNX baru dengan `imgsz=320` dulu.

Untuk preview paling ringan, tampilkan window tanpa menyimpan MP4/CSV:

```bash
vision_trial/run_video_trial.sh vision_trial/testkamera.mp4 --show --fast --no-save-video --no-save-csv
```

Untuk preview deteksi lebih banyak tapi masih lebih ringan dari sliding tile:

```bash
vision_trial/run_video_trial.sh vision_trial/testkamera.mp4 --show --dense --no-save-video --no-save-csv
```

Output:

```text
vision_trial/output/crop_weed_trial.mp4
vision_trial/output/crop_weed_trial.csv
```

## Cara Training Ulang

Dataset crop/weed yang sudah dikonversi:

```text
/home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml
```

Training GPU RTX 3050:

```bash
IMGSZ=416 EPOCHS=100 BATCH=16 DEVICE=0 \
vision_trial/train_yolov8n_vision.sh /home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml crop_weed_416_gpu
```

Jika VRAM penuh:

```bash
IMGSZ=416 EPOCHS=100 BATCH=8 DEVICE=0 \
vision_trial/train_yolov8n_vision.sh /home/fawwazfa/Program/datasheet/yolo_crop_weed/data.yaml crop_weed_416_gpu
```

## Cara Konversi Dataset Flat

Jika ada dataset baru dengan struktur gambar dan label `.txt` dalam satu folder:

```bash
vision_trial/prepare_flat_yolo_dataset.py \
  /path/dataset_flat/data \
  /path/output_yolo_dataset \
  --classes /path/classes.txt
```

## Catatan

Model di folder ini hanya mendeteksi:

```text
crop
weed
```

Ini cocok untuk trial UAV crop/weed detection. Untuk proposal penuh, tetap perlu model tambahan untuk disease, dry soil, water stress, soil crack, dan standing water.
