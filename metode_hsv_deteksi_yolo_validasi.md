# Metode Dua Tahap: HSV sebagai Deteksi Utama dan YOLO sebagai Validasi Tahap Kedua

Dokumen ini menjelaskan pendekatan yang direkomendasikan untuk sistem MoonHarvest ketika deteksi berbasis **HSV** terbukti lebih stabil dan lebih akurat daripada **YOLO**, terutama pada kondisi footage UAV dan waktu pengembangan yang sangat terbatas. Dalam situasi seperti ini, pendekatan yang paling aman dan paling realistis adalah menggunakan **HSV sebagai tahap deteksi utama**, kemudian memakai **YOLO sebagai tahap validasi atau konfirmasi** terhadap area yang telah ditemukan oleh HSV.

Pendekatan ini dipilih karena HSV dan YOLO memiliki karakteristik yang berbeda. HSV bekerja dengan mendeteksi pola warna, distribusi piksel, dan dominasi area tertentu, sedangkan YOLO bergantung pada hasil pembelajaran model terhadap bentuk atau pola objek. Jika model YOLO belum cukup sesuai dengan domain UAV, maka kinerjanya dapat menurun dan tidak sebaik HSV. Oleh karena itu, arsitektur dua tahap ini memungkinkan sistem tetap stabil dengan tetap memanfaatkan kelebihan YOLO sebagai pemeriksa tambahan.

## Konsep Dasar Metode

Metode ini dibangun dengan alur berikut:

1. **Frame diambil dari video atau kamera.**
2. **Frame dipreprocess** untuk memperbaiki kualitas input.
3. **HSV dijalankan terlebih dahulu** untuk menemukan kandidat area target.
4. **Kandidat area dari HSV diubah menjadi contour atau bounding box.**
5. **Setiap area kandidat di-crop** dari frame utama.
6. **Crop tersebut dikirim ke YOLO** untuk divalidasi.
7. **Hasil HSV dan YOLO digabung** dalam logika fusion.
8. **Keputusan akhir dihaluskan** dengan temporal smoothing.
9. **Hasil ditampilkan** dalam overlay dan log output.

Secara ringkas, alurnya dapat ditulis sebagai berikut:

```python
frame
 -> preprocess
 -> hsv_detect
 -> candidate_regions
 -> crop each region
 -> yolo_validate(crop)
 -> fuse result
 -> temporal smoothing
 -> final decision
```

Pendekatan ini sering disebut sebagai **two-stage pipeline** atau **region proposal + validation**, di mana HSV bertindak sebagai pencari kandidat dan YOLO berperan sebagai pemeriksa tahap kedua.

## Alasan Pendekatan Ini Layak Digunakan

Pendekatan HSV sebagai deteksi utama dan YOLO sebagai validasi memiliki beberapa keunggulan penting. Pertama, sistem menjadi lebih ringan karena YOLO tidak perlu melakukan pencarian pada seluruh frame. Kedua, area yang diperiksa oleh YOLO sudah disaring terlebih dahulu oleh HSV, sehingga noise dapat dikurangi. Ketiga, jika YOLO tidak stabil, sistem tetap bisa berjalan karena keputusan utama tidak sepenuhnya bergantung pada YOLO. Keempat, metode ini lebih aman untuk deadline singkat karena lebih mudah diimplementasikan dibandingkan membangun full detection fusion yang sepenuhnya baru.

## Peran YOLO pada Tahap Kedua

Dalam arsitektur ini, YOLO tidak dijadikan komponen utama, melainkan komponen validasi. Peran YOLO pada tahap kedua dapat dibedakan menjadi tiga fungsi utama.

Fungsi pertama adalah **validator positif**, yaitu memeriksa apakah area yang ditemukan HSV memang sesuai dengan target yang diinginkan. Jika YOLO mendukung, maka kepercayaan hasil meningkat.

Fungsi kedua adalah **confidence booster**, yaitu menaikkan tingkat keyakinan keputusan akhir ketika HSV dan YOLO sama-sama menunjukkan indikasi positif. Dalam kasus ini, hasil akhir menjadi lebih meyakinkan untuk ditampilkan pada demo atau presentasi.

Fungsi ketiga adalah **penolak false positive**, yaitu membantu menurunkan atau menandai area yang dideteksi HSV tetapi tampak tidak sesuai menurut model YOLO. Namun, untuk deadline yang sangat dekat, fungsi ini sebaiknya tidak dibuat terlalu agresif karena dapat justru merusak kestabilan hasil HSV yang sudah baik.

## Logika Keputusan yang Direkomendasikan

Untuk kondisi saat ini, keputusan akhir sebaiknya tetap didominasi oleh HSV. YOLO dipakai untuk mengonfirmasi atau menambah keyakinan, bukan sebagai hakim mutlak. Salah satu logika sederhana yang aman adalah sebagai berikut:

```python
if hsv_detected:
    if yolo_valid:
        status = "confirmed"
    else:
        status = "hsv-primary"
else:
    status = "negative"
```

Jika diperlukan versi yang sedikit lebih detail, logika dapat menggunakan skor HSV dan confidence YOLO:

