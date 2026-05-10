using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Services.Optimization;

namespace Pigeon_Uno.Services
{
    /// <summary>
    /// Optimized UI renderer service for Uno Platform with emulator-specific optimizations
    /// </summary>
    public class OptimizedRenderer : IOptimizedRenderer, IDisposable
    {
        private readonly IEmulatorDetector? _emulatorDetector;
        private readonly IResourceManager? _resourceManager;
        private readonly ILoggingService _logger;
        
        // Rendering settings
        private bool _isEmulatorModeEnabled = false;
        private RenderingQuality _currentQuality = RenderingQuality.Medium;
        private int _targetFPS = 30;
        private bool _hardwareAccelerationEnabled = false;
        private bool _visualEffectsReduced = false;
        private bool _virtualizationEnabled = false;

        // Performance tracking
        private RenderingMetrics _currentMetrics;
        private DateTime _lastMetricsUpdate = DateTime.MinValue;
        private double _frameTimeAccumulator = 0.0;
        private int _frameCount = 0;
        private readonly object _metricsLock = new();

        public event EventHandler<RenderingPerformanceEventArgs>? RenderingPerformanceChanged;
        public event EventHandler<HardwareAccelerationEventArgs>? HardwareAccelerationChanged;

        public OptimizedRenderer(
            IEmulatorDetector? emulatorDetector,
            IResourceManager? resourceManager,
            ILoggingService logger)
        {
            _emulatorDetector = emulatorDetector;
            _resourceManager = resourceManager;
            _logger = logger;
            
            _currentMetrics = new RenderingMetrics
            {
                CurrentFPS = 0,
                TargetFPS = _targetFPS,
                QualityLevel = (int)_currentQuality,
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogInfo("OptimizedRenderer initialized", nameof(OptimizedRenderer));
        }

        /// <summary>
        /// Initialize the renderer with emulator detection
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Detect emulator mode if detector is available
                if (_emulatorDetector != null)
                {
                    _isEmulatorModeEnabled = await _emulatorDetector.IsRunningInEmulatorAsync();
                    
                    if (_isEmulatorModeEnabled)
                    {
                        var capabilities = await _emulatorDetector.GetEmulatorCapabilitiesAsync();
                        ApplyEmulatorOptimizations(capabilities);
                        _logger.LogInfo("Emulator optimizations applied", nameof(OptimizedRenderer));
                    }
                }

                // Initialize hardware acceleration detection
                DetectHardwareAcceleration();
                
                _logger.LogInfo($"Renderer initialized - Quality: {_currentQuality}, Target FPS: {_targetFPS}", nameof(OptimizedRenderer));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing renderer: {ex.Message}", nameof(OptimizedRenderer));
            }
        }

        /// <summary>
        /// Apply emulator-specific optimizations
        /// </summary>
        private void ApplyEmulatorOptimizations(EmulatorCapabilities capabilities)
        {
            // Reduce quality for emulators
            _currentQuality = RenderingQuality.Low;
            _targetFPS = 24;
            _visualEffectsReduced = true;
            _virtualizationEnabled = true;
            
            // Disable hardware acceleration if not supported
            if (!capabilities.SupportsHardwareAcceleration)
            {
                _hardwareAccelerationEnabled = false;
            }
            
            // Update metrics
            _currentMetrics.QualityLevel = (int)_currentQuality;
            _currentMetrics.TargetFPS = _targetFPS;
            
            RenderingPerformanceChanged?.Invoke(this, new RenderingPerformanceEventArgs
            {
                Quality = _currentQuality,
                TargetFPS = _targetFPS,
                IsEmulatorMode = true
            });
        }

