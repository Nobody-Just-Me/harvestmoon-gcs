# Pigeon Harvest Full Implementation Plan

Dokumen ini adalah rencana lengkap untuk membuat `Pigeon_Harvest` menjalankan seluruh fungsi yang dijelaskan dalam dokumen EFRISA/MoonHarvest Preliminary Evaluation Report.

Target akhirnya adalah satu aplikasi Ground Control Station lintas platform yang menggabungkan kontrol UAV, live video, YOLOv8n offline inference, analisis vegetasi, waypoint, geofence, telemetry logging, dan laporan hasil misi.

## 1. Target Akhir Sistem

`Pigeon_Harvest` harus menjadi satu aplikasi utama yang dapat:

- Mengontrol UAV melalui MAVLink menggunakan UDP, TCP, atau Serial.
- Menampilkan telemetry real-time: attitude, GPS, altitude, airspeed, ground speed, battery, armed state, dan flight mode.
- Menampilkan live video dari UAV atau kamera lokal.
- Menjalankan YOLOv8n ONNX secara offline di perangkat lokal.
- Menampilkan bounding box dan confidence score langsung di live feed.
- Menampilkan overlay analisis vegetasi berbasis OpenCV.
- Membuat waypoint survey dan geofence.
- Memberi peringatan saat UAV keluar geofence.
- Mendeteksi crop stress, pest indication, kekeringan, tanah retak, dan kebutuhan irigasi.
- Membuat titik/zona prioritas irigasi dengan estimasi koordinat GPS.
- Menyimpan TLOG/telemetry log.
- Mengekspor hasil misi dan analisis ke JSON, CSV, dan PDF.
- Berjalan di Windows, Linux/Desktop, dan Android dari satu codebase C# Uno Platform.

## 2. Status Saat Ini

Fitur yang sudah tersedia atau sebagian tersedia:

- Aplikasi utama sudah diarahkan ke `Pigeon_Harvest`.
- Dashboard modern sudah menjadi halaman utama.
- Sidebar modern sudah menggabungkan fitur Harvest dan fitur Pigeon lama.
- YOLOv8n ONNX sudah tersedia di `Pigeon_Harvest/Pigeon_Uno/Assets/models/yolov8n.onnx`.
- Class file YOLOv8n sudah tersedia.
- Analisis citra berbasis YOLO/OpenCV sudah tersedia melalui `HarvestFunctionalService`.
- Statistik kondisi lahan sudah menampilkan sehat, stress, kekeringan, tanah terbuka/retak, dan jumlah deteksi YOLO.
- Zona prioritas irigasi sudah memiliki row, column, severity, rekomendasi, dan estimasi GPS.
- Validasi ground-truth kelembaban tanah sudah tersedia di halaman statistik.
- Export JSON dan CSV untuk analisis sudah tersedia.
- Report history sudah tersedia.
- MAVLink service, telemetry parser, connection manager, mission protocol, geofence helper, dan TLOG helper sudah ada di codebase.
- Build desktop `net9.0-desktop` sudah berhasil tanpa error.

Gap utama yang masih perlu diselesaikan:

- Live video belum sepenuhnya menampilkan overlay YOLO bounding box secara real-time di dashboard.
- Runtime setting untuk model `.onnx`, class file, confidence threshold, dan NMS threshold belum sepenuhnya tersimpan dan digunakan dari UI.
- Android belum aktif sebagai target build utama di project file.
- Geofence alert dan geofence logging belum jelas tampil di UI.
- TLOG otomatis per sesi misi perlu dirapikan.
- Performance testing target `>15 FPS` belum dibuat sebagai skenario resmi.
- SITL testing ArduPilot/PX4 belum dibuat menjadi alur uji tetap.

## 3. Modul 1 - Penyatuan Aplikasi

Tujuan:

Menjadikan `Pigeon_Harvest` sebagai satu-satunya aplikasi utama yang berisi seluruh fitur GCS dan fitur MoonHarvest.

Pekerjaan:

