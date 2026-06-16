 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using OpenCvSharp;
using Windows.UI;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.ViewModels;
using HarvestmoonGCS.Helpers;
using HarvestmoonGCS.ViewModels;
using HarvestmoonGCS.Controls;
using HarvestmoonGCS.Services;
using XamlEllipse = Microsoft.UI.Xaml.Shapes.Ellipse;
using XamlLine = Microsoft.UI.Xaml.Shapes.Line;

namespace HarvestmoonGCS.Views;

public sealed partial class DashboardPage : Page
{
    private readonly FlightViewModel? _flightViewModel;
    private readonly MapViewModel? _mapViewModel;
    private readonly ICameraService? _cameraService;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private readonly IVideoRecorderService? _videoRecorderService;
    private readonly IOfflineMapService? _offlineMapService;
    private readonly IGeofenceService? _geofenceService;
    private readonly GeofenceMonitor? _geofenceMonitor;
    private readonly IncidentTimelineService? _timelineService;
    private readonly IMavLinkService? _mavLinkService;

    private TelemetryData? _telemetry;
    private byte[]? _lastFrameData;
    private bool _mapInitialized;
    private bool _mapViewModelSubscribed;
    private bool _cameraHandlersAttached;
    private bool _analysisRunning;
    private bool _gridOverlayDrawn;
    private bool _isPageActive;
    private DateTime _lastAnalysisTime = DateTime.MinValue;
    private double _centerLat = -6.9175;
    private double _centerLon = 107.6191;
    private DateTime _lastDashboardVehicleRender = DateTime.MinValue;
    private const int DashboardVehicleRenderIntervalMs = 200;

    // YOLO inference FPS counter
    private int _inferenceCount;
    private DateTime _lastFpsCalcTime = DateTime.UtcNow;
    private double _currentInferenceFps;

    // Throttling for telemetry UI updates.
    // MAVLink packets raise many PropertyChanged events per packet so we coalesce them
    // into at most one dashboard refresh every UiRefreshIntervalMs (~10 Hz).
    private const int UiRefreshIntervalMs = 100;
    private DateTime _lastUiRefresh = DateTime.MinValue;
    private bool _uiRefreshScheduled;

    // UI state
    private bool _isRecording;
    private bool _aiOn = true;
    private bool _showImuHud = true;
    private int _confThreshold = 25;
    private double _pitch;
    private double _roll;
    private double _yaw;
    private bool _missionTimerStarted;
    private readonly DispatcherTimer _imuTimer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private readonly DispatcherTimer _missionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _missionStart = DateTime.Now;
    private int _telemetryFrameCount;
    private int _lastDetectionCount;
    private double _lastConfidence;
    private string? _lastAlertSignature;
    private DateTime _lastGeofenceMonitorCheck = DateTime.MinValue;
    private string? _lastGeofenceMonitorEvent;
    private string _lastGeofenceMonitorSeverity = "info";
    private DateTime _lastYoloTimelineEvent = DateTime.MinValue;
    private HarvestFunctionalService.YoloBenchmarkResult? _lastBenchmarkResult;

    // Demo mode state
    private bool _isDemoRunning;
    private int _demoStep;
    private double _demoBattery = 95.0;
    private double _demoHeading = 90.0;  // tracks current bearing for smooth roll
    private const int DemoTicksPerSegment = 4; // 40 m at ~10 m/s ≈ 4 s per segment
    private readonly DispatcherTimer _demoTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    // 7 rows × 6 cols × 40 m spacing → 240 m × 200 m ≈ 4.8 ha (~5 ha survey area)
    private static readonly (double Lat, double Lon)[] DemoWaypoints = GenerateSurveyWaypoints(-6.8148, 107.6172, rows: 7, cols: 6, spacingMeters: 40);
    private static readonly string[] DemoClassificationEvents =
    {
        "healthy_crop (conf 0.93) · Zone 2-3",
        "stressed_crop (conf 0.81) · Zone 4-1",
        "healthy_crop (conf 0.96) · Zone 1-2",
        "drought_stress (conf 0.74) · Zone 3-4",
        "disease_stress_vegetation (conf 0.87) · Zone 2-2",
        "healthy_crop (conf 0.91) · Zone 5-1",
    };

    public DashboardPage()
    {
        this.InitializeComponent();

        _flightViewModel = App.Current.Services.GetService<FlightViewModel>();
        _mapViewModel = App.Current.Services.GetService<MapViewModel>();
        _cameraService = App.Current.Services.GetService<ICameraService>();
        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();
        _videoRecorderService = App.Current.Services.GetService<IVideoRecorderService>();
        _offlineMapService = App.Current.Services.GetService<IOfflineMapService>();
        _geofenceService = App.Current.Services.GetService<IGeofenceService>();
        _geofenceMonitor = App.Current.Services.GetService<GeofenceMonitor>();
        _timelineService = App.Current.Services.GetService<IncidentTimelineService>();
        _mavLinkService  = App.Current.Services.GetService<IMavLinkService>();

        _imuTimer.Tick += OnImuTick;
        _missionTimer.Tick += OnMissionTick;

        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
        SizeChanged += DashboardPage_SizeChanged;

        if (_timelineService != null)
        {
            _timelineService.TimelineChanged += OnTimelineChanged;
        }
    }

    public void OnPageActivated()
    {
        _isPageActive = true;
        DashboardMapControl?.InvalidateArrange();
        AttachCameraHandlers();
        ResumeVideoRendering();
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        SubscribeToTelemetry();
        SubscribeToGeofenceMonitor();
        AttachCameraHandlers();
        SyncYoloStateFromService();
        if (AlertsStack.Children.Count == 0) UpdateAlertCenter(_flightViewModel?.Telemetry, _flightViewModel?.IsConnected == true);
        if (SummaryHealthyCount.Text is "0" or "0%") SeedSummary();
        RebuildGridOverlay();
        ApplyLayerVisibility();
        RefreshDashboard();
        RenderIncidentTimeline();
        if (!_missionTimerStarted)
        {
            _missionStart = DateTime.Now;
            MissionIdText.Text = $"MH-{DateTime.Now:yyyyMMdd-HHmm}";
            MissionStartedText.Text = DateTime.Now.ToString("HH:mm:ss");
            _missionTimerStarted = true;
        }
        _imuTimer.Start();
        _missionTimer.Start();

        // Start camera only if it's not already running globally.
        // On re-navigate back to Dashboard, just resume rendering the existing stream.
        if (_cameraService != null && _cameraService.IsStreaming)
        {
            ResumeVideoRendering();
        }
        else
        {
            _ = StartDashboardCameraAsync();
        }
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
        // Keep camera service running globally so user can switch menus without tearing down the stream.
        // Only stop this page's timers and telemetry binding.
        UnsubscribeFromTelemetry();
        UnsubscribeFromGeofenceMonitor();
        // Leave camera handlers attached: events are idempotent and FrameReceived updates the cached
        // VideoStreamControl which is re-used when the page is reactivated from the page cache.
        // Detaching + reattaching creates a gap where frames are dropped and the control appears frozen.
        _imuTimer.Stop();
        _missionTimer.Stop();
    }

