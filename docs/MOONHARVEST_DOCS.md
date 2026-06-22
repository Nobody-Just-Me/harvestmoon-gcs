# MoonHarvest — Dokumentasi Teknis Lengkap

**Versi:** 2026-06-21 (demo-teknofest branch)  
**Platform:** .NET 9 MAUI + Python 3 + YOLOv8 + OpenCV  
**Hardware target:** UAV + laptop Linux (RTX 3050 6GB, CUDA)

---

## Daftar Isi

1. [Gambaran Sistem](#1-gambaran-sistem)
2. [Arsitektur Program](#2-arsitektur-program)
3. [Model AI](#3-model-ai)
4. [Pipeline Deteksi HSV](#4-pipeline-deteksi-hsv)
5. [Pipeline Deteksi YOLO Grid](#5-pipeline-deteksi-yolo-grid)
6. [Pipeline Fusion HSV + YOLO](#6-pipeline-fusion-hsv--yolo)
7. [Script Python — Referensi Lengkap](#7-script-python--referensi-lengkap)
8. [Aplikasi GCS (HarvestmoonGCS)](#8-aplikasi-gcs-harvestmoongcs)
9. [Dataset & Training](#9-dataset--training)
10. [Konfigurasi HSV](#10-konfigurasi-hsv)
11. [Output & File](#11-output--file)
12. [Cara Menjalankan](#12-cara-menjalankan)

---

## 1. Gambaran Sistem

MoonHarvest adalah sistem GCS (Ground Control Station) untuk UAV pertanian yang menggabungkan:

- **Computer Vision berbasis HSV** — segmentasi warna tanaman tanpa deep learning
- **YOLO Classification** — klasifikasi kondisi lahan berbasis patch neural network
- **Fusion adaptif** — menggabungkan hasil HSV dan YOLO per-region dengan bobot confidence
- **Dashboard real-time** — aplikasi desktop .NET MAUI yang menerima stream video + deteksi dari Python

```
UAV Video Feed
     │
     ▼
Python Detection Script
  ├── HSV Segmentasi
  ├── YOLO Classification  
  └── Fusion (HSV + YOLO)
     │
     ▼ JSON stdout (base64 frame + detection data)
     │
HarvestmoonGCS (.NET MAUI)
  ├── Dashboard (live stats)
  ├── Camera Page (video + bounding box)
  ├── Reports Page (laporan sesi)
  └── AI Settings Page (model selector)
```

---

## 2. Arsitektur Program

```
Harvestmoon/
├── moonharvest_detect.py              ← Standalone: HSV + Fusion (1 file)
│
├── Pigeon_Harvest/
│   ├── HarvestmoonGCS/                ← Aplikasi .NET MAUI GCS
│   │   ├── Services/
│   │   │   ├── PythonCameraService.cs ← Bridge C# → Python
│   │   │   └── HarvestFunctionalService.cs ← Laporan & analitik
│   │   ├── Views/
│   │   │   ├── DashboardPage.xaml     ← Status UAV + stats
│   │   │   ├── CameraPage.xaml        ← Live video + detection
│   │   │   ├── AISettingsPage.xaml    ← Pilih model & mode
│   │   │   ├── ReportsHarvestPage.xaml ← Riwayat deteksi
│   │   │   ├── StatsPage.xaml         ← Grafik kelas
│   │   │   └── MapPage.xaml           ← Peta waypoint
│   │   ├── moonharvest_detect_stream.py ← Stream: fusion HSV+YOLO
│   │   └── yolo_classify_stream.py    ← Stream: grid YOLO saja
│   │
│   └── scripts/
│       └── run_detection_video.py     ← Deteksi video lokal (grid YOLO)
│
├── test_program/moonharvest_hsv_detector/moonharvest_package/
│   ├── moonharvest_hsv.py             ← HSV pipeline lengkap
│   ├── moonharvest_fusion.py          ← Fusion HSV + YOLO
│   ├── hsv_config.json                ← Threshold HSV (dikalibrasi)
│   └── hsv_calibration.json          ← Statistik pixel gabung.mp4
│
├── runs/classify/
│   ├── health_train_v1-2/weights/best.pt   ← Model v1 (5 kelas, fusion)
│   └── health_train_v3-20260621/weights/best.pt ← Model v4 (6 kelas, grid)
│
├── tools/
│   └── label_frames.py                ← Tool labeling manual frame UAV
│
└── docs/
    └── MOONHARVEST_DOCS.md            ← File ini
```

---

## 3. Model AI

### Model v1 — `health_train_v1-2`
| Properti | Nilai |
|----------|-------|
| Arsitektur | YOLOv8s-cls (classification) |
| Kelas | 5 kelas |
| Digunakan di | HSV+YOLO Fusion, `moonharvest_detect_stream.py` |
| Akurasi | ~85% (dataset campuran, bukan UAV-spesifik) |

**Kelas (urutan training = alfabet):**

| Index | Nama Internal | Deskripsi |
|-------|--------------|-----------|
| 0 | `bare_soil` | Tanah kosong / tidak ada tanaman |
| 1 | `disease_stress_vegetation` | Tanaman terserang penyakit, nekrosis |
| 2 | `drought_stress` | Kekeringan, coklat-tan kering |
| 3 | `healthy_crop` | Tanaman sehat, hijau |
| 4 | `stressed_crop` | Tanaman stress non-spesifik, menguning |

---

### Model v4 — `health_train_v3-20260621`
| Properti | Nilai |
|----------|-------|
| Arsitektur | YOLOv8s-cls (classification) |
| Kelas | 6 kelas |
| Digunakan di | Grid detection, `run_detection_video.py`, `yolo_classify_stream.py` |
| Akurasi | **95.0%** (termasuk 330 frame UAV dari gabung.mp4) |

**Kelas:**

| Nama | Deskripsi |
|------|-----------|
| `lush_green` | Sawah hijau lebat, sehat |
| `well_irrigated` | Irigasi baik, kondisi normal |
| `inconsistent_growth` | Pertumbuhan tidak merata |
| `soil_issues` | Masalah tanah / bare soil |
| `disease` | Penyakit tanaman |
| `pest` | Serangan hama |

---

### Perbandingan Performa pada gabung.mp4

| Metode | Model | Field Health (rata2) | Catatan |
|--------|-------|----------------------|---------|
| YOLO Grid | v4 (6 kelas) | ~7% | Domain mismatch — over-detect pest/disease |
| HSV saja | hsv_config.json | 90.0% | Akurat untuk sawah hijau UAV |
| **Fusion HSV+YOLO** | **v1 (5 kelas)** | **78.9%** | **Terbaik — consensus detection** |

---

## 4. Pipeline Deteksi HSV

**File:** `moonharvest_hsv.py` / terintegrasi di `moonharvest_detect.py`

### Tahapan Pipeline

```
Frame BGR
  │
  ├─[1] Gray-World White Balance
  │     Koreksi iluminasi agar threshold stabil antar kondisi cahaya
  │
  ├─[2] CLAHE (Contrast Limited Adaptive Histogram Equalization)
  │     Diterapkan pada channel V dari HSV (clip=2.0, grid=8×8)
  │
  ├─[3] Hitung ExG (Excess Green Index)
  │     ExG = (2g - r - b) / (r + g + b)
  │     Nilai tinggi = vegetasi hijau kuat
  │
  ├─[4] Hitung Tekstur (Local Std Dev)
  │     Sliding window 9×9 pada grayscale
  │     Nilai tinggi = bercak/penyakit
  │
  ├─[5] Klasifikasi Per-Piksel (vektorisasi NumPy)
  │     Urutan prioritas:
  │     a. Shadow (V < 45)
  │     b. Bare Soil (S ≤ 30, non-vegetasi)
  │     c. Background (H=100-140, S ≥ 34 → langit/air)
  │     d. Healthy (H=30-100, S ≥ 15, ExG ≥ 0.069)
  │     e. Disease (H merah, S ≥ 45, tekstur tinggi)
  │     f. Stressed (H=15-46, S ≥ 15, ExG ≥ 0.02)
  │     g. Drought (H=10-20, S ≥ 28)
  │     h. Bare Soil (residual)
  │     i. Background (sisa)
  │
  ├─[6] Reassign Stressed → Healthy
  │     Piksel "stressed" yang BUKAN kuning sejati → healthy
  │     Kuning sejati: H=18-36, ExG < 0.085, S ≥ 36
  │
  ├─[7] Dark Pixels → Bare Soil
  │     V < 55 → bare_soil (sebelum suppress_structures)
  │
  ├─[8] Median Filter Label (kernel 7×7)
  │     Haluskan batas antar kelas
  │
  ├─[9] Suppress Structures
  │     Buka morfologi besar (kernel 11) pada bare_soil
  │     → hapus jalan/pematang tipis dan rumah kecil
  │
  ├─[10] Extract Regions (Connected Components)
  │      Area minimum: 0.15% dari total frame
  │      Output: bounding box + confidence + centroid per region
  │
  ├─[11] EMA Temporal Smoothing (video)
  │      alpha=0.4 — distribusi kelas dirata-rata antar frame
  │
  └─[12] Field Health Index
         FHI = 100 - Σ(SEVERITY[k] × pct[k])
         Severity: healthy=0.0, stressed=0.45, drought=0.75, disease=1.0
```

### Kelas Output HSV

| Index | Kelas | Warna Overlay (BGR) | Severity |
|-------|-------|---------------------|----------|
| 0 | `healthy_crop` | (60, 200, 60) hijau | 0.0 |
| 1 | `stressed_crop` | (40, 220, 230) kuning | 0.45 |
| 2 | `disease_stress_vegetation` | (40, 40, 230) biru-merah | 1.0 |
| 3 | `drought_stress` | (30, 140, 250) oranye | 0.75 |
| 4 | `bare_soil` | (110, 120, 140) abu | 0.0 |

---

## 5. Pipeline Deteksi YOLO Grid

**File:** `run_detection_video.py`, `yolo_classify_stream.py`

### Cara Kerja

Frame dibagi menjadi grid **5×7 = 35 sel**. Tiap sel diklasifikasi oleh YOLO v4 (6 kelas). Sel-sel yang bersebelahan dengan kelas sama digabung menjadi region (connected component).

```
Frame (1920×1080)
  │
  ├─ Resize → 1280×720 (atau sesuai --width)
  │
  ├─ Bagi grid 5×7
  │
  ├─ Tiap sel → YOLO classify (imgsz=640)
  │   output: kelas + confidence
  │
  ├─ Connected-component merging
  │   Sel bersebelahan dengan kelas sama → satu region
  │
  ├─ NMS (IoU 0.45) + min_cells filter
  │   Tolak region < 2 sel (noise terisolir)
  │
  ├─ Draw bounding box semi-transparan (fill 20%, border 3px)
  │
  └─ Output frame + statistik distribusi kelas
```

### Demo Mode (MOONHARVEST_DEMO=1)

Nama kelas internal v4 → label proposal TEKNOFEST:

| Internal | Demo Label | Warna |
|----------|-----------|-------|
| `lush_green` | Lush Green | (50, 205, 50) |
| `well_irrigated` | Well Irrigated | (200, 150, 2) |
| `inconsistent_growth` | Inconsistent Growth | (0, 200, 255) |
| `soil_issues` | Soil Issues | (55, 64, 93) |
| `disease` | Disease | (0, 60, 255) |
| `pest` | Pest | (0, 140, 255) |

---

## 6. Pipeline Fusion HSV + YOLO

**File:** `moonharvest_fusion.py`, `moonharvest_detect_stream.py`, `moonharvest_detect.py`

### Cara Kerja

Tidak memotong grid — HSV menentukan region secara organik, lalu YOLO mengklasifikasi patch tiap region.

```
Frame BGR
  │
  ├─[1] HSV Pipeline (lihat §4)
  │     Output: regions[] dengan bbox, kelas, confidence
  │
  ├─[2] YOLO Per-Region
  │     Untuk tiap region:
  │     - Potong patch frame[y:y+h, x:x+w]
  │     - Tolak jika patch < 48×48 px (YOLO tidak reliable)
  │     - Tolak jika YOLO confidence < 0.40
  │     - Remapping: urutan alfabet YOLO → urutan HSV_CLASSES
  │
  └─[3] Fusion Adaptif (confidence-gated)
        Prioritas (dari atas ke bawah):
        a. YOLO tidak valid → pakai HSV langsung
        b. Konflik HSV_WINS: (healthy vs stressed/disease) → HSV menang
        c. Kedua setuju → confidence boost ×1.05
        d. Selisih conf > 0.25 → pakai detektor yang lebih yakin
        e. Konflik kecil → weighted blend CLASS_ALPHA_YOLO
```

### Bobot Fusion Per Kelas

| Kelas | α YOLO | α HSV | Alasan |
|-------|--------|-------|--------|
| `healthy_crop` | 0.20 | 0.80 | HSV jauh lebih andal untuk hijau UAV |
| `stressed_crop` | 0.55 | 0.45 | Seimbang |
| `disease_stress_vegetation` | 0.45 | 0.55 | YOLO sering false-positive di hijau/kuning |
| `drought_stress` | 0.50 | 0.50 | Seimbang |
| `bare_soil` | 0.45 | 0.55 | HSV sedikit lebih andal |

### Konflik HSV Selalu Menang

| HSV | YOLO | Keputusan |
|-----|------|-----------|
| healthy_crop | stressed_crop | HSV (hijau → tidak mungkin stress) |
| healthy_crop | disease | HSV (hijau → bukan penyakit) |
| stressed_crop | disease | HSV (kuning ≠ penyakit) |

### Output 3-Panel (video fusion)

```
┌─────────────┬─────────────┬─────────────┐
│  YOLO saja  │   FUSED     │  HSV saja   │
│  FH=59.4    │  FH=78.9    │  FH=90.0    │
└─────────────┴─────────────┴─────────────┘
│ Agreement: 27.6%  YOLO:59 HSV:90 FUSED:78│
└───────────────────────────────────────────┘
Kotak HIJAU = setuju  │  Kotak BIRU = tidak setuju
```

---

## 7. Script Python — Referensi Lengkap

---

### 7.1 `moonharvest_detect.py` ← GUNAKAN INI

**Path:** `Harvestmoon/moonharvest_detect.py`  
**Deskripsi:** Standalone satu file, menggabungkan HSV + Fusion. Tidak perlu import modul lain.

#### Subcommand: `video` (fusion)

```bash
python3 moonharvest_detect.py video -i VIDEO.mp4 -o OUT_DIR/
```

| Argumen | Default | Keterangan |
|---------|---------|-----------|
| `-i` | wajib | Path video input |
| `-o` | `fusion_out/` | Direktori output |
| `--weights` | model v1 | Path file `.pt` YOLO |
| `--fps` | `2.0` | Frame per detik yang dianalisis |
| `--width` | `1280` | Resize lebar frame sebelum proses |
| `--panel-w` | `480` | Lebar tiap panel output |
| `--no-video` | off | Jangan tulis file video output |
| `--no-display` | off | Sembunyikan jendela preview |
| `--config` | None | Path hsv_config.json override |

**Output:**
- `OUT_DIR/VIDEO_fused.mp4` — video 3-panel (YOLO | FUSED | HSV)
- `OUT_DIR/VIDEO_fused_only.mp4` — video fused-only
- `OUT_DIR/VIDEO_log.csv` — log per-region per-frame
- `OUT_DIR/VIDEO_summary.json` — ringkasan statistik

#### Subcommand: `hsv` (HSV saja, tanpa YOLO)

```bash
python3 moonharvest_detect.py hsv -i VIDEO.mp4 -o OUT_DIR/
```

Argumen sama seperti `video`, tanpa `--weights` dan `--panel-w`.

**Output:**
- `OUT_DIR/VIDEO_hsv.mp4` — video dengan overlay HSV
- `OUT_DIR/VIDEO_hsv_summary.json` — ringkasan

#### Subcommand: `image` (satu gambar)

```bash
python3 moonharvest_detect.py image -i frame.jpg -o OUT_DIR/
```

**Output:**
- `OUT_DIR/frame_fused.jpg` — gambar 3-panel hasil fusion

---

### 7.2 `moonharvest_hsv.py`

**Path:** `test_program/moonharvest_hsv_detector/moonharvest_package/moonharvest_hsv.py`  
**Deskripsi:** HSV pipeline lengkap dengan subcommand `image`, `video`, `calibrate`, `train`.

```bash
# Proses satu gambar
python3 moonharvest_hsv.py image -i frame.jpg -o out/

# Proses video (HSV saja)
python3 moonharvest_hsv.py video -i video.mp4 -o out/ --fps 2 --no-display

# Auto-kalibrasi threshold dari footage
python3 moonharvest_hsv.py calibrate -i video.mp4 -o hsv_config.json --k 6

# Latih model statistik Gaussian dari swatch berlabel
python3 moonharvest_hsv.py train -i referensi.jpg --swatches swatches.json -o model.json
```

**Engine klasifikasi:**
- `--engine rule` (default) — aturan HSV threshold
- `--engine stat` — klasifikasi Mahalanobis distance ke Gaussian per kelas (lebih akurat tapi butuh `train` dulu)

---

### 7.3 `moonharvest_fusion.py`

**Path:** `test_program/moonharvest_hsv_detector/moonharvest_package/moonharvest_fusion.py`  
**Deskripsi:** Fusion HSV + YOLO per-region dengan threading (compute + display terpisah).

```bash
# Fusion pada video
python3 moonharvest_fusion.py video \
    -i VIDEO.mp4 \
    --weights PATH/best.pt \
    -o out/ \
    --fps 2 \
    --no-display

# Fusion pada satu gambar
python3 moonharvest_fusion.py image \
    -i frame.jpg \
    --weights PATH/best.pt \
    -o out/
```

| Argumen | Default | Keterangan |
|---------|---------|-----------|
| `--alpha` | `0.55` | Bobot global YOLO (sisa = HSV) |
| `--panel-w` | `480` | Lebar tiap panel |
| `--fps` | `2.0` | Frame/detik dianalisis |
| `--no-display` | off | Tanpa jendela preview |

---

### 7.4 `moonharvest_detect_stream.py`

**Path:** `Pigeon_Harvest/HarvestmoonGCS/moonharvest_detect_stream.py`  
**Deskripsi:** Script stream untuk dashboard GCS. Membaca video/kamera, output JSON ke stdout.

**Dipanggil oleh:** `PythonCameraService.cs` saat mode fusion diaktifkan di GCS.

**Protocol output (stdout):**
```json
{"type": "frame",     "data": "<base64_jpeg>"}
{"type": "detection", "data": {"count": 5, "summary": "Lush Green: 90%", "classes": {...}}}
{"type": "end",       "data": "Video ended"}
{"type": "error",     "data": "error message"}
```

```bash
python3 moonharvest_detect_stream.py \
    --input VIDEO.mp4 \
    --model PATH/best.pt \
    --width 960 \
    --fps 15
```

---

### 7.5 `yolo_classify_stream.py`

**Path:** `Pigeon_Harvest/HarvestmoonGCS/yolo_classify_stream.py`  
**Deskripsi:** Stream YOLO grid (tanpa HSV). Dipakai saat mode "YOLO Only" di GCS.

```bash
python3 yolo_classify_stream.py \
    --model PATH/best.pt \
    --input VIDEO.mp4 \
    --grid-x 7 --grid-y 5 \
    --width 960 \
    --fps 15
```

---

### 7.6 `run_detection_video.py`

**Path:** `Pigeon_Harvest/scripts/run_detection_video.py`  
**Deskripsi:** Deteksi YOLO grid pada video lokal (bukan streaming). Simpan output `.mp4`.

```bash
python3 run_detection_video.py \
    -i VIDEO.mp4 \
    -o OUTPUT.mp4 \
    --model PATH/best.pt \
    --grid-x 7 --grid-y 5 \
    --min-conf 0.3 \
    --no-display
```

**Fitur:**
- Grid 5×7, tiap sel diklasifikasi YOLO
- Connected-component merging antar sel
- NMS IoU=0.45 + filter `min_cells=2` (hapus noise terisolir)
- Semi-transparent fill (20%) + border 3px
- Auto ffmpeg re-encode ke H.264 setelah selesai

---

### 7.7 `label_frames.py`

**Path:** `tools/label_frames.py`  
**Deskripsi:** Tool labeling frame UAV secara manual untuk training dataset.

```bash
python3 tools/label_frames.py \
    -i gabung.mp4 \
    -o /path/to/dataset/ \
    --step 10
```

**Kontrol keyboard:**

| Tombol | Aksi |
|--------|------|
| `1` | Simpan → `lush_green/` |
| `2` | Simpan → `well_irrigated/` |
| `3` | Simpan → `inconsistent_growth/` |
| `4` | Simpan → `soil_issues/` |
| `5` | Simpan → `disease/` |
| `6` | Simpan → `pest/` |
| `SPACE` / `D` | Skip (tidak disimpan) |
| `A` / `←` | Mundur 30 frame |
| `Z` | Undo (hapus frame terakhir yang disimpan) |
| `Q` / `ESC` | Keluar |

---

### 7.8 `calibrate_hsv.py`

**Path:** `test_program/moonharvest_hsv_detector/moonharvest_package/calibrate_hsv.py`  
**Deskripsi:** Kalibrasi threshold HSV dari footage UAV baru menggunakan statistik pixel.

```bash
python3 calibrate_hsv.py \
    -i VIDEO.mp4 \
    -o hsv_config_baru.json \
    --sample-frames 165
```

Hasilkan `hsv_calibration.json` berisi statistik pixel (persentil H, S, V) dari frame UAV.

---

## 8. Aplikasi GCS (HarvestmoonGCS)

**Platform:** .NET 9 MAUI (Multi-platform App UI)  
**Target:** Desktop Linux, Windows

### 8.1 Halaman Utama

| Halaman | Fungsi |
|---------|--------|
| **Dashboard** | Status koneksi UAV, GPS, baterai, altitude, telemetri MAVLink real-time |
| **Camera** | Live video feed + overlay deteksi (kotak + label), switch mode deteksi |
| **AI Settings** | Pilih model YOLO, pilih mode (YOLO Grid / Fusion), atur threshold |
| **Reports** | Riwayat sesi deteksi, export PDF/CSV, peta zona prioritas |
| **Stats** | Grafik distribusi kelas per sesi, tren field health |
| **Map** | Peta waypoint, geofence, posisi UAV real-time |
| **Flight** | Kontrol penerbangan, throttle, mode flight |
| **Mission** | Perencanaan misi waypoint |
| **Diagnostics** | Log sistem, performa, error |

---

### 8.2 `PythonCameraService.cs`

Jembatan antara C# dan Python. Menjalankan script Python sebagai subprocess dan membaca JSON dari stdout.

**Mode deteksi yang didukung:**

| Mode | Script | Model |
|------|--------|-------|
| HSV + YOLO Fusion | `moonharvest_detect_stream.py` | v1 (5 kelas) |
| YOLO Grid Only | `yolo_classify_stream.py` | v4 (6 kelas) |
| Kamera saja | `camera_service.py` | — |

**Resolusi model (urutan pencarian `best.pt`):**
1. `runs/classify/health_train_v3-20260621/weights/best.pt` ← v4 (default)
2. `runs/classify/health_train_v3-20260619/weights/best.pt` ← v3 fallback
3. `runs/classify/health_train_v1-2/weights/best.pt` ← v1 fallback
4. Path relatif dari MAUI app bundle

---

### 8.3 `HarvestFunctionalService.cs`

Layanan inti analitik dan pelaporan.

**Fitur:**
- `HarvestReportRecord` — struktur data laporan sesi (deteksi, persentase kelas, zona prioritas, path video/screenshot)
- `HarvestZonePriority` — koordinat zona lahan yang butuh tindakan
- Export laporan ke JSON/CSV
- Penyimpanan ke database lokal
- Bundle evidence (screenshot, tlog, video clip)

---

## 9. Dataset & Training

### Lokasi Dataset

```
/home/fawwazfa/Program/datasheet/moonharvest_retrain/
├── raw/
│   ├── gabung_labeled/            ← 330 frame UAV dari gabung.mp4 (auto-labeled)
│   │   ├── lush_green/            79 frame (H=55-95, S≥25, ExG>0.05)
│   │   ├── well_irrigated/        214 frame (H=55-95, S≥25, ExG≤0.05)
│   │   └── inconsistent_growth/   37 frame (H=20-55, S≥30)
│   └── [dataset lain]             ~7200 gambar dataset ground-level
└── prepare_dataset.py             ← Script persiapan dataset (balance + split)
```

### Statistik Dataset v4 (setelah gabung_labeled)

| Kelas | Jumlah | Sumber |
|-------|--------|--------|
| lush_green | ≤1200 | Campuran ground + 79 UAV |
| well_irrigated | ≤1200 | Campuran ground + 214 UAV |
| inconsistent_growth | ≤1200 | Campuran ground + 37 UAV |
| soil_issues | ≤1200 | Dataset ground-level |
| disease | ≤1200 | Dataset ground-level |
| pest | ≤1200 | Dataset ground-level |

`MAX_PER_CLASS=1200` — dataset seimbang sempurna.

### Cara Training Ulang

```bash
# Aktifkan venv
source Pigeon_Harvest/.venv-yolo/bin/activate

# Siapkan dataset
cd /home/fawwazfa/Program/datasheet/moonharvest_retrain
python3 prepare_dataset.py

# Training YOLOv8s-cls
yolo classify train \
    data=/path/to/dataset \
    model=yolov8s-cls.pt \
    epochs=50 imgsz=224 \
    batch=32 \
    name=health_train_v3-YYYYMMDD \
    project=runs/classify
```

---

## 10. Konfigurasi HSV

**File:** `test_program/moonharvest_hsv_detector/moonharvest_package/hsv_config.json`

Threshold dikalibrasi dari `gabung.mp4` (sawah padi, UAV 60-80m, kondisi siang hari).

### Parameter Utama

| Parameter | Nilai | Keterangan |
|-----------|-------|-----------|
| `exg_veg_thr` | 0.0213 | ExG minimum untuk dianggap vegetasi |
| `exg_healthy_min` | 0.0693 | ExG minimum untuk dianggap healthy (hijau kuat) |
| `bg_h` | [100, 140] | Hue background (langit/air) |
| `bg_s_min` | 34 | Saturasi minimum background |
| `ema_alpha` | 0.4 | Smoothing temporal (0=lambat, 1=reaktif) |

### Threshold Per Kelas (OpenCV HSV: H=0-179, S=0-255, V=0-255)

| Kelas | H range | S min | V range | Keterangan |
|-------|---------|-------|---------|-----------|
| `healthy` | 30–100 | 15 | 69–255 | Hijau lebar (disesuaikan UAV) |
| `stressed` | 15–46 | 15 | 80–255 | Kuning-hijau lemah |
| `drought` | 10–20 | 28 | 90–255 | Coklat-oranye kering |
| `disease` | 0–10 + 168–179 | 45 | 25–215 | Merah/nekrosis |
| `soil` | — | s_max=30 | 110–240 | Saturasi sangat rendah |

### Threshold Kuning Sejati (untuk reassign stressed→healthy)

| Parameter | Nilai |
|-----------|-------|
| `yellow_h` | [18, 36] |
| `yellow_exg_max` | 0.085 |
| `yellow_s_min` | 36 |

---

## 11. Output & File

### File Deteksi Video

| File | Deskripsi |
|------|-----------|
| `VIDEO_fused.mp4` | Video 3-panel: YOLO \| FUSED \| HSV |
| `VIDEO_fused_only.mp4` | Video panel fused saja (lebih besar) |
| `VIDEO_hsv.mp4` | Video overlay HSV saja |
| `VIDEO_log.csv` | Log per-region: t, hsv_class, yolo_class, fused_class, confidence, agree |
| `VIDEO_summary.json` | Ringkasan: avg field health, agreement %, durasi proses |

### Format `summary.json` (Fusion)

```json
{
  "video": "gabung.mp4",
  "frames": 1646,
  "avg_field_health": {
    "yolo":  59.4,
    "hsv":   90.0,
    "fused": 78.9
  },
  "avg_agreement_pct": 27.6,
  "proc_seconds": 462.7
}
```

### Format `log.csv` (Fusion)

```
t,area,hsv_class,hsv_conf,yolo_class,yolo_conf,fused_class,fused_conf,agree
0.13,12540,healthy_crop,0.721,bare_soil,0.423,healthy_crop,0.721,0
0.13,8320,healthy_crop,0.698,healthy_crop,0.612,healthy_crop,0.648,1
```

---

## 12. Cara Menjalankan

### Prasyarat

```bash
# Python venv (CUDA)
source Pigeon_Harvest/.venv-yolo/bin/activate

# Paket yang dibutuhkan
# ultralytics, opencv-python, numpy
pip install ultralytics opencv-python numpy
```

### Deteksi Cepat (standalone)

```bash
cd /home/fawwazfa/Program/Harvestmoon
source Pigeon_Harvest/.venv-yolo/bin/activate

# Fusion HSV+YOLO pada gabung.mp4
python3 moonharvest_detect.py video \
    -i gabung.mp4 \
    -o demo_videos/out/ \
    --no-display

# HSV saja
python3 moonharvest_detect.py hsv \
    -i gabung.mp4 \
    -o demo_videos/out/ \
    --no-display

# Dengan model custom
python3 moonharvest_detect.py video \
    -i gabung.mp4 \
    -o out/ \
    --weights runs/classify/Pigeon_Harvest/runs/health_classification/health_train_v1-2/weights/best.pt \
    --fps 3 \
    --no-display
```

### Deteksi Grid YOLO (video lokal)

```bash
cd /home/fawwazfa/Program/Harvestmoon
source Pigeon_Harvest/.venv-yolo/bin/activate

python3 Pigeon_Harvest/scripts/run_detection_video.py \
    -i gabung.mp4 \
    -o demo_videos/gabung_grid.mp4 \
    --model runs/classify/health_train_v3-20260621/weights/best.pt \
    --no-display
```

### Menjalankan GCS (Aplikasi Desktop)

```bash
cd /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest
dotnet run --project HarvestmoonGCS -f net9.0-desktop
```

### Labeling Manual Frame UAV

```bash
python3 tools/label_frames.py \
    -i gabung.mp4 \
    -o /home/fawwazfa/Program/datasheet/moonharvest_retrain/raw/manual_labeled/ \
    --step 15
```

---

## Catatan Teknis Penting

### Domain Mismatch (v4 model)

Model v4 dilatih mayoritas dari gambar ground-level (bukan UAV), sehingga saat dijalankan pada video UAV 60-80m hasilnya tidak akurat (over-detect Pest/Disease). Solusi:
- Gunakan **Fusion HSV+YOLO v1** — HSV mengoreksi kesalahan YOLO
- Tambah manual labeling frame UAV → training ulang

### Codec Video

Script menggunakan `mp4v` codec lalu re-encode ke H.264 via ffmpeg secara otomatis. Jika ffmpeg tidak tersedia, output tetap `mp4v` (bisa diputar di VLC, mungkin tidak di browser).

### CUDA vs CPU

YOLO otomatis menggunakan GPU (CUDA) jika tersedia. Untuk menonaktifkan:
```python
yolo_model = YOLO("best.pt")
yolo_model.to("cpu")
```

### Threading (Fusion Video)

`moonharvest_fusion.py` dan `moonharvest_detect.py video` menggunakan dua thread:
- **Compute thread** — baca video, jalankan HSV+YOLO, tulis file output
- **Display thread (main)** — tampilkan preview ~30fps, tangkap keypress 'q'

Jangan redirect stdout dengan `| head -N` karena akan memutus pipe dan menghasilkan file output korup (moov atom tidak ditulis).

---

*Dokumentasi ini dibuat 2026-06-21. Untuk pertanyaan teknis lihat kode sumber di masing-masing file.*
