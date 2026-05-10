# YOLO Models for Pigeon Harvest

Folder ini berisi model YOLO dan class-name yang dipakai oleh `HarvestFunctionalService` untuk deteksi objek offline pada live camera dan image analysis.

## File yang sudah tersedia

- `yolov8n.onnx` (~12 MB): bobot resmi YOLOv8n (pre-trained COCO, 80 kelas). Siap dipakai langsung.
- `classes-yolov8n-coco.txt`: daftar 80 kelas COCO untuk `yolov8n.onnx`.
- `classes-yolov8n-agri-basic.txt`: daftar 12 kelas agriculture (healthy_crop, crop_stress, dry_soil, water_stress, dst) untuk model custom agri.
- `coco.names`, `vegetation.names`: arsip label lama, di-preserve untuk referensi.

## Cara kerja auto-load

`HarvestFunctionalService.EnsureInitialized` mencoba model berikut secara berurutan dan memakai pasangan pertama yang ada:

1. `Assets/models/yolov8n-agri.onnx` + `classes-yolov8n-agri-basic.txt`
2. `Assets/models/yolov8n.onnx` + `classes-yolov8n-coco.txt`
3. `Assets/models/moonharvest-v2.onnx` + `classes-id-v2.txt`
4. `yolov8n-agri.onnx` (base dir fallback)
5. `yolov8n.onnx` (base dir fallback)

Default yang aktif setelah build bersih: **yolov8n.onnx + classes-yolov8n-coco.txt**.
YOLO langsung aktif begitu toggle "Yolo" di sidebar dinyalakan dan camera feed masuk.

## Cara mengganti model

### Opsi A - Drop-in model agri custom

Taruh file berikut di folder ini (overwrite jika perlu):

- `yolov8n-agri.onnx` (model custom crop/pest)
- `classes-yolov8n-agri-basic.txt` (sudah ada; edit sesuai label model anda)

Rebuild atau restart aplikasi. `HarvestFunctionalService` otomatis pilih pasangan ini.

### Opsi B - Runtime switching via AISettingsPage

Buka menu **AI Settings** di sidebar. Pilih model `.onnx`, class file, confidence threshold, dan NMS threshold dari UI. Perubahan disimpan tanpa rebuild.

## Rekomendasi performance

- YOLOv8n input 640x640 berjalan ~15-30 FPS di CPU modern (Ryzen 5 / i5 ke atas).
- Untuk Android / perangkat low-end, konversi ke INT8 atau pakai input 416x416.
- Aktifkan CUDA otomatis jika tersedia (Windows + GPU NVIDIA + ONNX Runtime GPU build).
- Frame skipping dikontrol oleh `DashboardPage.AnalyzeDashboardFrameAsync` (default 2 detik per analisis). Turunkan untuk throughput lebih tinggi.
