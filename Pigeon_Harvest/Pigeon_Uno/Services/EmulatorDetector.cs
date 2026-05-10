using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services.Optimization;

namespace Pigeon_Uno.Services;

/// <summary>
/// Base emulator detection service with caching and multi-layered detection.
/// Ported from Pigeon_Avalonia with Uno Platform adaptations.
/// </summary>
public class EmulatorDetector : IEmulatorDetector
{
    private EmulatorCapabilities? _cachedCapabilities;
    private bool? _cachedEmulatorStatus;
    private EmulatorType? _cachedEmulatorType;

    // Known emulator indicators
    private static readonly Dictionary<string, EmulatorType> EmulatorIndicators = new()
    {
        { "goldfish", EmulatorType.AndroidStudioAVD },
        { "ranchu", EmulatorType.AndroidStudioAVD },
        { "vbox86", EmulatorType.Genymotion },
        { "genymotion", EmulatorType.Genymotion },
        { "bluestacks", EmulatorType.BlueStacks },
        { "qemu", EmulatorType.GenericQEMU }
    };

    /// <summary>
    /// Asynchronously detects if the application is running in an Android emulator.
    /// Implements caching to avoid repeated expensive checks.
    /// </summary>
    public async Task<bool> IsRunningInEmulatorAsync()
    {
        if (_cachedEmulatorStatus.HasValue)
        {
            return _cachedEmulatorStatus.Value;
        }

        try
        {
            // Multi-layered detection approach
            bool isEmulator = await DetectEmulatorAsync();
            _cachedEmulatorStatus = isEmulator;
            Debug.WriteLine($"[EmulatorDetector] Emulator detection result: {isEmulator}");
            return isEmulator;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error detecting emulator: {ex.Message}");
            _cachedEmulatorStatus = false;
            return false;
        }
    }

    /// <summary>
    /// Asynchronously retrieves comprehensive emulator capabilities.
    /// Results are cached for performance.
    /// </summary>
    public async Task<EmulatorCapabilities> GetEmulatorCapabilitiesAsync()
    {
        if (_cachedCapabilities != null)
        {
            return _cachedCapabilities;
        }

        try
        {
            var capabilities = new EmulatorCapabilities
            {
                Type = GetEmulatorType(),
                SupportsHardwareAcceleration = SupportsHardwareAcceleration(),
                AvailableMemoryMB = GetAvailableMemoryMB(),
                CPUCores = Environment.ProcessorCount,
                Graphics = await GetGraphicsCapabilitiesAsync(),
                Network = await GetNetworkCapabilitiesAsync(),
                Version = GetEmulatorVersion()
            };

            _cachedCapabilities = capabilities;
            Debug.WriteLine($"[EmulatorDetector] Capabilities: Type={capabilities.Type}, Memory={capabilities.AvailableMemoryMB}MB, CPU={capabilities.CPUCores} cores");
            return capabilities;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error getting capabilities: {ex.Message}");
            // Return safe defaults
            return new EmulatorCapabilities
            {
                Type = EmulatorType.None,
                SupportsHardwareAcceleration = false,
                AvailableMemoryMB = 512,
                CPUCores = Environment.ProcessorCount,
                Graphics = new GraphicsCapabilities(),
                Network = new NetworkCapabilities()
            };
        }
    }

