# MoonHarvest — Known Issues & Keterbatasan

**Diperbarui:** 2026-06-21

---

## Bug yang Sudah Diperbaiki

| ID | Deskripsi | Fix | File |
|----|-----------|-----|------|
| BUG-01 | `build_demo_counts()` tidak pernah menghitung region karena key mismatch ("Lush Green" vs "Healthy") | Tambah `DISPLAY_TO_GCS` mapping | `moonharvest_detect_stream.py:570` |
| BUG-02 | File video fusion corrupt (moov atom not found) jika proses Python di-pipe ke `\| head -N` | Jangan pipe stdout saat menulis video — gunakan `nohup` + log file | `moonharvest_fusion.py`, `moonharvest_detect.py` |
| BUG-03 | `avc1` codec tidak tersedia di sistem Linux → VideoWriter gagal | Fallback ke `mp4v` + ffmpeg re-encode ke H.264 | `run_detection_video.py`, `moonharvest_detect.py` |
| BUG-04 | Single-cell YOLO boxes tersebar (noise) pada grid detection | Filter `min_cells=2` di `apply_nms()` | `run_detection_video.py` |
| BUG-05 | Deteksi v4 pada gabung.mp4 dominasi Pest/Disease (domain mismatch) | Gunakan mode Fusion HSV+YOLO v1 untuk sawah UAV | `moonharvest_detect.py` |
| BUG-06 | `FormatGeofenceAlerts()` mencoba deserialize `List<string>` tapi JSON berisi objects → fallback ke raw JSON | Ganti dengan `JsonDocument.Parse()` + property extraction per item | `ReportsHarvestPage.xaml.cs:425` |
| BUG-07 | `RenderAnalysis()` StatsPage: `rawDisease = BareSoilPct > 0 ? 0 : 0` — selalu 0, ternary tidak pernah menghasilkan disease | Hapus ternary yang salah, set `rawDisease = 0` dengan komentar alasan (model tidak menyimpan field disease terpisah) | `StatsPage.xaml.cs:211` |
| BUG-08 | `RenderAnalysis()` StatsPage: confidence dihitung sebagai `100 - DroughtPercentage` — formula salah | Ganti dengan `100 - BareSoilPct - DroughtPct * 0.4` yang lebih merepresentasikan coverage terklasifikasi | `StatsPage.xaml.cs:219` |
| BUG-09 | `EnrichSeedReports()` mencari video di path lama yang tidak ada (`hsvv.mp4`, `detection_output.mp4`) | Update ke path demo video yang sebenarnya (`demo_videos/fusion_gabung/gabung_fused_only.mp4`) | `ReportsHarvestPage.xaml.cs:74` |

---

## Keterbatasan yang Diketahui (Aktif)

### KET-01: Domain Mismatch Model v4 pada UAV

**Deskripsi:** Model v4 (6 kelas) dilatih dari dataset campuran ground-level + 330 frame UAV. Pada video UAV 60-80m, model cenderung salah prediksi sebagai Pest/Soil Issues.

**Dampak:** Field Health YOLO v4 pada gabung.mp4 hanya ~7% (tidak realistis).

**Workaround saat ini:** Gunakan **Fusion HSV+YOLO v1** — HSV mengoreksi YOLO. FH fused = 78.9%.

**Rencana fix:** Tambah >500 frame UAV berlabel ke dataset training, retrain model v5.

---

### KET-02: FH Fusion Underestimate (78.9% vs HSV 90%)

**Deskripsi:** Karena YOLO v1 sering memprediksi `stressed_crop` pada area hijau, fusion menarik FHI ke bawah dari 90% (HSV) menjadi 78.9%.

**Dampak:** Nilai FHI tidak sepenuhnya mencerminkan kondisi lapangan (sawah sebenarnya lebih sehat dari 78.9%).

**Workaround:** Untuk demo, gunakan HSV saja (`moonharvest_detect.py hsv`) jika ingin FHI yang lebih optimistis.

---

### KET-03: Agreement Rate Rendah (27.6%)

**Deskripsi:** Hanya 27.6% region di mana YOLO dan HSV setuju. Ini menunjukkan YOLO v1 sering tidak cocok dengan HSV untuk domain UAV sawah.

**Dampak:** Fusion logic banyak menggunakan HSV sebagai tie-breaker. Ini sebenarnya baik (HSV lebih andal untuk domain ini), tapi menunjukkan bahwa YOLO v1 perlu retraining.

**Bukan bug** — desain fusion memang mengutamakan HSV untuk kelas `healthy_crop` (alpha HSV 80%).

---

### KET-04: `bare_soil` Tidak Ditampilkan di UI

**Deskripsi:** `bare_soil` di-hide di semua mode display (`DISPLAY_MAP[bare_soil] = None`). Region bare_soil tidak muncul di bounding box.

**Alasan:** bare_soil sering false positive (tanah di pematang, bayangan) sehingga memperbesar tampilan.

**Workaround:** Untuk analisis detail, buka `moonharvest_detect.py` dan ubah `None` ke `"Bare Soil"` di `DISPLAY_MAP`.

---

### KET-05: Performa Runtime ~2 FPS pada Video

**Deskripsi:** Mode fusion memproses ~2 frame/detik (RTX 3050 6GB). Untuk real-time streaming ke GCS, ini masih memadai tapi tidak smooth.

**Batasan hardware:** YOLO per-region × 30-50 region per frame = banyak inference.

**Workaround:** Kurangi `--fps` ke 1.0 untuk video panjang, atau gunakan `--max-regions 30` (jika ditambahkan).

---

### KET-06: Tidak Ada Validasi Lapangan Nyata

**Deskripsi:** Semua hasil adalah deteksi visual berbasis citra, bukan pengukuran agronomis langsung (NDVI, sampel daun, dsb).

**Dampak akademik:** FHI dan class label bersifat indikatif, bukan diagnostik.

**Narasi yang aman:** *"Sistem ini merupakan alat monitoring visual awal yang membantu prioritisasi area untuk inspeksi lebih lanjut."*

---

## Hal yang Belum Diimplementasikan (dari Proposal)

| Fitur Proposal | Status | Catatan |
|---------------|--------|---------|
| Telemetry MAVLink terintegrasi dengan deteksi | Parsial | MAVLink ada di GCS, belum terhubung ke output deteksi |
| Analisis zona geografis (GPS-tagged) | Belum | Dashboard punya placeholder, belum ada data nyata |
| Alert otomatis per zona prioritas | Parsial | StatsPage menampilkan zona prioritas statik |
| Multi-spektral / NDVI | Belum | Hanya visible spectrum (RGB) |
| Tracking temporal (same region, multiple passes) | Belum | Setiap frame independen |

---

## Cara Melaporkan Bug Baru

1. Catat: kondisi input, error message, file yang terlibat
2. Tambahkan ke tabel BUG di atas dengan format:
   - ID: BUG-XX
   - Deskripsi singkat
   - Fix (atau "Belum difix")
   - File dan baris
