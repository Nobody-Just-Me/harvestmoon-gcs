using System;
using System.Collections.Generic;
using System.Linq;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Runtime evaluator that computes confusion-matrix based anomaly metrics.
/// "Actual anomaly" is derived from safety thresholds to provide a consistent baseline.
/// </summary>
public sealed class AnomalyEvaluationService : IAnomalyEvaluationService
{
    private readonly AISettings _settings;
    private readonly object _gate = new();
    private readonly AnomalyEvaluationMetrics _metrics = new();

    public event EventHandler<AnomalyEvaluationMetrics>? MetricsUpdated;

    public AnomalyEvaluationService(AISettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void ObserveSnapshot(TelemetrySnapshot snapshot, IReadOnlyList<Anomaly>? predictedAnomalies)
    {
        if (snapshot == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var predictedPositive = (predictedAnomalies ?? Array.Empty<Anomaly>())
            .Any(a =>
                a != null &&
                a.Severity != AnomalySeverity.Info &&
                (now - a.Timestamp.ToUniversalTime()) <= TimeSpan.FromSeconds(10));

        var actualPositive = IsActualAnomaly(snapshot);

        lock (_gate)
        {
            if (predictedPositive && actualPositive)
            {
                _metrics.TruePositive++;
            }
            else if (predictedPositive && !actualPositive)
            {
                _metrics.FalsePositive++;
            }
            else if (!predictedPositive && actualPositive)
            {
                _metrics.FalseNegative++;
            }
            else
            {
                _metrics.TrueNegative++;
            }

            _metrics.LastUpdatedAt = now;
        }

        MetricsUpdated?.Invoke(this, GetMetrics());
    }

    public AnomalyEvaluationMetrics GetMetrics()
    {
        lock (_gate)
        {
            return new AnomalyEvaluationMetrics
            {
                TruePositive = _metrics.TruePositive,
                FalsePositive = _metrics.FalsePositive,
                TrueNegative = _metrics.TrueNegative,
                FalseNegative = _metrics.FalseNegative,
                LastUpdatedAt = _metrics.LastUpdatedAt
            };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _metrics.TruePositive = 0;
            _metrics.FalsePositive = 0;
            _metrics.TrueNegative = 0;
            _metrics.FalseNegative = 0;
            _metrics.LastUpdatedAt = DateTime.UtcNow;
        }

        MetricsUpdated?.Invoke(this, GetMetrics());
    }

    private bool IsActualAnomaly(TelemetrySnapshot snapshot)
    {
        var thresholds = _settings.AnomalyDetection?.Thresholds ?? new AnomalyThresholds();

        if (snapshot.BatteryPercent > 0 && snapshot.BatteryPercent <= thresholds.BatteryCritical)
        {
            return true;
        }

        if (snapshot.GpsSatellites > 0 && snapshot.GpsSatellites <= thresholds.GpsLostThreshold)
        {
            return true;
        }

        if (snapshot.GpsHdop >= 2.5)
        {
            return true;
        }

        if (snapshot.Altitude >= thresholds.AltitudeCritical)
        {
            return true;
        }

        if (snapshot.Speed >= thresholds.HighSpeedThreshold)
        {
            return true;
        }

        if (snapshot.VerticalSpeed <= thresholds.RapidDescentThreshold)
        {
            return true;
        }

        var vibrationMagnitude = Math.Abs(snapshot.VibrationX) + Math.Abs(snapshot.VibrationY) + Math.Abs(snapshot.VibrationZ);
        if (vibrationMagnitude >= thresholds.HighVibrationThreshold)
        {
            return true;
        }

        if (snapshot.WindSpeed >= thresholds.WindMaxSpeed)
        {
            return true;
        }

        return false;
    }
}

