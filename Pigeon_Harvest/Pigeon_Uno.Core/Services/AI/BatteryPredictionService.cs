using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// Dedicated battery prediction service with runtime MAPE evaluation support.
/// </summary>
public sealed class BatteryPredictionService
{
    private const int MaxApeSamples = 200;
    private static readonly TimeSpan ValidationMinimumAge = TimeSpan.FromMinutes(2);

    private readonly Func<ILLMService> _llmFactory;
    private readonly TelemetryBuffer _telemetryBuffer;
    private readonly object _gate = new();
    private readonly Queue<PendingPrediction> _pendingValidation = new();
    private readonly Queue<double> _apeSamples = new();

    public BatteryPrediction? LastPrediction { get; private set; }
    public BatteryPredictionMetrics LastMetrics { get; private set; } = new();

    public event EventHandler<BatteryPrediction>? PredictionUpdated;
    public event EventHandler<BatteryPredictionMetrics>? MetricsUpdated;

    public BatteryPredictionService(Func<ILLMService> llmFactory, TelemetryBuffer telemetryBuffer)
    {
        _llmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
    }

    public async Task<BatteryPrediction?> PredictAsync(CancellationToken ct = default)
    {
        var snapshots = _telemetryBuffer.GetAll();
        if (snapshots.Count < 2)
        {
            return null;
        }

        var heuristic = BuildHeuristicPrediction(snapshots);
        var prediction = heuristic;

        try
        {
            var llm = _llmFactory();
            if (llm.IsAvailable)
            {
                var llmResult = await llm.GenerateStructuredAsync<BatteryPrediction>(
                    BuildPrompt(snapshots, heuristic),
                    LLMRole.BatteryPrediction,
                    ct);

                if (llmResult != null)
                {
                    prediction = NormalizeLLMPrediction(llmResult, heuristic);
                }
            }
        }
        catch
        {
            // Keep heuristic fallback.
        }

        lock (_gate)
        {
            LastPrediction = prediction;
            TrackPredictionForMape(prediction);
            UpdateMapeSamplesWithLatestSnapshot(snapshots[^1]);
        }

        PredictionUpdated?.Invoke(this, prediction);
        MetricsUpdated?.Invoke(this, GetMetrics());
        return prediction;
    }

    public BatteryPredictionMetrics GetMetrics()
    {
        lock (_gate)
        {
            return new BatteryPredictionMetrics
            {
                SampleCount = LastMetrics.SampleCount,
                MeanAbsolutePercentageError = LastMetrics.MeanAbsolutePercentageError,
                LastUpdatedAt = LastMetrics.LastUpdatedAt
            };
        }
    }

    private static BatteryPrediction BuildHeuristicPrediction(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var latest = snapshots[^1];
        var currentBattery = Math.Clamp(latest.BatteryPercent, 0, 100);
        var drainRates = ComputeDrainRatesPerMinute(snapshots);
        var avgDrainRate = drainRates.Count > 0 ? drainRates.Average() : 1.8;
        avgDrainRate = Math.Clamp(avgDrainRate, 0.1, 30);

        var estimatedMinutes = currentBattery / avgDrainRate;
        estimatedMinutes = Math.Clamp(estimatedMinutes, 0, 240);

        var condition = DetermineCondition(currentBattery, estimatedMinutes);
        var confidence = EstimateConfidence(snapshots.Count, drainRates);
        var healthScore = EstimateHealthScore(currentBattery, avgDrainRate, drainRates);

        return new BatteryPrediction
        {
            Timestamp = latest.Timestamp == default ? DateTime.UtcNow : latest.Timestamp.ToUniversalTime(),
            CurrentBatteryPercent = currentBattery,
            EstimatedDrainRatePerMinute = avgDrainRate,
            EstimatedRemainingMinutes = estimatedMinutes,
            EstimatedDepletionAt = (latest.Timestamp == default ? DateTime.UtcNow : latest.Timestamp.ToUniversalTime()).AddMinutes(estimatedMinutes),
            HealthScore = healthScore,
            Condition = condition,
            Recommendation = BuildRecommendation(condition, estimatedMinutes),
            Confidence = confidence
        };
    }

    private static List<double> ComputeDrainRatesPerMinute(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var result = new List<double>();
        if (snapshots.Count < 2)
        {
            return result;
        }

        for (var i = 1; i < snapshots.Count; i++)
        {
            var previous = snapshots[i - 1];
            var current = snapshots[i];
            var elapsedMinutes = (current.Timestamp - previous.Timestamp).TotalMinutes;
            if (elapsedMinutes <= 0)
            {
                continue;
            }

            var drop = previous.BatteryPercent - current.BatteryPercent;
            if (drop > 0)
            {
                result.Add(drop / elapsedMinutes);
            }
        }

        return result;
    }

