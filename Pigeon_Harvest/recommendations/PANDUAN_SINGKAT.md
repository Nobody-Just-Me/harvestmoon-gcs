# Panduan Singkat - Sistem Rekomendasi MoonHarvest

## Gambaran Umum

Sistem rekomendasi MoonHarvest memberikan saran agronomis berbasis bukti ilmiah untuk manajemen tanaman padi. Semua rekomendasi didasarkan pada 13 jurnal internasional yang telah di-peer-review (2018-2026) dari IEEE, Elsevier, MDPI, Springer, Nature, dan PLOS ONE.

## Fitur Utama

✅ **Berbasis Jurnal Internasional**: Setiap rekomendasi terhubung ke publikasi ilmiah  
✅ **Prioritas Otomatis**: Tindakan diurutkan berdasarkan urgensi dan dampak  
✅ **Disesuaikan dengan Fase Pertumbuhan**: Rekomendasi adaptif sesuai umur tanaman  
✅ **Estimasi Biaya**: Perkiraan biaya untuk setiap intervensi  
✅ **Manajemen Hama Terpadu (IPM)**: Pedoman mengikuti standar internasional  
✅ **Multi-format Export**: JSON dan teks untuk fleksibilitas penggunaan

## Cara Penggunaan Cepat

### 1. Penggunaan Dasar

```python
from recommendations import RecommendationEngine

engine = RecommendationEngine()

# Data dari hasil deteksi UAV
class_percentages = {
    "healthy_crop": 45.2,      # Tanaman sehat (hijau)
    "stressed_crop": 32.1,     # Tanaman stres (kuning/tidak seragam)
    "drought_stress": 15.3,    # Stres kekeringan (coklat)
    "bare_soil": 7.4           # Tanah kosong/gap
}

report = engine.analyze_field(
    class_percentages=class_percentages,
    fhi=58.3,                  # Field Health Index (0-100)
    field_area_ha=2.5,         # Luas lahan dalam hektar
    days_after_transplant=35   # Umur tanaman (hari setelah tanam)
)

# Cetak ringkasan
print(engine.generate_quick_summary(report))

# Simpan laporan lengkap
engine.export_to_text(report, "rekomendasi_lapangan.txt")
```

### 2. Dengan Keterbatasan Sumber Daya

```python
# Jika ada keterbatasan sumber daya
available_resources = {
    "irrigation": True,        # Irigasi tersedia
    "fertilizer": True,        # Pupuk tersedia
    "pesticides": False,       # Pestisida TIDAK tersedia
    "labor": True              # Tenaga kerja tersedia
}

report = engine.analyze_field(
    class_percentages=class_percentages,
    fhi=58.3,
    field_area_ha=2.5,
    days_after_transplant=35,
    available_resources=available_resources
)
```

## Interpretasi Hasil

### Field Health Index (FHI)

| FHI | Status | Tindakan |
|-----|--------|----------|
| 75-100 | **BAIK** | Pemeliharaan rutin, monitoring preventif |
| 50-74 | **PERHATIAN** | Investigasi zona stres, tingkatkan monitoring |
| 0-49 | **KRITIS** | Intervensi darurat, tindakan dalam 24-48 jam |

### Tingkat Urgensi

- **Critical** (Kritis): Tindakan immediate (0-12 jam) - kelangsungan tanaman terancam
- **High** (Tinggi): Dalam 24-48 jam - dampak signifikan terhadap hasil
- **Moderate** (Sedang): Dalam 3-7 hari - kehilangan hasil dapat dicegah
- **Low** (Rendah): Berkelanjutan/preventif - menjaga status saat ini

### Distribusi Kesehatan Tanaman

**Healthy Crop (Hijau Subur)**
- \>70% Dominan: Lahan sangat sehat, lakukan pemeliharaan
- 40-70% Moderat: Sehat dengan stres minor, investigasi zona

