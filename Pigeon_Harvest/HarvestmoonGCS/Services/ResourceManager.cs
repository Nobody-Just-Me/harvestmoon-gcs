using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services.Optimization;
using Serilog;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Resource manager service for memory and CPU resource control with emulator-specific optimizations.
/// Ported from Pigeon_Avalonia with Uno Platform adaptations.
/// Validates Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8
/// </summary>
public class ResourceManager : IResourceManager, IDisposable
{
    private readonly Serilog.ILogger _logger;
    
    // Resource limits and settings
    private int _maxMemoryMB = 512;
    private int _maxCPUPercent = 80;
    private int _maxGpuMemoryMB = 128;
    private bool _isLowMemoryMode = false;
    private bool _isEmulatorMode = false;
    private int _backgroundTaskThrottlePercent = 0;
    private bool _textureCompressionEnabled = false;

    // Resource tracking
    private readonly ConcurrentDictionary<string, ResourceRequest> _activeRequests;
    private readonly ConcurrentDictionary<string, DateTime> _requestTimestamps;
    private long _totalAllocatedMemoryMB = 0;
    private double _totalAllocatedCpuPercent = 0.0;
    private int _totalAllocatedGpuMemoryMB = 0;

    // Memory management
    private int _garbageCollectionCount = 0;
    private DateTime _lastGarbageCollection = DateTime.MinValue;
    private Timer? _resourceMonitorTimer;
    private Timer? _cleanupTimer;
    
    // Memory leak detection
    private readonly List<long> _memorySnapshots = new();
    private DateTime _lastMemorySnapshot = DateTime.MinValue;
    private const int MaxMemorySnapshots = 10;
    private const double MemoryLeakThresholdMB = 50; // 50MB increase over 10 snapshots
    
    // Thresholds
    private int _lowMemoryThresholdMB = 400;
    private int _criticalMemoryThresholdMB = 450;
    private int _highCpuThresholdPercent = 70;
    
    // Background task management
    private readonly ConcurrentDictionary<string, BackgroundTaskInfo> _backgroundTasks;
    private Timer? _backgroundTaskTimer;

    // Texture compression
    private readonly ConcurrentDictionary<string, TextureInfo> _textureCache;
    private long _originalTextureMemoryMB = 0;
    private long _compressedTextureMemoryMB = 0;

    // Events
    public event EventHandler<ResourceLimitExceededEventArgs>? ResourceLimitExceeded;
    public event EventHandler<LowMemoryModeEventArgs>? LowMemoryModeActivated;

    public ResourceManager()
    {
        _logger = Log.ForContext<ResourceManager>();
        
        _activeRequests = new ConcurrentDictionary<string, ResourceRequest>();
        _requestTimestamps = new ConcurrentDictionary<string, DateTime>();
        _backgroundTasks = new ConcurrentDictionary<string, BackgroundTaskInfo>();
        _textureCache = new ConcurrentDictionary<string, TextureInfo>();

        // Initialize monitoring
        StartResourceMonitoring();
        StartCleanupTimer();
        StartBackgroundTaskManagement();

        _logger.Information("[ResourceManager] Initialized with default settings");
    }

    /// <summary>
    /// Initialize with emulator capabilities
    /// </summary>
    public async Task InitializeAsync(EmulatorCapabilities capabilities)
    {
        try
        {
            _isEmulatorMode = capabilities.Type != EmulatorType.None;
            
            if (_isEmulatorMode)
            {
                ApplyEmulatorOptimizations(capabilities);
            }
            
            _logger.Information("[ResourceManager] Initialized - Emulator mode: {IsEmulator}", _isEmulatorMode);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error during initialization");
        }
    }