- Pastikan startup selalu membuka `MainPage_Modern`.
- Pastikan route sidebar mengarah ke halaman yang benar.
- Hindari fitur duplikat yang membingungkan antara dashboard lama dan dashboard baru.
- Jadikan Dashboard sebagai pusat operasi misi.
- Tampilkan status global: connection, camera, AI, mission, battery, dan alerts.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/App.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/MainPage_Modern.xaml`
- `Pigeon_Harvest/Pigeon_Uno/MainPage_Modern.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Controls/ModernSidebar.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Controls/ModernSidebar.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/DashboardPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/DashboardPage.xaml.cs`

Checklist selesai:

- [ ] App title menjadi `Pigeon Harvest`.
- [ ] Dashboard menjadi halaman default.
- [ ] Semua menu penting bisa diakses dari sidebar.
- [ ] Tidak ada fitur penting yang hanya ada di `agri-gcs`.
- [ ] Build desktop sukses.

## 4. Modul 2 - MAVLink Connection Full

Tujuan:

Menyediakan koneksi MAVLink real-time melalui UDP, TCP, dan Serial sesuai proposal.

Pekerjaan:

- Rapikan UI koneksi MAVLink.
- Tambahkan pilihan koneksi:
  - UDP
  - TCP
  - Serial
- Tampilkan status:
  - Disconnected
  - Connecting
  - Connected
  - Reconnecting
  - Lost
- Pastikan heartbeat diterima.
- Pastikan telemetry parser mengisi data:
  - GPS latitude/longitude
  - altitude
  - airspeed
  - ground speed
  - roll/pitch/yaw
  - battery
  - flight mode
  - armed/disarmed
- Tambahkan auto reconnect.
- Tambahkan diagnostic log jika koneksi gagal.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Services/MavLinkService.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MavLink/ConnectionManager.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MavLink/TelemetryParser.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MavLink/HeartbeatManager.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MavLink/AutoReconnectManager.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/ConnectDialog.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/ConnectDialog.xaml.cs`

Checklist selesai:

- [ ] UDP MAVLink bisa connect ke SITL.
- [ ] TCP MAVLink bisa connect.
- [ ] Serial MAVLink bisa connect ke flight controller.
- [ ] Telemetry tampil real-time di dashboard.
- [ ] Status reconnect tampil jelas.
- [ ] Error connection tercatat di diagnostic log.

## 5. Modul 3 - Dashboard Telemetry Real-Time

Tujuan:

Dashboard harus menjadi tampilan utama yang menunjukkan kondisi UAV, misi, map, video, AI, dan alert.

Pekerjaan:

- Hubungkan dashboard ke telemetry live.
- Tampilkan:
  - altitude
  - speed
  - battery
  - GPS
  - mode
  - armed state
  - attitude
  - mission progress
  - AI status
- Update posisi UAV di map.
- Tampilkan geofence dan waypoint di map.
- Tampilkan alert jika battery rendah, GPS invalid, connection lost, atau geofence breach.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Views/DashboardPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/DashboardPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Controls/SkiaMapControl.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Controls/SkiaMapControl.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/BatteryWarningSystem.cs`

Checklist selesai:

- [ ] Telemetry berubah sesuai data MAVLink live.
- [ ] Posisi UAV bergerak di map.
- [ ] Battery warning tampil.
- [ ] Connection lost warning tampil.
- [ ] Flight mode tampil benar.

## 6. Modul 4 - Live Video Pipeline

Tujuan:

Menampilkan live video UAV/kamera lokal dan menyediakan frame untuk YOLO/OpenCV.

Pekerjaan:

- Pastikan camera service dapat membaca:
  - local camera
  - USB camera
  - network stream jika tersedia
- Setiap frame dikirim ke:
  - UI video preview
  - YOLO/OpenCV analyzer
- Tambahkan frame throttling agar UI tetap lancar.
- Tampilkan status camera:
  - detecting
  - live
  - stopped
  - error
- Tambahkan snapshot capture untuk laporan.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Views/CameraPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/CameraPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Controls/VideoStreamControl.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Controls/VideoStreamControl.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/DesktopCameraService.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/PythonCameraService.cs`