        /// <summary>
        /// Detect hardware acceleration support
        /// </summary>
        private void DetectHardwareAcceleration()
        {
            try
            {
                // Check platform-specific hardware acceleration
                #if __ANDROID__
                _hardwareAccelerationEnabled = true; // Assume supported on Android
                #elif __IOS__
                _hardwareAccelerationEnabled = true; // Assume supported on iOS
                #else
                _hardwareAccelerationEnabled = false; // Desktop/Web may vary
                #endif
                
                HardwareAccelerationChanged?.Invoke(this, new HardwareAccelerationEventArgs
                {
                    IsEnabled = _hardwareAccelerationEnabled,
                    Adapter = "Platform Default"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error detecting hardware acceleration: {ex.Message}", nameof(OptimizedRenderer));
                _hardwareAccelerationEnabled = false;
            }
        }

        /// <summary>
        /// Record frame time for FPS calculation
        /// </summary>
        public void RecordFrameTime(double frameTimeMs)
        {
            lock (_metricsLock)
            {
                _frameTimeAccumulator += frameTimeMs;
                _frameCount++;
                
                // Update metrics every second
                if (DateTime.Now - _lastMetricsUpdate > TimeSpan.FromSeconds(1))
                {
                    UpdateMetrics();
                }
            }
        }

        private void UpdateMetrics()
        {
            if (_frameCount > 0)
            {
                var avgFrameTime = _frameTimeAccumulator / _frameCount;
                var fps = avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;
                
                _currentMetrics.CurrentFPS = (int)fps;
                _currentMetrics.LastUpdated = DateTime.UtcNow;
                
                _logger.LogDebug($"FPS: {fps:F1}, Avg Frame Time: {avgFrameTime:F1}ms", nameof(OptimizedRenderer));
                
                // Reset counters
                _frameTimeAccumulator = 0;
                _frameCount = 0;
                _lastMetricsUpdate = DateTime.Now;
            }
        }

        /// <summary>
        /// Get current rendering metrics
        /// </summary>
        public RenderingMetrics GetMetrics()
        {
            lock (_metricsLock)
            {
                return _currentMetrics;
            }
        }
        
        /// <summary>
        /// Enable emulator mode
        /// </summary>
        public void EnableEmulatorMode(bool enable)
        {
            _isEmulatorModeEnabled = enable;
            if (enable)
            {
                SetRenderingQuality(RenderingQuality.Low);
                SetTargetFPS(24);
            }
        }
        
        /// <summary>
        /// Set rendering quality
        /// </summary>
        public void SetRenderingQuality(RenderingQuality quality)
        {
            _currentQuality = quality;
            _currentMetrics.QualityLevel = (int)quality;
            
            _logger.LogInfo($"Rendering quality set to: {quality}", nameof(OptimizedRenderer));
            
            RenderingPerformanceChanged?.Invoke(this, new RenderingPerformanceEventArgs
            {
                Quality = quality,
                TargetFPS = _targetFPS,
                IsEmulatorMode = _isEmulatorModeEnabled
            });
        }
        
        /// <summary>
        /// Set target FPS
        /// </summary>
        public void SetTargetFPS(int fps)
        {
            _targetFPS = fps;
            _currentMetrics.TargetFPS = fps;
        }
        
        /// <summary>
        /// Enable hardware acceleration
        /// </summary>
        public void EnableHardwareAcceleration(bool enable)
        {
            _hardwareAccelerationEnabled = enable;
            HardwareAccelerationChanged?.Invoke(this, new HardwareAccelerationEventArgs
            {
                IsEnabled = enable,
                Adapter = "Platform Default"
            });
        }
        
        /// <summary>
        /// Reduce visual effects
        /// </summary>
        public void ReduceVisualEffects(bool reduce)
        {
            _visualEffectsReduced = reduce;
        }
        
        /// <summary>
        /// Enable virtualization
        /// </summary>
        public void EnableVirtualization(bool enable)
        {
            _virtualizationEnabled = enable;
        }

        /// <summary>
        /// Check if running in emulator mode
        /// </summary>
        public bool IsEmulatorMode => _isEmulatorModeEnabled;

        /// <summary>
        /// Get current rendering quality
        /// </summary>
        public RenderingQuality CurrentQuality => _currentQuality;

        public void Dispose()
        {
            _logger.LogInfo("OptimizedRenderer disposed", nameof(OptimizedRenderer));
        }
    }

    public class RenderingPerformanceEventArgs : EventArgs
    {
        public RenderingQuality Quality { get; set; }
        public int TargetFPS { get; set; }
        public bool IsEmulatorMode { get; set; }
    }

    public class HardwareAccelerationEventArgs : EventArgs
    {
        public bool IsEnabled { get; set; }
        public string Adapter { get; set; } = string.Empty;
    }
}
