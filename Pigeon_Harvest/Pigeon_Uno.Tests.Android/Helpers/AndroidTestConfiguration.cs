using System;
using System.Collections.Generic;

namespace Pigeon_Uno.Tests.Android.Helpers;

/// <summary>
/// Configuration for Android test execution
/// </summary>
public class AndroidTestConfiguration
{
    public static AndroidTestConfiguration Default => new()
    {
        DeviceId = "emulator-5554",
        UseEmulator = true,
        ApiLevel = 28,
        RequiredPermissions = new List<string>
        {
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.CAMERA",
            "android.permission.WRITE_EXTERNAL_STORAGE",
            "android.permission.READ_EXTERNAL_STORAGE",
            "android.permission.INTERNET",
            "android.permission.ACCESS_NETWORK_STATE"
        },
        PerformanceThresholds = new Dictionary<string, object>
        {
            { "MaxHeapMemoryMB", 256 },
            { "MaxNativeMemoryMB", 128 },
            { "MaxUIThreadCpuPercent", 50.0 },
            { "MaxBackgroundThreadCpuPercent", 30.0 },
            { "MaxBatteryDrainPercentPerHour", 5.0 },
            { "MaxBackgroundBatteryDrainPercentPerHour", 1.0 },
            { "MaxStartupTimeSeconds", 3.0 },
            { "MaxWarmStartupTimeSeconds", 1.0 },
            { "MaxUIThreadBlockingMs", 16 }, // 60fps target
            { "MaxNetworkOverheadPercent", 20.0 },
            { "MaxReconnectionTimeSeconds", 5.0 }
        },
        DefaultTimeout = TimeSpan.FromSeconds(30),
        CaptureScreenshots = true,
        EnableDetailedLogging = true
    };

    public string DeviceId { get; set; } = string.Empty;
    public bool UseEmulator { get; set; }
    public int ApiLevel { get; set; }
    public List<string> RequiredPermissions { get; set; } = new();
    public Dictionary<string, object> PerformanceThresholds { get; set; } = new();
    public TimeSpan DefaultTimeout { get; set; }
    public bool CaptureScreenshots { get; set; }
    public bool EnableDetailedLogging { get; set; }

    /// <summary>
    /// Gets a performance threshold value
    /// </summary>
    public T GetThreshold<T>(string key, T defaultValue)
    {
        if (PerformanceThresholds.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(DeviceId) &&
               ApiLevel >= 21 && // Minimum Android 5.0
               DefaultTimeout > TimeSpan.Zero;
    }
}