Checklist selesai:

- [ ] Local camera tampil di dashboard.
- [ ] USB camera tampil jika tersedia.
- [ ] Frame dapat dianalisis oleh AI service.
- [ ] Snapshot dapat disimpan.
- [ ] Camera error tampil jelas.

## 7. Modul 5 - YOLOv8n Offline Inference

Tujuan:

Menjalankan YOLOv8n `.onnx` secara offline menggunakan ONNX Runtime sesuai proposal Zero-Internet Edge AI.

Pekerjaan:

- Pastikan `YoloDetector` memakai model YOLOv8n.
- Pastikan input tensor sesuai format model:
  - 640x640
  - RGB
  - normalized 0-1
- Pastikan post-process menghasilkan:
  - class id
  - class name
  - confidence
  - bounding box
- Tambahkan setting:
  - model path
  - class file path
  - confidence threshold
  - NMS threshold
  - use CPU/GPU jika tersedia
- Simpan setting ke file.
- Buat hot-swap model tanpa rebuild aplikasi.
- Jika model custom `yolov8n-agri.onnx` ada, prioritaskan model tersebut.
- Jika tidak ada, gunakan `yolov8n.onnx`.

File utama:

- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/YoloDetector.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/VegetationYoloAnalyzer.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/HarvestFunctionalService.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/AIHarvestPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/AIHarvestPage.xaml.cs`

Checklist selesai:

- [ ] YOLOv8n dapat load dari `Assets/models`.
- [ ] Custom `.onnx` dapat dipilih dari UI.
- [ ] Custom class file dapat dipilih dari UI.
- [ ] Threshold dapat diubah dari UI.
- [ ] Inference berjalan tanpa internet.
- [ ] Error model/class file tampil jelas.

## 8. Modul 6 - Real-Time YOLO Overlay

Tujuan:

Menampilkan bounding box dan confidence score langsung di live video seperti yang dijelaskan dalam dokumen.

Pekerjaan:

- Ambil hasil detection per frame.
- Render bounding box di atas video.
- Tampilkan label:
  - class name
  - confidence percentage
- Tampilkan FPS dan inference time.
- Tambahkan toggle:
  - YOLO ON/OFF
  - Bounding box ON/OFF
  - Confidence label ON/OFF
- Pastikan overlay tidak membuat UI freeze.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Controls/VideoStreamControl.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/DashboardPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/YoloDetector.cs`

Checklist selesai:

- [ ] Bounding box muncul di live feed.
- [ ] Confidence score muncul.
- [ ] FPS tampil.
- [ ] Inference time tampil.
- [ ] UI tetap responsif.

## 9. Modul 7 - Vegetation Analysis Overlay

Tujuan:

Menganalisis kondisi vegetasi dari citra UAV menggunakan OpenCV dan menampilkan overlay kesehatan tanaman.

Pekerjaan:

- Buat segmentasi warna:
  - hijau untuk sehat
  - kuning untuk stress
  - coklat/oranye untuk kekeringan
  - merah untuk critical/dry soil/soil crack
- Gabungkan hasil HSV dan YOLO.
- Hitung persentase:
  - healthy
  - stressed
  - drought
  - bare soil
- Tampilkan overlay real-time di video.
- Tampilkan statistik di dashboard dan stats page.

File utama:

- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/VegetationYoloAnalyzer.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/HarvestFunctionalService.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml.cs`

Checklist selesai:

- [ ] Overlay vegetasi bisa ON/OFF.
- [ ] Persentase kondisi lahan tampil.
- [ ] Hasil analisis konsisten dengan screenshot/snapshot.
- [ ] Dapat berjalan tanpa YOLO sebagai fallback OpenCV.

## 10. Modul 8 - Zona Prioritas Irigasi

Tujuan:

Menghasilkan titik/zona prioritas irigasi dari analisis crop stress dan kekeringan.

Pekerjaan:

- Bagi frame menjadi grid zona.
- Hitung severity setiap zona.
- Buat prioritas:
  - P1 kritis
  - P2 parah
  - P3 sedang
  - P4 ringan
- Estimasi koordinat GPS zona berdasarkan posisi UAV/center map.
- Tampilkan zona di tabel.
- Tampilkan zona di map.
- Export zona ke CSV/JSON.
- Buat rekomendasi watering/pesticide/fertilizer action.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Services/HarvestFunctionalService.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/VegetationYoloAnalyzer.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Controls/SkiaMapControl.xaml.cs`

Checklist selesai:

- [ ] Zona prioritas muncul setelah analisis.
- [ ] Setiap zona memiliki severity.
- [ ] Setiap zona memiliki rekomendasi.
- [ ] Setiap zona memiliki estimasi GPS.
- [ ] Zona bisa diekspor.
- [ ] Zona bisa ditampilkan di map.

## 11. Modul 9 - Ground-Truth Validation

Tujuan:

Memvalidasi hasil AI dengan sampel lapangan seperti soil moisture/manual sample.

Pekerjaan:

- Input sampel format `row,col,moisture%`.
- Cocokkan zona stress hasil AI dengan soil moisture rendah.
- Hitung match percentage.
- Simpan hasil validasi ke report.
- Export hasil validasi ke JSON/CSV.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/StatsPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/HarvestFunctionalService.cs`

Checklist selesai:

- [ ] Sampel ground-truth bisa diinput.
- [ ] Validasi menghasilkan match percentage.
- [ ] Validasi masuk ke report.
- [ ] Validasi bisa diekspor.

## 12. Modul 10 - Waypoint Mission Planner

Tujuan:

Membuat dan mengirim misi autonomous flight untuk survei lahan.

Pekerjaan:

- Buat mission type:
  - grid survey
  - corridor survey
  - circular survey
- Buat waypoint otomatis dari parameter:
  - center GPS
  - altitude
  - area width/height
  - spacing
  - speed
- Tampilkan waypoint di map.
- Upload mission ke flight controller.
- Download mission dari flight controller.
- Start/pause/resume mission jika MAVLink mendukung.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Views/MissionPlannerPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/MissionPlannerPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/MissionPlannerGenerator.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/VtolMissionGenerator.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MavLink/MissionProtocol.cs`

Checklist selesai:

- [ ] Grid survey dapat dibuat otomatis.
- [ ] Waypoint tampil di map.
- [ ] Mission dapat diupload.
- [ ] Mission dapat didownload.
- [ ] Mission dapat disimpan sebagai file.

## 13. Modul 11 - Geofence

Tujuan:

Membuat batas operasi UAV dan memberi alert/log saat UAV keluar batas.

Pekerjaan:

- Buat geofence circle.
- Buat geofence polygon.
- Upload geofence ke flight controller jika didukung.
- Monitor posisi UAV terhadap geofence.
- Tampilkan alert jika breach.
- Log breach ke report.
- Tampilkan breach marker di map.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Helpers/GeofenceMonitor.cs`
- `Pigeon_Harvest/Pigeon_Uno/Helpers/GeofenceRenderer.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/GeofenceMavLinkHelper.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/MapFeatureManager.cs`

Checklist selesai:

- [ ] Geofence circle dapat dibuat.
- [ ] Geofence polygon dapat dibuat.
- [ ] UAV position dicek terhadap geofence.
- [ ] Breach alert tampil.
- [ ] Breach masuk report/log.

## 14. Modul 12 - Telemetry Logging dan TLOG

Tujuan:

Menyimpan data penerbangan untuk replay, audit, dan laporan misi.

Pekerjaan:

- Mulai TLOG saat MAVLink connected.
- Stop TLOG saat disconnected atau misi selesai.
- Simpan packet MAVLink dengan timestamp.
- Tampilkan daftar log.
- Playback TLOG untuk melihat ulang telemetry.
- Hubungkan report dengan file TLOG.

File utama:

- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/TlogWriter.cs`
- `Pigeon_Harvest/Pigeon_Uno.Core/Helpers/TlogPlayer.cs`
- `Pigeon_Harvest/Pigeon_Uno/Views/TlogPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/TlogPage.xaml.cs`

