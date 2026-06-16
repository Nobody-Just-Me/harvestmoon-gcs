# HSV vs YOLO — Perbandingan untuk MoonHarvest

Program `moonharvest_hsv.py` **100% HSV / computer-vision klasik, tanpa YOLO**.
Dokumen ini membandingkannya dengan YOLO secara jujur, plus cara memadukannya.

## Ringkasan

| Aspek | HSV (engine `stat`) | YOLO (mis. YOLOv8-seg) |
|---|---|---|
| Butuh dataset berlabel | Tidak (cukup beberapa swatch) | Ya, ratusan–ribuan gambar dianotasi |
| Butuh training GPU | Tidak | Ya (idealnya GPU) |
| Ukuran model | ~7 KB (`model.json`) | 6–50+ MB (`.pt`/`.onnx`) |
| Kecepatan di CPU/tablet | Cepat (tanpa NN) | Lebih berat; perlu ONNX/NCNN agar ringan |
| Dependensi | OpenCV + NumPy | PyTorch/Ultralytics (besar) |
| Akurasi batas warna mirip | Sedang–baik (tergantung kalibrasi) | Lebih tinggi bila data cukup |
| Objek (rumah, jalan, orang) | Disingkirkan via morfologi, bukan dikenali | Bisa dikenali sebagai kelas tersendiri |
| Generalisasi lahan/cahaya baru | Perlu re-kalibrasi/train ringan | Lebih tahan bila data latih beragam |
| Transparan / mudah di-debug | Sangat (aturan eksplisit) | Black-box |
| Cocok untuk | Prototipe cepat, perangkat budget, severity per-piksel | Akurasi tinggi, deteksi objek, produk akhir |

## Kapan pilih yang mana
- **HSV saja**: anggaran data/komputasi terbatas, butuh peta kondisi per-piksel
  + Field Health, jalan di lapangan tanpa internet/GPU. (Kasus kamu sekarang.)
- **YOLO saja**: ada dataset berlabel besar + GPU, butuh deteksi objek/kotak
  dan ketahanan tinggi ke variasi.
- **Hybrid (terbaik untuk CDR TEKNOFEST)**: YOLO melokalisasi area
  `vegetation` vs `bare_soil`; HSV+ExG menilai kondisi/severity di dalamnya.
  Lebih akurat dari HSV murni, lebih murah data dari YOLO penuh.

## Catatan kondisi saat ini
Di lingkungan ini **tanpa internet dan tanpa PyTorch/ultralytics**, jadi YOLO
tidak bisa dilatih/dijalankan di sini. Karena itu perbandingan angka langsung
belum bisa dibuat di sini. Yang sudah disiapkan agar kamu bisa membandingkan
sendiri di mesinmu:

1. **`yolo_compare.py auto-label`** — mengubah mask HSV menjadi dataset
   YOLOv8-seg otomatis (bootstrap, tanpa anotasi manual). Inilah jalur tercepat
   menyiapkan data YOLO dari hasil HSV ini.
2. **`yolo_compare.py compare`** — menjalankan YOLO terlatih + HSV pada input
   sama dan menempelnya berdampingan (gambar/video) untuk perbandingan visual.

### Langkah praktis membandingkan
```bash
# 1) buat dataset YOLO dari frame, label dibuat HSV (engine stat)
python3 yolo_compare.py auto-label -i frames/ -o yolo_dataset --model model.json

# 2) latih YOLOv8-seg (butuh GPU/PyTorch)
pip install ultralytics
yolo segment train data=yolo_dataset/data.yaml model=yolov8n-seg.pt epochs=100 imgsz=960

# 3) bandingkan berdampingan
python3 yolo_compare.py compare -i derr.mp4 --weights runs/segment/train/weights/best.pt \
        --model model.json -o cmp --fps 2
```

> Catatan: dataset hasil auto-label mewarisi keterbatasan HSV (label = prediksi
> HSV, bukan kebenaran lapangan). Untuk akurasi sungguhan, perbaiki sebagian
> label secara manual sebelum melatih YOLO.
