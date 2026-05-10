using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Pigeon_Uno.Core.Services.Optimization;

namespace Pigeon_Uno.Services;

/// <summary>
/// Implementation of IPerformanceManager for coordinating performance optimizations.
/// Validates Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7
/// </summary>
public class PerformanceManager : IPerformanceManager
{
    private PerformanceProfile _activeProfile;
    private EmulatorCapabilities? _capabilities;
    private PerformanceMetrics _currentMetrics;
    private Timer? _metricsTimer;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isMonitoring;
    private readonly object _lock = new();

    // Performance thresholds for critical issue detection
    private const double CPU_CRITICAL_THRESHOLD = 90.0;
    private const long MEMORY_CRITICAL_THRESHOLD_MB = 450;
    private const double FPS_CRITICAL_THRESHOLD = 15.0;
    private const int NETWORK_LATENCY_CRITICAL_THRESHOLD_MS = 1000;

    // Frame rate monitoring
    private DateTime _lastFrameTime;
    private int _frameCount;
    private double _currentFps;

    public event EventHandler<PerformanceMetrics>? MetricsUpdated;
    public event EventHandler<PerformanceProfile>? ProfileChanged;
    public event EventHandler<PerformanceCriticalIssue>? CriticalIssueDetected;

    public PerformanceManager()
    {
        // Get dispatcher queue for UI thread operations
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        
        // Initialize with Balanced profile as default
        _activeProfile = PerformanceProfile.Balanced;
        _currentMetrics = new PerformanceMetrics
        {
            Timestamp = DateTime.Now
        };
        
        _lastFrameTime = DateTime.Now;
        _frameCount = 0;
        _currentFps = 0;

        Serilog.Log.Information("PerformanceManager initialized with Balanced profile");
    }

    public async Task InitializeAsync(EmulatorCapabilities capabilities)
    {
        _capabilities = capabilities;
        
        Serilog.Log.Information($"PerformanceManager initializing with capabilities: Type={capabilities.Type}, " +
            $"AvailableMemoryMB={capabilities.AvailableMemoryMB}, SupportsHardwareAcceleration={capabilities.SupportsHardwareAcceleration}");

        // Determine optimal profile based on capabilities
        var isEmulator = capabilities.Type != EmulatorType.None;
        var optimalProfile = await GetOptimalProfileAsync();
        SetActiveProfile(optimalProfile);

        // Apply emulator-specific optimizations if running in emulator
        if (isEmulator)
        {
            ApplyEmulatorOptimizations();
        }

        Serilog.Log.Information($"PerformanceManager initialized with profile: {_activeProfile.Name}");
    }

    public void ApplyEmulatorOptimizations()
    {
        Serilog.Log.Information("Applying emulator-specific optimizations");

        // Switch to EmulatorOptimized profile if not already using a more aggressive profile
        if (_activeProfile.Name != "Emulator Optimized" && _activeProfile.Name != "Power Saver")
        {
            SetActiveProfile(PerformanceProfile.EmulatorOptimized);
        }

        // Force garbage collection to free memory
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();

        Serilog.Log.Information("Emulator optimizations applied");
    }

