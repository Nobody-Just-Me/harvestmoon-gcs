using Pigeon_Uno.Tests.Android.Helpers;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Pigeon_Uno.Tests.Android;

/// <summary>
/// Base class for all Android tests providing common setup and utilities
/// </summary>
public abstract class AndroidTestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly AndroidTestConfiguration Config;
    protected readonly PerformanceMonitor PerformanceMonitor;

    protected AndroidTestBase(ITestOutputHelper output)
    {
        Output = output;
        Config = AndroidTestConfiguration.Default;
        PerformanceMonitor = new PerformanceMonitor();
    }

    /// <summary>
    /// Logs a message to test output
    /// </summary>
    protected void Log(string message)
    {
        if (Config.EnableDetailedLogging)
        {
            Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }

    /// <summary>
    /// Logs performance snapshot
    /// </summary>
    protected void LogPerformance(string context)
    {
        var snapshot = PerformanceMonitor.GetSnapshot();
        Log($"[PERF] {context}: {snapshot.ToReadableString()}");
    }

    /// <summary>
    /// Asserts that memory usage is within threshold
    /// </summary>
    protected void AssertMemoryWithinThreshold()
    {
        var thresholdMB = Config.GetThreshold("MaxHeapMemoryMB", 256);
        var thresholdBytes = thresholdMB * 1024 * 1024;
        var currentMemory = PerformanceMonitor.CurrentMemory;

        Assert.True(
            currentMemory <= thresholdBytes,
            $"Memory usage {currentMemory / 1024 / 1024}MB exceeds threshold {thresholdMB}MB"
        );
    }

    /// <summary>
    /// Asserts that no memory leak occurred
    /// </summary>
    protected void AssertNoMemoryLeak()
    {
        var toleranceMB = 5; // 5MB tolerance
        var toleranceBytes = toleranceMB * 1024 * 1024;
        var hasLeak = PerformanceMonitor.HasMemoryLeak(toleranceBytes);

        Assert.False(
            hasLeak,
            $"Memory leak detected: {PerformanceMonitor.MemoryDelta / 1024 / 1024}MB increase (tolerance: {toleranceMB}MB)"
        );
    }

    public virtual void Dispose()
    {
        PerformanceMonitor?.Dispose();
        GC.SuppressFinalize(this);
    }
}