    private void OnTimelineChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RenderIncidentTimeline);
    }

    private void ResumeVideoRendering()
    {
        // When returning to the dashboard, the VideoStreamControl keeps its _currentFrame in memory
        // because we never called ClearFrame(). But the overlay state may have drifted. Force the
        // canvas visible and invalidate so the cached frame paints immediately instead of showing
        // a blank panel until the next Python frame arrives.
        try
        {
            DashboardVideoStream?.HideOverlay();
            DashboardVideoStream?.EnsureStreamingVisible();
        }
        catch
        {
        }
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildGridOverlay();
    }

    // ================ TELEMETRY ================

    private void SubscribeToTelemetry()
    {
        if (_flightViewModel == null) return;
        _flightViewModel.PropertyChanged -= OnFlightViewModelPropertyChanged;
        _flightViewModel.PropertyChanged += OnFlightViewModelPropertyChanged;
        RewireTelemetry(_flightViewModel.Telemetry);
    }

    private void UnsubscribeFromTelemetry()
    {
        if (_flightViewModel == null) return;
        _flightViewModel.PropertyChanged -= OnFlightViewModelPropertyChanged;
        if (_telemetry != null)
        {
            _telemetry.PropertyChanged -= OnTelemetryPropertyChanged;
        }
        _telemetry = null;
    }

    private void RewireTelemetry(TelemetryData? nextTelemetry)
    {
        if (ReferenceEquals(_telemetry, nextTelemetry)) return;
        if (_telemetry != null) _telemetry.PropertyChanged -= OnTelemetryPropertyChanged;
        _telemetry = nextTelemetry;
        if (_telemetry != null) _telemetry.PropertyChanged += OnTelemetryPropertyChanged;
    }

    private void OnFlightViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlightViewModel.TelemetryData) ||
            e.PropertyName == nameof(FlightViewModel.Telemetry) ||
            e.PropertyName == nameof(FlightViewModel.IsConnected))
        {
            RewireTelemetry(_flightViewModel?.Telemetry);
            ScheduleDashboardRefresh();
        }
    }

    private void OnTelemetryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _telemetryFrameCount++;
        ScheduleDashboardRefresh();
    }

    /// <summary>
    /// Coalesces many telemetry PropertyChanged events into at most one UI refresh per
    /// <see cref="UiRefreshIntervalMs"/>. Without this the dashboard re-renders 30+ times
    /// per MAVLink packet which saturates the UI thread.
    /// </summary>
    private void ScheduleDashboardRefresh()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastUiRefresh).TotalMilliseconds >= UiRefreshIntervalMs)
        {
            _lastUiRefresh = now;
            DispatcherQueue.TryEnqueue(RefreshDashboard);
            return;
        }
        if (_uiRefreshScheduled) return;
        _uiRefreshScheduled = true;
        var delay = Math.Max(1, UiRefreshIntervalMs - (int)(now - _lastUiRefresh).TotalMilliseconds);
        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _uiRefreshScheduled = false;
                _lastUiRefresh = DateTime.UtcNow;
                RefreshDashboard();
            });
        });
    }

    // Cached last-rendered values to skip redundant XAML text updates.
    private string? _lastAltitudeText;
    private string? _lastSpeedText;
    private string? _lastHeadingText;
    private string? _lastGpsText;
    private string? _lastBatteryText;
    private string? _lastModeText;
    private string? _lastAltSpdHudText;
    private string? _lastHdgModeText;
    private string? _lastHeadingPillText;
    private string? _lastFpsHudText;
    private string? _lastGpsHudText;
    private string? _lastMissionProgressText;
    private string? _lastWaypointProgressText;
    private string? _lastMavLinkStatusText;
    private bool? _lastConnected;
    private double? _lastMissionProgressValue;

    private void RefreshDashboard()
    {
        if (_flightViewModel == null) return;

        var telemetry = _flightViewModel.Telemetry;
        var connected = _flightViewModel.IsConnected;

        var altitude = telemetry?.Altitude ?? 0.0;
        var speed = telemetry != null
            ? (telemetry.Speed > 0 ? telemetry.Speed : telemetry.GroundSpeed)
            : 0.0;

        var battery = telemetry?.BatteryPercent is > 0
            ? telemetry.BatteryPercent
            : telemetry?.BatteryRemaining ?? 0;
        battery = Math.Clamp(battery, 0, 100);
        var voltage = telemetry?.BatteryVoltage ?? 0;

        var heading = telemetry?.Heading ?? 0;
        var flightMode = (telemetry?.FlightMode.ToString() ?? (connected ? "AUTO" : "STANDBY")).ToUpperInvariant();
        var satCount = telemetry?.SatelliteCount ?? 0;
        var hdop = telemetry?.HDOP ?? 0;

        SetTextIfChanged(MavLinkStatusText, connected ? "UDP 14550 · Online" : "Disconnected", ref _lastMavLinkStatusText);
        if (_lastConnected != connected)
        {
            MavLinkStatusPill.Style = (Style)(connected ? Resources["PillSuccessStyle"] : Resources["PillCriticalStyle"]);
            _lastConnected = connected;
        }

        SetTextIfChanged(TelemetryAltitudeText, $"{altitude:F1} m", ref _lastAltitudeText);
        SetTextIfChanged(TelemetrySpeedText, $"{speed:F1} m/s", ref _lastSpeedText);
        SetTextIfChanged(TelemetryHeadingText, $"{heading:F0}°", ref _lastHeadingText);
        SetTextIfChanged(TelemetryGpsText, satCount > 0 ? $"{satCount} · HDOP {hdop:F1}" : "No GPS", ref _lastGpsText);
        SetTextIfChanged(TelemetryBatteryText, voltage > 0 ? $"{battery:F0}% · {voltage:F1}V" : $"{battery:F0}%", ref _lastBatteryText);
        SetTextIfChanged(TelemetryModeText, flightMode, ref _lastModeText);

        SetTextIfChanged(AltSpdHudText, $"ALT {altitude:F0} m · SPD {speed:F1} m/s", ref _lastAltSpdHudText);
        SetTextIfChanged(HdgModeText, $"HDG {heading:F0}° · MODE {flightMode}", ref _lastHdgModeText);
        SetTextIfChanged(HeadingPillText, $"HDG {heading:F0}°", ref _lastHeadingPillText);
        SetTextIfChanged(FpsHudText, _aiOn ? $"YOLOv8 · {(connected ? "active" : "standby")}" : "YOLO paused", ref _lastFpsHudText);

        var missionProgress = MissionProgressCalculator.Calculate(
            telemetry,
            _mapViewModel?.Waypoints,
            _mapViewModel?.WaypointRadius ?? 2.0);
        var progress = connected ? missionProgress.ProgressPercent : 0.0;
        if (_lastMissionProgressValue != progress)
        {
            MissionProgressBar.Value = progress;
            _lastMissionProgressValue = progress;
        }
        SetTextIfChanged(MissionProgressText, connected ? $"{progress:F0}%" : "0%", ref _lastMissionProgressText);
        SetTextIfChanged(
            WaypointProgressText,
            missionProgress.TotalWaypoints > 0
                ? $"{(connected ? missionProgress.CurrentWaypoint : 0)}/{missionProgress.TotalWaypoints}"
                : "0/0",
            ref _lastWaypointProgressText);

        // Frames counter updates roughly once per telemetry packet, OK to always write since it changes.
        MissionFramesText.Text = $"{_telemetryFrameCount:N0} frames";

        UpdateMap(telemetry, connected);

        var gpsHud = telemetry != null && IsValidCoordinate(telemetry.Latitude, telemetry.Longitude)
            ? $"{telemetry.Latitude:F5}, {telemetry.Longitude:F5}"
            : $"{_centerLat:F5}, {_centerLon:F5}";
        SetTextIfChanged(GpsHudText, gpsHud, ref _lastGpsHudText);
        UpdateAlertCenter(telemetry, connected);
        UpdateOfflineAndReadiness(telemetry, connected);
        _ = CheckGeofenceMonitorAsync(telemetry, connected);
    }

    private void SubscribeToGeofenceMonitor()
    {
        if (_geofenceMonitor == null)
        {
            return;
        }

        _geofenceMonitor.GeofenceViolated -= OnGeofenceViolated;
        _geofenceMonitor.GeofenceRestored -= OnGeofenceRestored;
        _geofenceMonitor.GeofenceViolated += OnGeofenceViolated;
        _geofenceMonitor.GeofenceRestored += OnGeofenceRestored;
    }

    private void UnsubscribeFromGeofenceMonitor()
    {
        if (_geofenceMonitor == null)
        {
            return;
        }

        _geofenceMonitor.GeofenceViolated -= OnGeofenceViolated;
        _geofenceMonitor.GeofenceRestored -= OnGeofenceRestored;
    }

    private void OnGeofenceViolated(object? sender, GeofenceViolationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _lastGeofenceMonitorEvent = e.Message;
            _lastGeofenceMonitorSeverity = "critical";
            _lastAlertSignature = null;
            UpdateAlertCenter(_flightViewModel?.Telemetry, _flightViewModel?.IsConnected == true);
        });
    }

    private void OnGeofenceRestored(object? sender, GeofenceViolationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _lastGeofenceMonitorEvent = e.Message;
            _lastGeofenceMonitorSeverity = "info";
            _lastAlertSignature = null;
            UpdateAlertCenter(_flightViewModel?.Telemetry, _flightViewModel?.IsConnected == true);
        });
    }

    private async Task CheckGeofenceMonitorAsync(TelemetryData? telemetry, bool connected)
    {
        if (_geofenceMonitor == null || !connected || telemetry == null ||
            !IsValidCoordinate(telemetry.Latitude, telemetry.Longitude))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastGeofenceMonitorCheck).TotalMilliseconds < 1000)
        {
            return;
        }

        _lastGeofenceMonitorCheck = now;
        await _geofenceMonitor.CheckGeofenceViolationAsync(new GeoCoordinate(
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.Altitude));
    }

    private static void SetTextIfChanged(TextBlock target, string value, ref string? cache)
    {
        if (cache == value) return;
        target.Text = value;
        cache = value;
    }

    private void UpdateMap(TelemetryData? telemetry, bool connected)
    {
        if (telemetry != null && IsValidCoordinate(telemetry.Latitude, telemetry.Longitude))
        {
            _centerLat = telemetry.Latitude;
            _centerLon = telemetry.Longitude;
        }

        if (!_mapInitialized && DashboardMapControl != null)
        {
            InitializeDashboardMap();
            _mapInitialized = true;
        }

        if (connected && DashboardMapControl != null)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastDashboardVehicleRender).TotalMilliseconds >= DashboardVehicleRenderIntervalMs)
            {
                DashboardMapControl.UpdateVehiclePosition(_centerLat, _centerLon);
                _lastDashboardVehicleRender = now;
            }
        }
    }

    /// <summary>
    /// Replicates the map setup the dedicated Map menu uses so the dashboard's embedded map
    /// looks and behaves the same way: identical tile provider, offline service binding,
    /// waypoint list, and geofence rendering sourced from <see cref="MapViewModel"/>.
    /// </summary>
    private void InitializeDashboardMap()
    {
        if (DashboardMapControl == null) return;

        // Sync tile provider from MapViewModel (mirrors whatever user selected in Map page)
        SyncTileProviderFromViewModel();

        try
        {
            if (_offlineMapService != null)
            {
                DashboardMapControl.SetOfflineMapService(_offlineMapService);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[DashboardPage] Failed to bind offline map service");
        }

        // Use same center as Map page (-7.2754, 112.7947 zoom 15) unless we have live telemetry
        var initialLat = IsValidCoordinate(_centerLat, _centerLon) && (_centerLat != -6.9175) ? _centerLat : -7.2754;
        var initialLon = IsValidCoordinate(_centerLat, _centerLon) && (_centerLon != 107.6191) ? _centerLon : 112.7947;
        DashboardMapControl.SetCenter(initialLat, initialLon, 15);

        // Sync follow-vehicle state
        DashboardMapControl.SetFollowVehicle(_mapViewModel?.IsFollowing ?? false);

        SyncWaypointsFromViewModel();
        SyncGeofenceFromViewModel();
        SubscribeToMapViewModel();
    }

    private void SubscribeToMapViewModel()
    {
        if (_mapViewModel == null || _mapViewModelSubscribed) return;
        _mapViewModel.PropertyChanged += OnMapViewModelPropertyChanged;
        _mapViewModel.Waypoints.CollectionChanged += OnMapViewModelWaypointsChanged;
        _mapViewModelSubscribed = true;
    }

    private void UnsubscribeFromMapViewModel()
    {
        if (_mapViewModel == null || !_mapViewModelSubscribed) return;
        _mapViewModel.PropertyChanged -= OnMapViewModelPropertyChanged;
        _mapViewModel.Waypoints.CollectionChanged -= OnMapViewModelWaypointsChanged;
        _mapViewModelSubscribed = false;
    }

    private void OnMapViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MapViewModel.VehiclePosition):
                var pos = _mapViewModel?.VehiclePosition;
                if (pos != null && IsValidCoordinate(pos.Latitude, pos.Longitude))
                {
                    _centerLat = pos.Latitude;
                    _centerLon = pos.Longitude;
                    var headingDeg = _telemetry?.Heading ?? 0;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DashboardMapControl?.UpdateVehiclePosition(pos.Latitude, pos.Longitude);
                        HeadingPillText.Text = $"HDG {headingDeg:F0}°";
                    });
                }
                break;
            case nameof(MapViewModel.IsGeofenceActive):
            case nameof(MapViewModel.GeofenceCenterLat):
            case nameof(MapViewModel.GeofenceCenterLon):
            case nameof(MapViewModel.GeofenceRadius):
            case nameof(MapViewModel.GeofenceType):
                DispatcherQueue.TryEnqueue(SyncGeofenceFromViewModel);
                break;
            case nameof(MapViewModel.MapProvider):
                DispatcherQueue.TryEnqueue(SyncTileProviderFromViewModel);
                break;
            case nameof(MapViewModel.IsFollowing):
                DispatcherQueue.TryEnqueue(() =>
                {
                    DashboardMapControl?.SetFollowVehicle(_mapViewModel?.IsFollowing ?? false);
                });
                break;
        }
    }

    private void OnMapViewModelWaypointsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(SyncWaypointsFromViewModel);
    }

    private void SyncWaypointsFromViewModel()
    {
        if (DashboardMapControl == null) return;
        DashboardMapControl.ClearWaypoints();
        if (_mapViewModel == null || _mapViewModel.Waypoints.Count == 0)
        {
            // No waypoints — show empty map just like Map menu does.
            return;
        }
        foreach (var wp in _mapViewModel.Waypoints.OrderBy(w => w.Sequence))
        {
            DashboardMapControl.AddWaypointMarker(wp.Sequence, wp.Latitude, wp.Longitude, wp.Altitude, wp.Command.ToString());
        }
    }

    private void SyncTileProviderFromViewModel()
    {
        if (DashboardMapControl == null || _mapViewModel == null) return;
        var provider = _mapViewModel.MapProvider?.ToLowerInvariant() switch
        {
            "arcgisimagery" => SkiaMapControl.MapTileProvider.ArcGISImagery,
            "arcgistopographic" => SkiaMapControl.MapTileProvider.ArcGISTopographic,
            "arcgisstreetmap" => SkiaMapControl.MapTileProvider.ArcGISStreetMap,
            "googlemap" => SkiaMapControl.MapTileProvider.GoogleMap,
            "googlesatellite" => SkiaMapControl.MapTileProvider.GoogleSatellite,
            "googleterrain" => SkiaMapControl.MapTileProvider.GoogleTerrain,
            "googlehybrid" => SkiaMapControl.MapTileProvider.GoogleHybrid,
            "openstreetmap" => SkiaMapControl.MapTileProvider.OpenStreetMap,
            _ => SkiaMapControl.MapTileProvider.ArcGISTopographic
        };
        DashboardMapControl.SetTileProvider(provider);
    }

    private void SyncGeofenceFromViewModel()
    {
        if (DashboardMapControl == null) return;
        if (_mapViewModel == null || !_mapViewModel.IsGeofenceActive)
        {
            // No geofence active — clear it, same as Map menu.
            DashboardMapControl.SetGeofence(true, 0, 0, 0);
            return;
        }

        if (_mapViewModel.GeofenceType == HarvestmoonGCS.Core.Models.GeofenceType.Circular)
        {
            DashboardMapControl.SetGeofence(true,
                _mapViewModel.GeofenceCenterLat,
                _mapViewModel.GeofenceCenterLon,
                _mapViewModel.GeofenceRadius);
        }
        else if (_mapViewModel.GeofenceVertices.Count >= 3)
        {
            var vertices = _mapViewModel.GeofenceVertices.Select(v => (v.Lat, v.Lon)).ToList();
            DashboardMapControl.SetGeofence(false, 0, 0, 0, vertices);
        }
    }

    // ================ CAMERA ================

    private void AttachCameraHandlers()
    {
        if (_cameraService == null || _cameraHandlersAttached) return;
        _cameraService.FrameReceived += OnDashboardFrameReceived;
        _cameraService.StreamingStatusChanged += OnDashboardCameraStreamingChanged;
        _cameraService.ConnectionError += OnDashboardCameraError;
        _cameraHandlersAttached = true;
    }

    private void DetachCameraHandlers()
    {
        if (_cameraService == null || !_cameraHandlersAttached) return;
        _cameraService.FrameReceived -= OnDashboardFrameReceived;
        _cameraService.StreamingStatusChanged -= OnDashboardCameraStreamingChanged;
        _cameraService.ConnectionError -= OnDashboardCameraError;
        _cameraHandlersAttached = false;
    }

    /// <summary>
    /// Path to derr.mp4 at project root.
    /// </summary>
    private static string ResolveDerpVideoPath()
    {
        // Try several locations where derr.mp4 might exist
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "derr.mp4"),
            Path.Combine(Directory.GetCurrentDirectory(), "derr.mp4"),
            Path.Combine(AppContext.BaseDirectory, "derr.mp4"),
            "/home/fawwazfa/Program/Harvestmoon/derr.mp4"
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return candidates[0]; // fallback even if doesn't exist
    }

    private async Task StartDashboardCameraAsync()
    {
        if (_cameraService == null)
        {
            DashboardVideoStream?.ShowStatus("Camera service tidak tersedia", false);
            return;
        }

        // If already streaming (e.g. user came back from another menu), just resume the visual.
        if (_cameraService.IsStreaming)
        {
            ResumeVideoRendering();
            return;
        }

        // Do NOT auto-start camera/video on dashboard load.
        // User must click "Start Demo" to play derr.mp4 with YOLO detections.
        DashboardVideoStream?.ShowStatus("Kamera standby · hubungkan UAV untuk mulai survey", false);
    }

    // Throttle UI frame rendering so very high-FPS camera sources don't saturate the UI thread.
    private const int VideoFrameIntervalMs = 16; // ~60 FPS / lower preview latency
    private DateTime _lastVideoFrameRender = DateTime.MinValue;

    private void OnDashboardFrameReceived(object? sender, byte[] frameData)
    {
        // Skip all work when the page isn't visible. The camera keeps streaming globally
        // so frames will resume when the user returns to the dashboard.
        if (!_isPageActive) return;

        // VideoStreamControl.UpdateFrame already marshals to the UI thread internally;
        // avoid a redundant DispatcherQueue.TryEnqueue hop here.
        var now = DateTime.UtcNow;
        if ((now - _lastVideoFrameRender).TotalMilliseconds < VideoFrameIntervalMs)
        {
            return;
        }
        _lastVideoFrameRender = now;
        _lastFrameData = frameData;

        DashboardVideoStream?.UpdateFrame(frameData);
        if (_videoRecorderService?.IsRecording == true)
        {
            _videoRecorderService.WriteFrame(frameData);
        }

        if (_aiOn && _harvestFunctionalService?.IsYoloOptionEnabled == true && !_analysisRunning)
        {
            _lastAnalysisTime = DateTime.Now;
            _ = AnalyzeDashboardFrameAsync(frameData);
        }
    }

    private async Task AnalyzeDashboardFrameAsync(byte[] frameData)
    {
        _analysisRunning = true;
        try
        {
            // Fast path: decode+detect directly on the frame bytes, no disk I/O.
            var boxes = await _harvestFunctionalService!.DetectInFrameAsync(frameData);

            DispatcherQueue.TryEnqueue(() =>
            {
                // If user turned off YOLO while inference was running, discard results.
                if (!_aiOn)
                {
                    return;
                }

                _lastDetectionCount = boxes.Count;
                if (boxes.Count > 0 && (DateTime.UtcNow - _lastYoloTimelineEvent).TotalSeconds >= 5)
                {
                    _lastYoloTimelineEvent = DateTime.UtcNow;
                    _timelineService?.Add("yolo", $"YOLO detection: {boxes.Count} object(s), avg confidence {boxes.Average(b => b.Confidence) * 100:F0}%", "info");
                    _ = _harvestFunctionalService?.SaveLatestYoloScreenshotAsync(frameData);
                }
                var avgConf = boxes.Count > 0 ? boxes.Average(b => (double)b.Confidence) : 0;
                _lastConfidence = avgConf;

                DetectionCountText.Text = $"{boxes.Count} det";
                DetectionsListTitle.Text = $"Live Detections ({boxes.Count})";
                ConfAvgText.Text = $"Conf avg {avgConf * 100:F0}%";
                SummaryConfidenceText.Text = $"{avgConf * 100:F0}%";
                MissionDetectionsText.Text = boxes.Count.ToString();

                // Calculate real inference FPS
                _inferenceCount++;
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastFpsCalcTime).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    _currentInferenceFps = _inferenceCount / elapsed;
                    _inferenceCount = 0;
                    _lastFpsCalcTime = now;
                }
                FpsHudText.Text = _aiOn
                    ? $"YOLOv8 · {_currentInferenceFps:F0} FPS"
                    : "YOLO paused";

                // Update top bar AI FPS indicator
                UpdateTopBarAiFps(_currentInferenceFps);

                DashboardVideoStream?.SetDetectionOverlays(boxes
                    .Where(b => b.Confidence * 100 >= _confThreshold)
                    .Select(box => new VideoDetectionOverlay
                    {
                        Label = box.ClassName,
                        Confidence = box.Confidence,
                        X = box.X,
                        Y = box.Y,
                        Width = box.Width,
                        Height = box.Height
                    }));

                if (!_isDemoRunning)
                {
                    RenderDetectionListFromBoxes(boxes);
                    UpdateSummaryFromDetections(boxes);
                }
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[DashboardPage] Analyze error");
        }
        finally
        {
            _analysisRunning = false;
        }
    }

    private void RenderDetectionListFromBoxes(List<HarvestFunctionalService.HarvestDetectionBox> boxes)
    {
        DetectionsListStack.Children.Clear();
        var filtered = boxes
            .Where(b => b.Confidence * 100 >= _confThreshold)
            .OrderByDescending(b => b.Confidence)
            .Take(20)
            .ToList();

        if (filtered.Count == 0)
        {
            DetectionsListStack.Children.Add(new TextBlock
            {
                Text = "Tidak ada deteksi di atas threshold.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            return;
        }

        foreach (var det in filtered)
        {
            DetectionsListStack.Children.Add(BuildCropDetectionRow(det.ClassName, det.Confidence));
        }
    }

    private void RenderDetectionList(HarvestFunctionalService.HarvestAnalysisResult result)
    {
        DetectionsListStack.Children.Clear();
        var filtered = result.DetectionBoxes
            .Where(b => b.Confidence * 100 >= _confThreshold)
            .OrderByDescending(b => b.Confidence)
            .Take(20)
            .ToList();

        if (filtered.Count == 0)
        {
            DetectionsListStack.Children.Add(new TextBlock
            {
                Text = "Tidak ada deteksi di atas threshold.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            return;
        }

        foreach (var det in filtered)
        {
            DetectionsListStack.Children.Add(BuildCropDetectionRow(det.ClassName, det.Confidence));
        }
    }

    private void UpdateSummaryAndNdvi(HarvestFunctionalService.HarvestAnalysisResult result)
    {
        var healthyPct  = result.HealthyPercentage;
        var stressedPct = result.StressedPercentage;
        var droughtPct  = result.DroughtPercentage;
        var barePct     = result.BareSoilPercentage;
        var diseasePct  = Math.Max(0, 100 - healthyPct - stressedPct - droughtPct - barePct);

        SummaryHealthyCount.Text  = $"{healthyPct:F0}%";
        SummaryStressCount.Text   = $"{stressedPct:F0}%";
        SummaryDiseaseCount.Text  = $"{diseasePct:F0}%";
        SummaryDroughtCount.Text  = $"{droughtPct:F0}%";
        SummaryBaresoilCount.Text = $"{barePct:F0}%";

        // NDVI bars
        var moderatePct = Math.Max(0, 100 - healthyPct - stressedPct - droughtPct - barePct);
        var criticalPct = droughtPct + barePct;

        NdviHealthyBar.Value = healthyPct;
        NdviHealthyText.Text = $"{healthyPct:F0}%";
        NdviModerateBar.Value = moderatePct;
        NdviModerateText.Text = $"{moderatePct:F0}%";
        NdviStressedBar.Value = stressedPct;
        NdviStressedText.Text = $"{stressedPct:F0}%";
        NdviCriticalBar.Value = criticalPct;
        NdviCriticalText.Text = $"{criticalPct:F0}%";
        NdviAvgText.Text = (healthyPct / 100.0).ToString("F2");
        NdviCoverageText.Text = $"{result.TotalZones * 0.05:F1} ha";

        // Priority pill
        var priority = diseasePct > 5 ? "High" : stressedPct > 15 || droughtPct > 10 ? "Medium" : "Low";
        SummaryPriorityText.Text = priority;
        SummaryPriorityPill.Style = (Style)(priority switch
        {
            "High" => Resources["PillCriticalStyle"],
            "Medium" => Resources["PillWarningStyle"],
            _ => Resources["PillSuccessStyle"]
        });
        SummaryPriorityText.Foreground = new SolidColorBrush(priority switch
        {
            "High" => Color.FromArgb(255, 185, 28, 28),
            "Medium" => Color.FromArgb(255, 178, 107, 0),
            _ => Color.FromArgb(255, 6, 95, 70)
        });
    }

    private static Color ColorForLabel(string label)
    {
        var lower = label.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        if (lower.Contains("disease")) return Color.FromArgb(255, 211, 47, 47);   // red
        if (lower.Contains("drought")) return Color.FromArgb(255, 230, 100, 20);  // orange
        if (lower.Contains("stressed") || lower.Contains("stress_crop")) return Color.FromArgb(255, 234, 179, 8); // yellow
        if (lower.Contains("bare"))    return Color.FromArgb(255, 71, 71, 71);    // dark gray
        return Color.FromArgb(255, 34, 139, 34);                                   // green (healthy)
    }

    private static (string Emoji, string Label, string Description) CropClassInfo(string className)
    {
        var lower = className.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        if (lower.Contains("disease"))  return ("🔴", "disease_stress_vegetation", "Terserang penyakit");
        if (lower.Contains("drought"))  return ("🟠", "drought_stress",            "Stress kekeringan");
        if (lower.Contains("stressed") || (lower.Contains("stress") && !lower.Contains("disease") && !lower.Contains("drought")))
                                        return ("🟡", "stressed_crop",             "Tanaman stress");
        if (lower.Contains("bare"))     return ("⚫", "bare_soil",                 "Tanah kosong");
        return                                 ("🟢", "healthy_crop",              "Tanaman sehat");
    }

    private Grid BuildCropDetectionRow(string className, double confidence)
    {
        var (emoji, label, desc) = CropClassInfo(className);
        var color = ColorForLabel(className);

        var row = new Grid { Padding = new Thickness(6, 5, 6, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Emoji + color dot composite
        var emojiBlock = new TextBlock
        {
            Text = emoji,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(emojiBlock, 0);
        row.Children.Add(emojiBlock);

        // Label + description stacked
        var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labelStack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color),
        });
        labelStack.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139)),
        });
        Grid.SetColumn(labelStack, 1);
        row.Children.Add(labelStack);

        // Confidence
        var confBlock = new TextBlock
        {
            Text = $"{confidence * 100:F0}%",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(confBlock, 2);
        row.Children.Add(confBlock);

        return row;
    }

    /// <summary>
    /// Updates Analysis Summary and Vegetation Health panels from actual YOLO detections.
    /// Maps COCO class names to agriculture categories for display.
    /// </summary>
    private void UpdateSummaryFromDetections(List<HarvestmoonGCS.Services.HarvestFunctionalService.HarvestDetectionBox> boxes)
    {
        if (boxes.Count == 0)
        {
            SummaryHealthyCount.Text  = "0%";
            SummaryStressCount.Text   = "0%";
            SummaryDiseaseCount.Text  = "0%";
            SummaryDroughtCount.Text  = "0%";
            SummaryBaresoilCount.Text = "0%";
            SummaryPriorityText.Text = "Standby";
            return;
        }

        int disease = 0, stressed = 0, drought = 0, bare = 0, healthy = 0;
        foreach (var box in boxes.Where(b => b.Confidence * 100 >= _confThreshold))
        {
            var cls = box.ClassName.ToLowerInvariant();
            if (cls.Contains("disease") || cls.Contains("pest") || cls.Contains("bug"))
                disease++;
            else if (cls.Contains("drought") || cls.Contains("dry") || cls.Contains("irrig"))
                drought++;
            else if (cls.Contains("bare") || cls.Contains("soil"))
                bare++;
            else if (cls.Contains("stress"))
                stressed++;
            else
                healthy++;
        }

        int total = Math.Max(1, healthy + stressed + disease + drought + bare);
        SummaryHealthyCount.Text  = $"{healthy  * 100 / total}%";
        SummaryStressCount.Text   = $"{stressed * 100 / total}%";
        SummaryDiseaseCount.Text  = $"{disease  * 100 / total}%";
        SummaryDroughtCount.Text  = $"{drought  * 100 / total}%";
        SummaryBaresoilCount.Text = $"{bare     * 100 / total}%";

        // Priority based on detections
        var priority = disease > 0 ? "High" : stressed > 0 || drought > 0 ? "Medium" : "Low";
        SummaryPriorityText.Text = priority;
        SummaryPriorityPill.Style = (Style)(priority switch
        {
            "High" => Resources["PillCriticalStyle"],
            "Medium" => Resources["PillWarningStyle"],
            _ => Resources["PillSuccessStyle"]
        });
        SummaryPriorityText.Foreground = new SolidColorBrush(priority switch
        {
            "High" => Color.FromArgb(255, 185, 28, 28),
            "Medium" => Color.FromArgb(255, 178, 107, 0),
            _ => Color.FromArgb(255, 6, 95, 70)
        });

        // Vegetation Health (NDVI) — derive from detection distribution
        double healthyPct2  = healthy  * 100.0 / total;
        double stressedPct2 = stressed * 100.0 / total;
        double criticalPct2 = (disease + drought) * 100.0 / total;
        double moderatePct2 = bare     * 100.0 / total;

        NdviHealthyBar.Value = healthyPct2;
        NdviHealthyText.Text = $"{healthyPct2:F0}%";
        NdviModerateBar.Value = moderatePct2;
        NdviModerateText.Text = $"{moderatePct2:F0}%";
        NdviStressedBar.Value = stressedPct2;
        NdviStressedText.Text = $"{stressedPct2:F0}%";
        NdviCriticalBar.Value = criticalPct2;
        NdviCriticalText.Text = $"{criticalPct2:F0}%";

        double avgNdvi = (healthyPct2 * 0.8 + moderatePct2 * 0.5 + stressedPct2 * 0.3 + criticalPct2 * 0.1) / 100.0;
        NdviAvgText.Text = avgNdvi.ToString("F2");
        NdviCoverageText.Text = $"{total * 0.1:F1} ha";
    }

    private void OnDashboardCameraStreamingChanged(object? sender, bool isStreaming)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isStreaming)
            {
                // Force the control to refresh so the next frame is drawn immediately.
                try { DashboardVideoStream?.HideOverlay(); } catch { }
            }
            else
            {
                // Only show a status when we genuinely lost the stream, not when another page is drawing.
                DashboardVideoStream?.ShowStatus("Camera stopped", false);
            }
        });
    }

    private void OnDashboardCameraError(object? sender, string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DashboardVideoStream?.ShowStatus(error, false);
        });
    }

    // ================ OVERLAY / AI TOGGLES ================

    private void SyncYoloStateFromService()
    {
        if (_harvestFunctionalService != null)
        {
            _aiOn = _harvestFunctionalService.IsYoloOptionEnabled;
            YoloToggleSwitch.IsOn = _aiOn;

            // Subscribe to external toggle changes (e.g. sidebar Yolo toggle)
            _harvestFunctionalService.YoloOptionChanged -= OnServiceYoloOptionChanged;
            _harvestFunctionalService.YoloOptionChanged += OnServiceYoloOptionChanged;
        }
    }

    private void OnServiceYoloOptionChanged(object? sender, bool enabled)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _aiOn = enabled;
            YoloToggleSwitch.IsOn = enabled;
            AiStatusText.Text = enabled ? "AI ON" : "AI Paused";
            AiStatusPill.Style = (Style)(enabled ? Resources["PillSuccessStyle"] : Resources["PillCriticalStyle"]);
            PauseAiLabel.Text = enabled ? "Pause AI" : "Resume AI";

            if (!enabled)
            {
                DashboardVideoStream?.SetDetectionOverlays(Array.Empty<VideoDetectionOverlay>());
                DetectionCountText.Text = "0 det";
                DetectionsListTitle.Text = "Live Detections (0)";
                ConfAvgText.Text = "Conf avg 0%";
                FpsHudText.Text = "YOLO paused";
                DetectionsListStack.Children.Clear();
                DetectionsListStack.Children.Add(new TextBlock
                {
                    Text = "YOLO dimatikan.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
                });
            }
        });
    }

    private void YoloToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _aiOn = YoloToggleSwitch.IsOn;
        _harvestFunctionalService?.SetYoloOptionEnabled(_aiOn);
        AiStatusText.Text = _aiOn ? "AI ON" : "AI Paused";
        AiStatusPill.Style = (Style)(_aiOn ? Resources["PillSuccessStyle"] : Resources["PillCriticalStyle"]);
        PauseAiLabel.Text = _aiOn ? "Pause AI" : "Resume AI";
        if (!_aiOn)
        {
            // Immediately clear all bounding boxes from the video overlay
            DashboardVideoStream?.SetDetectionOverlays(Array.Empty<VideoDetectionOverlay>());
            DetectionCountText.Text = "0 det";
            DetectionsListTitle.Text = "Live Detections (0)";
            ConfAvgText.Text = "Conf avg 0%";
            FpsHudText.Text = "YOLO paused";
            DetectionsListStack.Children.Clear();
            DetectionsListStack.Children.Add(new TextBlock
            {
                Text = "YOLO dimatikan.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
        }
    }

    private void VegToggle_Toggled(object sender, RoutedEventArgs e) => ApplyLayerVisibility();
    private void GridToggle_Toggled(object sender, RoutedEventArgs e) => ApplyLayerVisibility();
    private void HeatmapToggle_Toggled(object sender, RoutedEventArgs e) => ApplyLayerVisibility();
    private void ImuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _showImuHud = ImuToggleSwitch.IsOn;
        ImuHudPanel.Visibility = _showImuHud ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLayerVisibility()
    {
        VegetationOverlayLayer.Visibility = VegToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        GridOverlayCanvas.Visibility = GridToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        HeatmapOverlayLayer.Visibility = HeatmapToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        ImuHudPanel.Visibility = ImuToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ConfThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _confThreshold = (int)Math.Round(e.NewValue);
        ConfThresholdValue.Text = $"{_confThreshold}%";
    }

    private DateTime _lastGridRebuild = DateTime.MinValue;
    private double _lastGridWidth, _lastGridHeight;

    private void RebuildGridOverlay()
    {
        if (GridOverlayCanvas == null) return;
        var width = VideoRoot?.ActualWidth ?? 0;
        var height = VideoRoot?.ActualHeight ?? 0;
        if (width <= 0 || height <= 0) return;

        // Only rebuild when size actually changed significantly or on first draw.
        if (_gridOverlayDrawn && Math.Abs(_lastGridWidth - width) < 8 && Math.Abs(_lastGridHeight - height) < 8)
        {
            return;
        }
        _lastGridWidth = width;
        _lastGridHeight = height;

        GridOverlayCanvas.Children.Clear();
        var brush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        const double step = 48;
        for (double x = 0; x <= width; x += step)
        {
            var line = new XamlLine
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = height,
                Stroke = brush, StrokeThickness = 1
            };
            GridOverlayCanvas.Children.Add(line);
        }
        for (double y = 0; y <= height; y += step)
        {
            var line = new XamlLine
            {
                X1 = 0, Y1 = y, X2 = width, Y2 = y,
                Stroke = brush, StrokeThickness = 1
            };
            GridOverlayCanvas.Children.Add(line);
        }
        _gridOverlayDrawn = true;
    }

    // ================ HUD ACTIONS ================

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = !_isRecording;
        RecordLabel.Text = _isRecording ? "Stop" : "Record";
        RecStatusText.Text = _isRecording ? "REC" : "IDLE";
        if (_isRecording)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    $"harvest_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
                var started = _videoRecorderService != null && await _videoRecorderService.StartRecordingAsync(path);
                _timelineService?.Add("recording", started ? $"Video recording started: {path}" : "Video recording failed to start", started ? "success" : "warning");
            }
            catch (Exception ex)
            {
                _timelineService?.Add("recording", $"Video recording error: {ex.Message}", "warning");
            }
        }
        else
        {
            try
            {
                var path = _videoRecorderService?.CurrentRecordingPath ?? string.Empty;
                if (_videoRecorderService != null)
                {
                    await _videoRecorderService.StopRecordingAsync();
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    await _harvestFunctionalService!.AttachVideoRecordingToLatestReportAsync(path);
                    _timelineService?.Add("recording", $"Video recording saved: {path}", "success");
                }
            }
            catch (Exception ex)
            {
                _timelineService?.Add("recording", $"Video recording stop error: {ex.Message}", "warning");
            }
        }

        UpdateOfflineAndReadiness(_flightViewModel?.Telemetry, _flightViewModel?.IsConnected == true);
    }

    private async void SnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Path.GetTempPath(), $"harvest_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        DashboardVideoStream?.SaveCurrentFrame(path);
        if (_lastFrameData != null && _harvestFunctionalService != null)
        {
            var saved = await _harvestFunctionalService.SaveLatestYoloScreenshotAsync(_lastFrameData);
            _timelineService?.Add("snapshot", $"YOLO screenshot saved: {saved}", "success");
        }
    }

    private void PauseAiButton_Click(object sender, RoutedEventArgs e)
    {
        YoloToggleSwitch.IsOn = !YoloToggleSwitch.IsOn;
    }

    private async void StartDemoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDemoRunning) { StopDemoMode(); return; }
        await StartDemoMode();
    }

    private async Task StartDemoMode()
    {
        if (_isDemoRunning || _harvestFunctionalService == null) return;
        _isDemoRunning = true;
        _demoStep = 0;
        _demoBattery = 95.0;
        _demoHeading = 90.0;
        SetDemoButtonState(running: true);

        try
        {
            // 1. Video — prefer pre-annotated output, fall back to raw derr.mp4
            var videoPath = ResolveDetectedVideoPath() ?? ResolveDerpVideoPath();
            if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath) && _cameraService != null)
            {
                await _cameraService.StopCameraAsync();
                await _cameraService.StartCameraAsync(videoPath);
                _timelineService?.Add("camera", $"Camera stream: {System.IO.Path.GetFileName(videoPath)}", "success");
            }

            // 2. AI on
            _aiOn = true;
            if (YoloToggleSwitch != null) YoloToggleSwitch.IsOn = true;

            // 3. Activate geofence — covers 5 ha survey area (240m×200m) + 80m safety buffer
            _geofenceService?.SetGeofenceCenter(-6.8148, 107.6172);
            _geofenceService?.SetGeofenceRadius(280);
            _geofenceService?.SetGeofenceActive(true);

            // 4. Simulate connection — fires real MAVLink ConnectionStatusChanged so ALL pages respond
            _mavLinkService?.SimulateConnection(true);
            if (_flightViewModel != null) _flightViewModel.IsConnected = true;

            // 4a. Seed demo survey waypoints into MapViewModel so Map / Mission pages show the grid
            if (_mapViewModel != null)
            {
                _mapViewModel.Waypoints.Clear();
                for (int i = 0; i < DemoWaypoints.Length; i++)
                {
                    var (wpLat, wpLon) = DemoWaypoints[i];
                    _mapViewModel.Waypoints.Add(new WaypointData
                    {
                        Sequence    = i + 1,
                        Latitude    = wpLat,
                        Longitude   = wpLon,
                        Altitude    = 82,
                        Command     = WaypointCommand.Waypoint,
                    });
                }
                // Mirror the geofence into MapViewModel so MapPage draws the boundary
                _mapViewModel.GeofenceCenterLat = -6.8148;
                _mapViewModel.GeofenceCenterLon = 107.6172;
                _mapViewModel.GeofenceRadius    = 280;
                _mapViewModel.GeofenceType      = GeofenceType.Circular;
                _mapViewModel.IsGeofenceActive  = true;
            }

            // 5. Initial telemetry
            InjectDemoTelemetry(0);

            // 6. Seed timeline
            _timelineService?.Add("connected", "MAVLink connected · Lembang Field B",              "success");
            _timelineService?.Add("tlog",      "Auto TLOG recorder armed",                         "success");
            _timelineService?.Add("armed",     "Armed · AUTO mode · altitude 82m · survey aktif", "success");
            _timelineService?.Add("waypoint",  "Survey grid aktif · Lembang Zone B · 7×6 · ~5 ha", "info");
            RenderIncidentTimeline();
            UpdateAlertCenter(_flightViewModel?.Telemetry, true);

            // 7. Reset UI counters
            _lastDetectionCount = 0;
            _lastConfidence = 0;
            DetectionCountText.Text = "0 det";
            FpsHudText.Text = "YOLOv8 · live inference...";
            AddAlertRow("UAV survey aktif · Lembang Field B · AUTO mode", "info");

            // 8. Start tick timer (1 s interval)
            _demoTimer.Tick -= OnDemoTick;
            _demoTimer.Tick += OnDemoTick;
            _demoTimer.Start();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[DashboardPage] StartDemoMode failed");
            _timelineService?.Add("error", $"Error: {ex.Message}", "critical");
            StopDemoMode();
        }
    }

    private void StopDemoMode()
    {
        _isDemoRunning = false;
        _demoTimer.Stop();
        _demoTimer.Tick -= OnDemoTick;
        _geofenceService?.SetGeofenceActive(false);
        _mavLinkService?.SimulateConnection(false);
        if (_flightViewModel != null) _flightViewModel.IsConnected = false;

        // Restore MapViewModel to idle state
        if (_mapViewModel != null)
        {
            _mapViewModel.Waypoints.Clear();
            _mapViewModel.IsGeofenceActive = false;
        }
        SetDemoButtonState(running: false);
        _timelineService?.Add("disconnected", "Koneksi diputus", "warning");
        AddAlertRow("Koneksi UAV diputus", "warning");
        UpdateAlertCenter(_flightViewModel?.Telemetry, false);
        RenderIncidentTimeline();
    }

    private void OnDemoTick(object? sender, object e)
    {
        if (!_isDemoRunning) return;
        _demoStep++;
        InjectDemoTelemetry(_demoStep);
        InjectDemoTimelineEvents(_demoStep);
        InjectDemoDetectionUI(_demoStep);
        if (_demoStep >= DemoWaypoints.Length * 3) _demoStep = 0;
    }

    private void InjectDemoTelemetry(int step)
    {
        if (_flightViewModel == null) return;

        // --- Smooth interpolation along the survey path ---
        int totalSegments = DemoWaypoints.Length - 1;
        int globalStep    = step % (totalSegments * DemoTicksPerSegment);
        int segIdx        = globalStep / DemoTicksPerSegment;
        double t          = (globalStep % DemoTicksPerSegment) / (double)DemoTicksPerSegment;

        var (lat0, lon0) = DemoWaypoints[segIdx];
        var (lat1, lon1) = DemoWaypoints[Math.Min(segIdx + 1, DemoWaypoints.Length - 1)];

        // Linear interpolation — smooth continuous position
        double lat = lat0 + (lat1 - lat0) * t;
        double lon = lon0 + (lon1 - lon0) * t;

        // True bearing from current segment start → end
        double newHeading = GeodesicBearing(lat0, lon0, lat1, lon1);

        // Detect turns: heading change > 45° → UAV is banking
        double headingDelta = ((newHeading - _demoHeading + 540) % 360) - 180; // [-180, +180]
        bool isTurning      = Math.Abs(headingDelta) > 45;
        _demoHeading        = newHeading;

        // Altitude: gentle oscillation ±1.5 m around 82 m (air turbulence)
        double altitude     = 82.0 + Math.Sin(step * 0.31) * 1.5;

        // Roll: banking during turns, slight waggle on straights
        double roll = isTurning
            ? Math.Sign(headingDelta) * 15.0
            : Math.Sin(step * 0.4) * 2.5;

        // Speed: slightly slower through turns
        double speed = isTurning ? 7.2 : 9.4;

        // Vertical speed: tiny oscillation
        double verticalSpeed = Math.Sin(step * 0.5) * 0.15;

        _demoBattery = Math.Max(20, _demoBattery - 0.09); // ~42 min endurance at 95→20%

        var telemetry = new TelemetryData
        {
            Latitude           = lat,
            Longitude          = lon,
            Altitude           = altitude,
            RelativeAltitude   = altitude,
            Heading            = newHeading,
            Speed              = speed,
            GroundSpeed        = speed,
            BatteryPercentage  = _demoBattery,
            BatteryRemaining   = (int)_demoBattery,
            SatelliteCount     = 14,
            FlightMode         = HarvestmoonGCS.Core.Models.FlightMode.AUTO,
            IsArmed            = true,
            ThrottlePercent    = isTurning ? 60 : 68,
            Roll               = roll,
            Pitch              = isTurning ? -1.5 : -3.0,
            Yaw                = newHeading,
            VerticalSpeed      = verticalSpeed,
        };
        _flightViewModel.UpdateTelemetry(telemetry);

        // Fire real MAVLink telemetry event so FlightPage, MissionPage etc. all respond
        _mavLinkService?.SimulateTelemetry(BuildDemoFlightData(telemetry));

        // Keep MapViewModel vehicle marker in sync so Map page shows UAV moving
        _mapViewModel?.UpdateVehiclePosition(lat, lon, altitude, (float)newHeading);
    }

    /// <summary>
    /// Computes the initial compass bearing from (lat1,lon1) to (lat2,lon2) in degrees [0,360).
    /// </summary>
    private static double GeodesicBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double φ1   = lat1 * Math.PI / 180.0;
        double φ2   = lat2 * Math.PI / 180.0;
        double y    = Math.Sin(dLon) * Math.Cos(φ2);
        double x    = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
    }

    private static HarvestmoonGCS.Models.FlightData BuildDemoFlightData(TelemetryData t)
    {
        var fd = new HarvestmoonGCS.Models.FlightData
        {
            FlightMode = t.FlightMode,
        };
        fd.GPS.Latitude  = (int)(t.Latitude * 1e7);
        fd.GPS.Longitude = (int)(t.Longitude * 1e7);
        fd.Altitude      = (int)t.Altitude;
        fd.AltitudeFloat = (float)t.Altitude;
        fd.Speed         = (float)t.Speed;
        fd.Sats          = (byte)Math.Min(t.SatelliteCount, 255);
        return fd;
    }

    private void InjectDemoTimelineEvents(int step)
    {
        // DemoWaypoints = 7 rows × 6 cols = 42 waypoints. Row transitions at wp 6, 12, 18, 24, 30, 36.
        switch (step)
        {
            case 2:  _timelineService?.Add("yolo",     "YOLO inference aktif · scan row 1 of 7",               "success"); break;
            case 5:  _timelineService?.Add("waypoint", "WP 1 → Survey row 1 · Lembang Zone B North",           "success"); break;
            case 10: _timelineService?.Add("yolo",     DemoClassificationEvents[0],                             "info");    break;
            case 14: _timelineService?.Add("waypoint", "WP 7 → Survey row 2 · tracking East",                  "success"); break;
            case 18: _timelineService?.Add("yolo",     DemoClassificationEvents[1],                             "warning"); break;
            case 22: _timelineService?.Add("waypoint", "WP 13 → Survey row 3 · mid-field",                     "success"); break;
            case 26: _timelineService?.Add("geofence", "Geofence safe · 85m to boundary (zona aman)",          "info");    break;
            case 30: _timelineService?.Add("yolo",     DemoClassificationEvents[2],                             "info");    break;
            case 34: _timelineService?.Add("waypoint", "WP 19 → Survey row 4 · tracking West",                 "success"); break;
            case 38: _timelineService?.Add("yolo",     DemoClassificationEvents[3],                             "warning"); break;
            case 42: _timelineService?.Add("waypoint", "WP 25 → Survey row 5 · 60% area covered",              "success"); break;
            case 46: _timelineService?.Add("yolo",     DemoClassificationEvents[4],                             "critical"); break;
            case 50: _timelineService?.Add("waypoint", "WP 31 → Survey row 6 · 80% area covered",              "success"); break;
            case 54: _timelineService?.Add("yolo",     DemoClassificationEvents[5],                             "info");    break;
            case 58: _timelineService?.Add("waypoint", "WP 37 → Survey row 7 · 95% area covered",              "success"); break;
            case 62: _timelineService?.Add("tlog",     $"Telemetry batch flushed · {step * 2} packets saved",  "info");    break;
        }
        if (step % 5 == 0 && step > 0) RenderIncidentTimeline();
    }

    // Rotating demo detection frames — each entry is a snapshot of crop health detections
    private static readonly (string Class, double Conf)[][] DemoDetectionFrames =
    {
        new[] { ("healthy_crop", 0.93), ("healthy_crop", 0.88), ("stressed_crop", 0.79) },
        new[] { ("healthy_crop", 0.91), ("stressed_crop", 0.82), ("drought_stress", 0.74), ("bare_soil", 0.68) },
        new[] { ("healthy_crop", 0.96), ("healthy_crop", 0.90), ("healthy_crop", 0.85), ("stressed_crop", 0.77) },
        new[] { ("disease_stress_vegetation", 0.87), ("stressed_crop", 0.81), ("healthy_crop", 0.89) },
        new[] { ("healthy_crop", 0.94), ("drought_stress", 0.76), ("bare_soil", 0.71), ("stressed_crop", 0.83) },
        new[] { ("healthy_crop", 0.92), ("healthy_crop", 0.88), ("disease_stress_vegetation", 0.80) },
        new[] { ("stressed_crop", 0.85), ("drought_stress", 0.78), ("healthy_crop", 0.91) },
        new[] { ("healthy_crop", 0.95), ("healthy_crop", 0.89), ("bare_soil", 0.65), ("stressed_crop", 0.72) },
    };

    private void InjectDemoDetectionUI(int step)
    {
        var frame = DemoDetectionFrames[step % DemoDetectionFrames.Length];
        int count = frame.Length;
        double avgConf = frame.Average(f => f.Conf);

        _lastDetectionCount = count;
        _lastConfidence = avgConf;

        // Update counters
        DetectionCountText.Text = $"{count} det";
        MissionDetectionsText.Text = ((_demoStep / 2) + count).ToString();
        ConfAvgText.Text = $"Conf avg {avgConf * 100:F0}%";
        SummaryConfidenceText.Text = $"{avgConf * 100:F0}%";
        DetectionsListTitle.Text = $"Live Detections ({count})";

        // Render detection rows
        DetectionsListStack.Children.Clear();
        foreach (var (cls, conf) in frame)
        {
            DetectionsListStack.Children.Add(BuildCropDetectionRow(cls, conf));
        }

        // Class distribution from fusion video (hsvv_fused.mp4)
        // healthy 33.8%, stressed 19.4%, disease 4.9%, drought 5.4%, bare_soil 36.5%
        double jitter = Math.Sin(step * 0.7) * 2.5;
        if (SummaryHealthyCount  != null) SummaryHealthyCount.Text  = $"{33.8 + jitter:F0}%";
        if (SummaryStressCount   != null) SummaryStressCount.Text   = $"{19.4 - jitter * 0.5:F0}%";
        if (SummaryDiseaseCount  != null) SummaryDiseaseCount.Text  = $"{4.9  + Math.Abs(jitter) * 0.3:F0}%";
        if (SummaryDroughtCount  != null) SummaryDroughtCount.Text  = $"{5.4  - jitter * 0.2:F0}%";
        if (SummaryBaresoilCount != null) SummaryBaresoilCount.Text = $"{36.5 + jitter * 0.4:F0}%";

        // NDVI bars — derived from fusion class distribution
        double jitter2      = Math.Sin(step * 0.5) * 1.5;
        double ndviHealthy  = Math.Max(0, 33.8 + jitter2);
        double ndviModerate = Math.Max(0, 36.5 - jitter2 * 0.5);   // bare soil → moderate cover
        double ndviStressed = Math.Max(0, 19.4 + jitter2 * 0.3);
        double ndviCritical = Math.Max(0, 10.3 - Math.Abs(jitter2) * 0.2);
        double avgNdvi      = (ndviHealthy * 0.75 + ndviModerate * 0.45 + ndviStressed * 0.25 + ndviCritical * 0.05) / 100.0;

        if (NdviHealthyBar   != null) { NdviHealthyBar.Value   = ndviHealthy;  NdviHealthyText.Text   = $"{ndviHealthy:F0}%"; }
        if (NdviModerateBar  != null) { NdviModerateBar.Value  = ndviModerate; NdviModerateText.Text  = $"{ndviModerate:F0}%"; }
        if (NdviStressedBar  != null) { NdviStressedBar.Value  = ndviStressed; NdviStressedText.Text  = $"{ndviStressed:F0}%"; }
        if (NdviCriticalBar  != null) { NdviCriticalBar.Value  = ndviCritical; NdviCriticalText.Text  = $"{ndviCritical:F0}%"; }
        if (NdviAvgText      != null) NdviAvgText.Text      = avgNdvi.ToString("F2");
        if (NdviCoverageText != null) NdviCoverageText.Text = "4.8 ha";

        // Auto-retry video if camera stopped (e.g. after bad file was fixed)
        if (step % 10 == 0 && _cameraService != null && !_cameraService.IsStreaming && _isDemoRunning)
        {
            var vp = ResolveDetectedVideoPath() ?? ResolveDerpVideoPath();
            if (!string.IsNullOrWhiteSpace(vp) && File.Exists(vp))
                _ = _cameraService.StartCameraAsync(vp);
        }

        UpdateAlertCenter(_flightViewModel?.Telemetry, true);
    }

    private void SetDemoButtonState(bool running)
    {
        StartDemoButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = running ? "" : "", FontSize = 11 },
                new TextBlock { Text = running ? "Stop Demo" : "Start Demo" }
            }
        };
    }

    private static string? ResolveDetectedVideoPath()
    {
        var candidates = new[]
        {
            "/home/fawwazfa/Program/Harvestmoon/test_program/moonharvest_hsv_detector/moonharvest_package/fusion_out/hsvv_fused_only.mp4",
            "/home/fawwazfa/Program/Harvestmoon/test_program/moonharvest_hsv_detector/moonharvest_package/fusion_out/hsvv_fused.mp4",
            "/home/fawwazfa/Program/Harvestmoon/runs/uav_detection/derr_detected.mp4",
            "/home/fawwazfa/Program/Harvestmoon/runs/demo/detection_output.mp4",
            Path.Combine(AppContext.BaseDirectory, "derr_detected.mp4"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static (double Lat, double Lon)[] GenerateSurveyWaypoints(
        double centerLat, double centerLon, int rows, int cols, double spacingMeters)
    {
        const double mPerDegLat = 111320.0;
        double mPerDegLon = mPerDegLat * Math.Cos(centerLat * Math.PI / 180);
        double dLat = spacingMeters / mPerDegLat;
        double dLon = spacingMeters / mPerDegLon;
        double startLat = centerLat - (rows / 2.0) * dLat;
        double startLon = centerLon - (cols / 2.0) * dLon;
        var wps = new List<(double, double)>();
        for (int r = 0; r < rows; r++)
        {
            double lat = startLat + r * dLat;
            bool ltr = r % 2 == 0;
            for (int c = 0; c < cols; c++)
            {
                int col = ltr ? c : (cols - 1 - c);
                wps.Add((lat, startLon + col * dLon));
            }
        }
        return wps.ToArray();
    }

        // ================ EXPORTS ================

    private async void ExportPdfButton_Click(object sender, RoutedEventArgs e) => await ExportReportAsync("pdf");
    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e) => await ExportReportAsync("csv");
    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e) => await ExportReportAsync("json");

    private async Task ExportReportAsync(string format)
    {
        if (_harvestFunctionalService == null) return;
        var telemetry = _flightViewModel?.Telemetry;
        var connected = _flightViewModel?.IsConnected == true;
        var videoPath = _videoRecorderService?.CurrentRecordingPath ?? string.Empty;
        var mapPath = _harvestFunctionalService.SaveMapSnapshotPlaceholder(
            telemetry?.Latitude ?? _centerLat,
            telemetry?.Longitude ?? _centerLon,
            connected ? "Live dashboard position" : "Dashboard standby position");
        var yoloPath = _lastFrameData != null
            ? await _harvestFunctionalService.SaveLatestYoloScreenshotAsync(_lastFrameData)
            : string.Empty;

        var record = new HarvestFunctionalService.HarvestReportRecord
        {
            Id = MissionIdText.Text,
            Area = "Dashboard Session",
            DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Duration = MissionElapsedText.Text,
            Detections = _lastDetectionCount,
            Priority = _lastDetectionCount > 0 ? "Medium" : "Low",
            AiModelUsed = _harvestFunctionalService.IsYoloOptionEnabled ? "YOLOv8n ONNX local" : "OpenCV fallback",
            OperatorNote = $"Avg conf {(_lastConfidence * 100):F0}%",
            MapScreenshotPath = mapPath,
            YoloScreenshotPath = yoloPath,
            VideoRecordingPath = videoPath,
            IncidentTimelineJson = _timelineService?.ToJson() ?? string.Empty
        };
        try
        {
            await _harvestFunctionalService.CreateEvidenceBundleAsync(record);
            var baseName = $"dashboard-{DateTime.Now:yyyyMMddHHmmss}";
            switch (format)
            {
                case "pdf": await _harvestFunctionalService.ExportReportPdfAsync(record, baseName); break;
                case "csv": await _harvestFunctionalService.ExportReportCsvAsync(record, baseName); break;
                case "json": await _harvestFunctionalService.ExportReportJsonAsync(record, baseName); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] Export {format} error: {ex.Message}");
        }
    }

    private static string? FindDemoVideoPath()
    {
        var root = FindRepoRoot();
        var candidates = new[]
        {
            Path.Combine(root, "vision_trial", "testkamera.mp4"),
            Path.Combine(root, "vision_trial", "output", "crop_weed_trial.mp4"),
            Path.Combine(root, "testkamera.mp4"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "testkamera.mp4")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "HarvestmoonGCS")) &&
                Directory.Exists(Path.Combine(dir.FullName, "vision_trial")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static byte[] ExtractFirstFrameBytes(string videoPath)
    {
        try
        {
            using var capture = new VideoCapture(videoPath);
            if (!capture.IsOpened())
            {
                return Array.Empty<byte>();
            }

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty())
            {
                return Array.Empty<byte>();
            }

            Cv2.ImEncode(".jpg", frame, out var buffer);
            return buffer;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[DashboardPage] Failed to extract demo frame from {Video}", videoPath);
            return Array.Empty<byte>();
        }
    }

    private void AddAlertRow(string text, string severity)
    {
        _lastAlertSignature = null;
        AlertsStack.Children.Insert(0, BuildAlertRow(text, DateTime.Now.ToString("HH:mm:ss"), severity));
    }

    private void GoCropAnalysisButton_Click(object sender, RoutedEventArgs e)
    {
        // Navigation event is handled by sidebar; simulate by finding parent frame
        if (this.Frame != null)
        {
            this.Frame.Navigate(typeof(StatsPage));
        }
    }

    // ================ IMU HUD ================

    private void OnImuTick(object? sender, object e)
    {
        // Use real telemetry when available; skip the random wobble to avoid forcing a redraw
        // every 700 ms. When connected we still redraw because the telemetry path already did.
        if (_telemetry != null)
        {
            _pitch = _telemetry.Pitch * 180.0 / Math.PI;
            _roll = _telemetry.Roll * 180.0 / Math.PI;
            _yaw = _telemetry.Heading;
        }
        else
        {
            // Gentle idle animation when disconnected so the artificial horizon isn't frozen.
            var rnd = Random.Shared;
            _pitch = Math.Clamp(_pitch + (rnd.NextDouble() - 0.5) * 4, -30, 30);
            _roll = Math.Clamp(_roll + (rnd.NextDouble() - 0.5) * 6, -45, 45);
            _yaw = (_yaw + (rnd.NextDouble() - 0.5) * 4 + 360) % 360;
        }

        ImuRollTransform.Angle = _roll;
        ImuPitchTransform.Y = Math.Clamp(_pitch * 1.4, -42, 42);
        ImuLabelText.Text = $"P{_pitch:F0}° R{_roll:F0}° Y{_yaw:F0}°";
    }

    private void OnMissionTick(object? sender, object e)
    {
        var elapsed = DateTime.Now - _missionStart;
        MissionElapsedText.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        var plannedSeconds = _mapViewModel?.CalculateMissionDuration() ?? 0;
        var total = plannedSeconds > 0
            ? TimeSpan.FromSeconds(plannedSeconds)
            : TimeSpan.FromMinutes(12);
        var remaining = total - elapsed;
        if (remaining.TotalSeconds <= 0) remaining = TimeSpan.Zero;
        MissionEtaText.Text = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    // ================ ALERTS / SUMMARY SEED ================

    private void UpdateAlertCenter(TelemetryData? telemetry, bool connected)
    {
        var alerts = BuildRuntimeAlerts(telemetry, connected).ToList();
        var signature = string.Join("|", alerts.Select(alert => $"{alert.Severity}:{alert.Text}"));
        if (signature == _lastAlertSignature)
        {
            return;
        }

        _lastAlertSignature = signature;
        AlertsStack.Children.Clear();
        foreach (var alert in alerts)
        {
            AlertsStack.Children.Add(BuildAlertRow(alert.Text, DateTime.Now.ToString("HH:mm:ss"), alert.Severity));
        }
    }

    private void UpdateOfflineAndReadiness(TelemetryData? telemetry, bool connected)
    {
        var modelName = Path.GetFileName(_harvestFunctionalService?.RuntimeModelPath ?? "bundled-auto");
        OfflineModeText.Text = _harvestFunctionalService?.IsYoloRuntimeReady == true
            ? "Running offline · AI local model active · No cloud dependency"
            : "Running offline · AI fallback active · No cloud dependency";
        ModelRuntimeText.Text = $"Model: {modelName} · threshold {_harvestFunctionalService?.RuntimeConfidenceThreshold * 100:F0}%";
        RecorderRuntimeText.Text = _videoRecorderService?.IsRecording == true
            ? $"Recorder: recording · {_videoRecorderService.CurrentRecordingPath}"
            : "Recorder: standby";

        var battery = telemetry?.BatteryPercent is > 0
            ? telemetry.BatteryPercent
            : telemetry?.BatteryRemaining ?? 0;
        var gpsReady = telemetry != null && IsValidCoordinate(telemetry.Latitude, telemetry.Longitude) && telemetry.SatelliteCount >= 6;
        var geofenceReady = _mapViewModel?.IsGeofenceActive == true || _geofenceService?.CurrentGeofence.IsActive == true;
        var waypointReady = (_mapViewModel?.Waypoints?.Count ?? 0) > 0;

        SetChecklistText(ReadyTelemetryText, connected, "Telemetry connected");
        SetChecklistText(ReadyGpsText, gpsReady, $"GPS fix ({telemetry?.SatelliteCount ?? 0} sats)");
        SetChecklistText(ReadyBatteryText, battery >= 25, battery > 0 ? $"Battery {battery:F0}%" : "Battery unknown");
        SetChecklistText(ReadyCameraText, _cameraService?.IsStreaming == true, "Camera active");
        SetChecklistText(ReadyYoloText, _harvestFunctionalService?.IsYoloRuntimeReady == true, "YOLO ready");
        SetChecklistText(ReadyGeofenceText, geofenceReady, "Geofence loaded");
        SetChecklistText(ReadyWaypointText, waypointReady, $"Waypoint loaded ({_mapViewModel?.Waypoints?.Count ?? 0})");
        SetChecklistText(ReadyRecorderText, _videoRecorderService != null, _videoRecorderService?.IsRecording == true ? "Recorder active" : "Recorder ready");
    }

    private static void SetChecklistText(TextBlock textBlock, bool ok, string label)
    {
        textBlock.Text = $"{(ok ? "✓" : "○")} {label}";
        textBlock.Foreground = new SolidColorBrush(ok
            ? Color.FromArgb(255, 6, 95, 70)
            : Color.FromArgb(255, 100, 116, 139));
    }

    private void RenderIncidentTimeline()
    {
        if (IncidentTimelineStack == null)
        {
            return;
        }

        IncidentTimelineStack.Children.Clear();
        var entries = _timelineService?.Entries.Take(8).ToList() ?? new List<IncidentTimelineEntry>();
        if (entries.Count == 0)
        {
            IncidentTimelineStack.Children.Add(new TextBlock
            {
                Text = "Belum ada event misi.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            return;
        }

        foreach (var entry in entries)
        {
            IncidentTimelineStack.Children.Add(new TextBlock
            {
                Text = $"{entry.Timestamp:HH:mm:ss} · {entry.Type}: {entry.Message}",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(entry.Severity == "critical"
                    ? Color.FromArgb(255, 185, 28, 28)
                    : entry.Severity == "success"
                        ? Color.FromArgb(255, 6, 95, 70)
                        : Color.FromArgb(255, 79, 107, 92))
            });
        }
    }

    private IEnumerable<(string Text, string Severity)> BuildRuntimeAlerts(TelemetryData? telemetry, bool connected)
    {
        if (!connected)
        {
            yield return ("Connection lost - MAVLink disconnected", "critical");
        }

        var satelliteCount = telemetry?.SatelliteCount ?? 0;
        var hasGps = telemetry != null && IsValidCoordinate(telemetry.Latitude, telemetry.Longitude);
        if (!hasGps || satelliteCount < 6)
        {
            yield return ($"GPS weak - sats {satelliteCount}", "warning");
        }

        var battery = telemetry?.BatteryPercent is > 0
            ? telemetry.BatteryPercent
            : telemetry?.BatteryRemaining ?? 0;
        if (battery > 0 && battery < 20)
        {
            yield return ($"Battery low - {battery:F0}%", battery < 12 ? "critical" : "warning");
        }

        foreach (var alert in BuildGeofenceAlerts(telemetry, hasGps))
        {
            yield return alert;
        }

        if (!string.IsNullOrWhiteSpace(_lastGeofenceMonitorEvent))
        {
            yield return (_lastGeofenceMonitorEvent, _lastGeofenceMonitorSeverity);
        }

        if (_harvestFunctionalService?.IsYoloOptionEnabled == true)
        {
            yield return (_harvestFunctionalService.IsYoloRuntimeReady
                ? "YOLO offline runtime ready"
                : _harvestFunctionalService.YoloStatusMessage,
                _harvestFunctionalService.IsYoloRuntimeReady ? "info" : "warning");
        }

        yield return ($"Mission detections: {_lastDetectionCount}", _lastDetectionCount > 0 ? "info" : "warning");
    }

    private IEnumerable<(string Text, string Severity)> BuildGeofenceAlerts(TelemetryData? telemetry, bool hasGps)
    {
        var geofenceActive = _mapViewModel?.IsGeofenceActive == true ||
            _geofenceService?.CurrentGeofence.IsActive == true;

        if (!geofenceActive)
        {
            yield return ("Geofence inactive - set boundary in Map", "warning");
            yield break;
        }

        if (telemetry == null || !hasGps)
        {
            yield return ("Geofence active - waiting for valid GPS", "warning");
            yield break;
        }

        var altitude = telemetry.Altitude;
        double distanceToBoundary;
        if (_mapViewModel?.IsGeofenceActive == true)
        {
            distanceToBoundary = CalculateMapViewModelGeofenceDistance(telemetry.Latitude, telemetry.Longitude, altitude);
        }
        else if (_geofenceService != null)
        {
            distanceToBoundary = _geofenceService.CalculateDistanceToBoundary(telemetry.Latitude, telemetry.Longitude, altitude);
        }
        else
        {
            yield break;
        }

        if (distanceToBoundary < 0)
        {
            yield return ($"GEOFENCE BREACH - outside by {Math.Abs(distanceToBoundary):F0} m", "critical");
        }
        else if (distanceToBoundary < 30)
        {
            yield return ($"Geofence warning - {distanceToBoundary:F0} m to boundary", "warning");
        }
        else
        {
            yield return ($"Geofence safe - {distanceToBoundary:F0} m to boundary", "info");
        }
    }

    private double CalculateMapViewModelGeofenceDistance(double latitude, double longitude, double altitude)
    {
        if (_mapViewModel == null)
        {
            return double.MaxValue;
        }

        if (altitude > _mapViewModel.GeofenceMaxAltitude)
        {
            return -(altitude - _mapViewModel.GeofenceMaxAltitude);
        }

        if (_mapViewModel.GeofenceType == GeofenceType.Circular)
        {
            var distanceFromCenter = GeoMath.CalculateDistance(
                _mapViewModel.GeofenceCenterLat,
                _mapViewModel.GeofenceCenterLon,
                latitude,
                longitude);
            return _mapViewModel.GeofenceRadius - distanceFromCenter;
        }

        if (_mapViewModel.GeofenceVertices.Count < 3)
        {
            return double.MaxValue;
        }

        var inside = IsPointInPolygon(latitude, longitude, _mapViewModel.GeofenceVertices);
        var minDistance = double.MaxValue;
        for (var i = 0; i < _mapViewModel.GeofenceVertices.Count; i++)
        {
            var a = _mapViewModel.GeofenceVertices[i];
            var b = _mapViewModel.GeofenceVertices[(i + 1) % _mapViewModel.GeofenceVertices.Count];
            minDistance = Math.Min(minDistance, DistanceToSegmentMeters(latitude, longitude, a.Lat, a.Lon, b.Lat, b.Lon));
        }

        return inside ? minDistance : -minDistance;
    }

    private static bool IsPointInPolygon(double latitude, double longitude, IEnumerable<GeofenceVertex> vertices)
    {
        var points = vertices.ToList();
        var inside = false;
        var j = points.Count - 1;
        for (var i = 0; i < points.Count; i++)
        {
            var vi = points[i];
            var vj = points[j];
            if ((vi.Lon > longitude) != (vj.Lon > longitude) &&
                latitude < (vj.Lat - vi.Lat) * (longitude - vi.Lon) / (vj.Lon - vi.Lon) + vi.Lat)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    private static double DistanceToSegmentMeters(double lat, double lon, double lat1, double lon1, double lat2, double lon2)
    {
        var x = lon;
        var y = lat;
        var x1 = lon1;
        var y1 = lat1;
        var x2 = lon2;
        var y2 = lat2;
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((x - x1) * dx + (y - y1) * dy) / lengthSquared, 0, 1);
        var projectedLat = y1 + t * dy;
        var projectedLon = x1 + t * dx;
        return GeoMath.CalculateDistance(lat, lon, projectedLat, projectedLon);
    }

    private Border BuildAlertRow(string text, string time, string severity)
    {
        var tint = severity switch
        {
            "critical" => Color.FromArgb(255, 254, 242, 242),
            "warning" => Color.FromArgb(255, 255, 251, 235),
            _ => Color.FromArgb(255, 239, 246, 255)
        };
        var dotColor = severity switch
        {
            "critical" => Color.FromArgb(255, 211, 47, 47),
            "warning" => Color.FromArgb(255, 251, 192, 45),
            _ => Color.FromArgb(255, 25, 118, 210)
        };

        var border = new Border
        {
            Background = new SolidColorBrush(tint),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new XamlEllipse { Width = 8, Height = 8, Fill = new SolidColorBrush(dotColor), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 15, 48, 36)), TextTrimming = TextTrimming.CharacterEllipsis });
        body.Children.Add(new TextBlock { Text = time, FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 118, 107)) });
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        var ack = new Button
        {
            Content = "ACK",
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        ack.Click += (_, _) => AlertsStack.Children.Remove(border);
        Grid.SetColumn(ack, 2);
        grid.Children.Add(ack);

        border.Child = grid;
        return border;
    }

    private void SeedSummary()
    {
        SummaryHealthyCount.Text = "0%";
        SummaryStressCount.Text = "0%";
        SummaryDiseaseCount.Text = "0%";
        SummaryDroughtCount.Text = "0%";
        SummaryBaresoilCount.Text = "0%";
        SummaryPriorityText.Text = "Standby";
        SummaryConfidenceText.Text = "0%";

        NdviHealthyBar.Value = 0;
        NdviHealthyText.Text = "0%";
        NdviModerateBar.Value = 0;
        NdviModerateText.Text = "0%";
        NdviStressedBar.Value = 0;
        NdviStressedText.Text = "0%";
        NdviCriticalBar.Value = 0;
        NdviCriticalText.Text = "0%";
        NdviAvgText.Text = "0.00";
        NdviCoverageText.Text = "0 ha";
    }

    private static bool IsValidCoordinate(double lat, double lon)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon)) return false;
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return false;
        return Math.Abs(lat) > 0.000001 || Math.Abs(lon) > 0.000001;
    }

    private void UpdateTopBarAiFps(double fps)
    {
        try
        {
            // Walk up to MainPage_Modern which hosts the TopBar
            if (this.Frame?.Parent is Grid grid && grid.Parent is HarvestmoonGCS.MainPage_Modern mainPage)
            {
                // Access TopBar via FindName or direct field — use reflection-free approach
                // MainPage_Modern exposes TopBar as x:Name in XAML
            }
            // Simpler: find TopBar from the window's visual tree
            var window = App.MainWindow;
            if (window?.Content is Frame rootFrame && rootFrame.Content is HarvestmoonGCS.MainPage_Modern mp)
            {
                var topBar = mp.FindName("TopBar") as HarvestmoonGCS.Controls.ModernTopBar;
                if (topBar != null)
                {
                    topBar.AiFps = fps;
                }
            }
        }
        catch
        {
            // Non-critical UI update, swallow errors
        }
    }
}