```python
if hsv_score >= 0.60:
    if yolo_conf >= 0.50:
        status = "confirmed"
        final_score = 0.8 * hsv_score + 0.2 * yolo_conf
    else:
        status = "hsv-primary"
        final_score = 0.9 * hsv_score + 0.1 * yolo_conf
elif hsv_score >= 0.40:
    if yolo_conf >= 0.60:
        status = "confirmed"
        final_score = 0.7 * hsv_score + 0.3 * yolo_conf
    else:
        status = "review"
        final_score = 0.8 * hsv_score + 0.2 * yolo_conf
else:
    if yolo_conf >= 0.75:
        status = "review"
        final_score = 0.4 * hsv_score + 0.6 * yolo_conf
    else:
        status = "negative"
        final_score = 0.0
```

Logika ini memberi ruang bagi HSV untuk tetap menjadi penentu utama, sambil memanfaatkan YOLO sebagai penguat keputusan.

## Hal Teknis yang Perlu Diperhatikan

Agar tahap kedua bekerja dengan baik, area crop yang dikirim ke YOLO harus dipersiapkan dengan benar. Bounding box HSV sebaiknya diberi **padding tambahan** agar objek tidak terpotong terlalu sempit. Sebagai contoh, padding 10–20 persen dari ukuran bounding box biasanya cukup aman.

```python
pad = 0.15
px = int(w * pad)
py = int(h * pad)

x1 = max(0, x - px)
y1 = max(0, y - py)
x2 = min(frame.shape[1], x + w + px)
y2 = min(frame.shape[0], y + h + py)

crop = frame[y1:y2, x1:x2]
```

Selain itu, tidak semua region HSV perlu dikirim ke YOLO. Region yang sangat kecil, terlalu tipis, terlalu dekat pinggir frame, atau memiliki skor HSV yang sangat rendah sebaiknya dibuang terlebih dahulu agar proses validasi lebih efisien dan lebih stabil.

## Temporal Smoothing

Keputusan akhir sebaiknya tidak hanya didasarkan pada satu frame. Untuk meningkatkan kestabilan, sistem perlu menggunakan **temporal smoothing** pada beberapa frame terakhir, misalnya 3–5 frame. Jika suatu region konsisten terdeteksi positif oleh HSV dan sesekali dikonfirmasi YOLO, maka statusnya dapat dipertahankan agar tidak mudah berubah-ubah akibat flicker, noise, atau perubahan pencahayaan sesaat.

Dengan smoothing, hasil visual pada demo akan tampak lebih stabil dan lebih profesional.

## Status Output yang Direkomendasikan

Agar hasil mudah dipahami pada tampilan overlay maupun laporan, sistem dapat menggunakan beberapa status berikut:

- **confirmed**, yaitu HSV mendeteksi dan YOLO memvalidasi.
- **hsv-primary**, yaitu HSV mendeteksi dengan kuat tetapi YOLO belum mendukung penuh.
- **review**, yaitu ada indikasi tetapi belum cukup kuat untuk dipastikan.
- **negative**, yaitu tidak ada bukti yang cukup dari kedua tahap.

Struktur status seperti ini sangat berguna untuk presentasi karena menunjukkan bahwa sistem tidak bekerja secara biner sederhana, tetapi memiliki tingkat keyakinan yang lebih realistis.

## Kelebihan Metode Ini untuk Deadline Singkat

Untuk deadline yang sangat dekat, metode ini sangat menguntungkan karena tidak menuntut retraining besar pada YOLO. Sistem tetap dapat memanfaatkan YOLO sebagai komponen cerdas tahap kedua tanpa harus menggantungkan seluruh performa pada model tersebut. Dengan kata lain, jika YOLO masih belum stabil pada data UAV, sistem tetap dapat tampil baik selama HSV sudah cukup kuat.

Dari sisi presentasi, pendekatan ini juga mudah dijelaskan. Penjelasan yang paling aman adalah bahwa sistem menggunakan **HSV untuk mendeteksi kandidat area berdasarkan warna**, lalu menggunakan **YOLO untuk memvalidasi kandidat tersebut sebagai tahap kedua**. Penjelasan ini terdengar matang secara teknis sekaligus realistis terhadap kondisi pengembangan yang tersedia.

## Kesimpulan

Pendekatan **HSV sebagai deteksi utama dan YOLO sebagai validasi tahap kedua** merupakan solusi yang sangat tepat ketika performa HSV lebih stabil daripada YOLO dan waktu implementasi sangat terbatas. Dalam metode ini, HSV bertugas mencari kandidat area secara cepat dan konsisten, sedangkan YOLO berfungsi sebagai validator untuk menambah kepercayaan hasil. Keputusan akhir tetap harus lebih berat ke HSV, sementara YOLO digunakan untuk mengonfirmasi, memperkuat, atau menandai hasil tertentu.

Dengan arsitektur dua tahap ini, sistem menjadi lebih ringan, lebih stabil, lebih mudah dijelaskan, dan lebih aman untuk kebutuhan demo atau presentasi awal. Oleh karena itu, metode ini sangat direkomendasikan sebagai desain final MoonHarvest dalam kondisi deadline yang tersisa sangat singkat.