**Stressed Crop (Pertumbuhan Tidak Seragam)**
- \>40% Dominan: **URGENT** - Diagnosa penyebab dalam 24 jam
- 20-40% Moderat: Manajemen zona target, perbaiki kondisi

**Drought Stress (Stres Kekeringan Parah)**
- \>30% Dominan: **KRITIS** - Irigasi darurat, evaluasi viabilitas
- 15-30% Moderat: Irigasi urgent, monitoring harian

**Bare Soil (Tanah Kosong/Gap)**
- \>25% Dominan: Diagnosa gap, keputusan replanting
- 10-25% Moderat: Kontrol gulma, dukung tanaman tersisa

## Contoh Output

### Ringkasan Cepat
```
Field Health Index: 58.3/100 (Fair - Attention Needed). 
URGENT: 3 actions required within 24 hours. 
Primary diagnosis: Significant Crop Stress - Immediate Action Required. 
Priority 1: Diagnose Stress Cause.
```

### Tindakan Prioritas
```
IMMEDIATE (0-12 hours):
  1. Emergency Irrigation
     Priority: 1 | Deadline: Within 12 hours
     Apply 75-100mm irrigation immediately to prevent permanent wilting
     Evidence: Zhang2023, Bouguettaya2022

TODAY (12-24 hours):
  2. Diagnose Stress Cause
     Priority: 1 | Deadline: Tomorrow by 02:30 PM
     Rapid field assessment to identify stress type
     Evidence: Mahmood2025, Logavitool2025
```

### Estimasi Biaya
```
Total Estimated Cost: $187.50 USD
Per Hectare: $75.00 USD/ha

Cost Breakdown:
  Emergency Irrigation           : $  25.00
  Nutrient Deficiency Correction : $  75.00
  Disease/Pest Intervention      : $  70.00
  UAV Monitoring (weekly)        : $  17.50
```

## Integrasi dengan Pipeline Deteksi

### Dengan moonharvest_detect.py

```python
# Di akhir fungsi cmd_video() atau cmd_hsv()
from recommendations import RecommendationEngine

engine = RecommendationEngine()

report = engine.analyze_field(
    class_percentages=summary["class_pct"],
    fhi=summary["field_health"],
    field_area_ha=2.5
)

engine.export_to_text(report, "output/rekomendasi.txt")
print(engine.generate_quick_summary(report))
```

### Dengan moonharvest_v2_4class.py

```python
# Setelah menyimpan summary JSON
from recommendations import RecommendationEngine

engine = RecommendationEngine()

report = engine.analyze_field(
    class_percentages=summary["avg_class_pct"],
    fhi=summary["avg_field_health"],
    field_area_ha=2.0
)

engine.export_to_text(report, f"{args.output}/rekomendasi.txt")
```

## Pedoman Manajemen Hama Terpadu (IPM)

Sistem menyediakan pedoman IPM lengkap berdasarkan jurnal internasional:

### Hama Padi Umum
- **Penggerek Batang**: Ambang 5-10% infestasi pada fase vegetatif
- **Wereng Coklat**: Ambang 5-10 serangga per rumpun saat anakan
- **Pelipat Daun**: Ambang 2-3 daun rusak per rumpun

### Penyakit Padi Umum
- **Hawar Daun Bakteri (BLB)**: Copper-based bactericides, hindari N berlebihan
- **Blast**: Fungisida tricyclazole/azoxystrobin, pupuk berimbang
- **Hawar Pelepah**: Validamycin/hexaconazole, drainase berkala

Semua dengan protokol manajemen detail dan bukti jurnal.

## Fase Pertumbuhan

Rekomendasi otomatis menyesuaikan dengan umur tanaman:

**Vegetatif (0-50 HST)**
- Fokus: Pembentukan anakan, ketahanan terhadap penyakit
- Pupuk: 40% basal + 30% anakan + 30% inisiasi malai
- Irigasi: Air dangkal 2-5cm, AWD setelah 20 HST

