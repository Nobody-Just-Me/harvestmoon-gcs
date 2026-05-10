using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pigeon_Uno.Core.Services.Optimization;

namespace Pigeon_Uno.Services;

/// <summary>
/// Initializes optimization services in the correct order with error handling.
/// Validates Requirements 2.1, 8.1
/// </summary>
public class OptimizationServiceInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private bool _isInitialized;
    private bool _profileHookAttached;

    public OptimizationServiceInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Initializes all optimization services in the correct order.
    /// Order: EmulatorDetector → PerformanceManager → ResourceManager → Others
    /// </summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
        {
            Serilog.Log.Warning("Optimization services already initialized");
            return true;
        }

        Serilog.Log.Information("Starting optimization services initialization");
        var startTime = DateTime.Now;

        try
        {
            // STEP 1: Initialize EmulatorDetector
            Serilog.Log.Information("Step 1: Initializing EmulatorDetector");
            var emulatorDetector = _serviceProvider.GetService<IEmulatorDetector>();
            
            if (emulatorDetector == null)
            {
                Serilog.Log.Warning("EmulatorDetector service not registered, skipping emulator detection");
                return await FallbackInitializationAsync();
            }

            // Detect emulator and get capabilities
            bool isEmulator = await emulatorDetector.IsRunningInEmulatorAsync();
            var capabilities = await emulatorDetector.GetEmulatorCapabilitiesAsync();
            
            Serilog.Log.Information($"Emulator detection complete: IsEmulator={isEmulator}, " +
                $"AvailableMemory={capabilities.AvailableMemoryMB}MB, " +
                $"HardwareAcceleration={capabilities.SupportsHardwareAcceleration}");

            // STEP 2: Initialize PerformanceManager
            Serilog.Log.Information("Step 2: Initializing PerformanceManager");
            var performanceManager = _serviceProvider.GetService<IPerformanceManager>();
            
            if (performanceManager == null)
            {
                Serilog.Log.Warning("PerformanceManager service not registered, skipping performance optimization");
            }
            else
            {
                await performanceManager.InitializeAsync(capabilities);
                
                // Start performance monitoring
                performanceManager.MonitorPerformanceMetrics();
                
                Serilog.Log.Information($"PerformanceManager initialized with profile: {performanceManager.GetActiveProfile().Name}");
            }

            // STEP 3: Initialize ResourceManager (if available)
            Serilog.Log.Information("Step 3: Initializing ResourceManager");
            var resourceManager = _serviceProvider.GetService<IResourceManager>();
            
            if (resourceManager == null)
            {
                Serilog.Log.Information("ResourceManager service not registered, skipping resource management");
            }
            else
            {
                await resourceManager.InitializeAsync(capabilities);
                Serilog.Log.Information("ResourceManager initialized");
            }

            // STEP 4: Initialize other optimization services (TelemetryHandler, NetworkManager, etc.)
            Serilog.Log.Information("Step 4: Initializing renderer/network/telemetry optimizers");
            var renderer = _serviceProvider.GetService<IOptimizedRenderer>();
            var networkManager = _serviceProvider.GetService<IOptimizedNetworkManager>();
            var telemetryHandler = _serviceProvider.GetService<IOptimizedTelemetryHandler>();

            if (renderer is OptimizedRenderer optimizedRenderer)
            {
                await optimizedRenderer.InitializeAsync();
            }

            if (networkManager is OptimizedNetworkManager optimizedNetworkManager)
            {
                await optimizedNetworkManager.InitializeAsync();
            }

            var activeProfile = performanceManager?.GetActiveProfile() ?? PerformanceProfile.Balanced;
            ApplyProfile(activeProfile, resourceManager, renderer, networkManager, telemetryHandler);

            if (performanceManager != null && !_profileHookAttached)
            {
                performanceManager.ProfileChanged += (_, profile) =>
                {
                    ApplyProfile(profile, resourceManager, renderer, networkManager, telemetryHandler);
                };
                _profileHookAttached = true;
            }

            _isInitialized = true;
            var elapsed = DateTime.Now - startTime;
            Serilog.Log.Information($"Optimization services initialization completed successfully in {elapsed.TotalMilliseconds:F0}ms");
            
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to initialize optimization services");
            return await FallbackInitializationAsync();
        }
    }

    /// <summary>
    /// Fallback initialization when optimization services fail.
    /// Ensures app can still run without optimizations.
    /// </summary>
    private async Task<bool> FallbackInitializationAsync()
    {
        await Task.CompletedTask;
        
        Serilog.Log.Warning("Using fallback initialization - app will run without optimizations");
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// Applies performance profile values to all optimization workers.
    /// </summary>
    private static void ApplyProfile(
        PerformanceProfile profile,
        IResourceManager? resourceManager,
        IOptimizedRenderer? renderer,
        IOptimizedNetworkManager? networkManager,
        IOptimizedTelemetryHandler? telemetryHandler)
    {
        resourceManager?.SetMemoryLimits(profile.MaxMemoryMB);
        resourceManager?.SetBackgroundTaskThrottling(profile.BackgroundTaskThrottlePercent);
        resourceManager?.EnableLowMemoryMode(profile.EnableLowPowerMode);

        if (renderer != null)
        {
            renderer.SetRenderingQuality(profile.RenderQuality);
            renderer.SetTargetFPS(profile.TargetFrameRate);
            renderer.ReduceVisualEffects(profile.EnableLowPowerMode || profile.RenderQuality == RenderingQuality.Low);
            renderer.EnableVirtualization(profile.RenderQuality != RenderingQuality.High);
            renderer.EnableEmulatorMode(profile.Name == PerformanceProfile.EmulatorOptimized.Name);
        }

        if (networkManager is OptimizedNetworkManager optimizedNetworkManager)
        {
            optimizedNetworkManager.SetOptimizationLevel(profile.NetworkLevel);
        }

        networkManager?.EnableCompression(profile.NetworkLevel != NetworkOptimizationLevel.None);
        networkManager?.SetBufferSize(profile.RenderQuality == RenderingQuality.High ? 32 * 1024 : 16 * 1024);

        if (telemetryHandler is OptimizedTelemetryHandler optimizedTelemetryHandler)
        {
            optimizedTelemetryHandler.SetProcessingMode(profile.TelemetryMode);
        }

        telemetryHandler?.SetUpdateRate(Math.Max(1, profile.TargetFrameRate));
        telemetryHandler?.EnableBatching(profile.TelemetryMode != TelemetryProcessingMode.Full);

        Serilog.Log.Information(
            "Optimization profile applied: {Profile} | FPS={TargetFps} | Telemetry={TelemetryMode} | Network={NetworkLevel}",
            profile.Name,
            profile.TargetFrameRate,
            profile.TelemetryMode,
            profile.NetworkLevel);
    }

    /// <summary>
    /// Gets the initialization status.
    /// </summary>
    public bool IsInitialized => _isInitialized;
}
