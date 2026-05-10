using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Tests.Android.Helpers;

/// <summary>
/// Monitors performance metrics during test execution
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private long _startMemory;
    private long _peakMemory;
    private CancellationTokenSource? _monitoringCts;
    private int _sampleCount;
    private double _totalCpuUsage;

    public long StartMemory => _startMemory;
    public long CurrentMemory => GC.GetTotalMemory(false);
    public long PeakMemory => _peakMemory;
    public long MemoryDelta => CurrentMemory - _startMemory;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public double AverageCpuUsage => _sampleCount > 0 ? _totalCpuUsage / _sampleCount : 0;

    /// <summary>
    /// Starts performance monitoring
    /// </summary>
    public void Start()
    {
        _startMemory = GC.GetTotalMemory(false);
        _peakMemory = _startMemory;
        _stopwatch.Start();
        StartContinuousMonitoring();
    }

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    public void Stop()
    {
        _stopwatch.Stop();
        StopContinuousMonitoring();
    }

    /// <summary>
    /// Gets current performance snapshot
    /// </summary>
    public PerformanceSnapshot GetSnapshot()
    {
        var currentMemory = CurrentMemory;
        if (currentMemory > _peakMemory)
        {
            _peakMemory = currentMemory;
        }

        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            MemoryUsage = currentMemory,
            PeakMemory = _peakMemory,
            MemoryDelta = MemoryDelta,
            CpuUsage = GetCurrentCpuUsage(),
            Elapsed = Elapsed
        };
    }

    /// <summary>
    /// Checks if memory usage exceeds threshold
    /// </summary>
    public bool IsMemoryWithinThreshold(long thresholdBytes)
    {
        return CurrentMemory <= thresholdBytes;
    }

    /// <summary>
    /// Checks for memory leaks by comparing start and end memory
    /// </summary>
    public bool HasMemoryLeak(long toleranceBytes = 5 * 1024 * 1024) // 5MB default tolerance
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var leak = finalMemory - _startMemory;
        return leak > toleranceBytes;
    }

    /// <summary>
    /// Gets current CPU usage percentage (simplified)
    /// </summary>
    private double GetCurrentCpuUsage()
    {
        // TODO: Implement actual CPU usage monitoring
        // This is a placeholder that would need platform-specific implementation
        return 0.0;
    }

    /// <summary>
    /// Starts continuous monitoring in background
    /// </summary>
    private void StartContinuousMonitoring()
    {
        _monitoringCts = new CancellationTokenSource();
        var token = _monitoringCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var currentMemory = CurrentMemory;
                if (currentMemory > _peakMemory)
                {
                    _peakMemory = currentMemory;
                }

                var cpuUsage = GetCurrentCpuUsage();
                _totalCpuUsage += cpuUsage;
                _sampleCount++;

                await Task.Delay(100, token); // Sample every 100ms
            }
        }, token);
    }

    /// <summary>
    /// Stops continuous monitoring
    /// </summary>
    private void StopContinuousMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    public void Dispose()
    {
        Stop();
        _monitoringCts?.Dispose();
    }
}

public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public long MemoryUsage { get; set; }
    public long PeakMemory { get; set; }
    public long MemoryDelta { get; set; }
    public double CpuUsage { get; set; }
    public TimeSpan Elapsed { get; set; }

    public string ToReadableString()
    {
        return $"Memory: {MemoryUsage / 1024 / 1024}MB (Peak: {PeakMemory / 1024 / 1024}MB, Delta: {MemoryDelta / 1024 / 1024}MB), " +
               $"CPU: {CpuUsage:F2}%, Elapsed: {Elapsed.TotalSeconds:F2}s";
    }
}