    private static string DetermineCondition(double batteryPercent, double minutesRemaining)
    {
        if (batteryPercent <= 20 || minutesRemaining <= 6)
        {
            return "CRITICAL";
        }

        if (batteryPercent <= 35 || minutesRemaining <= 15)
        {
            return "WARNING";
        }

        return "GOOD";
    }

    private static string BuildRecommendation(string condition, double minutesRemaining)
    {
        return condition switch
        {
            "CRITICAL" => "Segera lakukan landing atau RTL, baterai mendekati batas aman.",
            "WARNING" => $"Kurangi manuver agresif dan rencanakan pendaratan dalam {Math.Max(1, (int)Math.Round(minutesRemaining))} menit.",
            _ => "Kondisi baterai aman. Tetap pantau tren drain rate secara berkala."
        };
    }

    private static double EstimateConfidence(int sampleCount, IReadOnlyList<double> drainRates)
    {
        var sampleFactor = Math.Clamp(sampleCount / 120.0, 0.0, 1.0);
        var stability = 0.6;
        if (drainRates.Count > 1)
        {
            var mean = drainRates.Average();
            var variance = drainRates.Sum(x => Math.Pow(x - mean, 2)) / (drainRates.Count - 1);
            var stdDev = Math.Sqrt(Math.Max(variance, 0));
            var normalizedNoise = mean <= 0.1 ? 1.0 : Math.Clamp(stdDev / mean, 0, 1.5);
            stability = Math.Clamp(1.0 - (normalizedNoise / 1.5), 0.15, 1.0);
        }

        return Math.Clamp(0.45 + (0.25 * sampleFactor) + (0.30 * stability), 0.3, 0.95);
    }

    private static double EstimateHealthScore(double batteryPercent, double drainRate, IReadOnlyList<double> drainRates)
    {
        var volatilityPenalty = 0.0;
        if (drainRates.Count > 1)
        {
            var mean = drainRates.Average();
            var variance = drainRates.Sum(x => Math.Pow(x - mean, 2)) / (drainRates.Count - 1);
            volatilityPenalty = Math.Min(25, Math.Sqrt(Math.Max(variance, 0)) * 6);
        }

        var score = batteryPercent - (drainRate * 4.5) - volatilityPenalty;
        return Math.Clamp(score, 0, 100);
    }

    private static BatteryPrediction NormalizeLLMPrediction(BatteryPrediction llmResult, BatteryPrediction fallback)
    {
        var normalized = new BatteryPrediction
        {
            Timestamp = llmResult.Timestamp == default ? fallback.Timestamp : llmResult.Timestamp.ToUniversalTime(),
            CurrentBatteryPercent = llmResult.CurrentBatteryPercent > 0 ? llmResult.CurrentBatteryPercent : fallback.CurrentBatteryPercent,
            EstimatedDrainRatePerMinute = llmResult.EstimatedDrainRatePerMinute > 0 ? llmResult.EstimatedDrainRatePerMinute : fallback.EstimatedDrainRatePerMinute,
            EstimatedRemainingMinutes = llmResult.EstimatedRemainingMinutes > 0 ? llmResult.EstimatedRemainingMinutes : fallback.EstimatedRemainingMinutes,
            HealthScore = llmResult.HealthScore > 0 ? llmResult.HealthScore : fallback.HealthScore,
            Condition = string.IsNullOrWhiteSpace(llmResult.Condition) ? fallback.Condition : llmResult.Condition.Trim().ToUpperInvariant(),
            Recommendation = string.IsNullOrWhiteSpace(llmResult.Recommendation) ? fallback.Recommendation : llmResult.Recommendation.Trim(),
            Confidence = llmResult.Confidence > 0 ? llmResult.Confidence : fallback.Confidence
        };

        normalized.CurrentBatteryPercent = Math.Clamp(normalized.CurrentBatteryPercent, 0, 100);
        normalized.EstimatedDrainRatePerMinute = Math.Clamp(normalized.EstimatedDrainRatePerMinute, 0.1, 40);
        normalized.EstimatedRemainingMinutes = Math.Clamp(normalized.EstimatedRemainingMinutes, 0, 360);
        normalized.HealthScore = Math.Clamp(normalized.HealthScore, 0, 100);
        normalized.Confidence = Math.Clamp(normalized.Confidence, 0, 1);
        normalized.EstimatedDepletionAt = normalized.Timestamp.AddMinutes(normalized.EstimatedRemainingMinutes);

        if (normalized.Condition is not ("GOOD" or "WARNING" or "CRITICAL"))
        {
            normalized.Condition = fallback.Condition;
        }

        return normalized;
    }

