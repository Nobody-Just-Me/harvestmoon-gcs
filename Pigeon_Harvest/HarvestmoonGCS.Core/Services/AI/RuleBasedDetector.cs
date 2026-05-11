using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Real-time rule-based anomaly detector for telemetry data.
/// Checks each TelemetrySnapshot against configurable rules and fires events when anomalies are detected.
/// </summary>
public class RuleBasedDetector : IAnomalyDetector
{
    private readonly AnomalyThresholds _thresholds;

    public string Name => "RuleBased";

    /// <summary>
    /// Event fired when an anomaly is detected.
    /// </summary>
    public event EventHandler<Anomaly>? AnomalyDetected;

    /// <summary>
    /// Creates a new RuleBasedDetector with the specified thresholds.
    /// </summary>
    /// <param name="thresholds">Configuration for battery, GPS, altitude, vibration, and speed thresholds</param>
    public RuleBasedDetector(AnomalyThresholds thresholds)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    /// <summary>
    /// Evaluates a telemetry snapshot against all rules and returns detected anomalies.
    /// Checks battery levels, GPS status, altitude limits, vibration, speed, descent rate, and battery drain.
    /// </summary>
    /// <param name="snapshot">Telemetry snapshot to evaluate</param>
    /// <returns>List of detected anomalies (may be empty)</returns>
    public IList<Anomaly> Evaluate(TelemetrySnapshot snapshot)
    {
        var anomalies = new List<Anomaly>();

        CheckBatteryRules(snapshot, anomalies);
        CheckGpsRules(snapshot, anomalies);
        CheckAltitudeRules(snapshot, anomalies);
        CheckVibrationRules(snapshot, anomalies);
        CheckSpeedRules(snapshot, anomalies);
        CheckDescentRules(snapshot, anomalies);
        CheckBatteryDrainRules(snapshot, anomalies);

        FireAnomalyEvents(anomalies);

        return anomalies;
    }

    private void CheckBatteryRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.BatteryPercent < _thresholds.BatteryCritical)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.BatteryCritical,
                AnomalySeverity.Critical,
                $"Battery critical: {snapshot.BatteryPercent:F1}% (below {_thresholds.BatteryCritical}%)",
                "Land immediately or return to launch",
                100));
        }
        else if (snapshot.BatteryPercent < _thresholds.BatteryWarning)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.BatteryWarning,
                AnomalySeverity.Warning,
                $"Battery warning: {snapshot.BatteryPercent:F1}% (below {_thresholds.BatteryWarning}%)",
                "Consider returning to launch",
                70));
        }
    }

    private void CheckGpsRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.GpsSatellites < _thresholds.GpsLostThreshold)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.GpsLost,
                AnomalySeverity.Critical,
                $"GPS lost: only {snapshot.GpsSatellites} satellites (below {_thresholds.GpsLostThreshold})",
                "Switch to non-GPS flight mode immediately",
                95));
        }
        else if (snapshot.GpsSatellites < _thresholds.GpsWarningThreshold)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.GpsDegraded,
                AnomalySeverity.Warning,
                $"GPS degraded: {snapshot.GpsSatellites} satellites (below {_thresholds.GpsWarningThreshold})",
                "Monitor position accuracy closely",
                60));
        }
    }

    private void CheckAltitudeRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.Altitude > _thresholds.AltitudeCritical)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.AttitudeUnstable,
                AnomalySeverity.Critical,
                $"Altitude critical: {snapshot.Altitude:F1}m (above {_thresholds.AltitudeCritical}m limit)",
                "Descend immediately to comply with altitude regulations",
                90));
        }
    }

    private void CheckVibrationRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        var vibrationSum = snapshot.VibrationX + snapshot.VibrationY + snapshot.VibrationZ;
        
        if (vibrationSum > _thresholds.HighVibrationThreshold)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.VibrationHigh,
                AnomalySeverity.Warning,
                $"High vibration detected: {vibrationSum:F1} (X:{snapshot.VibrationX:F1}, Y:{snapshot.VibrationY:F1}, Z:{snapshot.VibrationZ:F1})",
                "Check for loose components or propeller imbalance",
                65));
        }
    }

    private void CheckSpeedRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.Speed > _thresholds.HighSpeedThreshold)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.PerformanceSuboptimal,
                AnomalySeverity.Warning,
                $"High speed: {snapshot.Speed:F1} m/s (above {_thresholds.HighSpeedThreshold} m/s)",
                "Reduce speed for safer operation",
                55));
        }
    }

    private void CheckDescentRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.VerticalSpeed < _thresholds.RapidDescentThreshold)
        {
            anomalies.Add(CreateAnomaly(
                AnomalyType.AttitudeUnstable,
                AnomalySeverity.Warning,
                $"Rapid descent: {snapshot.VerticalSpeed:F1} m/s (below {_thresholds.RapidDescentThreshold} m/s)",
                "Reduce descent rate to avoid ground impact",
                75));
        }
    }

    private void CheckBatteryDrainRules(TelemetrySnapshot snapshot, List<Anomaly> anomalies)
    {
        if (snapshot.BatteryDrainRate.HasValue && snapshot.BatteryDrainRate.Value > 0)
        {
            var drainRate = snapshot.BatteryDrainRate.Value;
            var minutesUntilDepletion = snapshot.BatteryPercent / drainRate;

            if (minutesUntilDepletion <= _thresholds.LowBatteryDrainMinutes)
            {
                anomalies.Add(CreateAnomaly(
                    AnomalyType.BatteryCritical,
                    AnomalySeverity.Critical,
                    $"Rapid battery drain: {drainRate:F1}%/min, ~{minutesUntilDepletion:F1} min remaining",
                    "Land immediately - battery will deplete soon",
                    98));
            }
        }
    }

    private Anomaly CreateAnomaly(AnomalyType type, AnomalySeverity severity, string message, string recommendation, double priority)
    {
        return new Anomaly
        {
            Type = type,
            Severity = severity,
            Message = message,
            Recommendation = recommendation,
            Priority = priority,
            Timestamp = DateTime.UtcNow
        };
    }

    private void FireAnomalyEvents(List<Anomaly> anomalies)
    {
        foreach (var anomaly in anomalies)
        {
            AnomalyDetected?.Invoke(this, anomaly);
        }
    }

    public Task<IList<Anomaly>> EvaluateBatchAsync(IList<TelemetrySnapshot> snapshots)
    {
        var allAnomalies = new List<Anomaly>();
        foreach (var snapshot in snapshots)
        {
            allAnomalies.AddRange(Evaluate(snapshot));
        }
        return Task.FromResult<IList<Anomaly>>(allAnomalies);
    }
}
