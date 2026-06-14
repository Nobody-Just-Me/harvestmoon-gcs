# MoonHarvest Vision Dataset And Model Datasheet

Dokumen ini adalah datasheet khusus vision system sesuai proposal MoonHarvest. Fokusnya adalah data visual yang harus disiapkan agar fitur **YOLO Crop Detection** dan **Vegetation Analysis Overlay** dapat berjalan pada Harvestmoon GCS.

## Scope Proposal

Berdasarkan proposal, vision module wajib memenuhi hal berikut:

- Memproses live UAV video frame-by-frame.
- Menjalankan YOLO berbasis ONNX Runtime untuk deteksi real-time.
- Menampilkan bounding box dan confidence score pada live feed.
- Mendukung model `.onnx` dan class file yang dapat diganti sesuai jenis tanaman.
- Menjalankan overlay kesehatan vegetasi berbasis OpenCV/color transformation.
- Menghasilkan zona prioritas untuk indikasi crop stress, pest, drought, atau irrigation issue.

## Vision Pipeline

| Tahap | Input | Proses | Output |
| --- | --- | --- | --- |
| Video capture | USB camera, RTSP stream, atau video UAV | Frame JPEG/RGB masuk ke GCS | Frame live |
| Preprocess | Frame RGB/BGR | Resize ke input model, normalisasi, konversi channel | Tensor YOLO |
| YOLO detection | Tensor + `.onnx` model | Inference ONNX Runtime + confidence filtering + NMS | Bounding box, class, confidence |
| Vegetation overlay | Frame BGR | HSV color segmentation + grid 4x4 default | Healthy/stressed/drought/soil percentage |
| Fusion | YOLO result + HSV zones | Severity scoring + irrigation priority | Report, waypoint priority, overlay |

## Model Yang Dibutuhkan

### Model A - MVP Vegetation Severity

Model ini paling cocok untuk dicoba cepat dalam 2 hari karena sesuai langsung dengan `VegetationYoloAnalyzer.cs`.

| Item | Nilai |
| --- | --- |
| Nama model | `yolov8n-vegetation-320.onnx` atau `yolov8n-vegetation.onnx` |
| Class file | `vegetation.names` |
| Jumlah kelas | 4 |
| Input size rekomendasi | 320x320 untuk Android/tablet, 640x640 untuk desktop |
| Format model | ONNX |
| Format label training | YOLO bbox text |
| Confidence awal | 0.35 desktop, 0.40 Android |
| NMS awal | 0.45 desktop, 0.40 Android |

Class wajib:

| ID | Class | Deskripsi visual | Dampak di analyzer |
| ---: | --- | --- | --- |
| 0 | `green_healthy` | Daun hijau tua/segar, area tanaman normal | Healthy/None |
| 1 | `yellow_stress` | Daun kuning, hijau pucat, gejala awal stress | Mild/Moderate |
| 2 | `brown_drought` | Area coklat/kering, tanaman mati/kekeringan | Severe |
| 3 | `soil_crack` | Retakan tanah, tanah sangat kering | Critical |

File class yang sudah ada:

```text
HarvestmoonGCS/Assets/models/vegetation.names
```

### Model B - Proposal Full Agriculture Monitoring

Model ini lebih lengkap untuk narasi proposal dan pengembangan lanjutan.

| Item | Nilai |
| --- | --- |
| Nama model | `yolov8n-agri-320.onnx` atau `yolov8n-agri.onnx` |
| Class file | `classes-yolov8n-agri-basic.txt` |
| Jumlah kelas | 12 |
| Input size rekomendasi | 320x320 Android, 640x640 desktop |
| Tujuan | Crop stress, pest, dry soil, weed, water stress, irrigation issue |

Class proposal:

| ID | Class | Kebutuhan data visual |
| ---: | --- | --- |
| 0 | `healthy_crop` | Kanopi tanaman sehat |
| 1 | `crop_stress` | Area tanaman tidak normal umum |
| 2 | `dry_soil` | Permukaan tanah kering |
| 3 | `water_stress` | Gejala kekurangan air pada tanaman |
| 4 | `pest_damage` | Lubang/kerusakan daun/hama |
| 5 | `weed` | Gulma di antara tanaman |
| 6 | `disease` | Bercak/penyakit daun |
| 7 | `yellowing` | Klorosis/daun menguning |
| 8 | `wilting` | Tanaman layu |
| 9 | `bare_soil` | Tanah terbuka tanpa vegetasi |
| 10 | `irrigation_channel` | Kanal/parit/jalur irigasi |
| 11 | `standing_water` | Genangan air |

