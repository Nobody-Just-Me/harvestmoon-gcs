# MoonHarvest Demo Results

**Tanggal:** 2026-06-21  
**Mode:** Fusion HSV + YOLO v1  
**Video sumber:** gabung.mp4 (1646 frame, 55 detik, 1920×1080 @ 30fps)

---

## Hasil Deteksi

| Metrik | Nilai |
|--------|-------|
| Field Health Index (FUSED) | **78.9%** — Status: BAIK |
| Field Health Index (HSV) | 90.0% |
| Field Health Index (YOLO) | 59.4% |
| Agreement HSV-YOLO | 27.6% |
| Rata-rata region/frame | 31.2 |
| Durasi proses | 462.7 detik (~7.7 menit) |

## File Demo

| File | Keterangan |
|------|-----------|
| `gabung_fusion_summary.json` | Statistik lengkap hasil deteksi |
| `FINAL_CONFIG.json` | Parameter yang dikunci untuk demo |
| `screenshot_1_frame329.jpg` | Screenshot ~20% video |
| `screenshot_2_frame823.jpg` | Screenshot ~50% video |
| `screenshot_3_frame1234.jpg` | Screenshot ~75% video |

## Video Output (di demo_videos/fusion_gabung/)

| File | Keterangan |
|------|-----------|
| `gabung_fused.mp4` | 3-panel: YOLO \| FUSED \| HSV (154 MB) |
| `gabung_fused_only.mp4` | Panel fused saja (91 MB) |

## Cara Menjalankan Ulang

```bash
cd /home/fawwazfa/Program/Harvestmoon
bash run_demo.sh fusion gabung.mp4
```
