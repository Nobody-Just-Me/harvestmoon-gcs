using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Core.Services.AI
{
    /// <summary>
    /// Z-score based anomaly detector that maintains per-field RunningStats.
    /// This detector operates on a 30-second cadence and updates statistics
    /// with every new snapshot.
    /// </summary>
    public class StatisticalDetector : IAnomalyDetector
    {
        private readonly AISettings _settings;
        private readonly Dictionary<string, RunningStats> _statsByField = new();
        private DateTime _lastAnalysis = DateTime.MinValue;
        private readonly bool _forceContinuous;

        public string Name => "Statistical";

        public StatisticalDetector(AISettings settings, bool forceContinuous = false)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _forceContinuous = forceContinuous;
        }

        /// <summary>
        /// Analyzes the provided snapshot and returns any detected anomalies based on z-scores.
        /// Maintains RunningStats per field and detects values that deviate significantly from the mean.
        /// </summary>
        /// <param name="snapshot">Telemetry snapshot to analyze</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of detected statistical outliers</returns>
        public List<Anomaly> Detect(TelemetrySnapshot snapshot, CancellationToken ct = default)
        {
            var anomalies = new List<Anomaly>();
            if (snapshot == null) return anomalies;

            // Cadence control: allow tests to force continuous mode; otherwise enforce 30s cadence
            if (!_forceContinuous)
            {
                if ((DateTime.UtcNow - _lastAnalysis).TotalSeconds < 30)
                    return anomalies;
                _lastAnalysis = DateTime.UtcNow;
            }

            // Fields to monitor (name, accessor)
            var fields = new List<(string Name, Func<TelemetrySnapshot, double> Getter)>
            {
                ("BatteryVoltage", s => s.BatteryVoltage),
                ("BatteryPercent", s => s.BatteryPercent),
                ("GpsLatitude", s => s.GpsLatitude),
                ("GpsLongitude", s => s.GpsLongitude),
                ("GpsAltitude", s => s.GpsAltitude),
                ("GpsSatellites", s => (double)s.GpsSatellites),
                ("GpsHdop", s => s.GpsHdop),
                ("Altitude", s => s.Altitude),
                ("Speed", s => s.Speed),
                ("VerticalSpeed", s => s.VerticalSpeed),
                ("Heading", s => s.Heading),
                ("Roll", s => s.Roll),
                ("Pitch", s => s.Pitch),
                ("Yaw", s => s.Yaw),
                ("WindSpeed", s => s.WindSpeed),
                ("WindDirection", s => s.WindDirection),
                ("VibrationX", s => s.VibrationX),
                ("VibrationY", s => s.VibrationY),
                ("VibrationZ", s => s.VibrationZ),
            };

            foreach (var (name, getter) in fields)
            {
                double value = getter(snapshot);
                if (!_statsByField.TryGetValue(name, out var rs))
                {
                    rs = new RunningStats();
                    _statsByField[name] = rs;
                }
                var prevMean = rs.Mean;
                rs.Add(value);

                // Compute z-score if we have enough data
                var stdDev = rs.StdDev;
                if (rs.Count > 1 && stdDev > 0)
                {
                    var z = (value - prevMean) / stdDev;
                    var threshold = _settings.AnomalyDetection.Thresholds.ZScoreThreshold;
                    if (Math.Abs(z) >= threshold)
                    {
                        anomalies.Add(new Anomaly
                        {
                            Type = AnomalyType.StatisticalOutlier,
                            Severity = Math.Abs(z) >= threshold * 2 ? AnomalySeverity.Critical : AnomalySeverity.Warning,
                            Message = $"Statistical outlier detected for {name}: z={z:0.000}",
                            Priority = Math.Abs(z)
                        });
                    }
                }
            }

            return anomalies;
        }

        public IList<Anomaly> Evaluate(TelemetrySnapshot snapshot)
        {
            return Detect(snapshot);
        }

        public Task<IList<Anomaly>> EvaluateBatchAsync(IList<TelemetrySnapshot> snapshots)
        {
            var allAnomalies = new List<Anomaly>();
            foreach (var snapshot in snapshots)
            {
                allAnomalies.AddRange(Detect(snapshot));
            }
            return Task.FromResult<IList<Anomaly>>(allAnomalies);
        }
    }
}
