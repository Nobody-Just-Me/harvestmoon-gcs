using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Services;

public sealed class ResearchTelemetryExportService
{
    private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;

    private readonly ITelemetryStore _telemetryStore;
    private readonly List<ResearchSessionEvent> _events = new();
    private readonly object _sessionLock = new();
    private ResearchSessionInfo? _currentSession;

    public ResearchTelemetryExportService(ITelemetryStore telemetryStore)
    {
        _telemetryStore = telemetryStore;
    }

    public async Task<ResearchTelemetryExportResult> ExportAllTelemetryCsvAsync()
    {
        var records = await _telemetryStore.GetTelemetryRangeAsync(DateTime.MinValue, DateTime.MaxValue);
        var exportDirectory = Path.Combine(
            GetResearchExportDirectory(),
            $"validation_package_{DateTime.Now:yyyyMMdd_HHmmss}");

        await WriteValidationPackageAsync(
            exportDirectory,
            records,
            Array.Empty<ResearchSessionEvent>(),
            null,
            "unlabeled");

        return new ResearchTelemetryExportResult(exportDirectory, records.Count);
    }

    public ResearchSessionInfo StartSession(string sessionName, string conditionLabel, string notes)
    {
        lock (_sessionLock)
        {
            if (_currentSession?.IsActive == true)
            {
                throw new InvalidOperationException("Masih ada sesi penelitian yang sedang berjalan. Stop sesi dulu sebelum mulai sesi baru.");
            }

            var now = DateTime.UtcNow;
            _events.Clear();
            _currentSession = new ResearchSessionInfo
            {
                SessionName = string.IsNullOrWhiteSpace(sessionName)
                    ? $"research_session_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : sessionName.Trim(),
                ConditionLabel = string.IsNullOrWhiteSpace(conditionLabel) ? "normal" : conditionLabel.Trim(),
                Notes = notes?.Trim() ?? string.Empty,
                StartedAtUtc = now,
                IsActive = true
            };

            _events.Add(new ResearchSessionEvent(now, "session_start", _currentSession.ConditionLabel, "Sesi penelitian dimulai."));
            return _currentSession;
        }
    }

    public ResearchSessionInfo StopSession(string notes)
    {
        lock (_sessionLock)
        {
            if (_currentSession?.IsActive != true)
            {
                throw new InvalidOperationException("Belum ada sesi penelitian yang sedang berjalan.");
            }

            _currentSession.EndedAtUtc = DateTime.UtcNow;
            _currentSession.IsActive = false;
            if (!string.IsNullOrWhiteSpace(notes) &&
                !string.Equals(_currentSession.Notes, notes.Trim(), StringComparison.Ordinal))
            {
                _currentSession.Notes = string.IsNullOrWhiteSpace(_currentSession.Notes)
                    ? notes.Trim()
                    : $"{_currentSession.Notes} | Stop notes: {notes.Trim()}";
            }

            _events.Add(new ResearchSessionEvent(_currentSession.EndedAtUtc.Value, "session_stop", _currentSession.ConditionLabel, "Sesi penelitian dihentikan."));
            return _currentSession;
        }
    }

    public ResearchSessionEvent MarkEvent(string eventType, string label, string notes)
    {
        lock (_sessionLock)
        {
            if (_currentSession?.IsActive != true)
            {
                throw new InvalidOperationException("Mulai sesi penelitian dulu sebelum menandai kejadian.");
            }

            var sessionLabel = string.IsNullOrWhiteSpace(label)
                ? _currentSession.ConditionLabel
                : label.Trim();
            var sessionEvent = new ResearchSessionEvent(
                DateTime.UtcNow,
                string.IsNullOrWhiteSpace(eventType) ? "manual_marker" : eventType.Trim(),
                sessionLabel,
                notes?.Trim() ?? string.Empty);

            _events.Add(sessionEvent);
            return sessionEvent;
        }
    }

    public async Task<ResearchTelemetryExportResult> ExportCurrentSessionAsync()
    {
        ResearchSessionInfo session;
        List<ResearchSessionEvent> events;

        lock (_sessionLock)
        {
            if (_currentSession == null)
            {
                throw new InvalidOperationException("Belum ada sesi penelitian untuk diekspor.");
            }

            session = _currentSession.Clone();
            events = _events.ToList();
        }

        var end = session.EndedAtUtc ?? DateTime.UtcNow;
        var records = await _telemetryStore.GetTelemetryRangeAsync(session.StartedAtUtc, end);
        var sessionDirectory = GetSessionDirectory(session);
        await WriteValidationPackageAsync(sessionDirectory, records, events, session, session.ConditionLabel);

        return new ResearchTelemetryExportResult(sessionDirectory, records.Count);
    }