**Reproduktif (50-70 HST)**
- Fokus: Pembentukan malai, periode kritis
- Pupuk: 30% sisa N pada inisiasi malai
- Irigasi: **KRITIS** - pertahankan kelembaban 5-10cm
- Peringatan: Stres air sangat merugikan hasil

**Pengisian Bulir (70-110 HST)**
- Fokus: Pengisian dan pematangan bulir
- Pupuk: Tidak ada N tambahan (risiko rebah)
- Irigasi: Jenuh bertahap, kering total 1-2 minggu sebelum panen
- Peringatan: Patuhi Pre-Harvest Interval (PHI) pestisida

## Basis Bukti

Setiap rekomendasi dapat dilacak ke publikasi spesifik:

**Rekomendasi Irigasi** → Zhang2023, Bouguettaya2022  
**Rekomendasi Pemupukan** → Zhang2023, Bouguettaya2022  
**Manajemen Penyakit** → Mahmood2025, Logavitool2025, Zhao2025  
**Manajemen Hama** → Zhao2025, Mahmood2025  
**Strategi Fusi** → Mahmood2025, Montalban-Faet2026  

Lihat `JOURNAL_CITATIONS.md` untuk daftar lengkap 13 referensi jurnal.

## Keterbatasan

⚠️ **Estimasi Biaya**: Hanya perkiraan kasar, biaya aktual bervariasi per wilayah  
⚠️ **Identifikasi Penyakit**: UAV dapat mendeteksi stres tapi tidak mengidentifikasi patogen spesifik - perlu ground truth  
⚠️ **Konteks Lokal**: Rekomendasi mungkin perlu penyesuaian untuk varietas, iklim, tanah spesifik  
⚠️ **Regulasi**: Rekomendasi pestisida harus sesuai regulasi dan registrasi lokal  

## File dan Struktur

```
recommendations/
├── __init__.py                    # Inisialisasi paket
├── recommendation_database.py     # Database rekomendasi berbasis jurnal
├── action_prioritizer.py          # Logika prioritas dan filter
├── recommendation_engine.py       # Engine analisis utama
├── integration_guide.py           # Panduan integrasi
├── example_usage.py               # Contoh penggunaan
├── test_recommendations.py        # Test suite
├── README.md                      # Dokumentasi lengkap (English)
├── PANDUAN_SINGKAT.md             # Panduan ini
└── JOURNAL_CITATIONS.md           # Daftar lengkap sitasi jurnal
```

## Testing

Jalankan test suite untuk verifikasi sistem:

```bash
cd Pigeon_Harvest/recommendations
python3 test_recommendations.py
```

Expected output: 9-10/10 tests passed

## CLI Tool

Generate rekomendasi dari command line:

```bash
python3 integration_guide.py hasil_deteksi.json \
  --field-area 2.5 \
  --dat 45 \
  --field-name "Sawah-A" \
  --output-dir "rekomendasi"
```

## Pengembangan Mendatang

- [ ] Integrasi API prakiraan cuaca untuk prediksi stres
- [ ] Database biaya lokal untuk estimasi regional
- [ ] Dukungan multi-bahasa (Spanyol, Prancis, Mandarin, Hindi)
- [ ] Interface aplikasi mobile
- [ ] Tracking historis untuk perbandingan antar musim
- [ ] Machine learning untuk refinement rekomendasi
- [ ] Integrasi data uji tanah
- [ ] Integrasi harga pasar untuk analisis cost-benefit

## Kontak & Kontribusi

Bagian dari sistem MoonHarvest UAV Crop Monitoring  
© 2026 MoonHarvest Project

Untuk pertanyaan, saran, atau kontribusi:
- Project: MoonHarvest GCS
- Metodologi Deteksi: Lihat `METODE_DETEKSI_JURNAL.md`
- Pipeline Deteksi: Lihat `moonharvest_detect.py`

---

*Terakhir Diperbarui: 2026-06-28*  
*Versi: 1.0.0*
