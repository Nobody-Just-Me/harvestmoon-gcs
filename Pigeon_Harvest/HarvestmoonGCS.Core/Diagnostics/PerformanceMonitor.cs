using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Interface for monitoring performance of the telemetry pipeline.
    /// Tracks latency at each stage and overall update rates.
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Records the latency for a specific pipeline stage.
        /// </summary>
        /// <param name="stage">Name of the pipeline stage (e.g., "Transport", "Walker", "Parser")</param>
        /// <param name="latency">Time taken for this stage</param>
        void RecordStageLatency(string stage, TimeSpan latency);

        /// <summary>
        /// Records the current update rate in updates per second.
        /// </summary>
        /// <param name="updatesPerSecond">Number of updates in the last second</param>
        void RecordUpdateRate(int updatesPerSecond);

        /// <summary>
        /// Gets a comprehensive performance report with latency statistics and update rates.
        /// </summary>
        /// <returns>Performance report with all collected metrics</returns>
        PerformanceReport GetReport();
    }

    /// <summary>
    /// Monitors performance of the telemetry pipeline by tracking latency and update rates.
    /// Validates Requirements 9.1, 9.2, 9.3, 9.4.
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, List<TimeSpan>> _latencies = new();
        private readonly List<int> _updateRates = new();
        private readonly object _lock = new();
        private const int MAX_SAMPLES = 1000;

        /// <summary>
        /// Records the latency for a specific pipeline stage.
        /// Requirement 9.1: Measure and log time taken at each pipeline stage.
        /// </summary>
        public void RecordStageLatency(string stage, TimeSpan latency)
        {
            if (string.IsNullOrWhiteSpace(stage))
                throw new ArgumentException("Stage name cannot be null or empty", nameof(stage));

            _latencies.AddOrUpdate(stage,
                _ => new List<TimeSpan> { latency },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(latency);
                        // Keep only the most recent samples to prevent unbounded growth
                        if (list.Count > MAX_SAMPLES)
                            list.RemoveAt(0);
                    }
                    return list;
                });
        }

        /// <summary>
        /// Records the current update rate in updates per second.
        /// Requirement 9.3: Track actual update rate achieved versus target 30Hz.
        /// </summary>
        public void RecordUpdateRate(int updatesPerSecond)
        {
            if (updatesPerSecond < 0)
                throw new ArgumentException("Update rate cannot be negative", nameof(updatesPerSecond));

            lock (_lock)
            {
                _updateRates.Add(updatesPerSecond);
                // Keep only the most recent samples
                if (_updateRates.Count > MAX_SAMPLES)
                    _updateRates.RemoveAt(0);
            }
        }

        /// <summary>
        /// Gets a comprehensive performance report with latency statistics and update rates.
        /// Requirements 9.1, 9.2, 9.3, 9.4: Provide performance metrics and warnings.
        /// </summary>
        public PerformanceReport GetReport()
        {
            var report = new PerformanceReport();

            // Calculate latency statistics for each stage
            foreach (var kvp in _latencies)
            {
                List<TimeSpan> samples;
                lock (kvp.Value)
                {
                    samples = new List<TimeSpan>(kvp.Value);
                }

                if (samples.Count > 0)
                {
                    report.StageLatencies[kvp.Key] = new LatencyStats
                    {
                        Average = TimeSpan.FromMilliseconds(samples.Average(ts => ts.TotalMilliseconds)),
                        Min = samples.Min(),
                        Max = samples.Max(),
                        P95 = CalculatePercentile(samples, 0.95)
                    };
                }
            }

            // Calculate update rate statistics
            lock (_lock)
            {
                if (_updateRates.Count > 0)
                {
                    report.AverageUpdateRate = _updateRates.Average();
                    report.TargetUpdateRate = 30;
                }
            }

            return report;
        }

        /// <summary>
        /// Calculates the specified percentile from a list of time spans.
        /// Used for P95 latency calculation (Requirement 9.4).
        /// </summary>
        private TimeSpan CalculatePercentile(List<TimeSpan> values, double percentile)
        {
            if (values == null || values.Count == 0)
                return TimeSpan.Zero;

            if (percentile < 0 || percentile > 1)
                throw new ArgumentException("Percentile must be between 0 and 1", nameof(percentile));

            var sorted = values.OrderBy(v => v).ToList();
            int index = (int)(sorted.Count * percentile);
            // Ensure index is within bounds
            index = Math.Min(index, sorted.Count - 1);
            return sorted[index];
        }
    }

    /// <summary>
    /// Comprehensive performance report containing latency statistics and update rates.
    /// </summary>
    public class PerformanceReport
    {
        /// <summary>
        /// Latency statistics for each pipeline stage.
        /// Key: stage name (e.g., "Transport", "Walker", "Parser")
        /// Value: latency statistics for that stage
        /// </summary>
        public Dictionary<string, LatencyStats> StageLatencies { get; set; } = new();

        /// <summary>
        /// Average update rate achieved in updates per second.
        /// Requirement 9.3: Track actual update rate.
        /// </summary>
        public double AverageUpdateRate { get; set; }

        /// <summary>
        /// Target update rate (typically 30Hz for telemetry).
        /// </summary>
        public int TargetUpdateRate { get; set; }
    }

    /// <summary>
    /// Statistical summary of latency measurements for a pipeline stage.
    /// </summary>
    public class LatencyStats
    {
        /// <summary>
        /// Average latency across all measurements.
        /// Requirement 9.1: Measure time taken at each stage.
        /// </summary>
        public TimeSpan Average { get; set; }

        /// <summary>
        /// Minimum latency observed.
        /// </summary>
        public TimeSpan Min { get; set; }

        /// <summary>
        /// Maximum latency observed.
        /// </summary>
        public TimeSpan Max { get; set; }

        /// <summary>
        /// 95th percentile latency (P95).
        /// Requirement 9.4: Track latency percentiles for performance analysis.
        /// </summary>
        public TimeSpan P95 { get; set; }
    }
}