File class yang sudah ada:

```text
HarvestmoonGCS/Assets/models/classes-yolov8n-agri-basic.txt
```

## Dataset Yang Harus Dikumpulkan

### Minimum Untuk Demo 2 Hari

Jika belum ada dataset pertanian besar, gunakan target minimum ini:

| Kebutuhan | Jumlah minimum |
| --- | ---: |
| Gambar per kelas MVP 4 kelas | 50-100 gambar |
| Total gambar MVP | 200-400 gambar |
| Gambar validasi | 20% dari total |
| Resolusi minimal | 640x480 |
| Variasi cahaya | pagi, siang, mendung |
| Variasi altitude | rendah dan sedang |
| Format label | YOLO `.txt` per image |

Untuk demo proposal, model COCO bawaan boleh dipakai hanya untuk membuktikan pipeline live YOLO. Namun untuk klaim crop monitoring, minimal siapkan contoh dataset/label agriculture walaupun model final belum sempurna.

### Target Untuk Model Stabil

| Kebutuhan | Jumlah rekomendasi |
| --- | ---: |
| Gambar per kelas | 500+ |
| Total untuk 4 kelas | 2.000+ |
| Total untuk 12 kelas | 6.000+ |
| Validasi | 15-20% |
| Test set lapangan | 10-15% |
| Field/site berbeda | Minimal 3 lokasi |
| Kondisi cahaya | Minimal 4 kondisi |

## Struktur Dataset

Gunakan struktur YOLO standar:

```text
dataset/
  images/
    train/
    val/
    test/
  labels/
    train/
    val/
    test/
  data.yaml
```

Contoh label YOLO untuk satu object:

```text
1 0.512 0.438 0.214 0.180
```

Artinya:

| Kolom | Arti |
| --- | --- |
| `1` | class id |
| `0.512` | center x normalized |
| `0.438` | center y normalized |
| `0.214` | width normalized |
| `0.180` | height normalized |

## Contoh `data.yaml` MVP 4 Kelas

```yaml
path: ./dataset
train: images/train
val: images/val
test: images/test

names:
  0: green_healthy
  1: yellow_stress
  2: brown_drought
  3: soil_crack
```

## Contoh `data.yaml` Proposal 12 Kelas

