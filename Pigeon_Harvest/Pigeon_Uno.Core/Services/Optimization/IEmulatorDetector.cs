using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services.Optimization;

/// <summary>
/// Service interface for detecting Android emulator environments and measuring system capabilities.
/// Validates Requirements 1.1, 1.2, 1.3, 1.4, 1.5
/// </summary>
public interface IEmulatorDetector
{
    /// <summary>
    /// Asynchronously detects if the application is running in an Android emulator.
    /// </summary>
    /// <returns>True if running in an emulator, false otherwise.</returns>
    Task<bool> IsRunningInEmulatorAsync();

    /// <summary>
    /// Asynchronously retrieves comprehensive emulator capabilities including hardware and network information.
    /// </summary>
    /// <returns>EmulatorCapabilities object containing system information.</returns>
    Task<EmulatorCapabilities> GetEmulatorCapabilitiesAsync();

    /// <summary>
    /// Checks if hardware acceleration is supported on the current device.
    /// </summary>
    /// <returns>True if hardware acceleration is available, false otherwise.</returns>
    bool SupportsHardwareAcceleration();

    /// <summary>
    /// Identifies the specific type of emulator being used.
    /// </summary>
    /// <returns>EmulatorType enum value indicating the emulator type.</returns>
    EmulatorType GetEmulatorType();
}

/// <summary>
/// Represents comprehensive capabilities of an emulator or device.
/// </summary>
public class EmulatorCapabilities
{
    /// <summary>
    /// Indicates whether hardware acceleration is supported.
    /// </summary>
    public bool SupportsHardwareAcceleration { get; set; }

    /// <summary>
    /// Available memory in megabytes.
    /// </summary>
    public int AvailableMemoryMB { get; set; }

    /// <summary>
    /// Number of CPU cores available.
    /// </summary>
    public int CPUCores { get; set; }

    /// <summary>
    /// Graphics capabilities of the device.
    /// </summary>
    public GraphicsCapabilities Graphics { get; set; } = new();

    /// <summary>
    /// Network capabilities of the device.
    /// </summary>
    public NetworkCapabilities Network { get; set; } = new();

    /// <summary>
    /// Type of emulator detected.
    /// </summary>
    public EmulatorType Type { get; set; }

    /// <summary>
    /// Version string of the emulator or device.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Represents graphics capabilities of a device.
/// </summary>
public class GraphicsCapabilities
{
    /// <summary>
    /// Indicates whether OpenGL ES is supported.
    /// </summary>
    public bool SupportsOpenGLES { get; set; }

    /// <summary>
    /// Version of OpenGL ES supported.
    /// </summary>
    public string OpenGLESVersion { get; set; } = string.Empty;

    /// <summary>
    /// Maximum texture size supported by the GPU.
    /// </summary>
    public int MaxTextureSize { get; set; }

    /// <summary>
    /// GPU memory in megabytes.
    /// </summary>
    public int GPUMemoryMB { get; set; }

    /// <summary>
    /// Indicates whether hardware rendering is supported.
    /// </summary>
    public bool SupportsHardwareRendering { get; set; }
}

/// <summary>
/// Represents network capabilities of a device.
/// </summary>
public class NetworkCapabilities
{
    /// <summary>
    /// Indicates whether WiFi is supported.
    /// </summary>
    public bool SupportsWiFi { get; set; }

    /// <summary>
    /// Indicates whether cellular connectivity is supported.
    /// </summary>
    public bool SupportsCellular { get; set; }

    /// <summary>
    /// Maximum bandwidth in megabits per second.
    /// </summary>
    public double MaxBandwidthMbps { get; set; }

    /// <summary>
    /// Network latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Indicates whether network is throttled.
    /// </summary>
    public bool IsThrottled { get; set; }
}

/// <summary>
/// Enumeration of emulator types that can be detected.
/// </summary>
public enum EmulatorType
{
    /// <summary>
    /// Not running in an emulator (real device).
    /// </summary>
    None,

    /// <summary>
    /// Android Studio AVD (Android Virtual Device).
    /// </summary>
    AndroidStudioAVD,

    /// <summary>
    /// Genymotion emulator.
    /// </summary>
    Genymotion,

    /// <summary>
    /// BlueStacks emulator.
    /// </summary>
    BlueStacks,

    /// <summary>
    /// Generic QEMU-based emulator.
    /// </summary>
    GenericQEMU,

    /// <summary>
    /// Unknown emulator type.
    /// </summary>
    Unknown
}
