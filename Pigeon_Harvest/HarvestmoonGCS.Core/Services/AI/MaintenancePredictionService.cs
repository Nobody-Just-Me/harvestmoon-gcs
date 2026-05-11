using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

public class MaintenancePredictionService
{
    private readonly Func<ILLMService> _llmFactory;
    private readonly TelemetryBuffer _telemetryBuffer;
    private readonly AISettings _settings;

    public MaintenanceSchedule? LastSchedule { get; private set; }
    public event EventHandler<MaintenanceSchedule>? ScheduleUpdated;

    public MaintenancePredictionService(
        Func<ILLMService> llmFactory,
        TelemetryBuffer telemetryBuffer,
        AISettings settings)
    {
        _llmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<List<MaintenancePrediction>> PredictAsync(CancellationToken ct = default)
    {
        var snapshots = _telemetryBuffer.GetAll();
        var features = ExtractFeatures(snapshots);

        if (snapshots.Count == 0)
        {
            return new List<MaintenancePrediction>();
        }

        var prompt = BuildPredictionPrompt(features, snapshots.Count);
        try
        {
            var llm = _llmFactory();
            if (llm.IsAvailable)
            {
                var llmPrediction = await llm.GenerateStructuredAsync<List<MaintenancePrediction>>(
                    prompt,
                    LLMRole.MaintenancePrediction,
                    ct);

                if (llmPrediction != null && llmPrediction.Count > 0)
                {
                    return llmPrediction
                        .Where(p => !string.IsNullOrWhiteSpace(p.Component))
                        .OrderBy(p => p.EstimatedDaysUntilMaintenance)
                        .ToList();
                }
            }
        }
        catch
        {
            // fallback to local heuristics
        }

        return PredictWithHeuristics(features);
    }

    public async Task<MaintenanceSchedule> GenerateScheduleAsync(CancellationToken ct = default)
    {
        var predictions = await PredictAsync(ct);
        var schedule = new MaintenanceSchedule
        {
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var prediction in predictions)
        {
            var task = new MaintenanceTask
            {
                Component = prediction.Component,
                Type = DetermineType(prediction),
                Priority = prediction.Severity,
                DueDate = DateTime.UtcNow.AddDays(Math.Max(1, prediction.EstimatedDaysUntilMaintenance)),
                RecommendedAction = prediction.RecommendedAction,
                IsCompleted = false
            };
            schedule.Tasks.Add(task);
        }

        LastSchedule = schedule;
        ScheduleUpdated?.Invoke(this, schedule);
        return schedule;
    }

    private static MaintenanceType DetermineType(MaintenancePrediction prediction)
    {
        var severity = (prediction.Severity ?? "low").ToLowerInvariant();
        return severity switch
        {
            "critical" => MaintenanceType.Replace,
            "high" => MaintenanceType.Repair,
            "medium" => MaintenanceType.Inspect,
            _ => MaintenanceType.Clean
        };
    }

    private static MaintenanceFeatures ExtractFeatures(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return new MaintenanceFeatures();
        }

        var batteryDrainRates = snapshots.Where(s => s.BatteryDrainRate.HasValue).Select(s => s.BatteryDrainRate!.Value).ToList();
        var vibrationAverage = snapshots.Average(s => Math.Abs(s.VibrationX) + Math.Abs(s.VibrationY) + Math.Abs(s.VibrationZ));

        return new MaintenanceFeatures
        {
            AvgBatteryVoltage = snapshots.Average(s => s.BatteryVoltage),
            AvgBatteryDrainRate = batteryDrainRates.Count > 0 ? batteryDrainRates.Average() : 0,
            AvgVibration = vibrationAverage,
            AvgWindSpeed = snapshots.Average(s => s.WindSpeed),
            MaxTemperature = 0,
            FlightCount = 1,
            TotalFlightMinutes = Math.Max(1, (snapshots.Max(s => s.Timestamp) - snapshots.Min(s => s.Timestamp)).TotalMinutes)
        };
    }

    private static List<MaintenancePrediction> PredictWithHeuristics(MaintenanceFeatures features)
    {
        var results = new List<MaintenancePrediction>();

        var batteryCondition = Math.Clamp(100 - (features.AvgBatteryDrainRate * 12), 25, 100);
        results.Add(new MaintenancePrediction
        {
            Component = "Battery Pack",
            CurrentCondition = batteryCondition,
            PredictedFailureMode = batteryCondition < 55 ? "Capacity degradation" : "Normal wear",
            EstimatedDaysUntilMaintenance = batteryCondition < 55 ? 7 : 21,
            Severity = batteryCondition < 40 ? "high" : batteryCondition < 60 ? "medium" : "low",
            RecommendedAction = batteryCondition < 55 ? "Lakukan cycle test dan siapkan penggantian baterai." : "Lanjutkan pemantauan rutin baterai.",
            Confidence = 0.72
        });

        var vibrationCondition = Math.Clamp(100 - features.AvgVibration, 20, 100);
        results.Add(new MaintenancePrediction
        {
            Component = "Motor/Propeller",
            CurrentCondition = vibrationCondition,
            PredictedFailureMode = vibrationCondition < 60 ? "Imbalance or bearing wear" : "Normal",
            EstimatedDaysUntilMaintenance = vibrationCondition < 60 ? 5 : 18,
            Severity = vibrationCondition < 45 ? "high" : vibrationCondition < 65 ? "medium" : "low",
            RecommendedAction = vibrationCondition < 60 ? "Periksa balancing propeller dan kondisi bearing motor." : "Inspeksi visual rutin propeller.",
            Confidence = 0.75
        });

        var gpsCondition = Math.Clamp(100 - (features.AvgWindSpeed * 2), 30, 100);
        results.Add(new MaintenancePrediction
        {
            Component = "GPS/Compass",
            CurrentCondition = gpsCondition,
            PredictedFailureMode = gpsCondition < 60 ? "Potential drift under disturbance" : "Normal",
            EstimatedDaysUntilMaintenance = gpsCondition < 60 ? 10 : 24,
            Severity = gpsCondition < 50 ? "medium" : "low",
            RecommendedAction = gpsCondition < 60 ? "Lakukan kalibrasi compass dan validasi GPS fix." : "Kalibrasi berkala sesuai jadwal.",
            Confidence = 0.68
        });

        return results.OrderBy(p => p.EstimatedDaysUntilMaintenance).ToList();
    }

    private string BuildPredictionPrompt(MaintenanceFeatures features, int snapshotCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Anda adalah sistem predictive maintenance untuk UAV.");
        sb.AppendLine("Gunakan data berikut untuk memprediksi kebutuhan maintenance.");
        sb.AppendLine();
        sb.AppendLine($"SnapshotCount: {snapshotCount}");
        sb.AppendLine($"AvgBatteryVoltage: {features.AvgBatteryVoltage:F2}");
        sb.AppendLine($"AvgBatteryDrainRate: {features.AvgBatteryDrainRate:F3}");
        sb.AppendLine($"AvgVibration: {features.AvgVibration:F3}");
        sb.AppendLine($"AvgWindSpeed: {features.AvgWindSpeed:F2}");
        sb.AppendLine($"TotalFlightMinutes: {features.TotalFlightMinutes:F1}");
        sb.AppendLine();
        sb.AppendLine("Kembalikan JSON array maksimal 5 item dengan field:");
        sb.AppendLine("component,currentCondition,predictedFailureMode,estimatedDaysUntilMaintenance,severity,recommendedAction,confidence");
        return sb.ToString();
    }
}