```yaml
path: ./dataset
train: images/train
val: images/val
test: images/test

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

## Panduan Pengambilan Data

| Parameter | Rekomendasi |
| --- | --- |
| Kamera | UAV camera, action camera, IP camera RTSP, atau kamera HP untuk awal |
| Format video | H.264/RTSP atau file MP4 |
| Frame sampling | 1-3 FPS untuk ekstraksi dataset, jangan ambil semua frame agar tidak duplikatif |
| Altitude | 5-30 m untuk detail tanaman; 30-60 m untuk pola lahan |
| Sudut kamera | Nadir/downward lebih baik untuk vegetation map |
| Blur | Buang frame motion blur berat |
| Cuaca | Ambil cerah, mendung, dan bayangan |
| Metadata | Simpan tanggal, lokasi, crop type, altitude, kamera, dan catatan kondisi tanaman |

## Aturan Labeling

- Label hanya object/area yang terlihat jelas.
- Untuk `green_healthy`, label area tanaman sehat yang konsisten, bukan seluruh frame.
- Untuk `yellow_stress`, label daun/patch tanaman yang menguning atau pucat.
- Untuk `brown_drought`, label area tanaman/tanah kering berwarna coklat yang dominan.
- Untuk `soil_crack`, label retakan tanah yang terlihat jelas.
- Jangan mencampur kelas dalam satu bounding box jika gejalanya berbeda.
- Jika satu area mengandung healthy dan stress, buat dua bounding box terpisah bila batasnya terlihat.

## Vegetation Overlay HSV

`VegetationYoloAnalyzer.cs` memakai HSV sebagai fallback dan fusion:

| Kelas HSV | Range awal | Makna |
| --- | --- | --- |
| Healthy | H 35-85, S 40-255, V 40-255 | Vegetasi hijau |
| Stressed | H 15-35, S 30-255, V 40-255 | Kuning/pucat |
| Drought | H 5-20, S 30-255, V 40-200 | Coklat/kering |
| Bare soil | H 0-20, S 10-100, V 30-200 | Tanah terbuka |

Catatan: range HSV perlu dikalibrasi ulang jika kamera, cahaya, atau jenis tanaman berubah.

## Output Yang Harus Muncul Di GCS

| Output | Sumber |
| --- | --- |
| Bounding box | YOLO detection |
| Confidence score | YOLO postprocess |
| Detection count | YOLO detection list |
| Healthy percentage | HSV zone analysis |
| Stressed percentage | HSV zone analysis |
| Drought percentage | HSV zone analysis |
| Bare soil percentage | HSV zone analysis |
| Severity | Fusion YOLO + HSV |
| Irrigation priority | Zone severity ranking |
| GPS/waypoint estimate | Telemetry + zone mapping |

## Acceptance Criteria Vision

Untuk demo proposal, vision dianggap cukup jika:

- Kamera lokal atau RTSP tampil live di GCS.
- YOLO model `.onnx` berhasil diload.
- Bounding box dan confidence muncul pada live feed.
- Confidence/NMS/model/class bisa diganti atau disiapkan sebagai file runtime.
- Vegetation overlay menghasilkan minimal healthy/stressed/drought/bare soil percentage.
- Setidaknya satu zona stress/drought dapat ditandai sebagai irrigation priority.
- Jika model custom gagal, GCS tetap berjalan dengan fallback HSV/OpenCV.

## File Placement Di Project

Untuk MVP 4 kelas:

```text
HarvestmoonGCS/Assets/models/yolov8n-vegetation-320.onnx
HarvestmoonGCS/Assets/models/vegetation.names
```

Untuk model proposal 12 kelas:

```text
HarvestmoonGCS/Assets/models/yolov8n-agri-320.onnx
HarvestmoonGCS/Assets/models/classes-yolov8n-agri-basic.txt
```

Fallback yang sudah bisa dipakai sekarang:

```text
HarvestmoonGCS/Assets/models/yolov8n-320.onnx
HarvestmoonGCS/Assets/models/yolov8n.onnx
HarvestmoonGCS/Assets/models/classes-yolov8n-coco.txt
```

## Perintah Export Model

Jika training memakai Ultralytics YOLO:

```bash
yolo detect train model=yolov8n.pt data=data.yaml imgsz=640 epochs=100
yolo export model=runs/detect/train/weights/best.pt format=onnx imgsz=640
```

Untuk Android/tablet:

```bash
yolo export model=runs/detect/train/weights/best.pt format=onnx imgsz=320
```

## Datasheet Ringkas Untuk Proposal

| Item | Nilai yang disarankan |
| --- | --- |
| Vision task | Object detection + vegetation stress overlay |
| Model | YOLOv8n custom agriculture |
| Runtime | ONNX Runtime |
| Image backend | OpenCV/OpenCvSharp |
| Live input | USB camera / RTSP UAV camera |
| Input size | 320 atau 640 |
| Output | bbox, class, confidence, severity zone, irrigation priority |
| Minimum FPS demo | 5-10 FPS dengan YOLO; 15 FPS camera preview |
| Target FPS desktop | 15+ FPS preview, 5+ FPS inference |
| Target FPS Android | 10-15 FPS preview, inference setiap beberapa frame |
| Fallback | HSV vegetation analysis tanpa YOLO |

## Referensi Teknis

- Ultralytics YOLO export ONNX: https://docs.ultralytics.com/modes/export/
- ONNX Runtime execution providers: https://onnxruntime.ai/docs/execution-providers/
- ONNX Runtime NNAPI Android: https://onnxruntime.ai/docs/execution-providers/NNAPI-ExecutionProvider.html
- OpenCV VideoCapture: https://docs.opencv.org/4.x/d8/dfe/classcv_1_1VideoCapture.html
