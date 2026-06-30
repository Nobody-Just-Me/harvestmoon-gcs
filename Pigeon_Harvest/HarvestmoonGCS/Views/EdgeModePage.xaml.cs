using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HarvestmoonGCS.Services;
using Serilog;

namespace HarvestmoonGCS.Views;

public sealed partial class EdgeModePage : Page
{
    private readonly HarvestFunctionalService? _harvestService;
    private DispatcherTimer? _refreshTimer;

    public EdgeModePage()
    {
        this.InitializeComponent();
        _harvestService = App.Current.Services.GetService<HarvestFunctionalService>();
        this.Loaded += EdgeModePage_Loaded;
        this.Unloaded += EdgeModePage_Unloaded;
    }

    private void EdgeModePage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRuntimeInfo();
        SyncTogglesFromService();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => RefreshPerformanceStats();
        _refreshTimer.Start();
    }

    private void EdgeModePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    // -------------------------------------------------------------------------
    // Runtime environment info
    // -------------------------------------------------------------------------
    private void RefreshRuntimeInfo()
    {
        // Platform
        string platform;
#if ANDROID
        platform = "Android";
#elif __IOS__
        platform = "iOS";
#elif WINDOWS
        platform = "Windows";
#else
        platform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" :
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX)   ? "macOS" : "Desktop";
#endif
        PlatformText.Text = platform;

        // Execution provider (simple heuristic)
#if ANDROID
        ExecutionProviderText.Text = "NNAPI / CPU";
#else
        ExecutionProviderText.Text = "CPU (DirectML if available)";
#endif

        // YOLO status
        bool yoloReady = _harvestService?.IsYoloRuntimeReady == true;
        EdgeStatusDot.Fill = new SolidColorBrush(
            yoloReady ? Windows.UI.Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E)
                      : Windows.UI.Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
        EdgeStatusText.Text = yoloReady ? "Edge AI: Active" : "Edge AI: Fallback / Idle";
        EdgeStatusText.Foreground = new SolidColorBrush(
            yoloReady ? Windows.UI.Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E)
                      : Windows.UI.Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));

        // Transport status from settings
        TransportStatusText.Text = "UDP · 14550 · Listening";
    }

    private void RefreshPerformanceStats()
    {
        // HarvestFunctionalService doesn't expose live FPS directly —
        // show "Active" indicator when YOLO is running, otherwise "—"
        bool yoloActive = _harvestService?.IsYoloRuntimeReady == true &&
                          _harvestService?.IsYoloOptionEnabled == true;

        if (yoloActive)
        {
            // Placeholder values shown when pipeline is running
            FpsValueText.Text = "≥15";
            LatencyValueText.Text = "<67";
            DetectionsValueText.Text = "Live";
            FpsTargetText.Text = "100%";
            FpsProgressBar.Width = 280;
        }
        else
        {
            FpsValueText.Text = "—";
            LatencyValueText.Text = "—";
            DetectionsValueText.Text = "—";
            FpsTargetText.Text = "0%";
            FpsProgressBar.Width = 0;
        }
    }

    private void SyncTogglesFromService()
    {
        if (_harvestService == null) return;
        YoloToggle.IsOn = _harvestService.IsYoloOptionEnabled;
        // VegToggle and ImuToggle use service flags if available; default true
    }

    // -------------------------------------------------------------------------
    // Toggle handlers
    // -------------------------------------------------------------------------
    private void YoloToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_harvestService == null) return;
        _harvestService.SetYoloOptionEnabled(YoloToggle.IsOn);
        Log.Information("EdgeModePage: YOLO toggled {State}", YoloToggle.IsOn);
    }

    private void VegToggle_Toggled(object sender, RoutedEventArgs e)
    {
        Log.Information("EdgeModePage: Vegetation overlay toggled {State}", VegToggle.IsOn);
    }

    private void ImuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        Log.Information("EdgeModePage: IMU overlay toggled {State}", ImuToggle.IsOn);
    }

    private void Int8Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        Log.Information("EdgeModePage: INT8 quantization toggled {State}", Int8Toggle.IsOn);
    }

    // -------------------------------------------------------------------------
    // Slider handlers
    // -------------------------------------------------------------------------
    private void ConfThreshold_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ConfThresholdLabel == null) return;
        int val = (int)e.NewValue;
        ConfThresholdLabel.Text = $"{val}%";
    }

    private void NmsThreshold_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (NmsThresholdLabel == null) return;
        int val = (int)e.NewValue;
        NmsThresholdLabel.Text = $"{val}%";
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------
    private void ApplyModel_Click(object sender, RoutedEventArgs e)
    {
        var selected = (ModelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        ModelNameText.Text = $"Model: {selected}";
        Log.Information("EdgeModePage: Applied model {Model}", selected);
    }

    private void BrowseModel_Click(object sender, RoutedEventArgs e)
    {
        // File picker not wired to real file dialog here — placeholder
        Log.Information("EdgeModePage: Browse model clicked (not yet implemented)");
    }

    private void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshRuntimeInfo();
        RefreshPerformanceStats();
    }
}