    public ResearchSessionInfo? GetCurrentSession()
    {
        lock (_sessionLock)
        {
            return _currentSession?.Clone();
        }
    }

    private static string GetResearchExportDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            documents = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents");
        }

        return Path.Combine(documents, "Pigeon_Uno", "research_exports");
    }

    private static string GetSessionDirectory(ResearchSessionInfo session)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            documents = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents");
        }

        var folderName = $"{session.StartedAtUtc.ToLocalTime():yyyyMMdd_HHmmss}_{SanitizeFileName(session.SessionName)}";
        return Path.Combine(documents, "Pigeon_Uno", "research_sessions", folderName);
    }

    private static string BuildResearchCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "timestamp_utc,latitude,longitude,altitude_m,relative_altitude_m,ground_speed_mps,air_speed_mps,vertical_speed_mps,roll_deg,pitch_deg,yaw_deg,heading_deg,battery_voltage_v,battery_current_a,battery_percent,gps_satellite_count,gps_hdop,gps_fix_type,signal_strength,throttle_percent,flight_mode,is_armed,source,label,notes");

        foreach (var record in records)
        {
            AppendCsvRow(builder, record, defaultLabel);
        }

        return builder.ToString();
    }

    private static async Task WriteValidationPackageAsync(
        string rootDirectory,
        IReadOnlyList<TelemetryData> records,
        IReadOnlyList<ResearchSessionEvent> events,
        ResearchSessionInfo? session,
        string defaultLabel)
    {
        Directory.CreateDirectory(rootDirectory);

        var masterDirectory = Path.Combine(rootDirectory, "00_master_dataset");
        var anomalyDirectory = Path.Combine(rootDirectory, "01_validasi_anomali_precision_recall");
        var batteryDirectory = Path.Combine(rootDirectory, "02_validasi_prediksi_baterai_mape");
        var gpsDirectory = Path.Combine(rootDirectory, "03_validasi_kualitas_gps");
        var attitudeDirectory = Path.Combine(rootDirectory, "04_validasi_attitude_stability");
        var performanceDirectory = Path.Combine(rootDirectory, "05_validasi_performa_sistem");
        var documentationDirectory = Path.Combine(rootDirectory, "06_metadata_dan_event");

        foreach (var directory in new[]
        {
            masterDirectory,
            anomalyDirectory,
            batteryDirectory,
            gpsDirectory,
            attitudeDirectory,
            performanceDirectory,
            documentationDirectory
        })
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            Path.Combine(masterDirectory, "telemetry_master.csv"),
            BuildResearchCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(anomalyDirectory, "anomaly_validation_precision_recall.csv"),
            BuildAnomalyValidationCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(batteryDirectory, "battery_prediction_mape.csv"),
            BuildBatteryValidationCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(gpsDirectory, "gps_quality_validation.csv"),
            BuildGpsValidationCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(attitudeDirectory, "attitude_stability_validation.csv"),
            BuildAttitudeValidationCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(performanceDirectory, "system_performance_latency_template.csv"),
            BuildPerformanceValidationCsv(records, defaultLabel));

        await File.WriteAllTextAsync(
            Path.Combine(documentationDirectory, "events.csv"),
            BuildEventsCsv(events));

        await File.WriteAllTextAsync(
            Path.Combine(documentationDirectory, "metadata.json"),
            BuildMetadataJson(session, events, records, rootDirectory));

        await File.WriteAllTextAsync(
            Path.Combine(rootDirectory, "summary.txt"),
            BuildSummary(session, events, records));

        await File.WriteAllTextAsync(
            Path.Combine(rootDirectory, "README_VALIDASI.txt"),
            BuildValidationReadme());
    }

    private static void AppendCsvRow(StringBuilder builder, TelemetryData record, string defaultLabel)
    {
        var values = new[]
        {
            record.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CsvCulture),
            Format(record.Latitude, 7),
            Format(record.Longitude, 7),
            Format(record.Altitude, 3),
            Format(record.RelativeAltitude, 3),
            Format(record.GroundSpeed, 3),
            Format(record.AirSpeed, 3),
            Format(record.VerticalSpeed, 3),
            Format(record.Roll, 3),
            Format(record.Pitch, 3),
            Format(record.Yaw, 3),
            Format(record.Heading, 3),
            Format(record.BatteryVoltage, 3),
            Format(record.BatteryCurrent, 3),
            Format(record.BatteryPercentage, 2),
            record.SatelliteCount.ToString(CsvCulture),
            Format(record.HDOP, 2),
            record.GPSFixType.ToString(CsvCulture),
            record.SignalStrength.ToString(CsvCulture),
            record.ThrottlePercent.ToString(CsvCulture),
            record.FlightMode.ToString(),
            record.IsArmed ? "true" : "false",
            "mavlink_gcs",
            string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
            string.Empty
        };

        builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
    }

    private static string BuildEventsCsv(IReadOnlyList<ResearchSessionEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,event_type,label,notes");

        foreach (var sessionEvent in events)
        {
            var values = new[]
            {
                sessionEvent.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CsvCulture),
                sessionEvent.EventType,
                sessionEvent.Label,
                sessionEvent.Notes
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildMetadataJson(
        ResearchSessionInfo? session,
        IReadOnlyList<ResearchSessionEvent> events,
        IReadOnlyList<TelemetryData> records,
        string exportDirectory)
    {
        var metadata = new
        {
            export_directory = exportDirectory,
            export_type = session == null ? "all_telemetry_validation_package" : "research_session_validation_package",
            session_name = session?.SessionName ?? "all_telemetry",
            condition_label = session?.ConditionLabel ?? "unlabeled",
            started_at_utc = session?.StartedAtUtc,
            ended_at_utc = session?.EndedAtUtc,
            duration_seconds = session == null ? null : (double?)(((session.EndedAtUtc ?? DateTime.UtcNow) - session.StartedAtUtc).TotalSeconds),
            notes = session?.Notes ?? string.Empty,
            telemetry_rows = records.Count,
            event_count = events.Count,
            generated_files = new[]
            {
                "00_master_dataset/telemetry_master.csv",
                "01_validasi_anomali_precision_recall/anomaly_validation_precision_recall.csv",
                "02_validasi_prediksi_baterai_mape/battery_prediction_mape.csv",
                "03_validasi_kualitas_gps/gps_quality_validation.csv",
                "04_validasi_attitude_stability/attitude_stability_validation.csv",
                "05_validasi_performa_sistem/system_performance_latency_template.csv",
                "06_metadata_dan_event/events.csv",
                "06_metadata_dan_event/metadata.json",
                "summary.txt",
                "README_VALIDASI.txt"
            },
            master_columns = new[]
            {
                "timestamp_utc",
                "latitude",
                "longitude",
                "altitude_m",
                "ground_speed_mps",
                "roll_deg",
                "pitch_deg",
                "yaw_deg",
                "battery_voltage_v",
                "battery_current_a",
                "battery_percent",
                "gps_satellite_count",
                "gps_hdop",
                "flight_mode",
                "is_armed",
                "label"
            },
            research_metrics_supported = new[]
            {
                "Precision/Recall: gunakan file anomaly_validation_precision_recall.csv dan isi ground_truth_label",
                "MAPE baterai: gunakan battery_prediction_mape.csv dan isi predicted_battery_percent jika prediksi sistem tersedia",
                "Validasi GPS: gunakan gps_quality_validation.csv",
                "Validasi attitude: gunakan attitude_stability_validation.csv",
                "Validasi respons sistem: gunakan system_performance_latency_template.csv dan gabungkan log latency aplikasi bila tersedia"
            }
        };

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildSummary(
        ResearchSessionInfo? session,
        IReadOnlyList<ResearchSessionEvent> events,
        IReadOnlyList<TelemetryData> records)
    {
        var start = session?.StartedAtUtc ?? records.FirstOrDefault()?.Timestamp ?? DateTime.UtcNow;
        var end = session?.EndedAtUtc ?? records.LastOrDefault()?.Timestamp ?? DateTime.UtcNow;
        var builder = new StringBuilder();
        builder.AppendLine("Pigeon GCS - Ringkasan Paket Validasi Penelitian");
        builder.AppendLine($"Nama sesi      : {session?.SessionName ?? "all_telemetry"}");
        builder.AppendLine($"Label kondisi  : {session?.ConditionLabel ?? "unlabeled"}");
        builder.AppendLine($"Mulai UTC      : {start:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Selesai UTC    : {end:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Durasi         : {(end - start).TotalSeconds:F0} detik");
        builder.AppendLine($"Jumlah data    : {records.Count} baris");
        builder.AppendLine($"Jumlah event   : {events.Count}");
        builder.AppendLine($"Catatan        : {session?.Notes ?? string.Empty}");

        if (records.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Ringkasan Telemetri:");
            builder.AppendLine($"Battery awal   : {records.First().BatteryPercentage:F2}%");
            builder.AppendLine($"Battery akhir  : {records.Last().BatteryPercentage:F2}%");
            builder.AppendLine($"Battery min    : {records.Min(x => x.BatteryPercentage):F2}%");
            builder.AppendLine($"Voltage min    : {records.Min(x => x.BatteryVoltage):F3} V");
            builder.AppendLine($"Altitude max   : {records.Max(x => x.Altitude):F3} m");
            builder.AppendLine($"Speed max      : {records.Max(x => x.GroundSpeed):F3} m/s");
            builder.AppendLine($"GPS sat min    : {records.Min(x => x.SatelliteCount)}");
            builder.AppendLine($"HDOP max       : {records.Max(x => x.HDOP):F2}");
            builder.AppendLine($"Roll max abs   : {records.Max(x => Math.Abs(x.Roll)):F3} deg");
            builder.AppendLine($"Pitch max abs  : {records.Max(x => Math.Abs(x.Pitch)):F3} deg");
        }

        if (events.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Event Marker:");
            foreach (var sessionEvent in events)
            {
                builder.AppendLine($"- {sessionEvent.TimestampUtc:HH:mm:ss.fff} | {sessionEvent.EventType} | {sessionEvent.Label} | {sessionEvent.Notes}");
            }
        }

        return builder.ToString();
    }

    private static string BuildAnomalyValidationCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,battery_percent,battery_voltage_v,gps_satellite_count,gps_hdop,roll_abs_deg,pitch_abs_deg,altitude_m,ground_speed_mps,rule_low_battery,rule_gps_weak,rule_attitude_abnormal,rule_link_loss,predicted_rule_label,ground_truth_label,notes");

        foreach (var record in records)
        {
            var lowBattery = record.BatteryPercentage > 0 && record.BatteryPercentage <= 20;
            var gpsWeak = record.SatelliteCount > 0 && (record.SatelliteCount < 6 || record.HDOP > 2.5);
            var attitudeAbnormal = Math.Abs(record.Roll) > 35 || Math.Abs(record.Pitch) > 35;
            var linkLoss = false;
            var predicted = lowBattery
                ? "low_battery"
                : gpsWeak
                    ? "gps_weak"
                    : attitudeAbnormal
                        ? "attitude_abnormal"
                        : "normal";

            var values = new[]
            {
                Timestamp(record),
                Format(record.BatteryPercentage, 2),
                Format(record.BatteryVoltage, 3),
                record.SatelliteCount.ToString(CsvCulture),
                Format(record.HDOP, 2),
                Format(Math.Abs(record.Roll), 3),
                Format(Math.Abs(record.Pitch), 3),
                Format(record.Altitude, 3),
                Format(record.GroundSpeed, 3),
                lowBattery ? "1" : "0",
                gpsWeak ? "1" : "0",
                attitudeAbnormal ? "1" : "0",
                linkLoss ? "1" : "0",
                predicted,
                string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
                string.Empty
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildBatteryValidationCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,battery_voltage_v,battery_current_a,battery_percent_actual,predicted_battery_percent,absolute_percentage_error,estimated_remaining_time_sec,ground_speed_mps,altitude_m,flight_mode,ground_truth_label,notes");

        foreach (var record in records)
        {
            var values = new[]
            {
                Timestamp(record),
                Format(record.BatteryVoltage, 3),
                Format(record.BatteryCurrent, 3),
                Format(record.BatteryPercentage, 2),
                string.Empty,
                string.Empty,
                string.Empty,
                Format(record.GroundSpeed, 3),
                Format(record.Altitude, 3),
                record.FlightMode.ToString(),
                string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
                string.Empty
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildGpsValidationCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,latitude,longitude,gps_satellite_count,gps_hdop,gps_fix_type,gps_quality_rule,ground_truth_label,notes");

        foreach (var record in records)
        {
            var quality = record.SatelliteCount <= 0
                ? "unknown"
                : record.SatelliteCount < 6 || record.HDOP > 2.5
                    ? "gps_weak"
                    : "gps_ok";

            var values = new[]
            {
                Timestamp(record),
                Format(record.Latitude, 7),
                Format(record.Longitude, 7),
                record.SatelliteCount.ToString(CsvCulture),
                Format(record.HDOP, 2),
                record.GPSFixType.ToString(CsvCulture),
                quality,
                string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
                string.Empty
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildAttitudeValidationCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,roll_deg,pitch_deg,yaw_deg,heading_deg,ground_speed_mps,altitude_m,attitude_quality_rule,ground_truth_label,notes");

        foreach (var record in records)
        {
            var quality = Math.Abs(record.Roll) > 35 || Math.Abs(record.Pitch) > 35
                ? "attitude_abnormal"
                : "attitude_normal";

            var values = new[]
            {
                Timestamp(record),
                Format(record.Roll, 3),
                Format(record.Pitch, 3),
                Format(record.Yaw, 3),
                Format(record.Heading, 3),
                Format(record.GroundSpeed, 3),
                Format(record.Altitude, 3),
                quality,
                string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
                string.Empty
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildPerformanceValidationCsv(IReadOnlyList<TelemetryData> records, string defaultLabel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,telemetry_delta_ms,flight_mode,is_armed,battery_percent,gps_satellite_count,latency_ms,api_provider,api_token_input,api_token_output,api_cost,ground_truth_label,notes");

        DateTime? previous = null;
        foreach (var record in records)
        {
            var deltaMs = previous == null
                ? string.Empty
                : (record.Timestamp.ToUniversalTime() - previous.Value.ToUniversalTime()).TotalMilliseconds.ToString("F0", CsvCulture);
            previous = record.Timestamp;

            var values = new[]
            {
                Timestamp(record),
                deltaMs,
                record.FlightMode.ToString(),
                record.IsArmed ? "true" : "false",
                Format(record.BatteryPercentage, 2),
                record.SatelliteCount.ToString(CsvCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.IsNullOrWhiteSpace(defaultLabel) ? "unlabeled" : defaultLabel,
                string.Empty
            };

            builder.AppendLine(string.Join(",", Array.ConvertAll(values, EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string BuildValidationReadme()
    {
        return """
        Pigeon GCS - Paket Data Validasi Penelitian

        Folder ini dibuat dengan satu kali export dari GCS Pigeon.

        Isi folder:
        00_master_dataset/telemetry_master.csv
        - Dataset telemetri lengkap dari MAVLink/GCS.

        01_validasi_anomali_precision_recall/anomaly_validation_precision_recall.csv
        - Dipakai untuk validasi deteksi anomali.
        - Isi/cek kolom ground_truth_label untuk menghitung Precision, Recall, dan F1-score.

        02_validasi_prediksi_baterai_mape/battery_prediction_mape.csv
        - Dipakai untuk validasi prediksi baterai.
        - Isi kolom predicted_battery_percent jika ada hasil prediksi PIA, lalu hitung MAPE.

        03_validasi_kualitas_gps/gps_quality_validation.csv
        - Dipakai untuk validasi kondisi GPS normal/lemah/hilang.

        04_validasi_attitude_stability/attitude_stability_validation.csv
        - Dipakai untuk validasi roll/pitch/yaw dan kestabilan wahana.

        05_validasi_performa_sistem/system_performance_latency_template.csv
        - Dipakai untuk validasi interval telemetry, latency analisis, API provider, token, dan biaya.
        - Kolom latency/API disediakan sebagai template bila digabungkan dengan log PIA.

        06_metadata_dan_event/events.csv
        - Marker kejadian yang ditekan operator saat pengujian.

        06_metadata_dan_event/metadata.json
        - Informasi sesi, label, jumlah data, daftar file, dan metrik penelitian.
        """;
    }

    private static string Format(double value, int decimals)
    {
        return value.ToString($"F{decimals}", CsvCulture);
    }

    private static string Timestamp(TelemetryData record)
    {
        return record.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CsvCulture);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        sanitized = sanitized.Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "research_session" : sanitized;
    }
}

public sealed record ResearchTelemetryExportResult(string FilePath, int RecordCount);

public sealed class ResearchSessionInfo
{
    public string SessionName { get; set; } = string.Empty;
    public string ConditionLabel { get; set; } = "normal";
    public string Notes { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool IsActive { get; set; }

    public ResearchSessionInfo Clone()
    {
        return new ResearchSessionInfo
        {
            SessionName = SessionName,
            ConditionLabel = ConditionLabel,
            Notes = Notes,
            StartedAtUtc = StartedAtUtc,
            EndedAtUtc = EndedAtUtc,
            IsActive = IsActive
        };
    }
}

public sealed record ResearchSessionEvent(DateTime TimestampUtc, string EventType, string Label, string Notes);
