# MoonHarvest HSV Crop Condition Detector

Deteksi 5 kelas kondisi lahan dari citra/video UAV **menggunakan HSV + indeks
vegetasi (ExG) + analisis tekstur**. Murni computer vision klasik (OpenCV),
ringan, tanpa deep learning -> cocok untuk perangkat budget seperti RealmePad Mini.

## Kelas
| # | Kelas | Warna overlay | Dibedakan oleh |
|---|-------|---------------|----------------|
| 0 | `healthy_crop` | hijau | hijau kuat: hue hijau + saturasi cukup + ExG tinggi |
| 1 | `stressed_crop` | kuning | kuning / hijau lemah (menguning) |
| 2 | `disease_stress_vegetation` | merah | merah/nekrosis berbintik (tekstur + saturasi tinggi) |
| 3 | `drought_stress` | oranye | tan/oranye kering |
| 4 | `bare_soil` | abu-coklat | saturasi sangat rendah, non-vegetasi (tanah/tanah rusak) |
| - | background | biru | langit, air/sungai, kabut (DIABAIKAN) |
| - | shadow | gelap | bayangan (DIABAIKAN) |

Jalan, pematang, dan rumah otomatis disingkirkan (filter morfologi: garis tipis
& objek kecil pada bare_soil di-reklasifikasi menjadi background).

## Kalibrasi
Threshold di `DEFAULT_CFG` (dan `hsv_config.json`) DIKALIBRASI dari footage UAV
lahan padi milik pengguna. Nilai acuan per region (OpenCV HSV, H 0-179):

| Region | H (median) | S (median) | ExG (median) |
|--------|-----------|-----------|--------------|
| Hijau sehat (kiri) | 54 | 66 | 0.18 |
| Stress/kuning | 30 | 43 | 0.06 |
| Tanah rusak (tengah) | ~ (netral) | 14 | -0.02 |
| Air/sungai | 93 | 48 | 0.04 |

Aturan kunci:
- `healthy` butuh `ExG >= exg_healthy_min (0.11)` + `S >= 48` -> hijau lemah TIDAK
  dipaksa jadi sehat.
- `background` (air/langit) butuh hue kebiruan **dan** `S >= bg_s_min (34)` ->
  tanah rusak yang desaturasi (S~14) tidak ikut terbuang.

## Dua engine (keduanya 100% HSV, TANPA YOLO / tanpa deep learning)

**1. `rule` (default)** — ambang HSV keras per kelas. Cepat, transparan, mudah
ditala lewat `hsv_config.json`.

**2. `stat` (AKURAT, disarankan)** — klasifikasi statistik. Tiap kelas dimodelkan
sebagai Gaussian dalam ruang fitur 8 dimensi: `[cosH, sinH, S, V, ExG, GLI,
VARI, tekstur]`. Setiap piksel diklasifikasikan dengan **jarak Mahalanobis**
(memperhitungkan korelasi antar fitur) + confidence (softmax) + penolakan
outlier -> background. Hasil jauh lebih bersih & akurat daripada ambang keras,
tetap murni computer vision klasik. Model dilatih dari swatch berlabel di frame
Anda sendiri (`train`), jadi data-driven tanpa jaringan saraf.

## Pemakaian
```bash
# (engine AKURAT) 1) latih model sekali dari frame referensi berlabel
python3 moonharvest_hsv.py train -i ref.png -o model.json
#    -> tanpa --swatches dipakai DEFAULT_SWATCHES; atau beri swatch sendiri:
#    python3 moonharvest_hsv.py train -i ref.png --swatches swatches.json -o model.json

# 2) jalankan dengan engine stat (akurat)
python3 moonharvest_hsv.py image -i frame.jpg -o out --engine stat --model model.json
python3 moonharvest_hsv.py video -i clip.mp4 -o out --engine stat --model model.json --fps 2

# (engine cepat/rule) tanpa model
python3 moonharvest_hsv.py image -i frame.jpg -o out --grid 10x6
python3 moonharvest_hsv.py video -i clip.mp4 -o out --fps 2 --width 960

# Auto-kalibrasi ambang rule dari footage (KMeans warna dominan)
python3 moonharvest_hsv.py calibrate -i clip.mp4 -o hsv_config.json --k 6
```

### Format swatches.json (untuk train)
```json
{
  "healthy_crop":  [[0.03, 0.22, 0.18, 0.85]],
  "stressed_crop": [[0.24, 0.18, 0.40, 0.85]],
  "bare_soil":     [[0.49, 0.20, 0.70, 0.72]],
  "water":         [[0.91, 0.60, 0.99, 0.92]]
}
```
Koordinat `[x0,y0,x1,y1]` boleh **fraksi 0-1** (otomatis menyesuaikan resolusi)
atau **piksel**. Kelas yang tak diberi swatch (mis. drought/disease) otomatis
memakai prototipe warna agar 5 kelas selalu tersedia. Untuk akurasi maksimum,
tambahkan swatch nyata untuk drought & disease bila ada di lahan Anda.

## Output
- `*_overlay.jpg` : citra + mask + bounding box + panel Field Health
- `*_exg.jpg`     : heatmap indeks vegetasi (ExG)
- `*_mask.png`    : peta kelas berwarna
- `*_report.json` : distribusi kelas, region, analisis zona grid
- `*_timeline.csv`: (video) % tiap kelas + Field Health per waktu
- `*_summary.json`: (video) rata-rata, minimum, kelas terburuk

## Field Health Index (0-100)
`100 - sum(bobot_keparahan[kelas] * persen[kelas])`, bobot: healthy 0,
stressed 0.45, drought 0.75, disease 1.0, bare_soil 0.

## Dependensi
```bash
pip install opencv-python-headless numpy
```

## Catatan untuk CDR (TEKNOFEST)
Posisikan sebagai **hybrid color-texture classifier**. Untuk akurasi lebih
tinggi, gabungkan dengan YOLO (lihat strategi 2-tier): YOLO untuk lokalisasi
`vegetation` vs `bare_soil`, HSV/ExG untuk klasifikasi kondisi/severity.
Kalibrasi ulang threshold per lahan via subcommand `calibrate` tanpa ubah kode.
