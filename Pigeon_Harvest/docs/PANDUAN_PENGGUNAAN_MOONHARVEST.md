# Panduan Penggunaan MoonHarvest

**Precision Agriculture Monitoring System — Cross-Platform Ground Control Station dengan Real-Time Computer Vision**

*Tim EFRISA · TEKNOFEST Agricultural Technologies Competition · Team ID 783316*

MoonHarvest mengubah UAV berbiaya rendah menjadi pemindai kesehatan tanaman real-time. Satu aplikasi C# (Uno Platform) berjalan native di Windows, Linux, dan Android, menerima video serta telemetri MAVLink dari UAV, menjalankan inferensi YOLO on-device, dan menampilkan peta kesehatan vegetasi secara langsung — sepenuhnya offline. Panduan ini menjelaskan cara mengoperasikan seluruh fitur tersebut di lapangan, dari koneksi pertama hingga membaca laporan hasil panen.

- **Untuk**: Petani smallholder (1–10 ha) & menengah (10–50 ha), koperasi tani, konsultan agronomi
- **Protokol**: MAVLink · TCP / UDP / Serial
- **Platform**: Windows, Linux, Android — satu basis kode
- **UAV Kompatibel**: UAV ArduPilot/PX4 berbiaya rendah (kelas USD 300–800)

---

## Daftar Isi