    private static string BuildPrompt(IReadOnlyList<TelemetrySnapshot> snapshots, BatteryPrediction fallback)
    {
        var latest = snapshots[^1];
        var earliest = snapshots[0];
        var sb = new StringBuilder();
        sb.AppendLine("Anda adalah sistem prediksi baterai UAV untuk GCS.");
        sb.AppendLine("Prediksi sisa durasi baterai dan kondisi kesehatan baterai.");
        sb.AppendLine();
        sb.AppendLine($"WindowStart: {earliest.Timestamp:O}");
        sb.AppendLine($"WindowEnd: {latest.Timestamp:O}");
        sb.AppendLine($"SnapshotCount: {snapshots.Count}");
        sb.AppendLine($"CurrentBatteryPercent: {latest.BatteryPercent:F2}");
        sb.AppendLine($"CurrentBatteryVoltage: {latest.BatteryVoltage:F2}");
        sb.AppendLine($"HeuristicDrainRatePerMinute: {fallback.EstimatedDrainRatePerMinute:F4}");
        sb.AppendLine($"HeuristicRemainingMinutes: {fallback.EstimatedRemainingMinutes:F2}");
        sb.AppendLine();
        sb.AppendLine("Kembalikan JSON object dengan field:");
        sb.AppendLine("timestamp,currentBatteryPercent,estimatedDrainRatePerMinute,estimatedRemainingMinutes,estimatedDepletionAt,healthScore,condition,recommendation,confidence");
        sb.AppendLine("condition wajib: GOOD, WARNING, atau CRITICAL.");
        return sb.ToString();
    }

    private void TrackPredictionForMape(BatteryPrediction prediction)
    {
        _pendingValidation.Enqueue(new PendingPrediction
        {
            PredictedAt = prediction.Timestamp.ToUniversalTime(),
            BatteryAtPrediction = prediction.CurrentBatteryPercent,
            PredictedRemainingMinutes = prediction.EstimatedRemainingMinutes
        });

        while (_pendingValidation.Count > 200)
        {
            _pendingValidation.Dequeue();
        }
    }

    private void UpdateMapeSamplesWithLatestSnapshot(TelemetrySnapshot latestSnapshot)
    {
        if (_pendingValidation.Count == 0)
        {
            return;
        }

        var now = latestSnapshot.Timestamp == default ? DateTime.UtcNow : latestSnapshot.Timestamp.ToUniversalTime();
        var currentBattery = Math.Clamp(latestSnapshot.BatteryPercent, 0, 100);

        var pendingCount = _pendingValidation.Count;
        for (var i = 0; i < pendingCount; i++)
        {
            if (_pendingValidation.Count == 0)
            {
                break;
            }
            var pending = _pendingValidation.Dequeue();

            var elapsedMinutes = (now - pending.PredictedAt).TotalMinutes;
            var batteryDrop = pending.BatteryAtPrediction - currentBattery;
            if (elapsedMinutes < ValidationMinimumAge.TotalMinutes || batteryDrop < 1.0)
            {
                _pendingValidation.Enqueue(pending);
                continue;
            }

            var observedDrainPerMinute = batteryDrop / elapsedMinutes;
            if (observedDrainPerMinute <= 0.01)
            {
                continue;
            }

            var actualRemainingMinutes = pending.BatteryAtPrediction / observedDrainPerMinute;
            if (actualRemainingMinutes <= 0.01)
            {
                continue;
            }

            var ape = Math.Abs(pending.PredictedRemainingMinutes - actualRemainingMinutes) / actualRemainingMinutes * 100.0;
            _apeSamples.Enqueue(Math.Clamp(ape, 0, 500));

            while (_apeSamples.Count > MaxApeSamples)
            {
                _apeSamples.Dequeue();
            }
        }

        LastMetrics = new BatteryPredictionMetrics
        {
            SampleCount = _apeSamples.Count,
            MeanAbsolutePercentageError = _apeSamples.Count == 0 ? 0 : _apeSamples.Average(),
            LastUpdatedAt = now
        };
    }

    private sealed class PendingPrediction
    {
        public DateTime PredictedAt { get; set; }
        public double BatteryAtPrediction { get; set; }
        public double PredictedRemainingMinutes { get; set; }
    }
}