    /// <summary>
    /// Set memory usage limits for the application
    /// </summary>
    public void SetMemoryLimits(int maxMemoryMB)
    {
        _maxMemoryMB = Math.Max(128, maxMemoryMB); // Minimum 128MB
        _lowMemoryThresholdMB = (int)(_maxMemoryMB * 0.8);
        _criticalMemoryThresholdMB = (int)(_maxMemoryMB * 0.9);
        
        _logger.Information("[ResourceManager] Memory limits set - Max: {MaxMB}MB, Low threshold: {LowMB}MB, Critical: {CriticalMB}MB",
            _maxMemoryMB, _lowMemoryThresholdMB, _criticalMemoryThresholdMB);
        
        // Check if we need to enable low memory mode
        CheckMemoryPressure();
    }

    /// <summary>
    /// Enable or disable low memory mode
    /// </summary>
    public void EnableLowMemoryMode(bool enabled)
    {
        if (_isLowMemoryMode == enabled)
        {
            return;
        }

        _isLowMemoryMode = enabled;
        
        if (enabled)
        {
            // Apply low memory optimizations
            ApplyLowMemoryOptimizations();
        }
        else
        {
            // Restore normal memory settings
            RestoreNormalMemorySettings();
        }

        // Raise event
        var memoryUsage = GetMemoryUsage();
        LowMemoryModeActivated?.Invoke(this, new LowMemoryModeEventArgs
        {
            IsActive = enabled,
            CurrentMemoryUsageMB = memoryUsage.CurrentUsageMB,
            Reason = enabled ? "Memory usage exceeded threshold" : "Memory usage returned to normal",
            Timestamp = DateTime.UtcNow
        });

        _logger.Information("[ResourceManager] Low memory mode {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Set background task throttling percentage
    /// </summary>
    public void SetBackgroundTaskThrottling(int throttlePercent)
    {
        _backgroundTaskThrottlePercent = Math.Max(0, Math.Min(100, throttlePercent));
        
        // Apply throttling to existing background tasks
        ApplyBackgroundTaskThrottling();
        
        _logger.Information("[ResourceManager] Background task throttling set to {Percent}%", _backgroundTaskThrottlePercent);
    }

    /// <summary>
    /// Enable or disable texture compression
    /// </summary>
    public void EnableTextureCompression(bool enabled)
    {
        _textureCompressionEnabled = enabled;
        
        if (enabled)
        {
            // Compress existing textures
            CompressExistingTextures();
        }
        else
        {
            // Restore original textures
            RestoreOriginalTextures();
        }
        
        _logger.Information("[ResourceManager] Texture compression {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Release unused resources and perform cleanup
    /// </summary>
    public void ReleaseUnusedResources()
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredRequests = new List<string>();

            // Find expired requests
            foreach (var kvp in _requestTimestamps)
            {
                var requestId = kvp.Key;
                var timestamp = kvp.Value;
                
                if (_activeRequests.TryGetValue(requestId, out var request))
                {
                    // Check if request has expired (using a default 5 minute timeout)
                    if ((now - timestamp).TotalMinutes > 5)
                    {
                        expiredRequests.Add(requestId);
                    }
                }
            }

            // Release expired requests
            foreach (var requestId in expiredRequests)
            {
                ReleaseResources(requestId);
            }

            // Perform memory cleanup
            if (_isLowMemoryMode || GetMemoryUsage().UsagePercent > 80)
            {
                PerformMemoryCleanup();
            }

            // Optimize graphics memory
            if (_textureCompressionEnabled)
            {
                OptimizeGraphicsMemory();
            }

            _logger.Debug("[ResourceManager] Released {Count} expired resource requests", expiredRequests.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error releasing unused resources");
        }
    }

    /// <summary>
    /// Get current memory usage information
    /// </summary>
    public MemoryUsageInfo GetMemoryUsage()
    {
        try
        {
            var currentMemory = GC.GetTotalMemory(false);
            var currentMemoryMB = currentMemory / (1024.0 * 1024.0);
            var availableMemoryMB = Math.Max(0, _maxMemoryMB - currentMemoryMB);
            var usagePercent = (_maxMemoryMB > 0) ? (currentMemoryMB / _maxMemoryMB) * 100.0 : 0;

            return new MemoryUsageInfo
            {
                CurrentUsageMB = (long)currentMemoryMB,
                MaxLimitMB = _maxMemoryMB,
                AvailableMB = (long)availableMemoryMB,
                UsagePercent = usagePercent,
                IsLowMemoryMode = _isLowMemoryMode,
                GarbageCollections = _garbageCollectionCount,
                LastGarbageCollection = _lastGarbageCollection,
                PressureLevel = GetMemoryPressureLevel(usagePercent)
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error getting memory usage");
            return new MemoryUsageInfo
            {
                CurrentUsageMB = 0,
                MaxLimitMB = _maxMemoryMB,
                AvailableMB = _maxMemoryMB,
                UsagePercent = 0,
                IsLowMemoryMode = _isLowMemoryMode,
                PressureLevel = MemoryPressureLevel.Low
            };
        }
    }

    /// <summary>
    /// Set CPU usage limits to prevent battery drain
    /// </summary>
    public void SetCPUUsageLimits(int maxCPUPercent)
    {
        _maxCPUPercent = Math.Max(10, Math.Min(100, maxCPUPercent));
        _highCpuThresholdPercent = (int)(_maxCPUPercent * 0.8);
        
        _logger.Information("[ResourceManager] CPU limits set - Max: {MaxPercent}%, High threshold: {HighPercent}%",
            _maxCPUPercent, _highCpuThresholdPercent);
    }

    /// <summary>
    /// Optimize graphics memory usage
    /// </summary>
    public void OptimizeGraphicsMemory()
    {
        try
        {
            if (_textureCompressionEnabled)
            {
                CompressTextures();
            }

            // Clear unused texture cache entries
            ClearUnusedTextures();

            // Force GPU memory cleanup if available
            if (_isEmulatorMode)
            {
                // More aggressive cleanup for emulators
                PerformAggressiveGraphicsCleanup();
            }

            _logger.Debug("[ResourceManager] Graphics memory optimization completed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error optimizing graphics memory");
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Apply emulator-specific optimizations
    /// </summary>
    private void ApplyEmulatorOptimizations(EmulatorCapabilities capabilities)
    {
        try
        {
            // Adjust limits based on emulator capabilities
            _maxMemoryMB = Math.Min(capabilities.AvailableMemoryMB, 256);
            _maxGpuMemoryMB = Math.Min(capabilities.Graphics?.GPUMemoryMB ?? 64, 64);
            _maxCPUPercent = 60; // Lower CPU limit for emulators

            // Enable aggressive optimizations
            _textureCompressionEnabled = true;
            _backgroundTaskThrottlePercent = 50;

            // Update thresholds
            _lowMemoryThresholdMB = (int)(_maxMemoryMB * 0.7);
            _criticalMemoryThresholdMB = (int)(_maxMemoryMB * 0.85);
            _highCpuThresholdPercent = 50;

            _logger.Information("[ResourceManager] Applied emulator optimizations - Memory: {MemoryMB}MB, GPU: {GpuMB}MB, CPU: {CpuPercent}%",
                _maxMemoryMB, _maxGpuMemoryMB, _maxCPUPercent);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error applying emulator optimizations");
        }
    }

    /// <summary>
    /// Get memory pressure level based on usage percentage
    /// </summary>
    private MemoryPressureLevel GetMemoryPressureLevel(double usagePercent)
    {
        if (usagePercent >= 90)
            return MemoryPressureLevel.Critical;
        else if (usagePercent >= 80)
            return MemoryPressureLevel.High;
        else if (usagePercent >= 60)
            return MemoryPressureLevel.Medium;
        else
            return MemoryPressureLevel.Low;
    }

    /// <summary>
    /// Check memory pressure and trigger low memory mode if needed
    /// </summary>
    private void CheckMemoryPressure()
    {
        var memoryUsage = GetMemoryUsage();
        
        if (!_isLowMemoryMode && memoryUsage.CurrentUsageMB > _lowMemoryThresholdMB)
        {
            EnableLowMemoryMode(true);
        }
        else if (_isLowMemoryMode && memoryUsage.CurrentUsageMB < (_lowMemoryThresholdMB * 0.8))
        {
            EnableLowMemoryMode(false);
        }

        // Check for critical memory usage
        if (memoryUsage.CurrentUsageMB > _criticalMemoryThresholdMB)
        {
            var eventArgs = new ResourceLimitExceededEventArgs
            {
                ResourceType = ResourceType.Memory,
                CurrentUsage = memoryUsage.CurrentUsageMB,
                MaxLimit = _criticalMemoryThresholdMB,
                SuggestedAction = "Force garbage collection, clear texture caches, reduce background processing",
                Timestamp = DateTime.UtcNow
            };
            
            ResourceLimitExceeded?.Invoke(this, eventArgs);
        }
    }

    /// <summary>
    /// Apply low memory optimizations
    /// </summary>
    private void ApplyLowMemoryOptimizations()
    {
        // Enable texture compression
        EnableTextureCompression(true);
        
        // Increase background task throttling
        SetBackgroundTaskThrottling(Math.Max(_backgroundTaskThrottlePercent, 75));
        
        // Perform aggressive memory cleanup
        PerformMemoryCleanup();
    }

    /// <summary>
    /// Restore normal memory settings
    /// </summary>
    private void RestoreNormalMemorySettings()
    {
        // Restore background task throttling
        if (!_isEmulatorMode)
        {
            SetBackgroundTaskThrottling(0);
            EnableTextureCompression(false);
        }
    }

    /// <summary>
    /// Perform memory cleanup
    /// </summary>
    private void PerformMemoryCleanup()
    {
        try
        {
            // Force garbage collection
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            
            _garbageCollectionCount++;
            _lastGarbageCollection = DateTime.UtcNow;
            
            _logger.Debug("[ResourceManager] Memory cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error during memory cleanup");
        }
    }

    /// <summary>
    /// Apply background task throttling
    /// </summary>
    private void ApplyBackgroundTaskThrottling()
    {
        foreach (var task in _backgroundTasks.Values)
        {
            task.ThrottlePercent = _backgroundTaskThrottlePercent;
            task.LastThrottleUpdate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Compress existing textures
    /// </summary>
    private void CompressExistingTextures()
    {
        // This would typically interface with the graphics system
        // For now, simulate texture compression
        _compressedTextureMemoryMB = (long)(_originalTextureMemoryMB * 0.6); // 40% compression
        _logger.Debug("[ResourceManager] Compressed textures - Original: {OriginalMB}MB, Compressed: {CompressedMB}MB",
            _originalTextureMemoryMB, _compressedTextureMemoryMB);
    }

    /// <summary>
    /// Restore original textures
    /// </summary>
    private void RestoreOriginalTextures()
    {
        _compressedTextureMemoryMB = _originalTextureMemoryMB;
        _logger.Debug("[ResourceManager] Restored original textures");
    }

    /// <summary>
    /// Compress textures for memory optimization
    /// </summary>
    private void CompressTextures()
    {
        // Simulate texture compression process
        var compressionRatio = _isEmulatorMode ? 0.5 : 0.7; // More aggressive compression for emulators
        _compressedTextureMemoryMB = (long)(_originalTextureMemoryMB * compressionRatio);
    }

    /// <summary>
    /// Clear unused texture cache entries
    /// </summary>
    private void ClearUnusedTextures()
    {
        var now = DateTime.UtcNow;
        var expiredTextures = new List<string>();

        foreach (var kvp in _textureCache)
        {
            var textureId = kvp.Key;
            var textureInfo = kvp.Value;
            
            // Remove textures not accessed in the last 5 minutes
            if ((now - textureInfo.LastAccessed).TotalMinutes > 5)
            {
                expiredTextures.Add(textureId);
            }
        }

        foreach (var textureId in expiredTextures)
        {
            _textureCache.TryRemove(textureId, out _);
        }

        if (expiredTextures.Count > 0)
        {
            _logger.Debug("[ResourceManager] Cleared {Count} unused textures", expiredTextures.Count);
        }
    }

    /// <summary>
    /// Perform aggressive graphics cleanup for emulators
    /// </summary>
    private void PerformAggressiveGraphicsCleanup()
    {
        // Clear all texture caches
        _textureCache.Clear();
        
        // Force graphics memory cleanup (simulated)
        _compressedTextureMemoryMB = (long)(_compressedTextureMemoryMB * 0.8);
        
        _logger.Debug("[ResourceManager] Performed aggressive graphics cleanup");
    }

    /// <summary>
    /// Release resources for a specific request
    /// </summary>
    private void ReleaseResources(string requestId)
    {
        try
        {
            if (_activeRequests.TryRemove(requestId, out var request))
            {
                _requestTimestamps.TryRemove(requestId, out _);

                // Update allocation counters
                Interlocked.Add(ref _totalAllocatedMemoryMB, -request.MemoryMB);
                _totalAllocatedCpuPercent -= request.CpuPercent;
                Interlocked.Add(ref _totalAllocatedGpuMemoryMB, -request.GpuMemoryMB);

                _logger.Debug("[ResourceManager] Resources released for {OperationName}", request.OperationName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error releasing resources");
        }
    }

    #endregion

    #region Timer Management

    /// <summary>
    /// Start resource monitoring timer
    /// </summary>
    private void StartResourceMonitoring()
    {
        _resourceMonitorTimer = new Timer(
            callback: _ => MonitorResources(),
            state: null,
            dueTime: TimeSpan.FromSeconds(5),
            period: TimeSpan.FromSeconds(5)
        );
    }

    /// <summary>
    /// Start cleanup timer
    /// </summary>
    private void StartCleanupTimer()
    {
        var cleanupInterval = _isEmulatorMode ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(5);
        
        _cleanupTimer = new Timer(
            callback: _ => ReleaseUnusedResources(),
            state: null,
            dueTime: cleanupInterval,
            period: cleanupInterval
        );
    }

    /// <summary>
    /// Start background task management
    /// </summary>
    private void StartBackgroundTaskManagement()
    {
        _backgroundTaskTimer = new Timer(
            callback: _ => ManageBackgroundTasks(),
            state: null,
            dueTime: TimeSpan.FromSeconds(10),
            period: TimeSpan.FromSeconds(10)
        );
    }

    /// <summary>
    /// Monitor system resources
    /// </summary>
    private void MonitorResources()
    {
        try
        {
            CheckMemoryPressure();
            
            // Check for memory leaks
            DetectMemoryLeaks();
            
            // Monitor CPU usage
            var cpuUsage = GetCurrentCpuUsage();
            if (cpuUsage > _highCpuThresholdPercent)
            {
                var eventArgs = new ResourceLimitExceededEventArgs
                {
                    ResourceType = ResourceType.CPU,
                    CurrentUsage = cpuUsage,
                    MaxLimit = _highCpuThresholdPercent,
                    SuggestedAction = "Increase background task throttling, reduce rendering quality, pause non-critical operations",
                    Timestamp = DateTime.UtcNow
                };
                
                ResourceLimitExceeded?.Invoke(this, eventArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error monitoring resources");
        }
    }

    /// <summary>
    /// Detect potential memory leaks by tracking memory growth over time
    /// </summary>
    private void DetectMemoryLeaks()
    {
        try
        {
            var now = DateTime.UtcNow;
            
            // Take memory snapshot every 30 seconds
            if ((now - _lastMemorySnapshot).TotalSeconds < 30)
            {
                return;
            }
            
            var currentMemory = GC.GetTotalMemory(false);
            var currentMemoryMB = currentMemory / (1024.0 * 1024.0);
            
            _memorySnapshots.Add((long)currentMemoryMB);
            _lastMemorySnapshot = now;
            
            // Keep only the last N snapshots
            if (_memorySnapshots.Count > MaxMemorySnapshots)
            {
                _memorySnapshots.RemoveAt(0);
            }
            
            // Check for memory leak pattern (continuous growth)
            if (_memorySnapshots.Count >= MaxMemorySnapshots)
            {
                var firstSnapshot = _memorySnapshots[0];
                var lastSnapshot = _memorySnapshots[_memorySnapshots.Count - 1];
                var memoryGrowth = lastSnapshot - firstSnapshot;
                
                // Check if memory is continuously increasing
                bool isContinuousGrowth = true;
                for (int i = 1; i < _memorySnapshots.Count; i++)
                {
                    if (_memorySnapshots[i] < _memorySnapshots[i - 1])
                    {
                        isContinuousGrowth = false;
                        break;
                    }
                }
                
                if (isContinuousGrowth && memoryGrowth > MemoryLeakThresholdMB)
                {
                    _logger.Warning("[ResourceManager] Potential memory leak detected - Memory increased by {GrowthMB}MB over {Minutes} minutes",
                        memoryGrowth, (MaxMemorySnapshots * 30) / 60);
                    
                    var eventArgs = new ResourceLimitExceededEventArgs
                    {
                        ResourceType = ResourceType.Memory,
                        CurrentUsage = lastSnapshot,
                        MaxLimit = firstSnapshot + MemoryLeakThresholdMB,
                        SuggestedAction = $"Potential memory leak detected. Memory grew by {memoryGrowth:F1}MB. Check for unreleased resources, event handlers, or cached data.",
                        Timestamp = DateTime.UtcNow
                    };
                    
                    ResourceLimitExceeded?.Invoke(this, eventArgs);
                    
                    // Clear snapshots to avoid repeated warnings
                    _memorySnapshots.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error detecting memory leaks");
        }
    }

    /// <summary>
    /// Manage background tasks
    /// </summary>
    private void ManageBackgroundTasks()
    {
        try
        {
            var now = DateTime.UtcNow;
            
            foreach (var task in _backgroundTasks.Values)
            {
                // Apply throttling based on current settings
                if (_backgroundTaskThrottlePercent > 0)
                {
                    var throttleDelay = TimeSpan.FromMilliseconds(100 * _backgroundTaskThrottlePercent / 100.0);
                    if ((now - task.LastExecution) < throttleDelay)
                    {
                        task.IsThrottled = true;
                        continue;
                    }
                }
                
                task.IsThrottled = false;
                task.LastExecution = now;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ResourceManager] Error managing background tasks");
        }
    }

    /// <summary>
    /// Get current CPU usage percentage
    /// </summary>
    private double GetCurrentCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;

            Thread.Sleep(100);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return Math.Max(0, Math.Min(100, cpuUsageTotal * 100));
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Dispose of all resources and timers
    /// </summary>
    public void Dispose()
    {
        _resourceMonitorTimer?.Dispose();
        _cleanupTimer?.Dispose();
        _backgroundTaskTimer?.Dispose();
        
        _logger.Information("[ResourceManager] Disposed");
    }

    #endregion

    #region Internal Data Models

    /// <summary>
    /// Resource request information
    /// </summary>
    private class ResourceRequest
    {
        public string OperationName { get; set; } = string.Empty;
        public long MemoryMB { get; set; }
        public double CpuPercent { get; set; }
        public int GpuMemoryMB { get; set; }
    }

    /// <summary>
    /// Background task information
    /// </summary>
    private class BackgroundTaskInfo
    {
        public string TaskId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ThrottlePercent { get; set; }
        public bool IsThrottled { get; set; }
        public DateTime LastExecution { get; set; }
        public DateTime LastThrottleUpdate { get; set; }
    }

    /// <summary>
    /// Texture information for compression tracking
    /// </summary>
    private class TextureInfo
    {
        public string TextureId { get; set; } = string.Empty;
        public long OriginalSizeBytes { get; set; }
        public long CompressedSizeBytes { get; set; }
        public bool IsCompressed { get; set; }
        public DateTime LastAccessed { get; set; }
        public string Format { get; set; } = string.Empty;
    }

    #endregion
}
