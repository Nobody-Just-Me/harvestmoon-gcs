using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarvestmoonGCS.Core.Diagnostics;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Simple performance monitor implementation
/// </summary>
internal class PerformanceMonitor : IPerformanceMonitor
{
    private readonly Dictionary<string, List<TimeSpan>> _latencies = new Dictionary<string, List<TimeSpan>>();
    private readonly List<int> _updateRates = new List<int>();
    private readonly object _metricsLock = new object();
    private const int MaxMetricsPerOperation = 1000;
    
    public void RecordStageLatency(string stage, TimeSpan latency)
    {
        if (string.IsNullOrWhiteSpace(stage))
            throw new ArgumentException("Stage name cannot be null or empty", nameof(stage));

        lock (_metricsLock)
        {
            if (!_latencies.ContainsKey(stage))
            {
                _latencies[stage] = new List<TimeSpan>();
            }
            
            _latencies[stage].Add(latency);
            
            if (_latencies[stage].Count > MaxMetricsPerOperation)
            {
                _latencies[stage].RemoveAt(0);
            }
        }
    }
    
    public void RecordUpdateRate(int updatesPerSecond)
    {
        if (updatesPerSecond < 0)
            throw new ArgumentException("Update rate cannot be negative", nameof(updatesPerSecond));

        lock (_metricsLock)
        {
            _updateRates.Add(updatesPerSecond);
            
            if (_updateRates.Count > MaxMetricsPerOperation)
            {
                _updateRates.RemoveAt(0);
            }
        }
    }
    
    public PerformanceReport GetReport()
    {
        var report = new PerformanceReport();

        lock (_metricsLock)
        {
            // Calculate latency statistics for each stage
            foreach (var kvp in _latencies)
            {
                if (kvp.Value.Count > 0)
                {
                    var samples = kvp.Value.ToList();
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
            if (_updateRates.Count > 0)
            {
                report.AverageUpdateRate = _updateRates.Average();
                report.TargetUpdateRate = 30;
            }
        }

        return report;
    }

    private TimeSpan CalculatePercentile(List<TimeSpan> values, double percentile)
    {
        if (values == null || values.Count == 0)
            return TimeSpan.Zero;

        if (percentile < 0 || percentile > 1)
            throw new ArgumentException("Percentile must be between 0 and 1", nameof(percentile));

        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)(sorted.Count * percentile);
        index = Math.Min(index, sorted.Count - 1);
        return sorted[index];
    }
}
