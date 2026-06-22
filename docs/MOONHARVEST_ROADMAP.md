# Roadmap Lengkap MoonHarvest
## Evaluasi, Penyelesaian Deadline, dan Rencana Pengerjaan ke Depan

**Tanggal:** 2026-06-21  
**Status Sistem:** Prototype Fungsional  
**Branch:** demo-teknofest

---

## Ringkasan Eksekutif

MoonHarvest sudah berada pada tahap prototype fungsional. Sistem ini bukan lagi sekadar ide — sudah memiliki inti teknis yang nyata: pipeline pemrosesan citra, model deteksi, mekanisme fusion, antarmuka monitoring, dan dokumentasi sistem. Ini merupakan modal yang sangat baik untuk proposal, demo, maupun pengembangan lanjutan.

Namun, kalau dilihat sebagai sistem yang ingin dipresentasikan secara meyakinkan, MoonHarvest masih perlu dirapikan pada tiga area utama:

1. **Konsistensi class dan narasi sistem**
2. **Stabilitas mode demo**
3. **Penyelarasan antara kemampuan aktual program dengan tampilan proposal**

Fondasi teknisnya sudah ada — produk akhirnya tinggal disusun agar terlihat utuh, stabil, dan mudah dijelaskan.

Dokumen ini mencakup dua horizon waktu:
- **Jangka sangat pendek** — menyelesaikan sistem agar aman untuk deadline dan demo
- **Jangka menengah dan lanjutan** — mengembangkan MoonHarvest menjadi sistem yang lebih matang, konsisten, dan bernilai secara teknis maupun akademik

---

## Daftar Isi