    /// <summary>
    /// Checks if hardware acceleration is supported on the current device.
    /// </summary>
    public bool SupportsHardwareAcceleration()
    {
        try
        {
            // Platform-specific implementation will override this
            // Base implementation provides conservative default
            var hardware = GetSystemProperty("ro.hardware");
            var qemu = GetSystemProperty("ro.kernel.qemu");

            if (qemu == "1")
            {
                // Check if QEMU has hardware acceleration
                var qemuAccel = GetSystemProperty("ro.kernel.qemu.gles");
                return qemuAccel == "1" || hardware.Contains("ranchu");
            }

            // Genymotion typically has good hardware acceleration
            if (hardware.Contains("vbox"))
            {
                return true;
            }

            // Modern emulators generally support hardware acceleration
            return !string.IsNullOrEmpty(hardware) && !hardware.Contains("goldfish");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Identifies the specific type of emulator being used.
    /// </summary>
    public EmulatorType GetEmulatorType()
    {
        if (_cachedEmulatorType.HasValue)
        {
            return _cachedEmulatorType.Value;
        }

        try
        {
            var hardware = GetSystemProperty("ro.hardware").ToLowerInvariant();
            var product = GetSystemProperty("ro.product.model").ToLowerInvariant();
            var manufacturer = GetSystemProperty("ro.product.manufacturer").ToLowerInvariant();
            var brand = GetSystemProperty("ro.product.brand").ToLowerInvariant();

            // Check each indicator
            foreach (var indicator in EmulatorIndicators)
            {
                if (hardware.Contains(indicator.Key) ||
                    product.Contains(indicator.Key) ||
                    manufacturer.Contains(indicator.Key) ||
                    brand.Contains(indicator.Key))
                {
                    _cachedEmulatorType = indicator.Value;
                    Debug.WriteLine($"[EmulatorDetector] Detected emulator type: {indicator.Value}");
                    return indicator.Value;
                }
            }

            // Additional specific checks
            if (manufacturer.Contains("genymotion") || brand.Contains("genymotion"))
            {
                _cachedEmulatorType = EmulatorType.Genymotion;
                return EmulatorType.Genymotion;
            }

            if (product.Contains("sdk") || product.Contains("emulator"))
            {
                _cachedEmulatorType = EmulatorType.AndroidStudioAVD;
                return EmulatorType.AndroidStudioAVD;
            }

            // If we detected an emulator but can't identify the type
            if (IsEmulatorDetected())
            {
                _cachedEmulatorType = EmulatorType.Unknown;
                return EmulatorType.Unknown;
            }

            _cachedEmulatorType = EmulatorType.None;
            return EmulatorType.None;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error detecting emulator type: {ex.Message}");
            _cachedEmulatorType = EmulatorType.None;
            return EmulatorType.None;
        }
    }

    /// <summary>
    /// Multi-layered emulator detection approach.
    /// </summary>
    private async Task<bool> DetectEmulatorAsync()
    {
        // Layer 1: System property analysis (most reliable)
        if (CheckSystemProperties())
        {
            Debug.WriteLine("[EmulatorDetector] Detected via system properties");
            return true;
        }

        // Layer 2: Performance characteristic profiling
        if (await CheckPerformanceCharacteristicsAsync())
        {
            Debug.WriteLine("[EmulatorDetector] Detected via performance characteristics");
            return true;
        }

        // Layer 3: File system analysis
        if (CheckFileSystemIndicators())
        {
            Debug.WriteLine("[EmulatorDetector] Detected via file system indicators");
            return true;
        }

        // Layer 4: Hardware analysis
        if (CheckHardwareIndicators())
        {
            Debug.WriteLine("[EmulatorDetector] Detected via hardware indicators");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check Android system properties for emulator indicators.
    /// </summary>
    private bool CheckSystemProperties()
    {
        try
        {
            var properties = new[]
            {
                "ro.kernel.qemu",
                "ro.hardware",
                "ro.product.model",
                "ro.product.manufacturer",
                "ro.product.brand",
                "ro.build.product",
                "ro.build.fingerprint"
            };

            foreach (var property in properties)
            {
                var value = GetSystemProperty(property).ToLowerInvariant();

                if (string.IsNullOrEmpty(value))
                    continue;

                // Check for known emulator indicators
                var emulatorKeywords = new[]
                {
                    "qemu", "goldfish", "ranchu", "vbox", "genymotion",
                    "bluestacks", "emulator", "simulator", "sdk"
                };

                foreach (var keyword in emulatorKeywords)
                {
                    if (value.Contains(keyword))
                    {
                        Debug.WriteLine($"[EmulatorDetector] Found '{keyword}' in {property}");
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error checking system properties: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check performance characteristics typical of emulators.
    /// </summary>
    private async Task<bool> CheckPerformanceCharacteristicsAsync()
    {
        try
        {
            // Measure CPU performance
            var cpuScore = await MeasureCPUPerformanceAsync();

            // Measure memory access patterns
            var memoryScore = MeasureMemoryPerformance();

            // Emulators typically have lower performance scores
            var isEmulatorLikely = cpuScore < 0.5 || memoryScore < 0.3;

            if (isEmulatorLikely)
            {
                Debug.WriteLine($"[EmulatorDetector] Performance suggests emulator (CPU: {cpuScore:F2}, Memory: {memoryScore:F2})");
            }

            return isEmulatorLikely;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error checking performance: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check file system for emulator-specific files.
    /// </summary>
    private bool CheckFileSystemIndicators()
    {
        try
        {
            var emulatorFiles = new[]
            {
                "/system/lib/libc_malloc_debug_qemu.so",
                "/sys/qemu_trace",
                "/system/bin/qemu-props",
                "/dev/qemu_pipe"
            };

            foreach (var file in emulatorFiles)
            {
                if (File.Exists(file))
                {
                    Debug.WriteLine($"[EmulatorDetector] Found emulator file: {file}");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error checking file system: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check hardware indicators for emulator detection.
    /// </summary>
    private bool CheckHardwareIndicators()
    {
        try
        {
            // Check CPU architecture
            var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

            // Emulators often run on x86/x64 even when targeting ARM
            if (architecture.Contains("x86") || architecture.Contains("x64"))
            {
                var product = GetSystemProperty("ro.product.cpu.abi").ToLowerInvariant();
                if (product.Contains("arm"))
                {
                    Debug.WriteLine("[EmulatorDetector] Architecture mismatch suggests emulator");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EmulatorDetector] Error checking hardware: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Simple emulator detection check.
    /// </summary>
    private bool IsEmulatorDetected()
    {
        var qemu = GetSystemProperty("ro.kernel.qemu");
        var hardware = GetSystemProperty("ro.hardware");

        return qemu == "1" ||
               hardware.Contains("goldfish") ||
               hardware.Contains("ranchu") ||
               hardware.Contains("vbox");
    }

    /// <summary>
    /// Get Android system property value.
    /// Platform-specific implementations will override this.
    /// </summary>
    protected virtual string GetSystemProperty(string propertyName)
    {
        // Base implementation returns empty string
        // Android-specific implementation will use Android.OS.SystemProperties
        return string.Empty;
    }

    /// <summary>
    /// Measure CPU performance characteristics.
    /// </summary>
    private async Task<double> MeasureCPUPerformanceAsync()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Perform CPU-intensive calculation
            await Task.Run(() =>
            {
                double result = 0;
                for (int i = 0; i < 1000000; i++)
                {
                    result += Math.Sqrt(i) * Math.Sin(i);
                }
            });

            stopwatch.Stop();

            // Normalize score (lower time = higher score)
            var score = Math.Max(0, 1.0 - (stopwatch.ElapsedMilliseconds / 1000.0));
            return Math.Min(1.0, score);
        }
        catch
        {
            return 0.5; // Default neutral score
        }
    }

    /// <summary>
    /// Measure memory performance characteristics.
    /// </summary>
    private double MeasureMemoryPerformance()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Perform memory-intensive operations
            var data = new byte[1024 * 1024]; // 1MB
            var random = new Random();

            for (int i = 0; i < data.Length; i += 1024)
            {
                data[i] = (byte)random.Next(256);
            }

            stopwatch.Stop();

            // Normalize score
            var score = Math.Max(0, 1.0 - (stopwatch.ElapsedMilliseconds / 100.0));
            return Math.Min(1.0, score);
        }
        catch
        {
            return 0.5; // Default neutral score
        }
    }

    /// <summary>
    /// Get available memory in MB.
    /// </summary>
    private int GetAvailableMemoryMB()
    {
        try
        {
            var workingSet = Environment.WorkingSet;
            return Math.Max(256, (int)(workingSet / (1024 * 1024)));
        }
        catch
        {
            return 512; // Conservative default
        }
    }

    /// <summary>
    /// Get graphics capabilities.
    /// </summary>
    private async Task<GraphicsCapabilities> GetGraphicsCapabilitiesAsync()
    {
        return await Task.FromResult(new GraphicsCapabilities
        {
            SupportsOpenGLES = true,
            OpenGLESVersion = "3.0",
            MaxTextureSize = 4096,
            GPUMemoryMB = 128,
            SupportsHardwareRendering = SupportsHardwareAcceleration()
        });
    }

    /// <summary>
    /// Get network capabilities.
    /// </summary>
    private async Task<NetworkCapabilities> GetNetworkCapabilitiesAsync()
    {
        return await Task.FromResult(new NetworkCapabilities
        {
            SupportsWiFi = true,
            SupportsCellular = false,
            MaxBandwidthMbps = 100.0,
            LatencyMs = 50,
            IsThrottled = false
        });
    }

    /// <summary>
    /// Get emulator version string.
    /// </summary>
    private string GetEmulatorVersion()
    {
        try
        {
            var buildVersion = GetSystemProperty("ro.build.version.release");
            var buildId = GetSystemProperty("ro.build.id");

            if (!string.IsNullOrEmpty(buildVersion))
            {
                return $"Android {buildVersion} ({buildId})";
            }

            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