    public void MonitorPerformanceMetrics()
    {
        lock (_lock)
        {
            if (_isMonitoring)
            {
                Serilog.Log.Warning("Performance monitoring already active");
                return;
            }

            _isMonitoring = true;
        }

        // Start metrics collection timer (every 2 seconds)
        _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        // Subscribe to CompositionTarget.Rendering for frame rate monitoring
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                CompositionTarget.Rendering += OnRendering;
            });
        }

        Serilog.Log.Information("Performance monitoring started");
    }

    private void OnRendering(object? sender, object e)
    {
        // Calculate frame rate
        _frameCount++;
        var now = DateTime.Now;
        var elapsed = (now - _lastFrameTime).TotalSeconds;

        if (elapsed >= 1.0)
        {
            _currentFps = _frameCount / elapsed;
            _frameCount = 0;
            _lastFrameTime = now;
        }
    }

    private void CollectMetrics(object? state)
    {
        try
        {
            var metrics = new PerformanceMetrics
            {
                Timestamp = DateTime.Now,
                CPUUsagePercent = GetCPUUsage(),
                MemoryUsageMB = GetMemoryUsage(),
                FrameRate = _currentFps,
                NetworkLatencyMs = 0, // Will be updated by NetworkManager
                GPUMemoryUsageMB = 0, // Platform-specific, not easily accessible
                ActiveBackgroundTasks = 0, // Will be updated by ResourceManager
                NetworkUtilizationPercent = 0 // Will be updated by NetworkManager
            };

            _currentMetrics = metrics;

            // Raise MetricsUpdated event
            MetricsUpdated?.Invoke(this, metrics);

            // Check for critical issues
            CheckForCriticalIssues(metrics);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error collecting performance metrics");
        }
    }

    private double GetCPUUsage()
    {
        try
        {
            // Get current process CPU usage
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;

            Thread.Sleep(100); // Small delay for measurement

            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return cpuUsageTotal * 100;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to get CPU usage");
            return 0;
        }
    }

    private long GetMemoryUsage()
    {
        try
        {
            // Get managed memory usage
            var managedMemory = GC.GetTotalMemory(false);
            
            // Get process working set (includes native memory)
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;

            // Return working set in MB (more accurate for total app memory)
            return workingSet / (1024 * 1024);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to get memory usage");
            return 0;
        }
    }

    private void CheckForCriticalIssues(PerformanceMetrics metrics)
    {
        // Check CPU usage
        if (metrics.CPUUsagePercent > CPU_CRITICAL_THRESHOLD)
        {
            RaiseCriticalIssue(IssueSeverity.High, 
                $"CPU usage is critically high: {metrics.CPUUsagePercent:F1}%",
                "Consider switching to Power Saver profile or closing background applications");
        }

        // Check memory usage
        if (metrics.MemoryUsageMB > MEMORY_CRITICAL_THRESHOLD_MB)
        {
            RaiseCriticalIssue(IssueSeverity.High,
                $"Memory usage is critically high: {metrics.MemoryUsageMB} MB",
                "Switching to low memory mode and triggering garbage collection");
            
            // Automatically trigger GC
            GC.Collect(2, GCCollectionMode.Forced, true, true);
        }

        // Check frame rate
        if (metrics.FrameRate > 0 && metrics.FrameRate < FPS_CRITICAL_THRESHOLD)
        {
            RaiseCriticalIssue(IssueSeverity.Medium,
                $"Frame rate is critically low: {metrics.FrameRate:F1} FPS",
                "Consider reducing rendering quality or switching to a lower performance profile");
        }

        // Check network latency
        if (metrics.NetworkLatencyMs > NETWORK_LATENCY_CRITICAL_THRESHOLD_MS)
        {
            RaiseCriticalIssue(IssueSeverity.Medium,
                $"Network latency is critically high: {metrics.NetworkLatencyMs} ms",
                "Check network connection or enable network compression");
        }
    }

    private void RaiseCriticalIssue(IssueSeverity severity, string description, string suggestedAction)
    {
        var issue = new PerformanceCriticalIssue
        {
            Severity = severity,
            Description = description,
            SuggestedAction = suggestedAction,
            Timestamp = DateTime.Now
        };

        Serilog.Log.Warning($"Critical performance issue detected: {description}");
        CriticalIssueDetected?.Invoke(this, issue);
    }

    public async Task<PerformanceProfile> GetOptimalProfileAsync()
    {
        await Task.CompletedTask; // Make async for future enhancements

        if (_capabilities == null)
        {
            Serilog.Log.Information("No capabilities available, returning Balanced profile");
            return PerformanceProfile.Balanced;
        }

        // Determine optimal profile based on capabilities
        var isEmulator = _capabilities.Type != EmulatorType.None;
        if (isEmulator)
        {
            Serilog.Log.Information("Emulator detected, recommending EmulatorOptimized profile");
            return PerformanceProfile.EmulatorOptimized;
        }

        // Check available memory
        if (_capabilities.AvailableMemoryMB < 2048)
        {
            Serilog.Log.Information($"Low memory detected ({_capabilities.AvailableMemoryMB} MB), recommending PowerSaver profile");
            return PerformanceProfile.PowerSaver;
        }

        // Check hardware acceleration
        if (!_capabilities.SupportsHardwareAcceleration)
        {
            Serilog.Log.Information("No hardware acceleration, recommending Balanced profile");
            return PerformanceProfile.Balanced;
        }

        // High-end device
        if (_capabilities.AvailableMemoryMB >= 4096 && _capabilities.SupportsHardwareAcceleration)
        {
            Serilog.Log.Information("High-end device detected, recommending HighPerformance profile");
            return PerformanceProfile.HighPerformance;
        }

        // Default to Balanced
        Serilog.Log.Information("Standard device, recommending Balanced profile");
        return PerformanceProfile.Balanced;
    }

    public void SetActiveProfile(PerformanceProfile profile)
    {
        var previousProfile = _activeProfile;
        _activeProfile = profile;

        Serilog.Log.Information($"Performance profile changed from '{previousProfile.Name}' to '{profile.Name}'");
        Serilog.Log.Information($"Profile settings: RenderQuality={profile.RenderQuality}, " +
            $"MaxMemoryMB={profile.MaxMemoryMB}, TargetFPS={profile.TargetFrameRate}, " +
            $"TelemetryMode={profile.TelemetryMode}, NetworkLevel={profile.NetworkLevel}");

        // Apply profile settings
        ApplyProfileSettings(profile);

        // Raise ProfileChanged event
        ProfileChanged?.Invoke(this, profile);
    }

    private void ApplyProfileSettings(PerformanceProfile profile)
    {
        // Apply GC settings based on profile
        switch (profile.GCLevel)
        {
            case GCAggressiveness.Conservative:
                // Let GC run naturally
                break;
            case GCAggressiveness.Normal:
                GC.Collect(1, GCCollectionMode.Optimized);
                break;
            case GCAggressiveness.Aggressive:
                GC.Collect(2, GCCollectionMode.Forced, true);
                break;
            case GCAggressiveness.Maximum:
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                break;
        }

        Serilog.Log.Information($"Applied profile settings for '{profile.Name}'");
    }

    public PerformanceProfile GetActiveProfile()
    {
        return _activeProfile;
    }

    public PerformanceMetrics GetCurrentMetrics()
    {
        return _currentMetrics;
    }

    public void Dispose()
    {
        _metricsTimer?.Dispose();
        
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                CompositionTarget.Rendering -= OnRendering;
            });
        }

        _isMonitoring = false;
        Serilog.Log.Information("PerformanceManager disposed");
    }
}
