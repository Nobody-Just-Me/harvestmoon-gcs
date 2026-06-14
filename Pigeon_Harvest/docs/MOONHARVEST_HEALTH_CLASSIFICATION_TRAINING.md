# MoonHarvest Health Vision Training

Panduan ini untuk training model vision sesuai kebutuhan proposal:

- `healthy_crop`
- `stressed_crop`
- `drought_stress`
- `bare_soil`
- `disease_stress_vegetation`

Model yang dipakai adalah `yolov8n-cls`, ringan untuk RTX 3050 dan masih bisa dicoba di CPU.

## Jalankan Training

Dari folder project:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
chmod +x scripts/train_moonharvest_health_cls.sh
DEVICE=0 EPOCHS=80 PATIENCE=20 BATCH=16 MAX_PER_CLASS=500 scripts/train_moonharvest_health_cls.sh
```

Script akan otomatis:

- membaca dataset yang terpetakan dari `/home/fawwazfa/Program/datasheet`
- membuat dataset training di `/home/fawwazfa/Program/datasheet/moonharvest_health_cls`
- training `yolov8n-cls.pt` dengan validasi dan early stopping
- validasi model
- export ONNX
- menyalin model ke `HarvestmoonGCS/Assets/models/moonharvest-health-cls.onnx`
- menyalin class file ke `HarvestmoonGCS/Assets/models/classes-moonharvest-health.txt`

## Mode Cepat untuk Trial

Kalau ingin uji cepat dulu:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
DEVICE=0 EPOCHS=10 BATCH=8 MAX_PER_CLASS=120 scripts/train_moonharvest_health_cls.sh
```

## Mode Seimbang yang Disarankan

Untuk hasil yang lebih kuat saat dipakai demo/proposal, gunakan mode seimbang:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
DEVICE=0 EPOCHS=80 PATIENCE=20 BATCH=16 MAX_PER_CLASS=500 scripts/train_moonharvest_health_cls.sh
```

Kalau RTX 3050 kehabisan VRAM, turunkan batch:

```bash
DEVICE=0 EPOCHS=80 PATIENCE=20 BATCH=8 MAX_PER_CLASS=500 scripts/train_moonharvest_health_cls.sh
```

`MAX_PER_CLASS=500` membatasi kelas besar agar model tidak terlalu berat sebelah. Karena `drought_stress` hanya sekitar 90 gambar, kelas itu tetap dipakai semua.

## Mode Semua Data

Kalau tetap ingin semua gambar yang terpetakan ikut training:

```bash
DEVICE=0 EPOCHS=100 PATIENCE=25 BATCH=16 MAX_PER_CLASS=0 scripts/train_moonharvest_health_cls.sh
```

`MAX_PER_CLASS=0` berarti tidak ada batas per kelas. Semua gambar yang ditemukan dan cocok dengan mapping MoonHarvest akan dimasukkan ke dataset training, tetapi training jauh lebih lama dan dataset sangat tidak seimbang.

Kalau GPU penuh atau CUDA bermasalah:

```bash
DEVICE=cpu EPOCHS=10 BATCH=4 MAX_PER_CLASS=80 scripts/train_moonharvest_health_cls.sh
```

## Mapping Dataset

Script mengambil sumber seperti ini:

- `healthy_crop`: plant-health healthy, lettuce healthy, dan subset PlantVillage healthy.
- `stressed_crop`: plant-health unhealthy.
- `drought_stress`: Agricultural Water Stress Image Dataset.
- `disease_stress_vegetation`: lettuce disease dan PlantVillage non-healthy.
- `bare_soil`: memakai folder soil/bare jika ada. Jika belum ada, script memakai proxy dari field/background image agar demo tetap bisa jalan.

Catatan penting: untuk hasil final proposal, `bare_soil` sebaiknya diganti dataset tanah kosong asli dari drone/agriculture karena proxy hanya cukup untuk demo awal.

## Output Penting

Setelah selesai, cek file ini:

```bash
ls -lh HarvestmoonGCS/Assets/models/moonharvest-health-cls.onnx
cat HarvestmoonGCS/Assets/models/classes-moonharvest-health.txt
cat /home/fawwazfa/Program/datasheet/moonharvest_health_cls/manifest.json
```

Hasil training lengkap ada di:

```bash
runs/moonharvest_health/
```

## Mencoba Hasil Training

Test satu folder gambar validasi:

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
source .venv-yolo/bin/activate
python vision_trial/trial_health_cls.py /home/fawwazfa/Program/datasheet/moonharvest_health_cls/val --device 0 --max-items 20
```

Test satu gambar:

```bash
python vision_trial/trial_health_cls.py /path/ke/gambar.jpg --device 0 --show
```

Test video:

```bash
vision_trial/run_health_testkamera.sh
```

Launcher ini otomatis menjalankan ONNX di CPU karena ONNX Runtime CUDA membutuhkan CUDA 12/cuDNN 9 lengkap. Kalau ingin mencoba GPU, pakai model `best.pt`:

```bash
MODEL=runs/moonharvest_health/health_cls_224/weights/best.pt DEVICE=0 vision_trial/run_health_testkamera.sh
```

Output trial tersimpan di:

```bash
runs/moonharvest_health/trial/
```

Script trial otomatis memakai:

```bash
HarvestmoonGCS/Assets/models/moonharvest-health-cls.onnx
```

Kalau ONNX belum ada, script akan mencoba mengambil `best.pt` terbaru dari `runs/moonharvest_health/*/weights/best.pt`.
