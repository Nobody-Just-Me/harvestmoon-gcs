# Demo Videos untuk HarvestmoonGCS

Folder ini berisi video demo yang sudah diproses dengan deteksi kesehatan tanaman.

## Video yang Tersedia

### YDXJ_fused_only_detected.mp4
- **Sumber**: Video hasil fusion YDXJ
- **Resolusi**: 480x270 (output scale 0.5)
- **Frame Rate**: 10 FPS
- **Total Frame**: 140 frames
- **Durasi**: ~14 detik
- **Prosesor**: MoonHarvest HSV-ONNX detection system

**Hasil Deteksi:**
- Lush Green (Hijau Subur): 365 deteksi (97.3%)
- Bare Soil/Gap (Tanah Terbuka): 7 deteksi (1.9%)
- Drought/Severe Stress: 3 deteksi (0.8%)

## Cara Menggunakan di GCS

### Metode 1: Melalui Camera Page
1. Buka aplikasi HarvestmoonGCS
2. Navigasi ke **Camera Page**
3. Pilih tab **"Video File"**
4. Klik tombol **"Browse"**
5. Pilih file `YDXJ_fused_only_detected.mp4`
6. Klik **"Start Camera"**

Video akan ditampilkan dengan overlay deteksi yang sudah diproses.

### Metode 2: Input Manual Path
1. Buka aplikasi HarvestmoonGCS
2. Navigasi ke **Camera Page**
3. Pilih tab **"Video File"**
4. Ketik path lengkap di textbox:
   ```
   /home/fawwazfa/Program/Harvestmoon/Pigeon_Harvest/HarvestmoonGCS/Assets/demo_videos/YDXJ_fused_only_detected.mp4
   ```
   atau path relatif:
   ```
   Assets/demo_videos/YDXJ_fused_only_detected.mp4
   ```
5. Klik **"Start Camera"**

## Catatan Teknis

- Video ini sudah memiliki bounding box dan label deteksi yang di-render langsung di dalam video
- Tidak perlu menjalankan deteksi ulang saat memutar di GCS
- Cocok untuk demo presentasi atau testing UI tanpa beban komputasi inference
- Format: MP4 (H.264 codec)

## Membuat Video Demo Baru

Untuk membuat video demo dengan deteksi dari video mentah:

```bash
cd /home/fawwazfa/Program/Harvestmoon
python3 run_detection_video.py <input_video.mp4> \
  --output Pigeon_Harvest/HarvestmoonGCS/Assets/demo_videos/<output_name>.mp4 \
  --min-conf 0.3 \
  --output-scale 0.5 \
  --demo
```

**Parameter:**
- `--min-conf`: Threshold confidence minimum (default: 0.3)
- `--output-scale`: Scale resolusi output (0.5 = setengah resolusi, lebih cepat)
- `--demo`: Aktifkan mode demo dengan label sesuai proposal (4 kelas)

## Troubleshooting

Jika video tidak muncul:
1. Pastikan path file benar
2. Cek apakah file memiliki permission read
3. Verifikasi format video didukung (MP4, MOV, AVI)
4. Lihat log di console untuk error detail

Untuk video RTSP stream atau live camera, gunakan tab yang sesuai di Camera Page.
