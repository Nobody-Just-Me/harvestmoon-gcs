using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// Produces structured flight-session summaries for post-flight review and reporting.
/// </summary>
public sealed class FlightSessionSummaryService
{
    private readonly Func<ILLMService> _llmFactory;
    private readonly TelemetryBuffer _telemetryBuffer;
    private readonly object _gate = new();

    private bool _sessionActive;
    private DateTime _sessionStartedAt = DateTime.UtcNow;
    private int _sessionAnomalyCount;

    public FlightSessionSummary? LastSummary { get; private set; }

    public event EventHandler<FlightSessionSummary>? SummaryGenerated;

    public FlightSessionSummaryService(Func<ILLMService> llmFactory, TelemetryBuffer telemetryBuffer)
    {
        _llmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
    }

    public void SetConnectionState(bool isConnected)
    {
        lock (_gate)
        {
            if (isConnected && !_sessionActive)
            {
                _sessionActive = true;
                _sessionStartedAt = DateTime.UtcNow;
                _sessionAnomalyCount = 0;
            }
            else if (!isConnected && _sessionActive)
            {
                _sessionActive = false;
            }
        }
    }

    public void RegisterAnomalies(IReadOnlyList<Anomaly>? anomalies)
    {
        if (anomalies == null || anomalies.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            _sessionAnomalyCount += anomalies.Count(a => a != null && a.Severity != AnomalySeverity.Info);
        }
    }

    public async Task<FlightSessionSummary?> GenerateSummaryAsync(
        bool finalizeSession = false,
        CancellationToken ct = default)
    {
        var snapshots = _telemetryBuffer.GetAll();
        if (snapshots.Count < 2)
        {
            return null;
        }

        int anomalyCount;
        DateTime startedAt;
        lock (_gate)
        {
            anomalyCount = _sessionAnomalyCount;
            startedAt = _sessionStartedAt;
        }

        var heuristic = BuildHeuristicSummary(snapshots, anomalyCount, startedAt);
        var summary = heuristic;

        try
        {
            var llm = _llmFactory();
            if (llm.IsAvailable)
            {
                var llmSummary = await llm.GenerateStructuredAsync<FlightSessionSummary>(
                    BuildPrompt(heuristic),
                    LLMRole.FlightSessionSummary,
                    ct);

                if (llmSummary != null)
                {
                    summary = NormalizeLLMSummary(llmSummary, heuristic);
                }
            }
        }
        catch
        {
            // Keep heuristic fallback.
        }

        LastSummary = summary;
        SummaryGenerated?.Invoke(this, summary);

        if (finalizeSession)
        {
            lock (_gate)
            {
                _sessionActive = false;
                _sessionAnomalyCount = 0;
            }
        }

        return summary;
    }

    private static FlightSessionSummary BuildHeuristicSummary(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        int anomalyCount,
        DateTime sessionStartedAt)
    {
        var first = snapshots[0];
        var last = snapshots[^1];
        var safeStart = first.Timestamp == default ? sessionStartedAt : first.Timestamp.ToUniversalTime();
        var safeEnd = last.Timestamp == default ? DateTime.UtcNow : last.Timestamp.ToUniversalTime();
        var durationMinutes = Math.Max(0, (safeEnd - safeStart).TotalMinutes);
        var startBattery = Math.Clamp(first.BatteryPercent, 0, 100);
        var endBattery = Math.Clamp(last.BatteryPercent, 0, 100);
        var batteryUsed = Math.Max(0, startBattery - endBattery);
        var avgSpeed = snapshots.Average(s => Math.Max(0, s.Speed));
        var maxAltitude = snapshots.Max(s => s.Altitude);

        var status = DetermineStatus(endBattery, anomalyCount, snapshots);
        var summaryText = BuildSummaryText(durationMinutes, batteryUsed, avgSpeed, maxAltitude, anomalyCount, status);
        var recommendation = BuildRecommendation(status, endBattery, anomalyCount);
        var confidence = EstimateConfidence(snapshots.Count, durationMinutes);

        return new FlightSessionSummary
        {
            GeneratedAt = DateTime.UtcNow,
            SessionStart = safeStart,
            SessionEnd = safeEnd,
            SnapshotCount = snapshots.Count,
            DurationMinutes = durationMinutes,
            StartBatteryPercent = startBattery,
            EndBatteryPercent = endBattery,
            BatteryUsedPercent = batteryUsed,
            AvgSpeed = avgSpeed,
            MaxAltitude = maxAltitude,
            AnomalyCount = anomalyCount,
            OverallStatus = status,
            SummaryText = summaryText,
            RecommendedAction = recommendation,
            Confidence = confidence
        };
    }

