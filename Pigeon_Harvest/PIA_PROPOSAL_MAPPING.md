# Matriks Kesesuaian Fitur PIA vs Proposal Penelitian

Tanggal pembaruan: 26 April 2026
Ruang lingkup: implementasi `Pigeon_Uno` (provider dipertahankan OpenRouter sesuai keputusan proyek).

## Ringkasan Skor

- Estimasi kesesuaian saat ini: **82%**
- Metode skor: `Sesuai = 1.0`, `Parsial = 0.7`, `Belum = 0.0`
- Hasil: `6 item Sesuai + 2 item Parsial + 1 item Belum = 7.4 / 9 = 82.2%`

## Matriks Fitur vs Proposal

| No | Indikator Proposal | Implementasi Saat Ini | Bukti Kode | Status | Gap yang Tersisa |
|---|---|---|---|---|---|
| 1 | Integrasi LLM berbasis cloud di GCS | Layanan LLM aktif via OpenRouter dan dipaksa konsisten di DI/settings | [ServiceCollectionExtensions.cs](./Pigeon_Uno.Core/Services/AI/ServiceCollectionExtensions.cs) (baris `281-285`) | Sesuai | Tidak ada gap teknis utama |
| 2 | Multi-provider + fallback provider | Secara teknis fallback object masih ada, tetapi provider dipaksa OpenRouter-only (deviasi scope disengaja) | [ServiceCollectionExtensions.cs](./Pigeon_Uno.Core/Services/AI/ServiceCollectionExtensions.cs) (baris `281-285`), [AISettingsPage.xaml](./Pigeon_Uno/Views/AISettingsPage.xaml) (provider combo nonaktif), [AISettingsPage.xaml.cs](./Pigeon_Uno/Views/AISettingsPage.xaml.cs) (baris `700-705`) | Parsial | Jika kembali strict proposal, perlu aktifkan multi-provider nyata |
| 3 | Telemetry buffer in-memory + analisis berkala real-time | Buffer in-memory tersedia, analisis periodik berjalan sesuai interval + buffer seconds | [TelemetryBuffer.cs](./Pigeon_Uno.Core/Services/AI/TelemetryBuffer.cs), [TelemetryAnalysisService.cs](./Pigeon_Uno.Core/Services/AI/TelemetryAnalysisService.cs) (baris `64-67`, `96-97`), [AISettings.cs](./Pigeon_Uno.Core/Models/AI/AISettings.cs) (baris `444-446`) | Parsial | Ada mismatch narasi: retention buffer DI diset 30 menit, proposal menekankan sliding window 30 detik |
| 4 | Deteksi anomali (rule-based + statistik/AI) | Pipeline 3 layer aktif (rule, statistik, AI) | [AnomalyDetectionService.cs](./Pigeon_Uno.Core/Services/AI/AnomalyDetectionService.cs), [RuleBasedDetector.cs](./Pigeon_Uno.Core/Services/AI/RuleBasedDetector.cs), [ServiceCollectionExtensions.cs](./Pigeon_Uno.Core/Services/AI/ServiceCollectionExtensions.cs) (baris `309-325`) | Sesuai | Perlu bukti performa lapangan lebih luas |
| 5 | Prediksi kondisi baterai berbasis historis | Ada service prediksi + metrik MAPE + penyimpanan histori | [BatteryPredictionService.cs](./Pigeon_Uno.Core/Services/AI/BatteryPredictionService.cs), [SqlitePIAHistoryStore.cs](./Pigeon_Uno.Core/Services/AI/SqlitePIAHistoryStore.cs) (tabel history + metrik), [App.xaml.cs](./Pigeon_Uno/App.xaml.cs) (baris `731-752`) | Sesuai | Perlu dataset flight nyata lebih besar untuk generalisasi |
| 6 | Interaksi bahasa alami (chat + voice command) | Chat command + voice recognition terintegrasi di runtime dan ViewModel | [App.xaml.cs](./Pigeon_Uno/App.xaml.cs) (baris `231`, `302-314`), [ChatViewModel.cs](./Pigeon_Uno/ViewModels/ChatViewModel.cs) (voice events/command handling) | Sesuai | Uji usability pilot nyata belum terdokumentasi lengkap |
| 7 | Optimasi biaya API (caching + pengurangan panggilan berulang) | Cache key/value + hit/miss + TTL + statistik health sudah ada | [OpenRouterService.cs](./Pigeon_Uno.Core/Services/AI/OpenRouterService.cs) (baris `54-56`, `254-267`, `586`) | Sesuai | Perlu baseline cost report sebelum/sesudah caching |
| 8 | Lintas platform + build APK Android | Target framework desktop/android/wasm dan format APK sudah dikonfigurasi | [Pigeon_Uno.csproj](./Pigeon_Uno/Pigeon_Uno.csproj) (baris `4`, `34`) | Sesuai | Validasi build Android di environment CI/dev perlu Android SDK terpasang |
| 9 | Validasi kuantitatif (Precision/Recall/MAPE/latency<3s) + UAT pilot | Gate target metrik sudah ditanam di aplikasi dan histori validasi tersimpan | [ChatViewModel.cs](./Pigeon_Uno/ViewModels/ChatViewModel.cs) (baris `57-60`, `1245-1261`), [SqlitePIAHistoryStore.cs](./Pigeon_Uno.Core/Services/AI/SqlitePIAHistoryStore.cs) | Belum | Belum ada paket hasil uji resmi end-to-end (real flight + laporan UAT) |

## Validasi Build/Test Terkini (26 April 2026)

- `dotnet build Pigeon_Uno/Pigeon_Uno/Pigeon_Uno.csproj -f net9.0-desktop` -> **sukses**
- `dotnet test Pigeon_Uno/Pigeon_Uno.Tests/Pigeon_Uno.Tests.csproj --filter "FullyQualifiedName~AISettingsTests|FullyQualifiedName~LLMServiceFactoryTests"` -> **29/29 lulus**
- `dotnet build Pigeon_Uno/Pigeon_Uno/Pigeon_Uno.csproj -f net9.0-android` -> **gagal di environment ini** (`XA5300`, Android SDK belum terpasang)

## Definisi "Siap Klaim Sesuai Proposal"

Dokumen ini menganggap proyek "siap klaim penuh" jika:

1. APK Android tervalidasi build/install minimal pada satu device target.
2. Laporan metrik kuantitatif final tersedia (Precision, Recall, MAPE, latency) dari skenario uji yang disepakati.
3. UAT pilot (skenario penerbangan nyata) terdokumentasi.
4. Narasi window telemetry (30 detik vs retention) diselaraskan dalam laporan teknis.