1. [Posisi MoonHarvest Saat Ini](#1-posisi-moonharvest-saat-ini)
2. [Penilaian Lengkap: Kelebihan, Kekurangan, dan Implikasi](#2-penilaian-lengkap-kelebihan-kekurangan-dan-implikasi)
3. [Tujuan Pengerjaan Berdasarkan Fase](#3-tujuan-pengerjaan-berdasarkan-fase)
4. [Rencana Pengerjaan Sangat Mendesak: 2 Hari ke Depan](#4-rencana-pengerjaan-sangat-mendesak-2-hari-ke-depan)
5. [Saran Prioritas Teknis Setelah Deadline](#5-saran-prioritas-teknis-setelah-deadline)
6. [Roadmap 1 Minggu, 1 Bulan, dan 3 Bulan](#6-roadmap-1-minggu-1-bulan-dan-3-bulan)
7. [Saran Rinci per Area Pengerjaan](#7-saran-rinci-per-area-pengerjaan)
8. [Daftar Backlog Pengerjaan](#8-daftar-backlog-pengerjaan)
9. [Risiko yang Perlu Diwaspadai](#9-risiko-yang-perlu-diwaspadai)
10. [Saran Narasi Akademik yang Aman](#10-saran-narasi-akademik-yang-aman)
11. [Checklist Kerja Harian](#11-checklist-kerja-harian)
12. [Kesimpulan Akhir](#12-kesimpulan-akhir)

---

## 1. Posisi MoonHarvest Saat Ini

### 1.1 Apa yang Sudah Dimiliki

| Komponen | Status |
|----------|--------|
| Pipeline analisis visual berbasis HSV | ✅ Selesai, dikalibrasi dari gabung.mp4 |
| Model YOLO klasifikasi (v1 & v4) | ✅ v4 akurasi 95.0%, v1 dipakai di fusion |
| Mekanisme fusion HSV + YOLO | ✅ Confidence-adaptive, bobot per kelas |
| Antarmuka GCS / dashboard monitoring | ✅ .NET MAUI, multi-halaman |
| Output visual overlay hasil deteksi | ✅ Bounding box, 3-panel comparison |
| Output data (log, JSON, CSV) | ✅ Per-frame, per-region |
| Standalone script 1 file | ✅ `moonharvest_detect.py` |
| Dokumentasi sistem teknis | ✅ `MOONHARVEST_DOCS.md` |

MoonHarvest sudah memiliki inti algoritma, lapisan integrasi, dan lapisan presentasi. Ini struktur yang baik karena memisahkan antara proses analisis, penyajian hasil, dan kebutuhan dokumentasi.

### 1.2 Apa yang Masih Menjadi Celah

| Celah | Dampak |
|-------|--------|
| Implementasi belum sepenuhnya setara mockup proposal | Gap presentasi |
| Class belum konsisten antara sisi teknis dan sisi UI | Narasi membingungkan |
| Model v4 performa kurang di domain UAV (domain mismatch) | False positive tinggi pada video drone |
| False positive dan ketidakstabilan masih mungkin muncul | Risiko demo |
| Dashboard belum menampilkan ringkasan analisis yang kuat | Sistem terkesan kurang matang |

### 1.3 Kesimpulan Posisi Saat Ini

> MoonHarvest sudah cukup kuat untuk dipresentasikan sebagai **prototype**, tetapi belum ideal jika diposisikan sebagai sistem akhir yang matang penuh.

Strategi pengerjaan harus fokus pada: **stabilisasi, perapihan, dan pembingkaian yang tepat**.

---

## 2. Penilaian Lengkap: Kelebihan, Kekurangan, dan Implikasi

### 2.1 Kelebihan Utama

**Arsitektur sistem sudah terstruktur**  
MoonHarvest menunjukkan pemisahan komponen yang baik antara analisis citra, model AI, fusion logic, dan dashboard. Setiap bagian dapat ditingkatkan tanpa mengubah seluruh sistem dari nol.

**Dokumentasi teknis kuat**  
Banyak prototype gagal terlihat meyakinkan karena dokumentasinya lemah. Di MoonHarvest, dokumentasi justru membantu menunjukkan bahwa sistem dibangun dengan serius.

**Pendekatan teknik relevan**  
Kombinasi HSV + YOLO sangat cocok untuk prototype pertanian berbasis pengamatan visual. HSV memberi analisis warna cepat dan ringan, YOLO memberi klasifikasi berbasis model. Fusion di antaranya adalah pendekatan yang masuk akal.

**Sudah ada dasar antarmuka sistem**  
Adanya GCS/dashboard memberi nilai lebih — sistem terlihat sebagai **platform monitoring**, bukan sekadar script inferensi.

**Sudah ada hasil yang bisa ditunjukkan**  
Overlay video, summary JSON, log, dan keluaran lainnya membuat MoonHarvest mudah dipresentasikan karena ada **bukti hasil kerja yang bisa diamati langsung**.

### 2.2 Kekurangan Utama

**Class deteksi belum sepenuhnya aman secara makna**  
Beberapa class terdengar komunikatif untuk UI, tetapi secara teknis masih berbasis inferensi visual — bukan pengukuran langsung. Ini bisa menimbulkan masalah saat diuji kritis.

**Ada gap antara proposal dan implementasi**  
Proposal menampilkan sistem sangat lengkap (telemetry, peta, zona). Implementasi saat ini terfokus pada inti deteksi. Gap ini harus ditangani lewat framing yang tepat.

**Model kurang cocok untuk domain UAV**  
Dataset training masih banyak berasal dari sudut pandang non-UAV. Hasil pada video drone akan sulit konsisten — inilah yang menjelaskan turunnya performa pada sebagian skenario.

**Terlalu banyak mode atau class**  
Untuk demo, banyak mode justru membuat sistem terkesan tidak stabil. Satu mode mantap lebih baik daripada banyak mode yang membingungkan.

### 2.3 Implikasi Praktis

MoonHarvest tidak perlu dikejar menjadi sistem sempurna dalam waktu dekat. Yang perlu dilakukan:

- Pilih mode terbaik untuk demo
- Pilih class yang paling aman
- Kunci parameter
- Susun roadmap pengembangan lanjutan secara realistis

---

## 3. Tujuan Pengerjaan Berdasarkan Fase

| Fase | Nama | Tujuan |
|------|------|--------|
| **A** | Penyelamatan Deadline & Demo | Stabil, bisa dijalankan, bisa didemokan, mudah dijelaskan, tidak overclaim |
| **B** | Pemantapan Prototype | Rapikan class, tingkatkan konsistensi, perkuat dashboard, tambah evaluasi |
| **C** | Pengembangan Sistem Lanjutan | Data UAV lebih kuat, dashboard aktif, analisis zona, validasi lapangan |

---

## 4. Rencana Pengerjaan Sangat Mendesak: 2 Hari ke Depan

### 4.1 Target Utama

Dalam 2 hari, target realistis bukan membuat sistem sempurna — melainkan memastikan bahwa:

- Program bisa berjalan stabil
- Mode demo final sudah dipilih
- Class final sudah dikunci
- Tampilan output mudah dibaca
- Ada hasil cadangan untuk presentasi

---

### 4.2 Hari Pertama: Mengunci Core System

#### Tugas 1 — Pilih Satu Mode Final

Tentukan **satu mode** yang paling stabil pada data yang akan dipresentasikan. Jangan tampilkan terlalu banyak pilihan mode saat demo.

> Rekomendasi: **Fusion HSV + YOLO v1** — hasil FH fused 78.9%, jauh lebih masuk akal dari grid YOLO saja.

**Output:**
- [ ] Satu mode final ditentukan
- [ ] Satu model final dipilih
- [ ] Satu konfigurasi final dikunci

---

#### Tugas 2 — Bekukan Class Final

Pilih maksimal 4–6 class yang benar-benar bisa dibedakan secara visual dan aman dijelaskan.

**Pilihan class aman (opsi A — deskriptif):**

| Label UI | Makna |
|----------|-------|
| Lush Green | Area hijau lebat, tanaman sehat |
| Inconsistent Growth | Pertumbuhan tidak merata |
| Soil Issues | Masalah tanah atau tanah terbuka |
| Disease | Indikasi penyakit visual |

**Pilihan class aman (opsi B — sederhana):**

| Label UI | Makna |
|----------|-------|
| Healthy Area | Area kondisi baik |
| Dry/Stressed Area | Area kekurangan air atau stress |
| Soil Issue | Tanah bermasalah |
| Uneven Growth | Pertumbuhan tidak rata |

**Output:**
- [ ] Daftar class final ditentukan
- [ ] Nama label final untuk UI ditetapkan
- [ ] Penjelasan singkat makna tiap class ditulis

---

#### Tugas 3 — Uji Input Demo

Coba program pada setidaknya 3 skenario:
- Video terbaik (kondisi ideal)
- Video normal (kondisi rata-rata)
- Video lebih sulit (kondisi buruk)

Tujuannya memilih **bahan demo yang paling aman**.

**Output:**
- [ ] 1 video utama untuk demo
- [ ] 1 video cadangan
- [ ] 3 screenshot terbaik disiapkan

---

#### Tugas 4 — Kunci Parameter

Bekukan parameter penting ke dalam satu file konfigurasi final:

| Parameter | Nilai Saat Ini | Catatan |
|-----------|---------------|---------|
| `imgsz` | 224 (patch), 640 (grid) | Jangan diubah saat demo |
| `min_conf` | 0.40 (YOLO fusion), 0.30 (grid) | |
| `iou_threshold` | 0.45 | NMS |
| `exg_veg_thr` | 0.0213 | HSV dikalibrasi dari gabung.mp4 |
| `ema_alpha` | 0.4 | Temporal smoothing |
| `YOLO_MIN_PATCH` | 48px | Tolak patch terlalu kecil |

**Output:**
- [ ] File konfigurasi final disimpan
- [ ] Catatan parameter final didokumentasikan

---

#### Tugas 5 — Rapikan Tampilan Overlay

Pastikan tampilan hasil tidak ramai dan mudah dibaca.

Perhatikan:
- Ukuran dan keterbacaan teks label
- Warna bounding box konsisten per class
- Label tidak terlalu panjang
- Jumlah box tidak terlalu banyak (batasi 15–20 region)
- Posisi confidence di label sudah jelas

**Output:**
- [ ] Overlay final yang enak dilihat
- [ ] Standar warna tiap class didokumentasikan

---

### 4.3 Hari Kedua: Menyusun Sistem Agar Terlihat Selesai

#### Tugas 1 — Tambahkan Summary Output

Tambahkan minimal salah satu dari berikut agar sistem terasa lebih seperti dashboard proposal:

- [ ] Jumlah region per class
- [ ] Persentase area per class
- [ ] Dominant class frame ini
- [ ] Average confidence keseluruhan
- [ ] Field Health Index (sudah ada di fusion)
- [ ] Status umum: "Kondisi Baik / Perhatian / Kritis"

---

#### Tugas 2 — Sinkronkan UI dan Narasi

Pastikan istilah yang dipakai di dashboard **sama persis** dengan:
- [ ] Istilah yang diucapkan saat presentasi
- [ ] Istilah yang tertulis di laporan

---

#### Tugas 3 — Siapkan Bahan Cadangan Demo

Wajib tersedia sebelum demo:
- [ ] Video output terbaik (sudah re-encode H.264)
- [ ] Screenshot terbaik (3–5 gambar)
- [ ] File log hasil (`_log.csv`)
- [ ] JSON summary (`_summary.json`)
- [ ] Penjelasan singkat tiap hasil

---

#### Tugas 4 — Latihan Presentasi Teknis

Buat tiga versi penjelasan:
- [ ] **Versi 30 detik** — satu kalimat sistem + satu kalimat hasil
- [ ] **Versi 1 menit** — tambahkan pipeline dan output
- [ ] **Versi 3 menit** — jelaskan detail teknis, keterbatasan, arah pengembangan

---

#### Tugas 5 — Final Check Sebelum Deadline

- [ ] Program bisa dijalankan dari nol tanpa error
- [ ] Input video demo tersedia di path yang benar
- [ ] Output terbaca dan tidak korup
- [ ] Narasi konsisten dengan tampilan
- [ ] Backup demo tersedia (video + screenshot)
- [ ] Tidak ada dependency yang belum terinstall

---

## 5. Saran Prioritas Teknis Setelah Deadline

### Prioritas 1: Konsolidasi Class

Langkah paling penting setelah deadline. Pastikan class:
- Relevan dengan tujuan proposal
- Bisa dilatih dengan data yang cukup
- Bisa dideteksi dengan cukup konsisten
- Mudah dipahami pengguna

**Pekerjaan:**
- Audit semua class yang ada sekarang
- Tandai class yang terlalu abstrak
- Tandai class yang terlalu mirip satu sama lain
- Tandai class yang datanya kurang
- Tetapkan versi class final jangka menengah

**Hasil yang diharapkan:** Skema class lebih rapi, confusion antar class berkurang.

---

### Prioritas 2: Perbaikan Dataset

Jika hasil model pada footage UAV masih belum konsisten, pembenahan dataset adalah langkah yang **paling bernilai**.

**Pekerjaan:**
- Kumpulkan lebih banyak frame UAV nyata
- Seimbangkan jumlah sampel per class
- Pastikan variasi pencahayaan, sudut, dan skala lebih luas
- Pisahkan train/val/test dengan lebih disiplin
- Dokumentasikan sumber data per class

**Hasil yang diharapkan:** Performa model lebih sesuai domain UAV, hasil demo lebih stabil.

---

### Prioritas 3: Evaluasi Model yang Lebih Rapi

Selain angka akurasi umum, perlu evaluasi yang bisa menjawab pertanyaan teknis.

**Pekerjaan:**
- Hitung precision, recall, dan mAP per class
- Buat confusion matrix
- Bandingkan performa pada data UAV vs non-UAV
- Catat jenis kesalahan paling sering
- Catat kondisi yang memicu false positive

**Hasil yang diharapkan:** Analisis model lebih kuat, bahan diskusi sidang lebih lengkap.

---

### Prioritas 4: Penyempurnaan Fusion Logic

Setelah core model stabil, sempurnakan cara HSV dan YOLO saling mendukung.

**Pekerjaan:**
- Evaluasi kapan HSV lebih akurat dari YOLO
- Evaluasi kapan YOLO lebih akurat dari HSV
- Revisi rule fusion berdasarkan temuan
- Tambahkan threshold adaptif jika perlu
- Catat kondisi lapangan yang mengubah perilaku sistem

**Hasil yang diharapkan:** Fusion lebih masuk akal, false positive dapat ditekan.

---

### Prioritas 5: Penguatan Dashboard dan Output Monitoring

Setelah inti analisis mantap, buat dashboard benar-benar menjadi alat monitoring yang berguna.

**Pekerjaan:**
- Tampilkan ringkasan per zona
- Tampilkan dominant condition dengan warna
- Tampilkan Field Health Index yang jelas
- Tampilkan histori pengamatan singkat
- Pastikan panel tidak berlebihan dan mudah dibaca

**Hasil yang diharapkan:** Dashboard lebih informatif, sistem terasa lebih matang.

---

## 6. Roadmap 1 Minggu, 1 Bulan, dan 3 Bulan

### 6.1 Roadmap 1 Minggu (Pasca-Deadline)

**Fokus:** Stabilisasi pasca-demo

| Target | Deliverable |
|--------|-------------|
| Rapikan kode yang masih darurat | Kode bersih tanpa patch sementara |
| Dokumentasikan parameter final | `FINAL_CONFIG.md` |
| Kumpulkan semua hasil uji | Folder `results/` terorganisir |
| Catat bug yang muncul saat demo | `KNOWN_ISSUES.md` |
| Evaluasi ulang class dan label | `CLASS_REVIEW.md` |
| Buat backlog pengembangan | Issue list / Trello / Notion |

---

### 6.2 Roadmap 1 Bulan

**Fokus:** Mengubah prototype demo menjadi prototype riset yang lebih kuat

| Target | Deliverable |
|--------|-------------|
| Retraining model dengan data UAV lebih baik | Model revisi `health_train_v5` |
| Revisi class jika perlu | Skema class final v2 |
| Tambahkan evaluasi per class | Tabel precision/recall/mAP |
| Rapikan fusion logic | `fusion_v2.py` atau patch fusion |
| Perbaiki summary dashboard | Dashboard tampilkan zone summary |
| Susun laporan hasil eksperimen | Dokumen eksperimen |

---

### 6.3 Roadmap 3 Bulan

**Fokus:** Pematangan sistem ke arah prototype yang lebih serius

| Target | Deliverable |
|--------|-------------|
| Tambah jumlah data lapangan | Dataset UAV ≥ 1000 frame labeled |
| Uji di lebih banyak kondisi | Laporan uji multi-kondisi |
| Tambahkan validasi temporal atau tracking sederhana | Modul tracking sederhana |
| Buat analisis zona yang lebih konsisten | Zone heatmap output |
| Rapikan pengalaman pengguna dashboard | UX improvement |
| Persiapkan untuk publikasi atau demo eksternal | Draft paper / poster |

---

## 7. Saran Rinci per Area Pengerjaan

### 7.1 Model dan Training

> Pastikan model tidak hanya bagus di training, tetapi juga masuk akal saat dijalankan pada data UAV nyata.

**Tugas rinci:**
- Audit dataset per class (jumlah, kualitas label)
- Cek ketimpangan data antar class
- Pastikan tidak ada kebocoran train-val-test
- Kumpulkan sampel UAV tambahan (gunakan `tools/label_frames.py`)
- Buat eksperimen beberapa ukuran input (224, 320, 416)
- Dokumentasikan hasil terbaik

**Saran praktis:**
- Jangan terlalu banyak class jika data belum cukup
- Pilih class yang benar-benar terlihat secara visual
- Jangan mengejar akurasi training tinggi tanpa validasi lapangan

---

### 7.2 HSV Pipeline

> HSV adalah keunggulan MoonHarvest — pertahankan karena inilah yang membuat sistem berbeda dan lebih ringan.

**Tugas rinci:**
- Pastikan konversi warna efisien (hindari proses berulang)
- Buat threshold lebih mudah dikonfigurasi (semua di `hsv_config.json`)
- Evaluasi mana threshold yang paling berpengaruh
- Buang langkah yang mahal tapi dampaknya kecil
- Dokumentasikan pengaruh masing-masing threshold

**Saran praktis:**
- Jalankan HSV sekali per frame
- Gunakan ROI jika hanya area tertentu yang relevan
- Simpan threshold dalam file config yang rapi, bukan hardcode

---

### 7.3 Fusion dan Rule System

> Fusion harus dibuat jelas dan mudah dijelaskan.

**Tugas rinci:**
- Dokumentasikan aturan fusion saat ini (sudah ada di `MOONHARVEST_DOCS.md`)
- Bandingkan hasil sebelum dan sesudah fusion per video
- Tentukan rule override yang paling penting
- Catat kapan fusion memperbaiki hasil dan kapan justru mengganggu

**Saran praktis:**
- Hindari rule yang terlalu banyak
- Prioritaskan rule yang mudah diuji secara empiris
- Fokus pada koreksi kesalahan yang paling sering muncul

---

### 7.4 UI dan Dashboard

> Dashboard adalah wajah sistem. Walaupun belum semuanya real-time penuh, harus terasa rapi.

**Tugas rinci:**
- Rapikan istilah panel (konsisten antara CameraPage, StatsPage, ReportsPage)
- Samakan bahasa dan label di semua halaman
- Pilih komponen yang benar-benar penting
- Hilangkan elemen yang belum siap jika mengganggu
- Tambahkan ringkasan yang mudah dipahami (FHI, dominant class)

**Saran praktis:**
- Tampilkan sedikit informasi tapi kuat
- Utamakan kejelasan daripada keramaian
- Jaga konsistensi warna dan ikon antar halaman

---

### 7.5 Dokumentasi dan Laporan

> Area yang sering diremehkan, padahal sangat penting untuk sidang dan pengembangan ke depan.

**Tugas rinci:**
- Catat versi model yang dipakai di tiap sesi uji
- Catat parameter final setiap kali ada perubahan
- Catat skenario uji (video apa, kondisi apa, hasil apa)
- Catat keterbatasan sistem secara jujur
- Catat arah pengembangan lanjutan

**Saran praktis:**
- Tulis singkat tapi konsisten
- Jangan menyembunyikan keterbatasan — justru ini menunjukkan pemahaman teknis
- Sertakan alasan pemilihan mode dan class

---

## 8. Daftar Backlog Pengerjaan

### Prioritas Sangat Tinggi

- [ ] Konsolidasi class final
- [ ] Audit dataset UAV per class
- [ ] Evaluasi per class (precision/recall)
- [ ] Stabilisasi mode utama (pilih satu)
- [ ] Perapihan summary dashboard

### Prioritas Tinggi

- [ ] Peningkatan fusion rule berdasarkan evaluasi
- [ ] Pembersihan false positive sistematis
- [ ] Dokumentasi parameter final
- [ ] Penyelarasan istilah UI dengan laporan
- [ ] Penambahan hasil uji lapangan UAV

### Prioritas Menengah

- [ ] Optimasi performa runtime (FPS)
- [ ] Visualisasi zona (heatmap atau grid color)
- [ ] Penyimpanan histori hasil per sesi
- [ ] Validasi temporal sederhana (tracking region)
- [ ] Perapihan modul konfigurasi (satu file config)

### Prioritas Rendah (Jangka Panjang)

- [ ] Telemetry MAVLink terintegrasi penuh dengan deteksi
- [ ] Integrasi peta dengan zone overlay
- [ ] Mode analisis tambahan (multispektral, dll)
- [ ] Fitur ekspor laporan otomatis (PDF)
- [ ] Mode multi-sesi atau cloud logging

---

## 9. Risiko yang Perlu Diwaspadai

### 9.1 Risiko Teknis

| Risiko | Kemungkinan | Dampak | Mitigasi |
|--------|-------------|--------|---------|
| Model bagus di training tapi buruk di lapangan | Tinggi | Tinggi | Gunakan fusion + HSV dominan |
| Class terlalu banyak, model bingung | Sedang | Tinggi | Batasi 4–6 class |
| Dashboard terlalu ramai | Rendah | Sedang | Rapikan sebelum demo |
| Parameter berubah tanpa pencatatan | Sedang | Sedang | Kunci parameter di config |
| False positive mengganggu demo | Sedang | Tinggi | Gunakan `min_cells=2`, conf 0.4 |

### 9.2 Risiko Presentasi

| Risiko | Mitigasi |
|--------|---------|
| Menjelaskan sistem lebih besar dari implementasinya | Gunakan narasi akademik yang aman (lihat §10) |
| Mengklaim class sebagai diagnosa pasti | Sampaikan sebagai "indikasi visual berbasis citra" |
| Tidak punya backup saat live demo gagal | Siapkan video output + screenshot |
| Istilah tidak konsisten antara presentasi dan laporan | Sinkronkan sebelum deadline |

### 9.3 Cara Mengurangi Risiko

- Gunakan narasi yang aman dan tidak overclaim
- Siapkan backup video dan screenshot sebelum demo
- Pilih satu mode final dan jangan diubah saat demo
- Jujur soal batas sistem — penguji menghargai kejujuran

---

## 10. Saran Narasi Akademik yang Aman

### Untuk Menjelaskan Posisi Sistem

> *"MoonHarvest merupakan prototype sistem monitoring visual pertanian berbasis drone yang memanfaatkan kombinasi analisis citra berbasis HSV dan model klasifikasi untuk mengidentifikasi kondisi area pertanian secara visual, serta menampilkan hasilnya pada antarmuka monitoring real-time."*

### Untuk Label atau Class

> *"Label yang ditampilkan oleh sistem merupakan indikasi visual berbasis citra yang digunakan untuk membantu monitoring awal kondisi area pertanian. Sistem ini belum dimaksudkan sebagai pengganti pengukuran agronomis langsung."*

### Untuk Field Health Index

> *"Field Health Index merupakan indeks komposit berbasis distribusi area kelas deteksi, dihitung sebagai pengurangan skor keparahan tertimbang dari 100. Nilai ini bukan pengukuran fisiologis langsung, melainkan proxy visual untuk membantu prioritisasi monitoring."*

### Untuk Keterbatasan

> *"Prototype ini masih berada pada tahap pengembangan, terutama pada penyempurnaan dataset UAV, konsistensi class, dan pematangan dashboard. Namun, inti sistem deteksi dan monitoring visual telah berhasil diimplementasikan dan diuji pada footage UAV nyata."*

### Untuk Fusion

> *"Mekanisme fusion adaptif berbasis confidence digunakan untuk menggabungkan keputusan dari dua sumber: analisis warna HSV dan klasifikasi model deep learning. Bobot fusion disesuaikan per kelas berdasarkan karakteristik masing-masing pendekatan pada domain UAV pertanian."*

---

## 11. Checklist Kerja Harian

### Checklist H-2 sampai Deadline

- [ ] Pilih satu mode final
- [ ] Pilih class final (maksimal 6)
- [ ] Pilih video utama demo
- [ ] Pilih video cadangan
- [ ] Bekukan parameter ke config file
- [ ] Rapikan tampilan overlay
- [ ] Tambahkan summary sederhana di output
- [ ] Sinkronkan label UI dan laporan
- [ ] Simpan screenshot terbaik (3–5 gambar)
- [ ] Simpan output video terbaik (H.264)
- [ ] Latihan narasi presentasi (30s / 1m / 3m)
- [ ] Final check: jalankan dari nol tanpa error

### Checklist Mingguan Setelah Deadline

- [ ] Audit dataset per class
- [ ] Catat bug dan error yang muncul saat demo
- [ ] Evaluasi confusion antar class
- [ ] Review rule fusion berdasarkan hasil uji
- [ ] Rapikan dokumentasi parameter
- [ ] Susun backlog revisi model

### Checklist Bulanan

- [ ] Tambah data UAV (gunakan `tools/label_frames.py`)
- [ ] Retrain model revisi
- [ ] Bandingkan hasil model lama vs baru (confusion matrix)
- [ ] Rapikan dashboard zone analysis
- [ ] Dokumentasikan hasil eksperimen
- [ ] Susun versi laporan yang lebih kuat

---

## 12. Kesimpulan Akhir

MoonHarvest memiliki dasar yang sangat baik untuk berkembang. Saat ini sistem sudah cukup kuat untuk disebut sebagai **prototype fungsional** — memiliki pipeline deteksi, fusion adaptif, dashboard multi-halaman, output nyata (video, JSON, CSV), dan dokumentasi yang jelas.

**Dalam waktu dekat**, fokus terbaik adalah:
1. Memastikan sistem aman untuk deadline dan demo
2. Menstabilkan mode, menyederhanakan class, merapikan output
3. Menyiapkan bahan cadangan

**Untuk jangka ke depan**, tiga hal besar yang harus dikerjakan:
1. **Konsolidasi class** — buat class yang lebih aman, relevan, dan bisa dilatih
2. **Penguatan dataset UAV** — domain mismatch adalah masalah utama model
3. **Pemantapan dashboard dan evaluasi teknis** — buat sistem terasa matang

> Strategi terbaik adalah tetap realistis: selesaikan yang paling penting sekarang, lalu bangun yang lebih kuat setelah deadline. Dengan pendekatan itu, kamu tidak hanya mengejar selesai — tetapi membangun fondasi yang benar untuk pengembangan berikutnya.

---

*Dokumen ini dibuat 2026-06-21 sebagai panduan pengerjaan MoonHarvest jangka pendek hingga jangka panjang.*  
*Untuk referensi teknis lengkap, lihat [MOONHARVEST_DOCS.md](MOONHARVEST_DOCS.md).*