    private static string DetermineStatus(double endBatteryPercent, int anomalyCount, IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var gpsDropouts = snapshots.Count(s => s.GpsSatellites == 0 || s.GpsHdop > 10);
        if (endBatteryPercent <= 20 || anomalyCount >= 5 || gpsDropouts >= 5)
        {
            return "CRITICAL";
        }

        if (endBatteryPercent <= 35 || anomalyCount >= 2 || gpsDropouts >= 2)
        {
            return "WARNING";
        }

        return "GOOD";
    }

    private static string BuildSummaryText(
        double durationMinutes,
        double batteryUsed,
        double avgSpeed,
        double maxAltitude,
        int anomalyCount,
        string status)
    {
        return $"Durasi penerbangan {durationMinutes:F1} menit, konsumsi baterai {batteryUsed:F1}%, " +
               $"rata-rata kecepatan {avgSpeed:F1} m/s, puncak altitude {maxAltitude:F1} m, " +
               $"anomali terdeteksi {anomalyCount}. Status keseluruhan: {status}.";
    }

    private static string BuildRecommendation(string status, double endBatteryPercent, int anomalyCount)
    {
        return status switch
        {
            "CRITICAL" => "Lakukan inspeksi menyeluruh sebelum misi berikutnya. Prioritaskan pemeriksaan baterai, GPS, dan kontrol getaran.",
            "WARNING" => $"Evaluasi ulang rencana misi dan pastikan cadangan baterai tersedia. Baterai akhir {endBatteryPercent:F1}% dengan {anomalyCount} anomali.",
            _ => "Kinerja misi stabil. Lanjutkan pemantauan berkala dan simpan baseline sebagai referensi misi berikutnya."
        };
    }

    private static double EstimateConfidence(int snapshotCount, double durationMinutes)
    {
        var sampleFactor = Math.Clamp(snapshotCount / 240.0, 0, 1);
        var durationFactor = Math.Clamp(durationMinutes / 20.0, 0, 1);
        return Math.Clamp(0.5 + (0.25 * sampleFactor) + (0.2 * durationFactor), 0.35, 0.95);
    }

