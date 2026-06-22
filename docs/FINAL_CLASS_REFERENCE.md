# MoonHarvest — Referensi Class Definitif

**Tanggal dikunci:** 2026-06-21  
**Berlaku untuk:** Semua mode (Fusion HSV+YOLO v1, HSV Only, Grid YOLO v4)

---

## Pemetaan Lengkap (Internal → Display → GCS)

| Internal (Python) | Label Display (UI/Video) | Key GCS C# | Warna (BGR) | Severity FHI |
|-------------------|--------------------------|------------|-------------|--------------|
| `healthy_crop` | **Lush Green** | `Healthy` | (50, 205, 50) hijau | 0.0 |
| `stressed_crop` | **Inconsistent Growth** | `Stress` | (0, 200, 255) kuning | 0.45 |
| `disease_stress_vegetation` | **Disease** | `Disease` | (0, 60, 255) merah | 1.0 |
| `drought_stress` | **Soil Issues** | `Stress` | (55, 64, 93) coklat | 0.75 |
| `bare_soil` | *(tersembunyi)* | — | — | 0.0 |

---

## Class Grid YOLO v4 (6 kelas)

| Nama Model | Label Display | Key GCS | Warna |
|-----------|---------------|---------|-------|
| `lush_green` | **Lush Green** | `Healthy` | (50, 205, 50) |
| `well_irrigated` | **Well Irrigated** | `Healthy` | (200, 150, 2) |
| `inconsistent_growth` | **Inconsistent Growth** | `Stress` | (0, 200, 255) |
| `soil_issues` | **Soil Issues** | `Stress` | (55, 64, 93) |
| `disease` | **Disease** | `Disease` | (0, 60, 255) |
| `pest` | **Pest** | `Pest` | (0, 140, 255) |

---

## Field Health Index (FHI)

```
FHI = 100 - Σ(SEVERITY[k] × pct[k])
```

| Nilai FHI | Status | Warna Indikator |
|-----------|--------|-----------------|
| 75 – 100 | **BAIK** | Hijau (50, 200, 50) |
| 50 – 74 | **PERHATIAN** | Kuning (0, 200, 255) |
| 0 – 49 | **KRITIS** | Merah (0, 60, 255) |

---

## Hasil gabung.mp4 (referensi demo)

| Metode | FHI rata-rata | Catatan |
|--------|--------------|---------|
| YOLO Grid v4 saja | ~7% | Domain mismatch — jangan dipakai untuk demo |
| HSV saja | 90.0% | Stabil untuk sawah hijau UAV |
| **Fusion HSV+YOLO v1** | **78.9%** | **Mode demo utama** |

---

## Narasi Aman untuk Presentasi

> *"Label yang ditampilkan merupakan indikasi visual berbasis analisis citra. Sistem menggunakan kombinasi analisis warna HSV dan model klasifikasi deep learning untuk mendeteksi kondisi area pertanian secara visual dari perspektif UAV 60–80m."*

---

## File yang Menggunakan Class Ini

| File | Pemetaan |
|------|----------|
| `moonharvest_detect.py` | `DISPLAY_MAP`, `DEMO_PALETTE` |
| `moonharvest_detect_stream.py` | `DISPLAY_MAP`, `build_demo_counts()` |
| `yolo_classify_stream.py` | `DISPLAY_MAP`, `DEMO_COLORS` |
| `run_detection_video.py` | `DISPLAY_MAP`, `DEMO_COLORS` |
| `DashboardPage.xaml.cs` | baris 677–680 (reads "Healthy","Stress","Disease","Pest") |
| `StatsPage.xaml.cs` | baris 249–250 |
| `FINAL_CONFIG.json` | `classes_display`, `classes_internal` |
