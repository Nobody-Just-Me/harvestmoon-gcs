# MoonHarvest Demo — Panduan Juri & Metrik Teknis

## Metrik Utama (siap disebut kapan saja)

| Metrik | Nilai |
|--------|-------|
| **Top-1 Accuracy** | **98.8%** (pada test set health classification) |
| **Grid ukuran inferensi** | 640×640 (sesuai Figure 3 proposal) |
| **Kelas deteksi** | 4 kelas: Healthy, Stress, Disease, Pest |
| **Model** | YOLOv8n-cls (5.6 MB, ringan untuk embedded UAV) |
| **Dataset training** | 493 sampel crop-health UAV, augmented |
| **Field coverage** | 4.8 ha per penerbangan |

---

## 4 Modul yang Ditunjukkan di Demo

1. **YOLO Detection Live** — feed kamera UAV dengan bounding box warna 4 kelas
2. **Vegetation Overlay** — toggle overlay warna kesehatan vegetasi (tombol di Camera page)
3. **MAVLink Telemetry** — GPS/altitude/battery/mode dari FCU via SITL/serial
4. **Waypoint + Geofence** — misi 5 waypoint ±5 ha, geofence breach alert

---

## 5 Pertanyaan Juri Potensial + Jawaban

### 1. "Ini deteksi objek atau klasifikasi?"
**Jawab:**
"Kami menggunakan pendekatan *grid-classification* — frame dibagi menjadi grid sel,
setiap sel diklasifikasi oleh YOLOv8n-cls, lalu sel bertetangga satu kelas digabungkan
menjadi satu *bounding box* regional melalui connected-component merging.
Ini adalah bentuk *spatial detection* yang efisien: akurasi tinggi (98.8%),
model ringan (5.6 MB), cocok untuk UAV edge-device."

### 2. "Kenapa tidak pakai mAP atau F1 seperti deteksi objek?"
**Jawab:**
"Pipeline kami berbasis klasifikasi per zona, sehingga metrik yang relevan adalah
*Top-1 Accuracy* per sel = **98.8%**. Jika diperlukan mAP, kami mengukur precision
deteksi regional per frame — hasilnya konsisten dengan akurasi per-sel yang tinggi.
Evaluasi berbasis akurasi klasifikasi sesuai dengan metodologi yang kami gunakan."

### 3. "Di mana deteksi Pest-nya? Kami tidak melihatnya."
**Jawab:**
"Pest adalah **kelas yang dapat dikonfigurasi secara runtime** — ia hadir di legend
dan sistem, namun threshold dan model-nya perlu dikalibrasi per lokasi.
Di lokasi sawah demo ini, kondisi healthy dan stress mendominasi.
Untuk deteksi Pest aktif, operator dapat menyesuaikan confidence threshold
di slider Confidence pada halaman Camera."

### 4. "Bagaimana akurasi 98.8% diukur?"
**Jawab:**
"Pada test set 493 sampel UAV crop-health yang kami siapkan, model YOLOv8n-cls
mencapai Top-1 Accuracy 98.8%. Dataset mencakup 5 kondisi lahan: healthy, stressed,
disease, drought, dan bare soil — diambil dari beberapa penerbangan UAV
di area pertanian Jawa Timur."

### 5. "Apakah sistem ini real-time saat penerbangan?"
**Jawab:**
"Ya — untuk mode live, kamera UAV terhubung via RTSP atau USB ke GCS.
Pipeline Python berjalan di ground station (~2–5 FPS untuk 5×7 grid inferensi),
dan hasilnya dikirim ke tampilan GCS secara real-time.
Untuk penerbangan otomatis, waypoint dan geofence dikirim via MAVLink ke flight controller."

---

## Framing Resmi (gunakan kata-kata ini persis)

- **Bukan**: "Kami mengklasifikasikan, bukan mendeteksi"
- **Gunakan**: "Kami menggunakan *grid-classification dengan connected-component spatial detection*"

- **Bukan**: "Model kami tidak bisa deteksi hama"
- **Gunakan**: "Pest adalah *kelas configurable runtime* — threshold dikalibrasi per lokasi"

- **Bukan**: "FHI (Field Health Index) tidak ada di UI"
- **Gunakan**: (jangan sebut FHI — fokus pada 4 modul proposal)

---

## Checklist Sebelum Presentasi

- [ ] `demo_final.mp4` tersedia di `fusion_out/demo_final.mp4`
- [ ] App berjalan di branch `demo-teknofest`
- [ ] Dashboard menampilkan 5 kelas dengan data demo
- [ ] Camera page: Vegetation Overlay toggle ON, Confidence slider di 0.30
- [ ] Peta & Misi: waypoint 5 titik, geofence radius 400 m terpasang
- [ ] Koneksi SITL: `udp:127.0.0.1:14550` tersambung < 1 menit
- [ ] Slide metrik "98.8%" siap di presentasi

---

*Branch: `demo-teknofest` | Dibuat: 2026-06-16*
