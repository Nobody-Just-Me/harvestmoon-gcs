# Harvestmoon GCS Proposal Run Datasheet

Dokumen ini merangkum datasheet/dokumen teknis yang dibutuhkan untuk menjalankan Harvestmoon GCS sesuai proposal MoonHarvest: UAV ArduPilot/PX4, telemetry MAVLink, live video, YOLO ONNX, vegetation overlay, waypoint/geofence, telemetry logging, Android, Linux, Windows, dan SITL.

## Minimum Komponen

| Area | Kebutuhan | Acuan teknis |
| --- | --- | --- |
| Flight controller | ArduPilot/PX4 compatible autopilot, disarankan Pixhawk 6C/6X atau setara | Pixhawk 6C technical specification: STM32H743 Cortex-M7 480 MHz, 2 MB flash, 1 MB SRAM, IMU/mag/barometer onboard. https://docs.holybro.com/autopilot/pixhawk-6c/technical-specification |
| Firmware UAV | ArduPilot atau PX4 dengan MAVLink aktif | ArduPilot MAVLink basics: MAVLink untuk komunikasi vehicle-GCS via serial/radio/Wi-Fi. https://ardupilot.org/dev/docs/mavlink-basics.html |
| Telemetry link | UDP/TCP untuk SITL/Wi-Fi, atau serial/SiK telemetry radio untuk hardware | Holybro SiK Telemetry Radio V3: open-source radio, 433/915 MHz, MAVLink-friendly. https://docs.holybro.com/radio/sik-telemetry-radio-v3 |
| GCS desktop | Windows/Linux dengan .NET/Uno desktop target, kamera USB/RTSP, CPU cukup untuk ONNX | Uno supported platforms. https://platform.uno/docs/articles/getting-started/requirements.html |
| GCS Android | Android device dengan USB host/OTG jika memakai telemetry serial, disarankan RAM 4 GB+ | Android USB host mode. https://developer.android.com/develop/connectivity/usb/host |
| Tablet target contoh | Realme Pad Mini atau setara untuk uji Android lapangan | Realme Pad Mini: Unisoc T616, RAM 3/4 GB, 8.7 inch 1340x800, baterai 6400 mAh. https://www.realme.com/id/realme-pad-mini/specs |
| Video input | USB camera/UVC atau RTSP IP/action camera dengan stream H.264/H.265 yang bisa dibaca OpenCV/LibVLC | OpenCV VideoCapture docs untuk camera/video stream. https://docs.opencv.org/4.x/d8/dfe/classcv_1_1VideoCapture.html |
| Video playback | RTSP/file/network media pada desktop/mobile bila memakai service player | LibVLCSharp supports media files, codecs, streaming protocols, desktop/mobile. https://github.com/videolan/libvlcsharp |
| AI inference | ONNX Runtime CPU; Android bisa memakai NNAPI bila model/operator mendukung | ONNX Runtime execution providers. https://onnxruntime.ai/docs/execution-providers/ |
| Android AI acceleration | NNAPI execution provider untuk akselerasi Android | ONNX Runtime NNAPI. https://onnxruntime.ai/docs/execution-providers/NNAPI-ExecutionProvider.html |
| YOLO model | YOLOv8n/YOLO custom agriculture diekspor ke ONNX | Ultralytics model export to ONNX. https://docs.ultralytics.com/modes/export/ |
| SITL testing | ArduPilot SITL + MAVProxy output UDP ke GCS port 14550 | ArduPilot SITL usage. https://ardupilot.org/dev/docs/using-sitl-for-ardupilot-testing.html |

## File Yang Sudah Ada Di Project