    private static FlightSessionSummary NormalizeLLMSummary(FlightSessionSummary llmSummary, FlightSessionSummary fallback)
    {
        var normalized = new FlightSessionSummary
        {
            GeneratedAt = llmSummary.GeneratedAt == default ? DateTime.UtcNow : llmSummary.GeneratedAt.ToUniversalTime(),
            SessionStart = llmSummary.SessionStart == default ? fallback.SessionStart : llmSummary.SessionStart.ToUniversalTime(),
            SessionEnd = llmSummary.SessionEnd == default ? fallback.SessionEnd : llmSummary.SessionEnd.ToUniversalTime(),
            SnapshotCount = llmSummary.SnapshotCount > 0 ? llmSummary.SnapshotCount : fallback.SnapshotCount,
            DurationMinutes = llmSummary.DurationMinutes > 0 ? llmSummary.DurationMinutes : fallback.DurationMinutes,
            StartBatteryPercent = llmSummary.StartBatteryPercent > 0 ? llmSummary.StartBatteryPercent : fallback.StartBatteryPercent,
            EndBatteryPercent = llmSummary.EndBatteryPercent > 0 ? llmSummary.EndBatteryPercent : fallback.EndBatteryPercent,
            BatteryUsedPercent = llmSummary.BatteryUsedPercent >= 0 ? llmSummary.BatteryUsedPercent : fallback.BatteryUsedPercent,
            AvgSpeed = llmSummary.AvgSpeed > 0 ? llmSummary.AvgSpeed : fallback.AvgSpeed,
            MaxAltitude = llmSummary.MaxAltitude > 0 ? llmSummary.MaxAltitude : fallback.MaxAltitude,
            AnomalyCount = llmSummary.AnomalyCount >= 0 ? llmSummary.AnomalyCount : fallback.AnomalyCount,
            OverallStatus = string.IsNullOrWhiteSpace(llmSummary.OverallStatus) ? fallback.OverallStatus : llmSummary.OverallStatus.Trim().ToUpperInvariant(),
            SummaryText = string.IsNullOrWhiteSpace(llmSummary.SummaryText) ? fallback.SummaryText : llmSummary.SummaryText.Trim(),
            RecommendedAction = string.IsNullOrWhiteSpace(llmSummary.RecommendedAction) ? fallback.RecommendedAction : llmSummary.RecommendedAction.Trim(),
            Confidence = llmSummary.Confidence > 0 ? llmSummary.Confidence : fallback.Confidence
        };

        normalized.StartBatteryPercent = Math.Clamp(normalized.StartBatteryPercent, 0, 100);
        normalized.EndBatteryPercent = Math.Clamp(normalized.EndBatteryPercent, 0, 100);
        normalized.BatteryUsedPercent = Math.Clamp(normalized.BatteryUsedPercent, 0, 100);
        normalized.Confidence = Math.Clamp(normalized.Confidence, 0, 1);

        if (normalized.OverallStatus is not ("GOOD" or "WARNING" or "CRITICAL"))
        {
            normalized.OverallStatus = fallback.OverallStatus;
        }

        return normalized;
    }

    private static string BuildPrompt(FlightSessionSummary heuristic)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Anda adalah analis pasca-misi UAV untuk Ground Control Station.");
        sb.AppendLine("Buat ringkasan sesi penerbangan yang ringkas dan operasional.");
        sb.AppendLine();
        sb.AppendLine($"SessionStart: {heuristic.SessionStart:O}");
        sb.AppendLine($"SessionEnd: {heuristic.SessionEnd:O}");
        sb.AppendLine($"SnapshotCount: {heuristic.SnapshotCount}");
        sb.AppendLine($"DurationMinutes: {heuristic.DurationMinutes:F2}");
        sb.AppendLine($"StartBatteryPercent: {heuristic.StartBatteryPercent:F2}");
        sb.AppendLine($"EndBatteryPercent: {heuristic.EndBatteryPercent:F2}");
        sb.AppendLine($"BatteryUsedPercent: {heuristic.BatteryUsedPercent:F2}");
        sb.AppendLine($"AvgSpeed: {heuristic.AvgSpeed:F2}");
        sb.AppendLine($"MaxAltitude: {heuristic.MaxAltitude:F2}");
        sb.AppendLine($"AnomalyCount: {heuristic.AnomalyCount}");
        sb.AppendLine($"HeuristicStatus: {heuristic.OverallStatus}");
        sb.AppendLine();
        sb.AppendLine("Kembalikan JSON object dengan field:");
        sb.AppendLine("generatedAt,sessionStart,sessionEnd,snapshotCount,durationMinutes,startBatteryPercent,endBatteryPercent,batteryUsedPercent,avgSpeed,maxAltitude,anomalyCount,overallStatus,summaryText,recommendedAction,confidence");
        sb.AppendLine("overallStatus wajib: GOOD, WARNING, atau CRITICAL.");
        return sb.ToString();
    }
}

