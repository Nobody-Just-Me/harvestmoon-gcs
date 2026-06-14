# Two-Day Trial Plan

Target uji: 2026-06-14.

Tujuan rencana ini adalah membuat Harvestmoon GCS bisa dicoba sebagai demo proposal MoonHarvest: koneksi MAVLink/SITL, kamera live, YOLO overlay, vegetation analysis, waypoint/geofence, dan export laporan.

## 2026-06-12 - Preflight Software

1. Jalankan preflight:
   ```bash
   scripts/prepare_two_day_trial.sh
   ```
2. Jika OpenCV Python belum tersedia:
   ```bash
   scripts/prepare_two_day_trial.sh --install-camera-deps
   ```
3. Pastikan desktop build lulus.
4. Pastikan model berikut tersedia minimal salah satu:
   - `HarvestmoonGCS/Assets/models/yolov8n-320.onnx`
   - `HarvestmoonGCS/Assets/models/yolov8n.onnx`
5. Jalankan app desktop:
   ```bash
   scripts/run_desktop_trial.sh
   ```
6. Uji halaman Camera dengan kamera laptop/USB atau RTSP.

## 2026-06-13 - SITL Dan Mission Test

1. Siapkan ArduPilot SITL di `~/ardupilot` atau set:
   ```bash
   export ARDUPILOT_DIR=/path/to/ardupilot
   ```
2. Jalankan SITL:
   ```bash
   HarvestmoonGCS/SITL/start_quadplane_4p1_sitl.sh
   ```
   Jika GCS berjalan di Android/tablet pada jaringan yang sama, arahkan output SITL ke IP tablet:
   ```bash
   SITL_OUT=udp:IP_TABLET:14550 HarvestmoonGCS/SITL/start_quadplane_4p1_sitl.sh
   ```
3. Buka Harvestmoon GCS dan connect MAVLink:
   - mode: `UDP`
   - port: `14550`
   - host: `127.0.0.1` jika diminta
4. Load sample mission:
   - `HarvestmoonGCS/SITL/quadplane_4p1_6wp.waypoints`
   - atau `HarvestmoonGCS/SITL/quadplane_4p1_payload_3drop.waypoints`
5. Uji minimal:
   - telemetry masuk
   - attitude/GPS/mode tampil
   - waypoint tampil dan bisa disimpan
   - geofence bisa dibuat
   - export telemetry/report bisa dibuat

## 2026-06-14 - Demo Trial

Urutan demo yang disarankan:

1. Jalankan:
   ```bash
   scripts/prepare_two_day_trial.sh
   scripts/run_desktop_trial.sh
   ```
2. Jika memakai SITL, jalankan SITL di terminal lain.
3. Connect MAVLink.
4. Buka Camera.
5. Aktifkan sumber kamera lokal/RTSP.
6. Pastikan teks `YOLO:` muncul pada overlay kamera.
7. Tunjukkan bounding box jika objek terdeteksi.
8. Buka analysis/report dan lakukan export JSON/CSV.
9. Buka Mission/Map, load waypoint sample, lalu upload/download mission bila SITL siap.
10. Buka geofence, buat boundary sederhana, lalu kirim ke vehicle/SITL.

## Minimum Demo Yang Harus Berjalan

- Desktop app bisa dibuka.
- Kamera lokal atau RTSP tampil.
- YOLO model berhasil diload atau fallback vegetation analysis tetap berjalan.
- MAVLink connect ke SITL/hardware.
- Waypoint sample bisa dimuat.
- Export report/telemetry bisa dibuat.

## Batas Aman

- Jika Android build masih bermasalah, demo tetap valid di desktop Linux/Windows karena proposal menyatakan cross-platform dan desktop adalah jalur stabil untuk pembuktian awal.
- Jika SITL belum terpasang, demo dapat memakai camera + YOLO + report dulu, lalu MAVLink diuji dengan hardware atau SITL setelah ArduPilot siap.
- Jika model agriculture custom belum ada, gunakan `yolov8n-320.onnx`/COCO untuk membuktikan pipeline real-time, lalu jelaskan agriculture model adalah drop-in `.onnx`.

## Bukti Yang Perlu Disimpan

- Screenshot app camera dengan YOLO status.
- Screenshot telemetry connected.
- File export JSON/CSV report.
- Log terminal dari `scripts/prepare_two_day_trial.sh`.
- Jika SITL dipakai, screenshot MAVLink UDP connected pada port `14550`.