| File/folder | Fungsi |
| --- | --- |
| `HarvestmoonGCS/Assets/models/yolov8n.onnx` | Baseline YOLOv8n ONNX untuk deteksi umum. |
| `HarvestmoonGCS/Assets/models/yolov8n-320.onnx` | Model lebih ringan untuk tablet/Android atau CPU rendah. |
| `HarvestmoonGCS/Assets/models/classes-yolov8n-coco.txt` | Class label COCO untuk model YOLO umum. |
| `HarvestmoonGCS/Assets/models/classes-yolov8n-agri-basic.txt` | Class label awal untuk skenario pertanian. |
| `docs/yolov8n-agri-basic-datasheet.md` | Datasheet lokal untuk model YOLO agriculture basic. |
| `HarvestmoonGCS/SITL/start_quadplane_4p1_sitl.sh` | Script SITL QuadPlane dari Pigeon_Uno. |
| `HarvestmoonGCS/SITL/quadplane_4p1.parm` | Parameter SITL QuadPlane. |
| `HarvestmoonGCS/SITL/quadplane_4p1_6wp.waypoints` | Sample waypoint SITL 6 waypoint. |
| `HarvestmoonGCS/SITL/quadplane_4p1_payload_3drop.waypoints` | Sample payload-drop waypoint. |
| `scripts/sitl_smoke.sh` | Smoke check build, camera bridge, dan model assets. |
| `docs/FIELD_TEST_CHECKLIST.md` | Checklist uji lapangan. |

## Konfigurasi Run Yang Direkomendasikan

### Desktop Linux/Windows

1. Build:
   ```bash
   dotnet build HarvestmoonGCS/HarvestmoonGCS.csproj -f net9.0-desktop
   ```
2. Jalankan SITL atau hubungkan UAV hardware.
3. Di GCS pilih koneksi `UDP`, port standar SITL `14550`.
4. Aktifkan kamera lokal atau masukkan RTSP URL.
5. Pastikan YOLO model/class tersedia di `HarvestmoonGCS/Assets/models`.

### Android

1. Gunakan perangkat Android dengan USB host/OTG jika telemetry via USB serial.
2. Build APK:
   ```bash
   dotnet build HarvestmoonGCS/HarvestmoonGCS.csproj -f net9.0-android
   ```
3. Untuk performa, gunakan model `yolov8n-320.onnx` atau model INT8 agriculture custom.
4. Jika telemetry via jaringan, gunakan UDP/TCP tanpa kabel serial.

### SITL

1. Dari output build atau source folder, jalankan:
   ```bash
   HarvestmoonGCS/SITL/start_quadplane_4p1_sitl.sh
   ```
2. Hubungkan GCS ke UDP `127.0.0.1:14550`.
3. Load waypoint sample dari folder `HarvestmoonGCS/SITL`.

Jika GCS berjalan di perangkat lain pada jaringan yang sama, set `SITL_OUT`, misalnya:

```bash
SITL_OUT=udp:192.168.1.50:14550 HarvestmoonGCS/SITL/start_quadplane_4p1_sitl.sh
```

## Model Agriculture Yang Perlu Disiapkan

Proposal menyebut runtime-configurable `.onnx`, class file, confidence, dan NMS. Untuk demo proposal:

- Model minimum: `yolov8n-320.onnx` atau `yolov8n.onnx`.
- Model ideal: `yolov8n-agri-320.onnx` hasil training dataset crop stress/pest/water stress.
- Class file ideal:
  ```text
  healthy_crop
  crop_stress
  pest_damage
  dry_soil
  water_stress
  weed
  ```
- Threshold awal: confidence `0.35` desktop, `0.40` Android; NMS `0.45`.

## Checklist Kesiapan Proposal

- [ ] MAVLink UDP/TCP/serial bisa connect ke SITL atau flight controller.
- [ ] Kamera lokal/RTSP tampil real-time.
- [ ] YOLO overlay menampilkan bounding box dan confidence score.
- [ ] Vegetation overlay bisa diaktifkan dari UI.
- [ ] Waypoint bisa dibuat, disimpan, dimuat, dan dikirim ke vehicle/SITL.
- [ ] Geofence bisa dibuat dan dikirim.
- [ ] Telemetry log/TLOG atau export telemetry berjalan.
- [ ] Android APK build dan diuji minimal pada satu perangkat.
- [ ] Field test mengikuti `docs/FIELD_TEST_CHECKLIST.md`.
