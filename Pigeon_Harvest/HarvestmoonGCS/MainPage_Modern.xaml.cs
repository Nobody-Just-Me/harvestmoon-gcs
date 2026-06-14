using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HarvestmoonGCS.ViewModels;
using HarvestmoonGCS.Views;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Helpers;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.Controls;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI;
using Windows.Storage;
using Serilog;
using System.Collections.Generic;
using System.Linq;
#if ANDROID
using Android.Content.Res;
using AndroidApp = Android.App.Application;
using AndroidOrientation = Android.Content.Res.Orientation;
#endif

namespace HarvestmoonGCS;

public sealed partial class MainPage_Modern : Page
{
    private MainViewModel? _viewModel;
    private readonly IMavLinkService? _mavLinkService;
    private readonly ILocalizationService? _localizationService;
    private readonly PageCacheManager? _pageCacheManager;
    private DateTime _lastTopBarTelemetryUpdate = DateTime.MinValue;
    private const int TopBarUpdateIntervalMs = 200;
    private (double Battery, int Sats, string Mode, bool IsArmed)? _lastTopBarState;
    private bool? _lastConnectionStatus;
    private bool _isNavigating;
    private string? _currentTarget;
    private string? _pendingNavigationTarget;
    private readonly ObservabilityService? _observabilityService;
    private DispatcherTimer? _observabilityOverlayTimer;
    private DispatcherTimer? _navigationPreloadTimer;
    private readonly Queue<Type> _navigationPreloadQueue = new();
    private bool _navigationPreloadStarted;
    private const double DefaultSidebarWidth = 232.0;
    private const double CompactSidebarWidth = 156.0;
    private const double VeryCompactSidebarWidth = 132.0;
    private const double OverlayMarginRight = 16.0;
    private const double OverlayMinimumWidth = 220.0;
    private const double OverlayMaximumWidth = 440.0;
#if !__WASM__
    private readonly ChatViewModel? _chatViewModel;
#endif

    public MainPage_Modern()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Modern_Loaded;
        this.Unloaded += MainPage_Modern_Unloaded;

        try
        {
            _viewModel = App.Current.Services.GetService<MainViewModel>();
            DataContext = _viewModel;

            _localizationService = App.Current.Services.GetService<ILocalizationService>();
            _pageCacheManager = App.Current.Services.GetService<PageCacheManager>();
            _mavLinkService = App.Current.Services.GetService<IMavLinkService>();
            _observabilityService = App.Current.Services.GetService<ObservabilityService>();
            App.Current.Services.GetService<MissionLoggingService>();
            if (_mavLinkService != null)
            {
                _mavLinkService.TelemetryReceived += OnTelemetryReceived;
                _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
            }

#if !__WASM__
            _chatViewModel = App.Current.Services.GetService<ChatViewModel>();
            if (PIAPanelOverlay != null && _chatViewModel != null)
            {
                PIAPanelOverlay.ViewModel = _chatViewModel;
            }
            SubscribeToChatViewModel();
#endif

            InitializeObservabilityOverlay();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MainPage_Modern constructor failed");
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyResponsiveLayout();
            NavigateToPage("Dashboard");
            UpdateTopBarForTarget("Dashboard");
            StartNavigationPreload();
        });
    }

    private void MainPage_Modern_Loaded(object sender, RoutedEventArgs e)
    {
        this.SizeChanged -= MainPage_Modern_SizeChanged;
        this.SizeChanged += MainPage_Modern_SizeChanged;

        ApplyResponsiveLayout();
        // Navigate to initial page
        NavigateToPage("Dashboard");
        UpdateTopBarForTarget("Dashboard");
        StartNavigationPreload();

#if !__WASM__
        // Subscribe to PIA button click from sidebar
        Sidebar.PIAButtonClicked -= OnPIAButtonClicked;
        Sidebar.PIAButtonClicked += OnPIAButtonClicked;
        if (PIAPanelOverlay != null)
        {
            PIAPanelOverlay.SettingsRequested -= OnPIASettingsRequested;
            PIAPanelOverlay.SettingsRequested += OnPIASettingsRequested;
        }
#endif
    }

    private void MainPage_Modern_Unloaded(object sender, RoutedEventArgs e)
    {
        this.SizeChanged -= MainPage_Modern_SizeChanged;

        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived -= OnTelemetryReceived;
            _mavLinkService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }

        if (_observabilityOverlayTimer != null)
        {
            _observabilityOverlayTimer.Stop();
            _observabilityOverlayTimer.Tick -= OnObservabilityOverlayTick;
            _observabilityOverlayTimer = null;
        }

        if (_navigationPreloadTimer != null)
        {
            _navigationPreloadTimer.Stop();
            _navigationPreloadTimer.Tick -= OnNavigationPreloadTick;
            _navigationPreloadTimer = null;
        }