1. [Tentang MoonHarvest](#1-tentang-moonharvest)
2. [Memulai & Koneksi](#2-memulai--koneksi)
3. [Dasbor & HUD](#3-dasbor--hud)
4. [Kalibrasi Drone](#4-kalibrasi-drone)
5. [Deteksi Kesehatan Tanaman (Computer Vision)](#5-deteksi-kesehatan-tanaman-computer-vision)
6. [Perencanaan Misi & Survei Otomatis](#6-perencanaan-misi--survei-otomatis)
7. [Parameter Drone](#7-parameter-drone)
8. [Fitur Keselamatan](#8-fitur-keselamatan)
9. [Pengaturan Aplikasi](#9-pengaturan-aplikasi)
10. [Statistik & Laporan Panen](#10-statistik--laporan-panen)
11. [Mode Edge — Zero-Internet Edge AI](#11-mode-edge--zero-internet-edge-ai)
12. [Asisten PIA](#12-asisten-pia)
13. [Pemecahan Masalah](#13-pemecahan-masalah)
14. [Kesesuaian dengan Proposal Kompetisi](#14-kesesuaian-dengan-proposal-kompetisi)

---

## 1. Tentang MoonHarvest

MoonHarvest adalah *Ground Control Station* (GCS) lintas platform yang mengintegrasikan computer vision real-time dengan teknologi UAV untuk pemantauan tanaman. Alih-alih alur kerja lama — terbang, ambil kartu SD, lalu analisis pasca-terbang di software cloud yang mahal — MoonHarvest menganalisis video langsung dari UAV secara instan, di dalam GCS itu sendiri, tanpa koneksi internet.

| Fitur Utama | Deskripsi |
|---|---|
| Deteksi Kesehatan Tanaman | YOLO on-device, 4 kelas kondisi vegetasi, ditampilkan langsung di atas video. |
| Zero-Internet Edge AI | Seluruh inferensi berjalan lokal di perangkat — tidak bergantung cloud. |
| Cross-Platform | Satu basis kode C# berjalan native di Windows, Linux, dan Android. |
| Misi & Geofence | Survei otonom berbasis waypoint dengan penegakan batas area. |
| Telemetri MAVLink | Sikap, GPS, ketinggian, airspeed, dan mode terbang secara real-time. |

---

## 2. Memulai & Koneksi

Sebelum data telemetri dan video dapat ditampilkan, MoonHarvest perlu tersambung ke flight controller UAV melalui salah satu dari tiga jenis koneksi MAVLink.

### Menyambungkan UAV (Connect Dialog)

1. Buka aplikasi, lalu klik tombol **Connect** di sidebar atau topbar.
2. Pilih jenis koneksi: **TCP**, **UDP**, atau **Serial / USB**.
3. Isi alamat sesuai jenis koneksi (lihat tabel di bawah), lalu klik **Connect**.
4. Status koneksi akan muncul di status bar dasbor begitu heartbeat MAVLink pertama diterima.

| Jenis Koneksi | Digunakan Untuk | Yang Diisi |
|---|---|---|
| TCP | Simulator (SITL), koneksi jaringan tetap | Alamat IP + port |
| UDP | Telemetry radio via companion computer | Alamat IP + port |
| Serial / USB | Kabel langsung ke flight controller | Nama port + baud rate |

> **Auto Connect.** Aktifkan opsi ini di **Settings** agar MoonHarvest otomatis menyambung kembali ke UAV terakhir setiap kali aplikasi dibuka — berguna untuk operasi lapangan berulang di koperasi maupun demplot.

---

## 3. Dasbor & HUD

Halaman **Dashboard** adalah layar operasi utama untuk memantau kondisi UAV secara langsung selama survei.

- **Attitude Indicator** — kemiringan (roll) dan sudut angguk (pitch) UAV.
- **Heading Indicator** — arah hadap UAV terhadap utara.
- **Air Speed Indicator** — kecepatan UAV relatif terhadap udara.
- **Status Bar** — status koneksi, mode terbang, dan level baterai.
- **Peta / Tracker** — posisi UAV secara langsung dengan opsi **Follow Vehicle**, serta progres area yang sudah tercakup (area scanned).

Kualitas sinyal koneksi dipantau terus-menerus; bila kualitas menurun, indikator status bar berubah agar operator menyadarinya sebelum sinyal terputus total — penting saat survei di ladang jauh dari infrastruktur jaringan.

---

## 4. Kalibrasi Drone

Semua kalibrasi dilakukan di satu halaman **Calibration**, wajib diselesaikan sebelum penerbangan pertama atau setelah pemasangan komponen baru.

### Kompas

1. Pilih perangkat kompas dari daftar (klik **Refresh** bila UAV memiliki lebih dari satu kompas).
2. Klik **Start Calibration**, lalu putar UAV perlahan ke seluruh sumbu sesuai instruksi di layar.
3. Setelah progres 100%, klik **Accept** untuk menyimpan offset, atau **Cancel** untuk mengulang.

### Accelerometer, Gyro & Barometer

Ikuti instruksi posisi UAV yang muncul berurutan (datar, miring kiri, miring kanan, terbalik, dst.) — setiap posisi dikonfirmasi setelah UAV diam sempurna.

### Radio (RC) & Flight Mode

Gerakkan setiap stick/switch remote control untuk merekam rentang channel, lalu tetapkan mode terbang (Stabilize, Auto, RTL) pada bagian **Flight Mode Mapping**.

### ESC & Uji Motor (Motor Test)

> ⚠️ **Lepas baling-baling sebelum menguji motor.** Fitur ini memutar motor langsung sesuai persentase throttle dan durasi yang ditentukan.

Jalankan uji motor satu per satu untuk memastikan arah putar dan urutan sudah benar. Tombol **Emergency Stop** selalu tersedia untuk mematikan seluruh motor secara instan.

### PID Tuning & Pengaturan Waypoint

Penyetelan PID (Roll, Pitch, Yaw, Velocity) untuk airframe Copter maupun Plane, serta parameter default waypoint (kecepatan, radius pencapaian titik) untuk pengguna lanjutan.

---

## 5. Deteksi Kesehatan Tanaman (Computer Vision)

Halaman **Camera** menampilkan video langsung dari kamera onboard UAV dengan analisis kesehatan tanaman berjalan on-device — inilah inti dari MoonHarvest.

### Empat Kelas Deteksi

Model YOLOv8n (ONNX Runtime) mengklasifikasikan setiap zona lahan ke salah satu dari empat kondisi, dengan bounding box dan skor keyakinan ditampilkan langsung di atas video:

| Kelas | Arti bagi Petani |
|---|---|
| **Lush Green** | Vegetasi sehat, tidak perlu tindakan. |
| **Inconsistent Growth** | Pertumbuhan tidak merata — perlu diperiksa. |
| **Drought/Severe Stress** | Kekeringan atau stres berat — prioritas penyiraman/perawatan. |
| **Bare Soil/Gap** | Tanah kosong atau celah tanaman — kandidat penyulaman. |

### Langkah Penggunaan

1. Buka halaman **Camera** setelah UAV terhubung dan video aktif.
2. Overlay deteksi tampil otomatis: jumlah deteksi per kelas, FPS, dan tingkat keyakinan rata-rata ditampilkan di sudut layar.
3. Atur ambang batas keyakinan (confidence threshold) di halaman **AI Settings** untuk menyaring deteksi yang kurang meyakinkan.
4. Ganti model deteksi (file `.onnx`) di **AI Settings** untuk beralih ke musim, jenis tanaman, atau hama yang berbeda — tanpa perlu update aplikasi.
5. Gunakan tombol rekam untuk menyimpan video sesi survei untuk arsip atau tinjauan ulang.

> **Kinerja di lapangan.** Pipeline dioptimalkan untuk berjalan >15 FPS pada laptop standar maupun Android kelas menengah, dengan target F1-score >80% dan mAP@0.5 ≥0.85 pada model terlatih.

---

## 6. Perencanaan Misi & Survei Otomatis

Halaman **Mission Planner** digunakan untuk menyusun jalur survei otonom di atas peta — mengganti penyusuran manual 4–6 jam per 10 ha dengan satu penerbangan otomatis berdurasi menit.

1. Klik pada peta untuk menambahkan titik waypoint; klik dua kali sebuah titik untuk membuka **Waypoint Edit Dialog** dan mengatur ketinggian, kecepatan, atau aksi di titik tersebut.
2. Gunakan **Undo** / **Redo** untuk membatalkan atau mengulang perubahan susunan waypoint.
3. Klik **Import** untuk memuat jalur waypoint yang sudah disiapkan sebelumnya dari file.
4. Klik **Upload to Drone** untuk mengirim seluruh misi ke flight controller, atau **Download from Drone** untuk menarik misi yang sudah tersimpan.
5. Setelah misi berjalan, zona bermasalah (dari hasil deteksi bagian 5) otomatis ditandai pada peta agar dapat langsung ditindaklanjuti hari yang sama.

---

## 7. Parameter Drone

Halaman parameter membaca dan menulis konfigurasi flight controller secara langsung dari UAV.

Saat halaman dibuka, progres menunjukkan jumlah parameter yang sudah dimuat dari total yang tersedia. Ubah nilai dengan hati-hati — nilai tidak valid akan ditolak disertai pesan kesalahan.

> ⚠️ **Untuk pengguna lanjutan.** Mengubah parameter secara sembarangan dapat memengaruhi kestabilan terbang. Catat nilai lama sebelum mengubah parameter penting.

---

## 8. Fitur Keselamatan

### Geofence

Aktifkan geofence di **Settings → Map** dan tentukan radiusnya. Batas geofence tergambar di peta; jika UAV melewati batas tersebut, sistem menampilkan notifikasi pelanggaran, dan notifikasi pemulihan saat UAV kembali ke dalam batas — memastikan survei tetap dalam area lahan yang direncanakan.

### Pemutusan Motor Darurat (Emergency Motor Cutoff)

Tombol darurat ini tersedia pada halaman kalibrasi/uji motor dan langsung menghentikan seluruh motor UAV — gunakan hanya dalam situasi darurat di darat, bukan saat UAV sedang terbang.

---

## 9. Pengaturan Aplikasi

Halaman **Settings** menyimpan seluruh preferensi aplikasi secara lokal di perangkat — tidak memerlukan akun atau koneksi server.

| Kategori | Isi |
|---|---|
| Koneksi | Jenis koneksi, IP/port, port serial, baud rate, dan Auto Connect. |
| Peta | Follow vehicle, jenis peta, geofence, dan nilai default waypoint. |
| Tampilan | Bahasa, tema terang/gelap, opsi lanjutan, dan suara peringatan. |
| AI | Ambang batas keyakinan deteksi dan pemilihan model vision (`.onnx`). |

---

## 10. Statistik & Laporan Panen

Halaman **Stats** menampilkan ringkasan statistik penerbangan dan sebaran kondisi tanaman per sesi survei. Halaman **Reports Harvest** menyajikan laporan hasil panen — termasuk peta zona (Lush Green / Inconsistent Growth / Drought-Stress / Bare Soil) beserta persentase luas area masing-masing, rekomendasi tindak lanjut, dan log penerbangan (Tlog) yang dapat diekspor untuk arsip koperasi atau penyuluh pertanian.

---

## 11. Mode Edge — Zero-Internet Edge AI

**Edge Mode** adalah fondasi dari desain MoonHarvest: seluruh inferensi YOLO, analisis vegetasi, dan perencanaan misi berjalan sepenuhnya on-device, tanpa bergantung pada cloud atau koneksi internet. Ini memungkinkan petani atau operator koperasi mendapatkan hasil analisis instan langsung di ladang, sekalipun tanpa sinyal data sama sekali — berbeda dari solusi berbasis cloud yang mengharuskan unggah data terlebih dahulu.

---

## 12. Asisten PIA

Panel **PIA** dapat dibuka dari sidebar sebagai jendela geser berisi asisten chat dengan perintah cepat, membantu operator mencari fungsi atau informasi tanpa berpindah halaman — mempercepat pelatihan operator baru di koperasi tani.

---

## 13. Pemecahan Masalah

| Gejala | Langkah Pemeriksaan |
|---|---|
| Tidak bisa Connect | Periksa jenis koneksi dan alamat/port yang diisi; pastikan UAV menyala dan kabel/telemetry radio terpasang benar. |
| Status koneksi sering putus | Perhatikan indikator kualitas koneksi di status bar; pindahkan lokasi atau periksa interferensi sinyal. |
| Parameter gagal tersimpan | Periksa pesan kesalahan yang muncul — nilai kemungkinan di luar rentang yang diizinkan flight controller. |
| Kalibrasi kompas gagal | Ulangi di area terbuka jauh dari logam/interferensi magnetik, pastikan rotasi dilakukan pada seluruh sumbu. |
| Video kamera tidak muncul | Periksa koneksi kamera pada halaman Camera dan pastikan UAV/companion device terhubung. |
| FPS deteksi rendah | Turunkan resolusi input model atau tutup aplikasi lain yang membebani perangkat; target operasional adalah >15 FPS. |

---

## 14. Kesesuaian dengan Proposal Kompetisi

Tabel berikut memetakan klaim utama pada proposal dan presentasi EFRISA ke fitur yang benar-benar dapat dioperasikan di aplikasi, sebagaimana dijelaskan pada bab-bab di atas — sebagai bukti bahwa prototipe sudah berfungsi sesuai yang diajukan.

| Klaim di Proposal/PPT | Dibuktikan di Manual Bab |
|---|---|
| Real-time CV analysis, YOLO built-in | Bab 5 — Deteksi Kesehatan Tanaman |
| Cross-platform Windows/Linux/Android, satu codebase | Bab 1, 3, 5 — seluruh fitur berjalan identik di ketiga platform |
| Zero-Internet Edge AI | Bab 11 — Mode Edge |
| MAVLink telemetry (attitude, GPS, altitude, airspeed, flight mode) | Bab 2, 3 — Koneksi & Dasbor/HUD |
| Waypoint & geofence dengan boundary enforcement | Bab 6, 8 — Misi & Fitur Keselamatan |
| 4-class detection (Lush Green, Inconsistent Growth, Drought/Severe Stress, Bare Soil/Gap) | Bab 5 — Empat Kelas Deteksi |
| Model & threshold dinamis (future-proof adaptability) | Bab 5 & 9 — AI Settings |
| Kompatibel UAV berbiaya rendah ($300–800) | Bab 2 — mendukung TCP/UDP/Serial standar MAVLink tanpa hardware proprietary |

---

*MoonHarvest — Panduan Pengguna. Disusun oleh Tim EFRISA untuk operator lapangan, koperasi tani, dan tim juri TEKNOFEST.*
