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
    private int _confThreshold = 40;
    private double _pitch;
    private double _roll;
    private double _yaw;
    private bool _missionTimerStarted;
    private readonly DispatcherTimer _imuTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
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

    // Real classify-stream detection data from Python (null = no data yet)
    private (int Healthy, int Stress, int Disease, int Pest, int Total)? _classifyRealData;
    private bool _classifyFirstFrameReceived;

    // Real FPS tracking for HSV classify stream
    private int _classifyFrameCount;
    private DateTime _classifyFrameStart = DateTime.MinValue;
    private double _classifyFps;

    // Demo mode state
    private bool _isDemoRunning;
    private int _demoStep;
    private double _demoBattery = 95.0;
    private double _demoHeading = 90.0;  // tracks current bearing for smooth roll
    private const int DemoTicksPerSegment = 10; // ~4 m/s cruise × 10 ticks per segment
    private readonly DispatcherTimer _demoTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    // Straight transect: WP 10-13 × 300 m → 900 m east–west over Lembang field
    private static readonly (double Lat, double Lon)[] DemoWaypoints = GenerateStraightLineWaypoints(-6.8148, 107.6172, count: 4, totalMeters: 900, bearingDeg: 90.0);
    private const int DemoWaypointStartSequence = 10;
    private static readonly string[] DemoClassificationEvents =
    {
        "Lush Green (conf 0.93) · Sector C",
        "Inconsistent Growth (conf 0.81) · Sector D",
        "Lush Green (conf 0.96) · Sector A",
        "Inconsistent Growth (conf 0.74) · Sector B",
        "Disease (conf 0.87) · Sector A",
        "Well Irrigated (conf 0.91) · Sector E",
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
        DashboardMapControl?.SetActive(true);
        DashboardMapControl?.InvalidateArrange();
        AttachCameraHandlers();
        ResumeVideoRendering();
        SubscribeToTelemetry();
        SubscribeToGeofenceMonitor();
        _imuTimer.Start();
        if (_missionTimerStarted) _missionTimer.Start();
    }

    public void OnPageDeactivated()
    {
        _isPageActive = false;
        DashboardMapControl?.SetActive(false);
        _imuTimer.Stop();
        _missionTimer.Stop();
        UnsubscribeFromTelemetry();
        UnsubscribeFromGeofenceMonitor();
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
        if (_cameraService is PythonCameraService pcs)
            pcs.ClassificationSummaryChanged += OnClassifyDetectionData;
        _cameraHandlersAttached = true;
    }

    private void DetachCameraHandlers()
    {
        if (_cameraService == null || !_cameraHandlersAttached) return;
        _cameraService.FrameReceived -= OnDashboardFrameReceived;
        _cameraService.StreamingStatusChanged -= OnDashboardCameraStreamingChanged;
        _cameraService.ConnectionError -= OnDashboardCameraError;
        if (_cameraService is PythonCameraService pcs)
            pcs.ClassificationSummaryChanged -= OnClassifyDetectionData;
        _cameraHandlersAttached = false;
    }

    private void OnClassifyDetectionData(object? sender, string dataJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(dataJson);
            var root = doc.RootElement;
            // Python emits float percentages (e.g. 53.2) — GetInt32() would throw; use GetDouble()
            int healthy = 0, stress = 0, disease = 0, pest = 0;
            if (root.TryGetProperty("classes", out var cls))
            {
                if (cls.TryGetProperty("Healthy", out var h)) healthy = (int)Math.Round(h.GetDouble());
                if (cls.TryGetProperty("Stress",  out var s)) stress  = (int)Math.Round(s.GetDouble());
                if (cls.TryGetProperty("Disease", out var d)) disease = (int)Math.Round(d.GetDouble());
                if (cls.TryGetProperty("Pest",    out var p)) pest    = (int)Math.Round(p.GetDouble());
            }
            int total = healthy + stress + disease + pest;
            if (total == 0) return;
            _classifyRealData = (healthy, stress, disease, pest, total);
        }
        catch { }
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
            DashboardVideoStream?.ShowStatus("Camera service not available", false);
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
    private const int VideoFrameIntervalMs = 50; // ~20 FPS — reduce UI thread load
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

        // On first frame from classify stream, hide the loading overlay
        var classifyActive = (_cameraService as PythonCameraService)?.IsClassificationStream == true;
        if (classifyActive && !_classifyFirstFrameReceived)
        {
            _classifyFirstFrameReceived = true;
            _classifyFrameStart = now;
            _classifyFrameCount = 0;
            DispatcherQueue.TryEnqueue(() => { try { DashboardVideoStream?.HideOverlay(); } catch { } });
        }

        // Track FPS for classify stream
        if (classifyActive && _classifyFirstFrameReceived)
        {
            _classifyFrameCount++;
            var elapsed = (now - _classifyFrameStart).TotalSeconds;
            if (elapsed >= 3.0 && _classifyFrameCount > 0)
            {
                _classifyFps = _classifyFrameCount / elapsed;
                _classifyFrameCount = 0;
                _classifyFrameStart = now;
            }
        }

        DashboardVideoStream?.UpdateFrame(frameData);
        if (_videoRecorderService?.IsRecording == true)
        {
            _videoRecorderService.WriteFrame(frameData);
        }
        if (!classifyActive && _aiOn && _harvestFunctionalService?.IsYoloOptionEnabled == true && !_analysisRunning)
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
                    .Select(b => (demo: MapToDemo(b.ClassName), b))
                    .Where(t => !string.IsNullOrEmpty(t.demo))
                    .Select(t => new VideoDetectionOverlay
                    {
                        Label = t.demo,
                        Confidence = t.b.Confidence,
                        X = t.b.X,
                        Y = t.b.Y,
                        Width = t.b.Width,
                        Height = t.b.Height
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
                Text = "No detections above threshold.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            return;
        }

        foreach (var det in filtered)
        {
            var demoName = MapToDemo(det.ClassName);
            if (string.IsNullOrEmpty(demoName)) continue;
            DetectionsListStack.Children.Add(BuildCropDetectionRow(demoName, det.Confidence));
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
                Text = "No detections above threshold.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });
            return;
        }

        foreach (var det in filtered)
        {
            var demoName = MapToDemo(det.ClassName);
            if (string.IsNullOrEmpty(demoName)) continue;
            DetectionsListStack.Children.Add(BuildCropDetectionRow(demoName, det.Confidence));
        }
    }

    private void UpdateSummaryAndNdvi(HarvestFunctionalService.HarvestAnalysisResult result)
    {
        // Hitung dari DetectionBoxes (YOLO v3 class names) — bukan HSV residual
        int lushGreen = 0, wellIrrig = 0, inconsistent = 0, soil = 0, disease = 0, pest = 0;
        foreach (var box in result.DetectionBoxes.Where(b => b.Confidence * 100 >= _confThreshold))
        {
            switch (box.ClassName.ToLowerInvariant())
            {
                case "disease":             disease++;      break;
                case "pest":                pest++;         break;
                case "inconsistent_growth": inconsistent++; break;
                case "soil_issues":         soil++;         break;
                case "well_irrigated":      wellIrrig++;    break;
                default:                    lushGreen++;    break;  // lush_green + unknown
            }
        }

        int total           = Math.Max(1, lushGreen + wellIrrig + inconsistent + soil + disease + pest);
        var lushGreenPct    = lushGreen    * 100.0 / total;
        var wellIrrigPct    = wellIrrig    * 100.0 / total;
        var inconsistentPct = inconsistent * 100.0 / total;
        var soilPct         = soil         * 100.0 / total;
        var diseasePct      = disease      * 100.0 / total;
        var pestPct         = pest         * 100.0 / total;

        SummaryHealthyCount.Text = $"{lushGreenPct:F0}%";
        if (SummaryWellIrrigatedCount != null) SummaryWellIrrigatedCount.Text = $"{wellIrrigPct:F0}%";
        SummaryStressCount.Text  = $"{inconsistentPct:F0}%";
        if (SummarySoilIssuesCount != null) SummarySoilIssuesCount.Text = $"{soilPct:F0}%";
        SummaryDiseaseCount.Text = $"{diseasePct:F0}%";
        SummaryPestCount.Text    = $"{pestPct:F0}%";

        if (SummaryHealthyBar        != null) SummaryHealthyBar.Value        = lushGreenPct;
        if (SummaryWellIrrigatedBar  != null) SummaryWellIrrigatedBar.Value  = wellIrrigPct;
        if (SummaryStressBar         != null) SummaryStressBar.Value         = inconsistentPct;
        if (SummarySoilIssuesBar     != null) SummarySoilIssuesBar.Value     = soilPct;
        if (SummaryDiseaseBar        != null) SummaryDiseaseBar.Value        = diseasePct;
        if (SummaryPestBar           != null) SummaryPestBar.Value           = pestPct;

        var maxHealthy = Math.Max(lushGreenPct, wellIrrigPct);
        var dominant = diseasePct >= pestPct && diseasePct >= inconsistentPct && diseasePct >= maxHealthy
            ? $"Disease {diseasePct:F0}%"
            : pestPct >= inconsistentPct && pestPct >= maxHealthy
                ? $"Pest {pestPct:F0}%"
                : inconsistentPct >= maxHealthy
                    ? $"Inconsistent Growth {inconsistentPct:F0}%"
                    : lushGreenPct >= wellIrrigPct
                        ? $"Lush Green {lushGreenPct:F0}%"
                        : $"Well Irrigated {wellIrrigPct:F0}%";
        if (SummaryDominantText != null) SummaryDominantText.Text = dominant;

        var priority = disease > 0 || pest > 0 ? "High" : inconsistent > 0 ? "Medium" : "Low";
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

    /// <summary>Remap v3 YOLO class name to display label. Returns "" for unknown.</summary>
    private static string MapToDemo(string className)
    {
        return className.ToLowerInvariant().Replace(" ", "_").Replace("-", "_") switch
        {
            "lush_green"          => "Lush Green",
            "well_irrigated"      => "Well Irrigated",
            "inconsistent_growth" => "Inconsistent Growth",
            "soil_issues"         => "Soil Issues",
            "disease"             => "Disease",
            "pest"                => "Pest",
            _ => string.Empty
        };
    }

    private static Color ColorForLabel(string label)
    {
        var lower = label.ToLowerInvariant();
        if (lower.Contains("disease"))      return Color.FromArgb(255, 211,  47,  47);  // red
        if (lower.Contains("pest"))         return Color.FromArgb(255, 124,  58, 237);  // purple
        if (lower.Contains("soil"))         return Color.FromArgb(255,  93,  64,  55);  // brown
        if (lower.Contains("inconsistent")) return Color.FromArgb(255, 178, 107,   0);  // amber
        if (lower.Contains("well"))         return Color.FromArgb(255,   2, 136, 209);  // teal
        return Color.FromArgb(255, 46, 125, 50);                                         // green (lush)
    }

    private static (string Emoji, string Label, string Description) CropClassInfo(string className)
    {
        var lower = className.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        if (lower.Contains("disease"))      return ("🔴", "Disease",             "Diseased vegetation");
        if (lower.Contains("pest"))         return ("🟣", "Pest",                "Pest infestation");
        if (lower.Contains("soil"))         return ("🟤", "Soil Issues",         "Dry or bare soil");
        if (lower.Contains("inconsistent")) return ("🟡", "Inconsistent Growth", "Stressed / uneven crop");
        if (lower.Contains("well"))         return ("💧", "Well Irrigated",      "Well-watered crop");
        return                                     ("🌿", "Lush Green",          "Healthy vegetation");
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
    /// Updates Analysis Summary panel from actual YOLO detections.
    /// Maps COCO class names to agriculture categories for display.
    /// </summary>
    private void UpdateSummaryFromDetections(List<HarvestmoonGCS.Services.HarvestFunctionalService.HarvestDetectionBox> boxes)
    {
        if (boxes.Count == 0)
        {
            SummaryHealthyCount.Text  = "0%";
            SummaryStressCount.Text   = "0%";
            SummaryDiseaseCount.Text  = "0%";
            SummaryPestCount.Text     = "0%";
            if (SummaryHealthyBar        != null) SummaryHealthyBar.Value        = 0;
            if (SummaryWellIrrigatedBar  != null) SummaryWellIrrigatedBar.Value  = 0;
            if (SummaryStressBar         != null) SummaryStressBar.Value         = 0;
            if (SummarySoilIssuesBar     != null) SummarySoilIssuesBar.Value     = 0;
            if (SummaryDiseaseBar        != null) SummaryDiseaseBar.Value        = 0;
            if (SummaryPestBar           != null) SummaryPestBar.Value           = 0;
            if (SummaryWellIrrigatedCount != null) SummaryWellIrrigatedCount.Text = "0%";
            if (SummarySoilIssuesCount    != null) SummarySoilIssuesCount.Text   = "0%";
            SummaryPriorityText.Text = "Standby";
            if (SummaryDominantText != null) SummaryDominantText.Text = "—";
            return;
        }

        int disease = 0, pest = 0, inconsistent = 0, soil = 0, lushGreen = 0, wellIrrig = 0;
        foreach (var box in boxes.Where(b => b.Confidence * 100 >= _confThreshold))
        {
            switch (box.ClassName.ToLowerInvariant())
            {
                case "disease":             disease++;      break;
                case "pest":                pest++;         break;
                case "inconsistent_growth": inconsistent++; break;
                case "soil_issues":         soil++;         break;
                case "well_irrigated":      wellIrrig++;    break;
                default:                    lushGreen++;    break;  // lush_green + unknown
            }
        }

        int total           = Math.Max(1, lushGreen + wellIrrig + inconsistent + soil + disease + pest);
        var lushGreenPct    = lushGreen   * 100 / total;
        var wellIrrigPct    = wellIrrig   * 100 / total;
        var inconsistentPct = inconsistent * 100 / total;
        var soilPct         = soil        * 100 / total;
        var diseasePct      = disease     * 100 / total;
        var pestPct         = pest        * 100 / total;

        SummaryHealthyCount.Text = $"{lushGreenPct:F0}%";
        SummaryStressCount.Text  = $"{inconsistentPct:F0}%";
        SummaryDiseaseCount.Text = $"{diseasePct:F0}%";
        SummaryPestCount.Text    = $"{pestPct:F0}%";
        if (SummaryWellIrrigatedCount != null) SummaryWellIrrigatedCount.Text = $"{wellIrrigPct:F0}%";
        if (SummarySoilIssuesCount    != null) SummarySoilIssuesCount.Text    = $"{soilPct:F0}%";

        if (SummaryHealthyBar        != null) SummaryHealthyBar.Value        = lushGreenPct;
        if (SummaryWellIrrigatedBar  != null) SummaryWellIrrigatedBar.Value  = wellIrrigPct;
        if (SummaryStressBar         != null) SummaryStressBar.Value         = inconsistentPct;
        if (SummarySoilIssuesBar     != null) SummarySoilIssuesBar.Value     = soilPct;
        if (SummaryDiseaseBar        != null) SummaryDiseaseBar.Value        = diseasePct;
        if (SummaryPestBar           != null) SummaryPestBar.Value           = pestPct;

        var maxHealthy = Math.Max(lushGreenPct, wellIrrigPct);
        var dominant = diseasePct >= inconsistentPct && diseasePct >= maxHealthy
            ? $"Disease {diseasePct:F0}%"
            : inconsistentPct >= maxHealthy
                ? $"Inconsistent Growth {inconsistentPct:F0}%"
                : lushGreenPct >= wellIrrigPct
                    ? $"Lush Green {lushGreenPct:F0}%"
                    : $"Well Irrigated {wellIrrigPct:F0}%";
        if (SummaryDominantText != null) SummaryDominantText.Text = dominant;

        var priority = disease > 0 || pest > 0 ? "High" : inconsistent > 0 ? "Medium" : "Low";
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

    private void OnDashboardCameraStreamingChanged(object? sender, bool isStreaming)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isStreaming)
            {
                try { DashboardVideoStream?.HideOverlay(); } catch { }
            }
            else
            {
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
    private void ImuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _showImuHud = ImuToggleSwitch.IsOn;
        ImuHudPanel.Visibility = _showImuHud ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLayerVisibility()
    {
        VegetationOverlayLayer.Visibility = VegToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        GridOverlayCanvas.Visibility = Visibility.Collapsed;
        HeatmapOverlayLayer.Visibility = Visibility.Collapsed;
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
            // 1. Video — use HSV+YOLO classify stream (Python annotates frames, C# skips double ONNX)
            var videoPath = ResolveDetectedVideoPath() ?? ResolveDerpVideoPath();
            if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath) && _cameraService != null)
            {
                var pythonSvc = _cameraService as PythonCameraService;
                if (pythonSvc != null)
                {
                    // HSV pixel-level stream: much faster than 35-cell YOLO grid
                    var modelPath = PythonCameraService.ResolveHealthModelPath();
                    await _cameraService.StopCameraAsync();
                    await pythonSvc.StartHsvStreamAsync(
                        videoPath,
                        modelPath: modelPath,
                        maxFps: 15f,
                        showOverlay: true,
                        demo: true);
                    _timelineService?.Add("camera", $"HSV detection: {System.IO.Path.GetFileName(videoPath)}", "success");
                }
                else
                {
                    await _cameraService.StopCameraAsync();
                    await _cameraService.StartCameraAsync(videoPath);
                    _timelineService?.Add("camera", $"Camera stream: {System.IO.Path.GetFileName(videoPath)}", "success");
                }
            }

            // 2. AI on (C# ONNX skipped when classify stream is active — Python already annotates frames)
            _aiOn = true;
            if (YoloToggleSwitch != null) YoloToggleSwitch.IsOn = true;

            // 3. Activate geofence — covers 2700m straight transect + 250m lateral buffer
            _geofenceService?.SetGeofenceCenter(-6.8148, 107.6172);
            _geofenceService?.SetGeofenceRadius(1600);
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
                        Sequence    = DemoWaypointStartSequence + i,
                        Latitude    = wpLat,
                        Longitude   = wpLon,
                        Altitude    = 82,
                        Command     = WaypointCommand.Waypoint,
                    });
                }
                // Mirror the geofence into MapViewModel so MapPage draws the boundary
                _mapViewModel.GeofenceCenterLat = -6.8148;
                _mapViewModel.GeofenceCenterLon = 107.6172;
                _mapViewModel.GeofenceRadius    = 1600;
                _mapViewModel.GeofenceType      = GeofenceType.Circular;
                _mapViewModel.IsGeofenceActive  = true;
            }

            // 4b. Center map on Lembang demo area and follow UAV
            DashboardMapControl?.SetCenter(-6.8148, 107.6172, 16);
            DashboardMapControl?.SetFollowVehicle(true);

            // 5. Initial telemetry
            InjectDemoTelemetry(0);

            // 6. Seed timeline
            _timelineService?.Add("connected", "MAVLink connected · Lembang Field B",              "success");
            _timelineService?.Add("tlog",      "Auto TLOG recorder armed",                         "success");
            _timelineService?.Add("armed",     "Armed · AUTO mode · altitude 82m · survey active", "success");
            _timelineService?.Add("waypoint",  "Jalur lurus aktif · 900m E-W · WP 10-13 · Lembang Zone B", "info");
            RenderIncidentTimeline();
            UpdateAlertCenter(_flightViewModel?.Telemetry, true);

            // 7. Reset UI counters
            _lastDetectionCount = 0;
            _lastConfidence = 0;
            DetectionCountText.Text = "0 det";
            FpsHudText.Text = "HSV+YOLO · starting...";
            AddAlertRow("UAV survey active · Lembang Field B · AUTO mode", "info");

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
        _classifyRealData = null;
        _classifyFirstFrameReceived = false;
        _classifyFps = 0;
        _classifyFrameCount = 0;
        _classifyFrameStart = DateTime.MinValue;
        _demoTimer.Stop();
        _demoTimer.Tick -= OnDemoTick;
        _geofenceService?.SetGeofenceActive(false);
        _mavLinkService?.SimulateConnection(false);
        if (_flightViewModel != null) _flightViewModel.IsConnected = false;

        // Restore MapViewModel and map control to idle state
        if (_mapViewModel != null)
        {
            _mapViewModel.Waypoints.Clear();
            _mapViewModel.IsGeofenceActive = false;
        }
        DashboardMapControl?.SetFollowVehicle(false);
        DashboardMapControl?.ClearWaypoints();
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
        int _demoLoopPeriod = (DemoWaypoints.Length - 1) * DemoTicksPerSegment; // 30 steps = full route
        if (_demoStep >= _demoLoopPeriod) _demoStep = 0;
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

        // Roll: banking during turns, visible waggle on straights for IMU animation
        double roll = isTurning
            ? Math.Sign(headingDelta) * 18.0
            : Math.Sin(step * 0.4) * 12.0;

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
            Roll               = roll  * Math.PI / 180.0,
            Pitch              = Math.Sin(step * 0.23) * 8.0 * Math.PI / 180.0,
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
        // DemoWaypoints = WP 10-13, straight east, 300m spacing → 900m transect. 3 segments × 10 ticks = 30s loop.
        switch (step)
        {
            case 2:  _timelineService?.Add("yolo",     "YOLO inference aktif · scan dimulai",                  "success"); break;
            case 5:  _timelineService?.Add("waypoint", "WP 1 → Titik awal · ujung barat lahan",                "success"); break;
            case 10: _timelineService?.Add("yolo",     DemoClassificationEvents[0],                             "info");    break;
            case 15: _timelineService?.Add("waypoint", "WP 3 → Memasuki zona tengah · 600m dari start",        "success"); break;
            case 20: _timelineService?.Add("yolo",     DemoClassificationEvents[1],                             "warning"); break;
            case 25: _timelineService?.Add("waypoint", "WP 5 → Tengah lintasan · 1350m",                       "success"); break;
            case 30: _timelineService?.Add("geofence", "Geofence safe · 250m ke batas (zona aman)",            "info");    break;
            case 35: _timelineService?.Add("yolo",     DemoClassificationEvents[2],                             "info");    break;
            case 40: _timelineService?.Add("waypoint", "WP 7 → 2100m · 78% jalur terselesaikan",               "success"); break;
            case 45: _timelineService?.Add("yolo",     DemoClassificationEvents[3],                             "warning"); break;
            case 50: _timelineService?.Add("waypoint", "WP 9 → Mendekati titik akhir · 2400m",                 "success"); break;
            case 54: _timelineService?.Add("yolo",     DemoClassificationEvents[4],                             "critical"); break;
            case 58: _timelineService?.Add("waypoint", "WP 10 → Jalur selesai · RTL dimulai",                  "success"); break;
            case 62: _timelineService?.Add("tlog",     $"Telemetry batch flushed · {step * 2} packets saved",  "info");    break;
        }
        if (step % 5 == 0 && step > 0) RenderIncidentTimeline();
    }

    // Rotating demo detection frames — proposal-aligned 4-class labels only
    private static readonly (string Class, double Conf)[][] DemoDetectionFrames =
    {
        new[] { ("Healthy", 0.93), ("Healthy", 0.88), ("Stress", 0.79) },
        new[] { ("Healthy", 0.91), ("Stress", 0.82), ("Stress", 0.74) },
        new[] { ("Healthy", 0.96), ("Healthy", 0.90), ("Healthy", 0.85), ("Stress", 0.77) },
        new[] { ("Disease", 0.87), ("Stress", 0.81), ("Healthy", 0.89) },
        new[] { ("Healthy", 0.94), ("Stress", 0.76), ("Stress", 0.71), ("Stress", 0.83) },
        new[] { ("Healthy", 0.92), ("Healthy", 0.88), ("Disease", 0.80) },
        new[] { ("Stress", 0.85), ("Stress", 0.78), ("Healthy", 0.91) },
        new[] { ("Healthy", 0.95), ("Healthy", 0.89), ("Stress", 0.65), ("Stress", 0.72) },
    };

    private void InjectDemoDetectionUI(int step)
    {
        double dHealthy, dStress, dDisease, dPest, avgConf;
        int count;

        // Use REAL Python detection data when classify stream has sent at least one result
        if (_classifyRealData.HasValue)
        {
            var r = _classifyRealData.Value;
            double tot = Math.Max(1, r.Total);
            dHealthy = r.Healthy * 100.0 / tot;
            dStress  = r.Stress  * 100.0 / tot;
            dDisease = r.Disease * 100.0 / tot;
            dPest    = r.Pest    * 100.0 / tot;
            count    = r.Total;
            avgConf  = 0.82 + Math.Sin(step * 0.3) * 0.06; // realistic jitter

            // Build detection rows from real class counts
            DetectionsListStack.Children.Clear();
            if (r.Stress  > 0) DetectionsListStack.Children.Add(BuildCropDetectionRow("Inconsistent Growth", 0.80 + Math.Sin(step * 0.4) * 0.05));
            if (r.Healthy > 0) DetectionsListStack.Children.Add(BuildCropDetectionRow("Lush Green",           0.88 + Math.Sin(step * 0.2) * 0.05));
            if (r.Disease > 0) DetectionsListStack.Children.Add(BuildCropDetectionRow("Disease",              0.76 + Math.Sin(step * 0.5) * 0.04));
            if (r.Pest    > 0) DetectionsListStack.Children.Add(BuildCropDetectionRow("Pest",                 0.72 + Math.Sin(step * 0.6) * 0.03));
        }
        else
        {
            // Fall back to fake cyclic data while Python model is loading
            var frame = DemoDetectionFrames[step % DemoDetectionFrames.Length];
            count   = frame.Length;
            avgConf = frame.Average(f => f.Conf);
            double jitter   = Math.Sin(step * 0.7) * 1.5;
            double rawH = Math.Max(0, 33.8 + jitter);
            double rawS = Math.Max(0, 24.8 - jitter * 0.4);
            double rawD = Math.Max(0, 4.9  + Math.Abs(jitter) * 0.2);
            double rawT = Math.Max(1, rawH + rawS + rawD);
            dHealthy = rawH * 100 / rawT;
            dStress  = rawS * 100 / rawT;
            dDisease = rawD * 100 / rawT;
            dPest    = 0;
            DetectionsListStack.Children.Clear();
            foreach (var (cls, conf) in frame)
                DetectionsListStack.Children.Add(BuildCropDetectionRow(cls, conf));
        }

        _lastDetectionCount = count;
        _lastConfidence = avgConf;

        DetectionCountText.Text  = $"{count} det";
        MissionDetectionsText.Text = ((_demoStep / 2) + count).ToString();
        ConfAvgText.Text         = $"Conf avg {avgConf * 100:F0}%";
        SummaryConfidenceText.Text = $"{avgConf * 100:F0}%";
        DetectionsListTitle.Text = $"Live Detections ({Math.Min(count, 4)})";

        double demoFps;
        if (_classifyFps > 1.0)
            demoFps = _classifyFps + Math.Sin(step * 0.4) * 0.5;          // real measured FPS ±0.5
        else if (_classifyRealData.HasValue)
            demoFps = 10.0 + Math.Abs(Math.Sin(step * 0.4)) * 3.0;        // HSV stream ~10-13 FPS
        else
            demoFps = 12.0 + Math.Abs(Math.Sin(step * 0.4)) * 2.0;        // placeholder ~12-14 FPS
        FpsHudText.Text = $"HSV+YOLO · {demoFps:F0} FPS";

        // Split healthy into Lush Green (60%) + Well Irrigated (40%) for display
        var dLushGreen    = dHealthy * 0.6;
        var dWellIrrig    = dHealthy * 0.4;
        // Treat stress as Inconsistent Growth; disease stays; soil issues ~small residual
        var dSoilIssues   = Math.Max(0, 100 - dHealthy - dStress - dDisease - dPest) * 0.5;

        if (SummaryHealthyCount       != null) SummaryHealthyCount.Text       = $"{dLushGreen:F0}%";
        if (SummaryWellIrrigatedCount != null) SummaryWellIrrigatedCount.Text = $"{dWellIrrig:F0}%";
        if (SummaryStressCount        != null) SummaryStressCount.Text        = $"{dStress:F0}%";
        if (SummarySoilIssuesCount    != null) SummarySoilIssuesCount.Text    = $"{dSoilIssues:F0}%";
        if (SummaryDiseaseCount       != null) SummaryDiseaseCount.Text       = $"{dDisease:F0}%";
        if (SummaryPestCount          != null) SummaryPestCount.Text          = $"{dPest:F0}%";
        if (SummaryHealthyBar        != null) SummaryHealthyBar.Value        = dLushGreen;
        if (SummaryWellIrrigatedBar  != null) SummaryWellIrrigatedBar.Value  = dWellIrrig;
        if (SummaryStressBar         != null) SummaryStressBar.Value         = dStress;
        if (SummarySoilIssuesBar     != null) SummarySoilIssuesBar.Value     = dSoilIssues;
        if (SummaryDiseaseBar        != null) SummaryDiseaseBar.Value        = dDisease;
        if (SummaryPestBar           != null) SummaryPestBar.Value           = dPest;

        var dDominant = dLushGreen >= dStress && dLushGreen >= dDisease
            ? $"Lush Green {dLushGreen:F0}%"
            : dStress >= dDisease
                ? $"Inconsistent Growth {dStress:F0}%"
                : $"Disease {dDisease:F0}%";
        if (SummaryDominantText != null) SummaryDominantText.Text = dDominant;

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
                new TextBlock { Text = running ? "Stop Detection" : "Start Detection" }
            }
        };
    }

    private static string? ResolveDetectedVideoPath()
    {
        var candidates = new[]
        {
            "/home/fawwazfa/Program/Harvestmoon/derr.mp4",
            "/home/fawwazfa/Program/Harvestmoon/test_program/hsvv.mp4",
            "/home/fawwazfa/Program/Harvestmoon/test_program/moonharvest_output/hsvv_annotated.mp4",
            "/home/fawwazfa/Program/Harvestmoon/runs/uav_detection/derr_detected.mp4",
            Path.Combine(AppContext.BaseDirectory, "derr_detected.mp4"),
        };
        return candidates.FirstOrDefault(p => File.Exists(p) && new FileInfo(p).Length > 10_000);
    }

    private static (double Lat, double Lon)[] GenerateStraightLineWaypoints(
        double centerLat, double centerLon, int count, double totalMeters, double bearingDeg)
    {
        const double mPerDegLat = 111320.0;
        double mPerDegLon = mPerDegLat * Math.Cos(centerLat * Math.PI / 180);
        double spacingMeters = totalMeters / (count - 1);
        double bearingRad = bearingDeg * Math.PI / 180;
        double dLat = spacingMeters * Math.Cos(bearingRad) / mPerDegLat;
        double dLon = spacingMeters * Math.Sin(bearingRad) / mPerDegLon;
        double startLat = centerLat - dLat * (count - 1) / 2.0;
        double startLon = centerLon - dLon * (count - 1) / 2.0;
        var wps = new (double, double)[count];
        for (int i = 0; i < count; i++)
            wps[i] = (startLat + i * dLat, startLon + i * dLon);
        return wps;
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
            _pitch = Math.Clamp(_pitch + (rnd.NextDouble() - 0.5) * 10, -25, 25);
            _roll  = Math.Clamp(_roll  + (rnd.NextDouble() - 0.5) * 14, -35, 35);
            _yaw   = (_yaw + (rnd.NextDouble() - 0.5) * 6 + 360) % 360;
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
        // IncidentTimelineStack removed — log is now inside Mission Log panel
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
        SummaryStressCount.Text  = "0%";
        SummaryDiseaseCount.Text = "0%";
        SummaryPestCount.Text    = "0%";
        if (SummaryWellIrrigatedCount != null) SummaryWellIrrigatedCount.Text = "0%";
        if (SummarySoilIssuesCount    != null) SummarySoilIssuesCount.Text    = "0%";
        SummaryPriorityText.Text   = "Standby";
        SummaryConfidenceText.Text = "0%";
        if (SummaryDominantText      != null) SummaryDominantText.Text      = "—";
        if (SummaryHealthyBar        != null) SummaryHealthyBar.Value        = 0;
        if (SummaryWellIrrigatedBar  != null) SummaryWellIrrigatedBar.Value  = 0;
        if (SummaryStressBar         != null) SummaryStressBar.Value         = 0;
        if (SummarySoilIssuesBar     != null) SummarySoilIssuesBar.Value     = 0;
        if (SummaryDiseaseBar        != null) SummaryDiseaseBar.Value        = 0;
        if (SummaryPestBar           != null) SummaryPestBar.Value           = 0;
    }

    private static bool IsValidCoordinate(double lat, double lon)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon)) return false;
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return false;
        return Math.Abs(lat) > 0.000001 || Math.Abs(lon) > 0.000001;
    }

    private void UpdateTopBarAiFps(double fps)
    {
        // FPS shown in video HUD only (FpsHudText) — top bar FPS removed to avoid duplication
    }
}
