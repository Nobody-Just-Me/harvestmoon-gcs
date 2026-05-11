using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Windows.Storage;
using Windows.Storage.Pickers;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Controls
{
    public sealed partial class StatsControl : UserControl
    {
        // Data storage - using ObservableCollection for LiveCharts reactivity
        private readonly List<double> _timeData = new();
        private readonly ObservableCollection<double> _yawData = new();
        private readonly ObservableCollection<double> _pitchData = new();
        private readonly ObservableCollection<double> _rollData = new();

        // Chart series
        private ISeries[] _yawSeries;
        private ISeries[] _pitchRollSeries;

        // Timer for updates
        private DispatcherQueueTimer? _updateTimer;
        private bool _isAutoUpdateEnabled = false;
        private bool _testMode = true;
        private DateTime _testStartTime = DateTime.Now;

        // Services
        private readonly IMavLinkService? _mavLinkService;
        private TelemetryData? _latestTelemetry;
        
        // Performance optimization
        private int _updateCounter = 0;
        private DateTime _lastAxisUpdate = DateTime.MinValue;
        private bool? _lastArmedState;

        public StatsControl()
        {
            Serilog.Log.Information("[StatsControl] Constructor START");
            
            try
            {
                Serilog.Log.Information("[StatsControl] Calling InitializeComponent()");
                this.InitializeComponent();
                Serilog.Log.Information("[StatsControl] InitializeComponent() completed");

                // Get services from DI container
                Serilog.Log.Information("[StatsControl] Getting MavLinkService from DI");
                _mavLinkService = App.GetService<IMavLinkService>();
                Serilog.Log.Information("[StatsControl] MavLinkService: {Status}", _mavLinkService != null ? "OK" : "NULL");

                // Subscribe to telemetry updates
                if (_mavLinkService != null)
                {
                    _mavLinkService.TelemetryReceived += OnTelemetryReceived;
                    Serilog.Log.Information("[StatsControl] Subscribed to TelemetryReceived event");
                }

                // Initialize charts
                Serilog.Log.Information("[StatsControl] Calling InitializeCharts()");
                InitializeCharts();
                Serilog.Log.Information("[StatsControl] InitializeCharts() completed");

                // Setup timer - reduced frequency for better performance
                Serilog.Log.Information("[StatsControl] Setting up timer");
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                Serilog.Log.Information("[StatsControl] DispatcherQueue: {Status}", dispatcherQueue != null ? "OK" : "NULL");
                if (dispatcherQueue != null)
                {
                    _updateTimer = dispatcherQueue.CreateTimer();
                    _updateTimer.Interval = TimeSpan.FromMilliseconds(200); // Increased from 500ms to reduce overhead
                    _updateTimer.Tick += UpdateTimer_Tick;
                    Serilog.Log.Information("[StatsControl] Timer created with 200ms interval");
                }
                else
                {
                    Serilog.Log.Error("[StatsControl] ERROR: DispatcherQueue is NULL!");
                }

                // Initialize button states - Start button disabled until armed
                Serilog.Log.Information("[StatsControl] Initializing button states");
                this.Loaded += (s, e) =>
                {
                    Serilog.Log.Information("[StatsControl] *** Loaded event FIRED ***");
                    // Set initial button state - disabled until armed
                    btn_start_stats.IsEnabled = false;
                    btn_stop_stats.IsEnabled = false;
                    Serilog.Log.Information("[StatsControl] Start button disabled until vehicle is armed");
                };
                
                Serilog.Log.Information("[StatsControl] Constructor COMPLETE");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[StatsControl] EXCEPTION in constructor");
            }
        }

        private void OnTelemetryReceived(object? sender, FlightData flightData)
        {
            // Convert FlightData to TelemetryData for internal use
            var telemetry = new TelemetryData
            {
                Yaw = flightData.IMU.Yaw,
                Pitch = flightData.IMU.Pitch,
                Roll = flightData.IMU.Roll,
                IsArmed = flightData.FlightMode != FlightMode.DISARMED
            };
            
            // Store latest telemetry data for use in AddToStatistik
            _latestTelemetry = telemetry;

            if (_lastArmedState.HasValue && _lastArmedState.Value == telemetry.IsArmed)
            {
                return;
            }

            _lastArmedState = telemetry.IsArmed;
             
            // Update Start button state based on armed status
            // Only enable Start button when vehicle is armed and test is not running
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (telemetry.IsArmed && !_isAutoUpdateEnabled)
                {
                    btn_start_stats.IsEnabled = true;
                }
                else if (!telemetry.IsArmed)
                {
                    // Disable Start button when disarmed
                    btn_start_stats.IsEnabled = false;
                    
                    // Stop test if running when vehicle disarms
                    if (_isAutoUpdateEnabled)
                    {
                        StopAutoUpdate();
                        btn_stop_stats.IsEnabled = false;
                        Serilog.Log.Information("[StatsControl] Test stopped - vehicle disarmed");
                    }
                }
            });
        }

        private void InitializeCharts()
        {
            // Initialize Yaw chart with performance optimizations
            _yawSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _yawData,
                    Name = "Heading (Yaw)",
                    Stroke = new SolidColorPaint(SKColors.DeepSkyBlue) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0, // Disable point markers for better performance
                    LineSmoothness = 0,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(100) // Faster animations
                }
            };

            // TEMPORARILY DISABLED: LiveCharts build errors
            // chartYaw.Series = _yawSeries;
            // chartYaw.XAxes = new Axis[] { ... };
            // chartYaw.YAxes = new Axis[] { ... };

            // Initialize Pitch/Roll chart with performance optimizations
            _pitchRollSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _pitchData,
                    Name = "Pitch",
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0, // Disable point markers for better performance
                    LineSmoothness = 0,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(100)
                },
                new LineSeries<double>
                {
                    Values = _rollData,
                    Name = "Roll",
                    Stroke = new SolidColorPaint(SKColors.LimeGreen) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0, // Disable point markers for better performance
                    LineSmoothness = 0,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(100)
                }
            };

            // TEMPORARILY DISABLED: LiveCharts build errors
            // chartPitchRoll.Series = _pitchRollSeries;
            // chartPitchRoll.XAxes = new Axis[] { ... };
            // chartPitchRoll.YAxes = new Axis[] { ... };
            // chartPitchRoll.LegendPosition = LiveChartsCore.Measure.LegendPosition.Right;
            // chartPitchRoll.LegendTextPaint = new SolidColorPaint(SKColors.White);
            // chartPitchRoll.LegendBackgroundPaint = new SolidColorPaint(SKColors.Transparent);

            Serilog.Log.Information("[StatsControl] Charts initialization skipped (LiveCharts disabled)");
        }

        private void UpdateTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (_isAutoUpdateEnabled)
            {
                AddToStatistik();
            }
        }

        private void AddToStatistik()
        {
            try
            {
                double time;
                float yaw, pitch, roll;

                // TEST MODE: Generate dummy data
                if (_testMode)
                {
                    var elapsed = DateTime.Now - _testStartTime;
                    time = elapsed.TotalSeconds;

                    yaw = (float)(Math.Sin(elapsed.TotalSeconds * 0.5) * 180);
                    pitch = (float)(Math.Sin(elapsed.TotalSeconds * 1.2) * 45);
                    roll = (float)(Math.Cos(elapsed.TotalSeconds * 0.8) * 45);
                }
                else
                {
                    // REAL MODE: Get data from MavLinkService telemetry
                    if (_mavLinkService == null || !_mavLinkService.IsConnected)
                    {
                        // Only log every 50 updates to reduce overhead
                        if (_updateCounter % 50 == 0)
                        {
                            Serilog.Log.Debug("[StatsControl] MavLinkService not connected");
                        }
                        return;
                    }

                    // Use latest telemetry data from event subscription
                    if (_latestTelemetry == null)
                    {
                        // Only log every 50 updates to reduce overhead
                        if (_updateCounter % 50 == 0)
                        {
                            Serilog.Log.Debug("[StatsControl] No telemetry data available yet");
                        }
                        return;
                    }

                    var elapsed = DateTime.Now - _testStartTime;
                    time = elapsed.TotalSeconds;
                    
                    // Get IMU data (already in degrees)
                    yaw = (float)_latestTelemetry.Yaw;
                    pitch = (float)_latestTelemetry.Pitch;
                    roll = (float)_latestTelemetry.Roll;
                }

                // Add data points
                _timeData.Add(time);
                _yawData.Add(yaw);
                _pitchData.Add(pitch);
                _rollData.Add(roll);

                // Keep data size manageable (300 points max)
                if (_timeData.Count > 300)
                {
                    _timeData.RemoveAt(0);
                    _yawData.RemoveAt(0);
                    _pitchData.RemoveAt(0);
                    _rollData.RemoveAt(0);
                }

                // Update chart X-axis limits only every 2 seconds to reduce overhead
                var now = DateTime.Now;
                if (_timeData.Count > 0 && time > 10 && (now - _lastAxisUpdate).TotalSeconds >= 2)
                {
                    _lastAxisUpdate = now;
                    var minTime = time - 10;
                    var maxTime = time;

                    // COMMENTED OUT: Chart controls not available in XAML
                    /*
                    if (chartYaw.XAxes != null && chartYaw.XAxes.Any())
                    {
                        chartYaw.XAxes.First().MinLimit = minTime;
                        chartYaw.XAxes.First().MaxLimit = maxTime;
                    }

                    if (chartPitchRoll.XAxes != null && chartPitchRoll.XAxes.Any())
                    {
                        chartPitchRoll.XAxes.First().MinLimit = minTime;
                        chartPitchRoll.XAxes.First().MaxLimit = maxTime;
                    }
                    */
                }

                // Increment counter and log every 50 data points to reduce overhead
                _updateCounter++;
                if (_updateCounter % 50 == 0)
                {
                    Serilog.Log.Debug("[StatsControl] Updates: {Count}, Time: {Time:F1}s, Yaw: {Yaw:F1}°, Pitch: {Pitch:F1}°, Roll: {Roll:F1}°", 
                        _updateCounter, time, yaw, pitch, roll);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[StatsControl] Error in AddToStatistik");
            }
        }

        private void StartAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            StartAutoUpdate();
            btn_start_stats.IsEnabled = false;
            btn_stop_stats.IsEnabled = true;
        }

        private void StopAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            _isAutoUpdateEnabled = false;
            _updateTimer?.Stop();

            btn_start_stats.IsEnabled = true;
            btn_stop_stats.IsEnabled = false;
        }

        private void ResetStats_Click(object sender, RoutedEventArgs e)
        {
            _timeData.Clear();
            _yawData.Clear();
            _pitchData.Clear();
            _rollData.Clear();
            _updateCounter = 0;
            _lastAxisUpdate = DateTime.MinValue;

            if (_testMode)
            {
                _testStartTime = DateTime.Now;
            }

            Serilog.Log.Information("[StatsControl] Statistics reset");
        }

        private async void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new FileSavePicker();
                
                // Get the window handle
                var window = App.MainWindow;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("CSV Files", new List<string>() { ".csv" });
                savePicker.SuggestedFileName = $"Statistics_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    var lines = new List<string> { "Time (s),Heading (deg),Pitch (deg),Roll (deg)" };

                    for (int i = 0; i < _timeData.Count; i++)
                    {
                        lines.Add($"{_timeData[i]:F2},{_yawData[i]:F2},{_pitchData[i]:F2},{_rollData[i]:F2}");
                    }

                    await FileIO.WriteLinesAsync(file, lines);

                    Serilog.Log.Information("[StatsControl] Export Success: {Count} records saved to {Path}", lines.Count - 1, file.Path);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[StatsControl] Export Error");
            }
        }

        private void StartAutoUpdate()
        {
            Serilog.Log.Information("[StatsControl] StartAutoUpdate called");
            _isAutoUpdateEnabled = true;
            _updateTimer?.Start();
        }

        private void StopAutoUpdate()
        {
            Serilog.Log.Information("[StatsControl] StopAutoUpdate called");
            _isAutoUpdateEnabled = false;
            _updateTimer?.Stop();
        }

        // Cleanup
        ~StatsControl()
        {
            if (_mavLinkService != null)
            {
                _mavLinkService.TelemetryReceived -= OnTelemetryReceived;
            }
        }
    }
}