#if !__WASM__
        Sidebar.PIAButtonClicked -= OnPIAButtonClicked;
        if (PIAPanelOverlay != null)
        {
            PIAPanelOverlay.SettingsRequested -= OnPIASettingsRequested;
        }
        UnsubscribeFromChatViewModel();
#endif
    }

#if !__WASM__
    private void SubscribeToChatViewModel()
    {
        if (_chatViewModel == null)
        {
            return;
        }

        _chatViewModel.PropertyChanged -= OnChatViewModelPropertyChanged;
        _chatViewModel.PropertyChanged += OnChatViewModelPropertyChanged;
        UpdateTopBarVoiceIndicator();
    }

    private void UnsubscribeFromChatViewModel()
    {
        if (_chatViewModel == null)
        {
            return;
        }

        _chatViewModel.PropertyChanged -= OnChatViewModelPropertyChanged;
    }

    private void OnChatViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.IsListening) ||
            e.PropertyName == nameof(ChatViewModel.VoiceExecutionStatus) ||
            e.PropertyName == nameof(ChatViewModel.VoiceAvailabilityStatus))
        {
            UpdateTopBarVoiceIndicator();
        }
    }

    private void UpdateTopBarVoiceIndicator()
    {
        if (_chatViewModel == null || TopBar == null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            TopBar.IsVoiceListening = _chatViewModel.IsListening;

            var nextStatus = _chatViewModel.IsListening
                ? "listening"
                : !string.IsNullOrWhiteSpace(_chatViewModel.VoiceExecutionStatus)
                    ? _chatViewModel.VoiceExecutionStatus
                    : _chatViewModel.VoiceAvailabilityStatus;

            TopBar.VoiceStatus = nextStatus ?? "idle";
        });
    }

    private void OnPIASettingsRequested(object? sender, EventArgs e)
    {
        if (PIAPanelOverlay == null)
        {
            return;
        }

        PIAPanelOverlay.Close();
        PIAPanelOverlay.Visibility = Visibility.Collapsed;

        if (ContentFrame.Content?.GetType() == typeof(AISettingsPage))
        {
            _currentTarget = "AISettings";
            return;
        }

        var navigated = ContentFrame.Navigate(typeof(AISettingsPage));
        if (navigated)
        {
            _currentTarget = "AISettings";
        }
    }

    private void OnPIAButtonClicked(object? sender, EventArgs e)
    {
        if (PIAPanelOverlay == null || _chatViewModel == null)
            return;

        if (PIAPanelOverlay.Visibility == Visibility.Visible)
        {
            PIAPanelOverlay.Close();
            PIAPanelOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            PIAPanelOverlay.Visibility = Visibility.Visible;
            PIAPanelOverlay.Open();
        }
    }
