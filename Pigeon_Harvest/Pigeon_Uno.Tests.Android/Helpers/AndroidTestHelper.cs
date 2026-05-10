using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Tests.Android.Helpers;

/// <summary>
/// Provides utilities for Android test environment setup and management
/// Requirements: 9.1, 9.2, 9.3, 9.4, 10.1, 10.2, 10.3
/// </summary>
public class AndroidTestHelper
{
    private static readonly Dictionary<string, bool> _grantedPermissions = new();
    private static AppLifecycleState _currentState = AppLifecycleState.Running;
    private static DeviceOrientation _currentOrientation = DeviceOrientation.Portrait;
    private static readonly List<string> _capturedLogs = new();
    private static readonly object _lockObject = new();

    /// <summary>
    /// Grants a permission programmatically for testing
    /// </summary>
    public static async Task<bool> GrantPermissionAsync(string permission)
    {
        await Task.Delay(50); // Simulate permission dialog
        
        lock (_lockObject)
        {
            _grantedPermissions[permission] = true;
            _capturedLogs.Add($"[PERMISSION] Granted: {permission}");
        }
        
        return true;
    }

    /// <summary>
    /// Revokes a permission programmatically for testing
    /// </summary>
    public static async Task<bool> RevokePermissionAsync(string permission)
    {
        await Task.Delay(50);
        
        lock (_lockObject)
        {
            _grantedPermissions[permission] = false;
            _capturedLogs.Add($"[PERMISSION] Revoked: {permission}");
        }
        
        return true;
    }

    /// <summary>
    /// Checks if a permission is granted
    /// </summary>
    public static bool HasPermission(string permission)
    {
        lock (_lockObject)
        {
            return _grantedPermissions.TryGetValue(permission, out var granted) && granted;
        }
    }

    /// <summary>
    /// Grants multiple permissions at once
    /// </summary>
    public static async Task<Dictionary<string, bool>> GrantMultiplePermissionsAsync(params string[] permissions)
    {
        var results = new Dictionary<string, bool>();
        
        foreach (var permission in permissions)
        {
            var granted = await GrantPermissionAsync(permission);
            results[permission] = granted;
        }
        
        return results;
    }

    /// <summary>
    /// Simulates app pause lifecycle event
    /// </summary>
    public static async Task SimulatePauseAsync()
    {
        await Task.Delay(100); // Simulate pause transition
        
        lock (_lockObject)
        {
            _currentState = AppLifecycleState.Paused;
            _capturedLogs.Add($"[LIFECYCLE] App paused at {DateTime.Now:HH:mm:ss.fff}");
        }
    }

    /// <summary>
    /// Simulates app resume lifecycle event
    /// </summary>
    public static async Task SimulateResumeAsync()
    {
        await Task.Delay(100); // Simulate resume transition
        
        lock (_lockObject)
        {
            _currentState = AppLifecycleState.Running;
            _capturedLogs.Add($"[LIFECYCLE] App resumed at {DateTime.Now:HH:mm:ss.fff}");
        }
    }

    /// <summary>
    /// Simulates app backgrounding
    /// </summary>
    public static async Task SimulateBackgroundAsync()
    {
        await Task.Delay(100);
        
        lock (_lockObject)
        {
            _currentState = AppLifecycleState.Background;
            _capturedLogs.Add($"[LIFECYCLE] App backgrounded at {DateTime.Now:HH:mm:ss.fff}");
        }
    }

    /// <summary>
    /// Simulates app foregrounding
    /// </summary>
    public static async Task SimulateForegroundAsync()
    {
        await Task.Delay(100);
        
        lock (_lockObject)
        {
            _currentState = AppLifecycleState.Running;
            _capturedLogs.Add($"[LIFECYCLE] App foregrounded at {DateTime.Now:HH:mm:ss.fff}");
        }
    }

    /// <summary>
    /// Gets current app lifecycle state
    /// </summary>
    public static AppLifecycleState GetCurrentState()
    {
        lock (_lockObject)
        {
            return _currentState;
        }
    }

    /// <summary>
    /// Simulates device orientation change
    /// </summary>
    public static async Task SimulateOrientationChangeAsync(DeviceOrientation orientation)
    {
        await Task.Delay(200); // Simulate orientation change animation
        
        lock (_lockObject)
        {
            var oldOrientation = _currentOrientation;
            _currentOrientation = orientation;
            _capturedLogs.Add($"[ORIENTATION] Changed from {oldOrientation} to {orientation}");
        }
    }

    /// <summary>
    /// Gets current device orientation
    /// </summary>
    public static DeviceOrientation GetCurrentOrientation()
    {
        lock (_lockObject)
        {
            return _currentOrientation;
        }
    }

    /// <summary>
    /// Captures current memory usage
    /// </summary>
    public static long GetCurrentMemoryUsage()
    {
        return GC.GetTotalMemory(false);
    }