Checklist selesai:

- [ ] TLOG otomatis dibuat saat misi.
- [ ] TLOG dapat diputar ulang.
- [ ] TLOG tercatat di report.
- [ ] Export/backup TLOG tersedia.

## 15. Modul 13 - Report dan Export

Tujuan:

Menghasilkan laporan misi lengkap untuk petani/operator.

Isi report:

- Mission ID.
- Date/time.
- Area/field name.
- Duration.
- UAV telemetry summary.
- AI model used.
- Detection count.
- Crop health distribution.
- Priority irrigation zones.
- Geofence alerts.
- Ground-truth validation.
- Operator notes.
- Export path.

Format export:

- JSON untuk data lengkap.
- CSV untuk tabel zona/validasi.
- PDF/text report untuk laporan operator.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Views/ReportsHarvestPage.xaml`
- `Pigeon_Harvest/Pigeon_Uno/Views/ReportsHarvestPage.xaml.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/HarvestFunctionalService.cs`
- `Pigeon_Harvest/Pigeon_Uno/Services/ResearchTelemetryExportService.cs`

Checklist selesai:

- [ ] Report otomatis dibuat setelah analisis.
- [ ] Report menyimpan data misi.
- [ ] JSON export tersedia.
- [ ] CSV export tersedia.
- [ ] PDF/text export tersedia.
- [ ] Report bisa dibuka kembali dari history.

## 16. Modul 14 - Android Support

Tujuan:

Memenuhi klaim proposal bahwa aplikasi berjalan di Android dengan satu codebase.

Pekerjaan:

- Aktifkan target `net9.0-android`.
- Pastikan permission:
  - camera
  - storage/media
  - network
  - USB/serial jika dibutuhkan
- Test ONNX Runtime di Android.
- Gunakan model ringan atau INT8 jika FPS rendah.
- Optimalkan frame size dan frame skipping.
- Pastikan UI responsive di layar kecil.

File utama:

- `Pigeon_Harvest/Pigeon_Uno/Pigeon_Uno.csproj`
- `Pigeon_Harvest/Pigeon_Uno/Platforms/Android/MainActivity.Android.cs`
- `Pigeon_Harvest/Pigeon_Uno/Platforms/Android/AndroidCompatibility.cs`

Checklist selesai:

- [ ] Android build berhasil.
- [ ] UI dashboard tampil baik di Android.
- [ ] Camera permission berjalan.
- [ ] YOLO inference berjalan.
- [ ] Performance minimal dapat diterima.

## 17. Modul 15 - Performance Optimization

Tujuan:

Mencapai target real-time computer vision, terutama target dokumen `>15 FPS` pada perangkat menengah.

Pekerjaan:

- Ukur FPS video.
- Ukur inference time YOLO.
- Tambahkan frame skipping.
- Tambahkan resize inference.
- Gunakan background task untuk inference.
- Hindari inference di UI thread.
- Cache tensor buffer jika memungkinkan.
- Tambahkan mode:
  - Quality
  - Balanced
  - Performance
- Siapkan opsi model INT8 untuk Android.

Checklist selesai:

- [ ] FPS tampil di UI.
- [ ] Inference time tampil di UI.
- [ ] UI tidak freeze saat YOLO aktif.
- [ ] Desktop mencapai target real-time.
- [ ] Android memiliki mode performance.

## 18. Modul 16 - SITL dan Field Testing

Tujuan:

Membuktikan fitur bekerja sebelum dicoba ke UAV fisik.

Skenario SITL:

- Jalankan ArduPilot/PX4 SITL.
- Connect via UDP.
- Pastikan heartbeat dan telemetry masuk.
- Buat waypoint mission.
- Upload mission.
- Simulasi UAV bergerak.
- Simulasi geofence breach.
- Simpan TLOG.
- Jalankan video test file/kamera.
- Aktifkan YOLOv8n.
- Export report.

Skenario field test:

- Connect ke flight controller fisik.
- Test telemetry.
- Test camera.
- Test misi pendek.
- Test geofence warning.
- Ambil citra UAV.
- Jalankan analisis.
- Bandingkan dengan sampel tanah/manual ground-truth.

Checklist selesai:

- [ ] SITL UDP connect berhasil.
- [ ] Telemetry SITL tampil.
- [ ] Mission upload SITL berhasil.
- [ ] Geofence breach terdeteksi.
- [ ] TLOG SITL tersimpan.
- [ ] Report SITL tersimpan.
- [ ] Field test dasar berhasil.

## 19. Demo Akhir

Alur demo yang harus bisa ditunjukkan:

1. Buka aplikasi `Pigeon Harvest`.
2. Connect ke MAVLink SITL atau UAV via UDP/TCP/Serial.
3. Telemetry real-time muncul di dashboard.
4. Start camera/live stream.
5. Aktifkan YOLOv8n.
6. Bounding box dan confidence muncul di live video.
7. Aktifkan vegetation overlay.
8. Buat waypoint survey.
9. Buat geofence.
10. Jalankan misi.
11. Zona stress/kekeringan muncul sebagai prioritas irigasi.
12. Titik prioritas muncul di tabel dan map.
13. Masukkan ground-truth soil moisture.
14. Validasi menghasilkan match percentage.
15. Export laporan JSON/CSV/PDF.
16. Playback TLOG jika diperlukan.

## 20. Urutan Pengerjaan Prioritas

Prioritas 1 - Core Demo:

- [ ] Live video + YOLOv8n overlay.
- [ ] Runtime model/class/threshold settings.
- [ ] Telemetry dashboard dari MAVLink live.
- [ ] Zona prioritas irigasi tampil di map.

Prioritas 2 - Mission System:

- [ ] Waypoint grid survey.
- [ ] Mission upload/download.
- [ ] Geofence circle/polygon.
- [ ] Geofence alert/log.

Prioritas 3 - Report System:

- [ ] Report misi lengkap.
- [ ] TLOG otomatis.
- [ ] Ground-truth validation masuk report.
- [ ] Export JSON/CSV/PDF lengkap.

Prioritas 4 - Cross-Platform:

- [ ] Android build.
- [ ] Android camera.
- [ ] Android ONNX Runtime.
- [ ] Android performance mode.

Prioritas 5 - Testing dan Finalisasi:

- [ ] SITL test script.
- [ ] Performance benchmark.
- [ ] Field test checklist.
- [ ] Dokumentasi penggunaan.

## 21. Kriteria Selesai Sesuai Proposal

Project dianggap sesuai dokumen jika semua poin berikut terpenuhi:

- [ ] GCS dapat connect UAV via MAVLink UDP/TCP/Serial.
- [ ] Telemetry real-time tampil lengkap.
- [ ] Live video tampil.
- [ ] YOLOv8n berjalan offline lokal.
- [ ] Bounding box dan confidence tampil real-time.
- [ ] Model `.onnx` dan class file dapat diganti runtime.
- [ ] Vegetation overlay berbasis OpenCV tampil.
- [ ] Waypoint mission dapat dibuat dan dikirim.
- [ ] Geofence dapat dibuat dan dimonitor.
- [ ] Alert geofence tampil dan tercatat.
- [ ] TLOG tersimpan.
- [ ] Analisis menghasilkan zona stress/kekeringan/irigasi.
- [ ] Rekomendasi tindakan muncul.
- [ ] Ground-truth validation tersedia.
- [ ] Export JSON/CSV/PDF tersedia.
- [ ] Build desktop berhasil.
- [ ] Build Android berhasil.
- [ ] SITL test berhasil.
- [ ] Demo flow akhir dapat dijalankan dari awal sampai export report.