#endif

    private void MainPage_Modern_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (Sidebar == null)
        {
            return;
        }

        double pageWidth = ActualWidth;
        if (double.IsNaN(pageWidth) || pageWidth <= 0)
        {
            if (XamlRoot?.Size.Width is > 0)
            {
                pageWidth = XamlRoot.Size.Width;
            }
            else
            {
                return;
            }
        }

        bool useCompactLayout = IsAndroidPortrait() || pageWidth < 960;
        bool useVeryCompactLayout = IsAndroidDevice() || pageWidth < 760;

        double sidebarWidth = useVeryCompactLayout
            ? VeryCompactSidebarWidth
            : useCompactLayout ? CompactSidebarWidth : DefaultSidebarWidth;
        Sidebar.Width = sidebarWidth;

        if (Sidebar.Content is FrameworkElement sidebarRoot)
        {
            sidebarRoot.Width = sidebarWidth;
        }

        if (ObservabilityOverlay != null)
        {
            double availableOverlayWidth = pageWidth - sidebarWidth - (OverlayMarginRight * 2);
            double overlayWidth = Math.Clamp(availableOverlayWidth, OverlayMinimumWidth, OverlayMaximumWidth);
            ObservabilityOverlay.Width = overlayWidth;
        }

        if (useCompactLayout && ObservabilityOverlay?.Visibility == Visibility.Visible)
        {
            ObservabilityOverlay.Visibility = Visibility.Collapsed;
            _observabilityOverlayTimer?.Stop();
        }
    }

    private static bool IsAndroidPortrait()
    {
#if ANDROID
        var orientation = AndroidApp.Context?.Resources?.Configuration?.Orientation;
        return orientation == AndroidOrientation.Portrait;
#else
        return false;
#endif
    }

    private static bool IsAndroidDevice()
    {
#if ANDROID
        return true;
#else
        return false;
#endif
    }

    private void Sidebar_NavigationRequested(object sender, string target)
    {
        if (string.Equals(target, "Connection", StringComparison.Ordinal))
        {
            _ = ShowConnectDialogAsync();
            return;
        }

        NavigateToPage(target);
    }

    private void NavigateToPage(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        Type? pageType = ResolvePageType(target);
        if (pageType == null)
        {
            return;
        }

        if (_isNavigating)
        {
            _pendingNavigationTarget = target;
            return;
        }

        if (string.Equals(_currentTarget, target, StringComparison.Ordinal) && ContentFrame.Content?.GetType() == pageType)
        {
            UpdateTopBarForTarget(target);
            return;
        }

        _isNavigating = true;
        bool forceFrameNavigation = target is "ClassicMap" or "Map";
        bool pageAlreadyCached = !forceFrameNavigation && _pageCacheManager?.IsCached(pageType) == true;
        bool useLoadingOverlay = target is "ClassicMap" or "Map" or "TlogPlayer" && !pageAlreadyCached;
        if (useLoadingOverlay)
        {
            MainLoadingIndicator.IsLoading = true;
            MainLoadingIndicator.LoadingText = $"Loading {target}...";
        }

        try
        {
            if (ContentFrame.Content?.GetType() == pageType)
            {
                _currentTarget = target;
                UpdateTopBarForTarget(target);
                return;
            }

            Page? pageInstance = null;
            if (_pageCacheManager != null && !forceFrameNavigation)
            {
                pageInstance = _pageCacheManager.GetOrCreatePage(pageType);
                ContentFrame.Content = pageInstance;
                ActivatePage(pageInstance);
            }
            else
            {
                if (forceFrameNavigation)
                {
                    pageInstance = Activator.CreateInstance(pageType) as Page;
                    if (pageInstance == null)
                    {
                        throw new InvalidOperationException($"Could not create page instance for {pageType.FullName}");
                    }

                    ContentFrame.Content = pageInstance;
                    ActivatePage(pageInstance);
                }
                else
                {
                    bool navigated = ContentFrame.Navigate(pageType);
                    if (!navigated)
                    {
                        throw new InvalidOperationException($"Frame navigation returned false for {pageType.FullName}");
                    }

                    if (ContentFrame.Content is Page navigatedPage)
                    {
                        ActivatePage(navigatedPage);
                    }
                }
            }

            Log.Debug("MainPage_Modern: navigated to {PageType}", pageType.Name);
            _currentTarget = target;
            UpdateTopBarForTarget(target);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Navigation failed for target: {Target}", target);
#if __ANDROID__
            Android.Util.Log.Error("PigeonGCS", $"Navigation FAILED for {target}: {ex}");
#endif
        }
        finally
        {
            _isNavigating = false;
            if (useLoadingOverlay)
            {
                MainLoadingIndicator.IsLoading = false;
            }

            if (!string.IsNullOrEmpty(_pendingNavigationTarget) &&
                !string.Equals(_pendingNavigationTarget, _currentTarget, StringComparison.Ordinal))
            {
                var pending = _pendingNavigationTarget;
                _pendingNavigationTarget = null;
                DispatcherQueue.TryEnqueue(() => NavigateToPage(pending));
            }
            else
            {
                _pendingNavigationTarget = null;
            }
        }
    }

    private void StartNavigationPreload()
    {
        if (_navigationPreloadStarted || _pageCacheManager == null)
        {
            return;
        }

        _navigationPreloadStarted = true;
        foreach (var pageType in GetPreloadPageTypes())
        {
            if (!_pageCacheManager.IsCached(pageType))
            {
                _navigationPreloadQueue.Enqueue(pageType);
            }
        }

        if (_navigationPreloadQueue.Count == 0)
        {
            return;
        }

        _navigationPreloadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _navigationPreloadTimer.Tick += OnNavigationPreloadTick;
        _navigationPreloadTimer.Start();
    }

    private void OnNavigationPreloadTick(object? sender, object e)
    {
        if (_pageCacheManager == null)
        {
            _navigationPreloadTimer?.Stop();
            return;
        }

        if (_isNavigating)
        {
            return;
        }

        while (_navigationPreloadQueue.Count > 0)
        {
            var pageType = _navigationPreloadQueue.Dequeue();
            if (_pageCacheManager.IsCached(pageType))
            {
                continue;
            }

            try
            {
                _pageCacheManager.GetOrCreatePage(pageType);
                Log.Debug("Preloaded navigation page {PageType}", pageType.Name);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to preload navigation page {PageType}", pageType.Name);
            }

            break;
        }

        if (_navigationPreloadQueue.Count == 0)
        {
            _navigationPreloadTimer?.Stop();
        }
    }

    private static IEnumerable<Type> GetPreloadPageTypes()
    {
        yield return typeof(DashboardPage);
        yield return typeof(CameraPage);
        yield return typeof(MissionPlannerPage);
        yield return typeof(StatsPage);
        yield return typeof(AIHarvestPage);
        yield return typeof(AISettingsPage);
        yield return typeof(ReportsHarvestPage);
        yield return typeof(SettingsPage);
    }

    private static Type? ResolvePageType(string target)
    {
        return target switch
        {
            "Dashboard" => typeof(DashboardPage),
            "Camera" => typeof(CameraPage),
            "Map" => typeof(MapPage),
            "MissionPlanner" => typeof(MissionPlannerPage),
            "Stats" => typeof(StatsPage),
            "AIHarvest" => typeof(AIHarvestPage),
            "AISettings" => typeof(AISettingsPage),
            "Tlog" => typeof(ReportsHarvestPage),
            "LoRa" => typeof(LoRaPage),
            "Settings" => typeof(SettingsPage),
            "FlightClassic" => typeof(FlightPage),
            "ClassicMap" => typeof(MapPage),
            "Calibrate" => typeof(CalibrationPage),
            "TlogPlayer" => typeof(TlogPage),
            "Parameter" => typeof(ParameterPage),
            "Diagnostics" => typeof(DiagnosticsPage),
            "Theme" => typeof(ThemePage),
            "Flight" => typeof(DashboardPage),
            _ => null
        };
    }

    private static void ActivatePage(Page page)
    {
        if (page is DashboardPage dashboardPage)
        {
            dashboardPage.OnPageActivated();
            return;
        }

        if (page is MapPage mapPage)
        {
            mapPage.OnPageActivated();
            return;
        }

        if (page is LoRaPage loRaPage)
        {
            loRaPage.OnPageActivated();
        }
    }

    private void UpdateTopBarForTarget(string target)
    {
        switch (target)
        {
            case "Dashboard":
            case "Flight":
                TopBar.UpdatePageTitle("Dashboard", "/ Live Operations · Field Site Alpha", "\uE80F");
                break;
            case "Map":
                TopBar.UpdatePageTitle("Map", "/ Full Map · Waypoints · Geofence · Offline Tiles", "\uE707");
                break;
            case "MissionPlanner":
                TopBar.UpdatePageTitle("Mission Planner", "/ Field Sector B · Bandung", "\uE707");
                break;
            case "Camera":
                TopBar.UpdatePageTitle("Camera", "/ Live UAV Capture", "\uE714");
                break;
            case "Stats":
                TopBar.UpdatePageTitle("Crop Analysis", "/ AI Monitoring · Real-time", "\uE9D9");
                break;
            case "AIHarvest":
                TopBar.UpdatePageTitle("AI Vision", "/ Zero-Internet Edge AI · YOLOv8n ONNX", "\uE950");
                break;
            case "AISettings":
                TopBar.UpdatePageTitle("AI Settings", "/ Diagnostics & Models", "\uE7C1");
                break;
            case "Tlog":
                TopBar.UpdatePageTitle("Reports", "/ Field Sector B · Bandung", "\uE8A5");
                break;
            case "Settings":
                TopBar.UpdatePageTitle("Settings", "/ System Preferences", "\uE713");
                break;
            case "FlightClassic":
                TopBar.UpdatePageTitle("Flight Instruments", "/ Pigeon avionics & MAVLink", "\uE709");
                break;
            case "ClassicMap":
                TopBar.UpdatePageTitle("Classic Map", "/ Pigeon map tools", "\uE707");
                break;
            case "Calibrate":
                TopBar.UpdatePageTitle("Calibration", "/ Sensor, radio, ESC, servo", "\uE713");
                break;
            case "TlogPlayer":
                TopBar.UpdatePageTitle("TLOG Player", "/ Telemetry playback", "\uE102");
                break;
            case "LoRa":
                TopBar.UpdatePageTitle("LoRa Relay", "/ Long-range field telemetry", "\uE704");
                break;
            case "Parameter":
                TopBar.UpdatePageTitle("Parameters", "/ Vehicle configuration", "\uE9D9");
                break;
            case "Diagnostics":
                TopBar.UpdatePageTitle("Diagnostics", "/ Runtime health", "\uE7BA");
                break;
            case "Theme":
                TopBar.UpdatePageTitle("Theme", "/ Layout and visual preferences", "\uE771");
                break;
            default:
                TopBar.UpdatePageTitle("Dashboard", "/ Live Operations", "\uE80F");
                break;
        }
    }

    private async void TopBar_ExitClicked(object sender, EventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Exit Application",
            Content = "Are you sure you want to exit?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Application.Current.Exit();
        }
    }

    private async void TopBar_ConnectClicked(object sender, EventArgs e)
    {
        Log.Information("Connect clicked on TopBar");
        await ShowConnectDialogAsync();
    }

    private async Task ShowConnectDialogAsync()
    {
        try
        {
            MainLoadingIndicator.IsLoading = false;
            var dialog = new ConnectDialog(_mavLinkService)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show ConnectDialog");
        }
    }
    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        if (_lastConnectionStatus.HasValue && _lastConnectionStatus.Value == isConnected)
        {
            return;
        }
        _lastConnectionStatus = isConnected;
        DispatcherQueue.TryEnqueue(() =>
        {
            var nextStatus = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            if (TopBar.Status != nextStatus)
            {
                TopBar.Status = nextStatus;
                _observabilityService?.Track("topbar.status.update");
            }
        });
    }

    private void OnTelemetryReceived(object? sender, HarvestmoonGCS.Models.FlightData data)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTopBarTelemetryUpdate).TotalMilliseconds < TopBarUpdateIntervalMs)
        {
            return;
        }

        _observabilityService?.Track("topbar.telemetry.ingest");

        // Calculate battery percentage (assuming 4S LiPo: 16.8V full, 14.0V empty)
        float voltage = data.MavlinkMiliVolt / 1000.0f;
        double batteryPercent = (voltage - 14.0f) / (16.8f - 14.0f) * 100.0;
        batteryPercent = Math.Clamp(batteryPercent, 0.0, 100.0);
        int sats = data.GPS.Sats;
        string mode = data.FlightMode.ToString().ToUpperInvariant();
        bool isArmed = data.FlightMode != HarvestmoonGCS.Core.Models.FlightMode.DISARMED;

        var newState = (Battery: batteryPercent, Sats: sats, Mode: mode, IsArmed: isArmed);
        if (_lastTopBarState.HasValue)
        {
            var previous = _lastTopBarState.Value;
            if (Math.Abs(previous.Battery - newState.Battery) < 0.05 &&
                previous.Sats == newState.Sats &&
                previous.Mode == newState.Mode &&
                previous.IsArmed == newState.IsArmed)
            {
                return;
            }
        }

        _lastTopBarTelemetryUpdate = now;
        _lastTopBarState = newState;

        DispatcherQueue.TryEnqueue(() =>
        {
            _observabilityService?.Track("topbar.telemetry.render");
            TopBar.Battery = batteryPercent;
            TopBar.GpsSats = sats;
            TopBar.Mode = mode;
            TopBar.IsArmed = isArmed;
        });
    }

    private void InitializeObservabilityOverlay()
    {
        if (_observabilityService == null)
        {
            return;
        }

        _observabilityOverlayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _observabilityOverlayTimer.Tick += OnObservabilityOverlayTick;
    }

    private void OnObservabilityOverlayTick(object? sender, object e)
    {
        if (_observabilityService == null || ObservabilityText == null || ObservabilityOverlay == null)
        {
            return;
        }

        if (ObservabilityOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        var snapshot = _observabilityService.GetSnapshot();
        if (snapshot.Count == 0)
        {
            ObservabilityText.Text = "Waiting for telemetry...";
            return;
        }

        static string FormatLine(ObservabilityService.CounterSnapshot value)
            => $"{value.Key,-26} {value.RatePerSecond,6:F1}/s   total {value.Total}";

        var preferredOrder = new[]
        {
            "mavlink.telemetry.ingest",
            "mavlink.telemetry.dispatch",
            "realtime.telemetry.ingest",
            "realtime.telemetry.dispatch",
            "flight.telemetry.apply",
            "flight.map.render",
            "flight.camera.render",
            "stats.chart.flush",
            "map.vehicle.render",
            "tlog.map.render",
            "topbar.telemetry.render",
            "topbar.status.update"
        };

        var lines = new List<string>
        {
            "OBSERVABILITY (rate/s)",
            "----------------------"
        };

        foreach (var key in preferredOrder)
        {
            if (snapshot.TryGetValue(key, out var value))
            {
                lines.Add(FormatLine(value));
            }
        }

        foreach (var value in snapshot.Values.OrderBy(v => v.Key))
        {
            if (preferredOrder.Contains(value.Key, StringComparer.Ordinal))
            {
                continue;
            }

            lines.Add(FormatLine(value));
        }

        ObservabilityText.Text = string.Join(Environment.NewLine, lines);
    }

    private void ToggleObservabilityOverlay(object sender, RoutedEventArgs e)
    {
        if (ObservabilityOverlay == null)
        {
            return;
        }

        var show = ObservabilityOverlay.Visibility != Visibility.Visible;
        ObservabilityOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (_observabilityOverlayTimer != null)
        {
            if (show)
            {
                _observabilityOverlayTimer.Start();
                OnObservabilityOverlayTick(this, EventArgs.Empty);
            }
            else
            {
                _observabilityOverlayTimer.Stop();
            }
        }

    }
}
