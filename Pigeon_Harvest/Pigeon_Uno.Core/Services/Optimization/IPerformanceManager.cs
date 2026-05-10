using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services.Optimization;

/// <summary>
/// Service interface for coordinating performance optimizations across all system components.
/// Validates Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7
/// </summary>
public interface IPerformanceManager
{
    /// <summary>
    /// Initializes the performance manager with detected device capabilities.
    /// </summary>
    /// <param name="capabilities">Emulator or device capabilities.</param>
    Task InitializeAsync(EmulatorCapabilities capabilities);

    /// <summary>
    /// Applies emulator-specific optimizations automatically.
    /// </summary>
    void ApplyEmulatorOptimizations();

    /// <summary>
    /// Starts continuous monitoring of performance metrics.
    /// </summary>
    void MonitorPerformanceMetrics();

    /// <summary>
    /// Determines the optimal performance profile based on device capabilities.
    /// </summary>
    /// <returns>Recommended performance profile.</returns>
    Task<PerformanceProfile> GetOptimalProfileAsync();

    /// <summary>
    /// Sets the active performance profile.
    /// </summary>
    /// <param name="profile">Performance profile to activate.</param>
    void SetActiveProfile(PerformanceProfile profile);

    /// <summary>
    /// Gets the currently active performance profile.
    /// </summary>
    /// <returns>Active performance profile.</returns>
    PerformanceProfile GetActiveProfile();

    /// <summary>
    /// Retrieves current performance metrics.
    /// </summary>
    /// <returns>Current performance metrics.</returns>
    PerformanceMetrics GetCurrentMetrics();

    /// <summary>
    /// Event raised when performance metrics are updated.
    /// </summary>
    event EventHandler<PerformanceMetrics>? MetricsUpdated;

    /// <summary>
    /// Event raised when the active performance profile changes.
    /// </summary>
    event EventHandler<PerformanceProfile>? ProfileChanged;

    /// <summary>
    /// Event raised when a critical performance issue is detected.
    /// </summary>
    event EventHandler<PerformanceCriticalIssue>? CriticalIssueDetected;
}

/// <summary>
/// Represents a performance profile with specific optimization settings.
/// </summary>
public class PerformanceProfile
{
    /// <summary>
    /// Name of the performance profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rendering quality level.
    /// </summary>
    public RenderingQuality RenderQuality { get; set; }

    /// <summary>
    /// Maximum memory limit in megabytes.
    /// </summary>
    public int MaxMemoryMB { get; set; }

    /// <summary>
    /// Target frame rate.
    /// </summary>
    public int TargetFrameRate { get; set; }

    /// <summary>
    /// Telemetry processing mode.
    /// </summary>
    public TelemetryProcessingMode TelemetryMode { get; set; }

    /// <summary>
    /// Network optimization level.
    /// </summary>
    public NetworkOptimizationLevel NetworkLevel { get; set; }

    /// <summary>
    /// Indicates whether low power mode is enabled.
    /// </summary>
    public bool EnableLowPowerMode { get; set; }

    /// <summary>
    /// Background task throttle percentage (0-100).
    /// </summary>
    public int BackgroundTaskThrottlePercent { get; set; }

    /// <summary>
    /// Garbage collection aggressiveness level.
    /// </summary>
    public GCAggressiveness GCLevel { get; set; }

    /// <summary>
    /// Indicates whether performance overlay is enabled for debugging.
    /// </summary>
    public bool EnablePerformanceOverlay { get; set; }

    // Predefined performance profiles
    public static PerformanceProfile HighPerformance => new()
    {
        Name = "High Performance",
        RenderQuality = RenderingQuality.High,
        MaxMemoryMB = 512,
        TargetFrameRate = 60,
        TelemetryMode = TelemetryProcessingMode.Full,
        NetworkLevel = NetworkOptimizationLevel.None,
        EnableLowPowerMode = false,
        BackgroundTaskThrottlePercent = 0,
        GCLevel = GCAggressiveness.Conservative,
        EnablePerformanceOverlay = false
    };

    public static PerformanceProfile Balanced => new()
    {
        Name = "Balanced",
        RenderQuality = RenderingQuality.Medium,
        MaxMemoryMB = 400,
        TargetFrameRate = 30,
        TelemetryMode = TelemetryProcessingMode.Optimized,
        NetworkLevel = NetworkOptimizationLevel.Basic,
        EnableLowPowerMode = false,
        BackgroundTaskThrottlePercent = 25,
        GCLevel = GCAggressiveness.Normal,
        EnablePerformanceOverlay = false
    };

    public static PerformanceProfile EmulatorOptimized => new()
    {
        Name = "Emulator Optimized",
        RenderQuality = RenderingQuality.Low,
        MaxMemoryMB = 256,
        TargetFrameRate = 24,
        TelemetryMode = TelemetryProcessingMode.EmulatorOptimized,
        NetworkLevel = NetworkOptimizationLevel.Advanced,
        EnableLowPowerMode = false,
        BackgroundTaskThrottlePercent = 50,
        GCLevel = GCAggressiveness.Aggressive,
        EnablePerformanceOverlay = false
    };

    public static PerformanceProfile PowerSaver => new()
    {
        Name = "Power Saver",
        RenderQuality = RenderingQuality.Low,
        MaxMemoryMB = 200,
        TargetFrameRate = 15,
        TelemetryMode = TelemetryProcessingMode.BatterySaver,
        NetworkLevel = NetworkOptimizationLevel.Maximum,
        EnableLowPowerMode = true,
        BackgroundTaskThrottlePercent = 75,
        GCLevel = GCAggressiveness.Maximum,
        EnablePerformanceOverlay = false
    };
}

/// <summary>
/// Represents current performance metrics.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CPUUsagePercent { get; set; }

    /// <summary>
    /// Memory usage in megabytes.
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// Current frame rate.
    /// </summary>
    public double FrameRate { get; set; }

    /// <summary>
    /// Network latency in milliseconds.
    /// </summary>
    public int NetworkLatencyMs { get; set; }

    /// <summary>
    /// Timestamp when metrics were captured.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// GPU memory usage in megabytes.
    /// </summary>
    public long GPUMemoryUsageMB { get; set; }

    /// <summary>
    /// Number of active background tasks.
    /// </summary>
    public int ActiveBackgroundTasks { get; set; }

    /// <summary>
    /// Network utilization percentage (0-100).
    /// </summary>
    public double NetworkUtilizationPercent { get; set; }
}

/// <summary>
/// Represents a critical performance issue.
/// </summary>
public class PerformanceCriticalIssue
{
    /// <summary>
    /// Severity level of the issue.
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Description of the issue.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Suggested action to resolve the issue.
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the issue was detected.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Enumeration of rendering quality levels.
/// </summary>
public enum RenderingQuality
{
    Low,
    Medium,
    High,
    Auto
}

/// <summary>
/// Enumeration of telemetry processing modes.
/// </summary>
public enum TelemetryProcessingMode
{
    Full,
    CriticalOnly,
    Optimized,
    Minimal,
    EmulatorOptimized,
    BatterySaver
}

/// <summary>
/// Enumeration of network optimization levels.
/// </summary>
public enum NetworkOptimizationLevel
{
    None,
    Basic,
    Advanced,
    Maximum
}

/// <summary>
/// Enumeration of garbage collection aggressiveness levels.
/// </summary>
public enum GCAggressiveness
{
    Conservative,
    Normal,
    Aggressive,
    Maximum
}

/// <summary>
/// Enumeration of issue severity levels.
/// </summary>
public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}
