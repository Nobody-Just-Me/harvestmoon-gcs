using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services.Optimization;

/// <summary>
/// Service interface for managing memory, CPU, and graphics resources efficiently.
/// Validates Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8
/// </summary>
public interface IResourceManager
{
    /// <summary>
    /// Initializes the resource manager with detected device capabilities.
    /// </summary>
    /// <param name="capabilities">Emulator or device capabilities.</param>
    Task InitializeAsync(EmulatorCapabilities capabilities);

    /// <summary>
    /// Sets memory usage limits.
    /// </summary>
    /// <param name="maxMemoryMB">Maximum memory in megabytes.</param>
    void SetMemoryLimits(int maxMemoryMB);

    /// <summary>
    /// Enables or disables low memory mode.
    /// </summary>
    /// <param name="enabled">True to enable low memory mode, false to disable.</param>
    void EnableLowMemoryMode(bool enabled);

    /// <summary>
    /// Sets background task throttling percentage.
    /// </summary>
    /// <param name="throttlePercent">Throttle percentage (0-100).</param>
    void SetBackgroundTaskThrottling(int throttlePercent);

    /// <summary>
    /// Enables or disables texture compression for graphics operations.
    /// </summary>
    /// <param name="enabled">True to enable texture compression, false to disable.</param>
    void EnableTextureCompression(bool enabled);

    /// <summary>
    /// Releases unused resources and triggers garbage collection.
    /// </summary>
    void ReleaseUnusedResources();

    /// <summary>
    /// Gets current memory usage information.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    MemoryUsageInfo GetMemoryUsage();

    /// <summary>
    /// Sets CPU usage limits to prevent battery drain.
    /// </summary>
    /// <param name="maxCPUPercent">Maximum CPU usage percentage (0-100).</param>
    void SetCPUUsageLimits(int maxCPUPercent);

    /// <summary>
    /// Optimizes graphics memory usage for mobile GPUs.
    /// </summary>
    void OptimizeGraphicsMemory();

    /// <summary>
    /// Event raised when resource limits are exceeded.
    /// </summary>
    event EventHandler<ResourceLimitExceededEventArgs>? ResourceLimitExceeded;

    /// <summary>
    /// Event raised when low memory mode is activated or deactivated.
    /// </summary>
    event EventHandler<LowMemoryModeEventArgs>? LowMemoryModeActivated;
}

/// <summary>
/// Represents current memory usage information.
/// </summary>
public class MemoryUsageInfo
{
    /// <summary>
    /// Current memory usage in megabytes.
    /// </summary>
    public long CurrentUsageMB { get; set; }

    /// <summary>
    /// Maximum memory limit in megabytes.
    /// </summary>
    public int MaxLimitMB { get; set; }

    /// <summary>
    /// Available memory in megabytes.
    /// </summary>
    public long AvailableMB { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double UsagePercent { get; set; }

    /// <summary>
    /// Indicates whether low memory mode is active.
    /// </summary>
    public bool IsLowMemoryMode { get; set; }

    /// <summary>
    /// Number of garbage collections performed.
    /// </summary>
    public int GarbageCollections { get; set; }

    /// <summary>
    /// Timestamp of the last garbage collection.
    /// </summary>
    public DateTime LastGarbageCollection { get; set; }

    /// <summary>
    /// Current memory pressure level.
    /// </summary>
    public MemoryPressureLevel PressureLevel { get; set; }
}

/// <summary>
/// Event arguments for resource limit exceeded events.
/// </summary>
public class ResourceLimitExceededEventArgs : EventArgs
{
    /// <summary>
    /// Type of resource that exceeded limits.
    /// </summary>
    public ResourceType ResourceType { get; set; }

    /// <summary>
    /// Current usage value.
    /// </summary>
    public double CurrentUsage { get; set; }

    /// <summary>
    /// Maximum limit value.
    /// </summary>
    public double MaxLimit { get; set; }

    /// <summary>
    /// Suggested action to resolve the issue.
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the limit was exceeded.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Event arguments for low memory mode events.
/// </summary>
public class LowMemoryModeEventArgs : EventArgs
{
    /// <summary>
    /// Indicates whether low memory mode is now active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Reason for the mode change.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Current memory usage in megabytes.
    /// </summary>
    public long CurrentMemoryUsageMB { get; set; }

    /// <summary>
    /// Timestamp of the mode change.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Enumeration of memory pressure levels.
/// </summary>
public enum MemoryPressureLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Enumeration of resource types.
/// </summary>
public enum ResourceType
{
    Memory,
    CPU,
    GPU,
    Network,
    Disk
}