    /// <summary>
    /// Gets detailed memory information
    /// </summary>
    public static MemoryInfo GetMemoryInfo()
    {
        var process = Process.GetCurrentProcess();
        
        return new MemoryInfo
        {
            ManagedMemory = GC.GetTotalMemory(false),
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            VirtualMemory = process.VirtualMemorySize64,
            PeakWorkingSet = process.PeakWorkingSet64
        };
    }

    /// <summary>
    /// Captures logs from the test session
    /// </summary>
    public static async Task<List<string>> CaptureLogsAsync(int lineCount = 100)
    {
        await Task.Delay(10);
        
        lock (_lockObject)
        {
            return _capturedLogs.TakeLast(lineCount).ToList();
        }
    }

    /// <summary>
    /// Adds a log entry
    /// </summary>
    public static void AddLog(string message)
    {
        lock (_lockObject)
        {
            _capturedLogs.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }

    /// <summary>
    /// Clears captured logs
    /// </summary>
    public static void ClearLogs()
    {
        lock (_lockObject)
        {
            _capturedLogs.Clear();
        }
    }

    /// <summary>
    /// Takes a screenshot of the current screen (simulated)
    /// </summary>
    public static async Task<byte[]> TakeScreenshotAsync()
    {
        await Task.Delay(100); // Simulate screenshot capture
        
        // In real implementation, this would capture actual screen
        // For testing, return a small dummy image
        var dummyImage = new byte[1024];
        new Random().NextBytes(dummyImage);
        
        lock (_lockObject)
        {
            _capturedLogs.Add($"[SCREENSHOT] Captured at {DateTime.Now:HH:mm:ss.fff}");
        }
        
        return dummyImage;
    }

    /// <summary>
    /// Saves screenshot to file
    /// </summary>
    public static async Task<string> SaveScreenshotAsync(string filename)
    {
        var screenshot = await TakeScreenshotAsync();
        var path = Path.Combine(Path.GetTempPath(), filename);
        await File.WriteAllBytesAsync(path, screenshot);
        
        lock (_lockObject)
        {
            _capturedLogs.Add($"[SCREENSHOT] Saved to {path}");
        }
        
        return path;
    }

    /// <summary>
    /// Monitors resource usage over a period
    /// </summary>
    public static async Task<ResourceUsageSnapshot> MonitorResourceUsageAsync(TimeSpan duration)
    {
        var startMemory = GetCurrentMemoryUsage();
        var startTime = DateTime.UtcNow;
        var peakMemory = startMemory;
        var samples = new List<long>();
        
        var cts = new CancellationTokenSource(duration);
        
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, cts.Token);
                var currentMemory = GetCurrentMemoryUsage();
                samples.Add(currentMemory);
                
                if (currentMemory > peakMemory)
                {
                    peakMemory = currentMemory;
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when duration expires
        }
        
        var endMemory = GetCurrentMemoryUsage();
        var actualDuration = DateTime.UtcNow - startTime;
        
        return new ResourceUsageSnapshot
        {
            StartMemory = startMemory,
            EndMemory = endMemory,
            PeakMemory = peakMemory,
            AverageMemory = samples.Any() ? (long)samples.Average() : startMemory,
            SampleCount = samples.Count,
            Duration = actualDuration
        };
    }

    /// <summary>
    /// Simulates low memory condition
    /// </summary>
    public static async Task SimulateLowMemoryAsync()
    {
        await Task.Delay(50);
        
        lock (_lockObject)
        {
            _capturedLogs.Add($"[MEMORY] Low memory condition simulated");
        }
        
        // Trigger garbage collection to simulate memory pressure
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Resets all test state
    /// </summary>
    public static void Reset()
    {
        lock (_lockObject)
        {
            _grantedPermissions.Clear();
            _currentState = AppLifecycleState.Running;
            _currentOrientation = DeviceOrientation.Portrait;
            _capturedLogs.Clear();
        }
    }
}

public enum DeviceOrientation
{
    Portrait,
    Landscape,
    PortraitUpsideDown,
    LandscapeRight
}

public enum AppLifecycleState
{
    Running,
    Paused,
    Background,
    Stopped
}

public class ResourceUsageSnapshot
{
    public long StartMemory { get; set; }
    public long EndMemory { get; set; }
    public long PeakMemory { get; set; }
    public long AverageMemory { get; set; }
    public int SampleCount { get; set; }
    public TimeSpan Duration { get; set; }
    public double MemoryDelta => EndMemory - StartMemory;
    public double MemoryDeltaMB => MemoryDelta / 1024.0 / 1024.0;
    public double PeakMemoryMB => PeakMemory / 1024.0 / 1024.0;
}

public class MemoryInfo
{
    public long ManagedMemory { get; set; }
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long VirtualMemory { get; set; }
    public long PeakWorkingSet { get; set; }
    
    public double ManagedMemoryMB => ManagedMemory / 1024.0 / 1024.0;
    public double WorkingSetMB => WorkingSet / 1024.0 / 1024.0;
    public double PrivateMemoryMB => PrivateMemory / 1024.0 / 1024.0;
}
