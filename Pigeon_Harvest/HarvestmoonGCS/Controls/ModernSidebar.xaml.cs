using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Services;
using Serilog;
using IThemeService = HarvestmoonGCS.Core.Services.IThemeService;

namespace HarvestmoonGCS.Controls;

public sealed partial class ModernSidebar : UserControl
{
    private readonly IThemeService _themeService;
    private readonly HarvestFunctionalService? _harvestFunctionalService;
    private Button? _currentActiveButton;
    private string? _currentTarget = "Dashboard";

    public event EventHandler<string>? NavigationRequested;
    public event EventHandler? PIAButtonClicked;

    public ModernSidebar()
    {
        this.InitializeComponent();

        _themeService = App.GetService<IThemeService>() ??
                        throw new InvalidOperationException("IThemeService is not registered in DI container.");
        _harvestFunctionalService = App.Current.Services.GetService<HarvestFunctionalService>();

        this.Loaded += ModernSidebar_Loaded;
        this.Unloaded += ModernSidebar_Unloaded;
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    private void ModernSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        // PulseAnimation dinonaktifkan untuk meningkatkan performance
        // PulseAnimation.Begin();
        
        SetActiveButton(FlightNavButton);

        ApplyThemeToggleVisualState();
        ApplyYoloOptionVisualState();
        if (_harvestFunctionalService != null)
        {
            _harvestFunctionalService.YoloOptionChanged += OnYoloOptionChanged;
        }
        _themeService.ThemeChanged += OnThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        // Re-apply active button styles when theme changes
        if (_currentActiveButton != null)
        {
            RefreshActiveButtonStyle(_currentActiveButton);
        }
    }

    private void RefreshActiveButtonStyle(Button button)
    {
        // Re-apply theme-aware colors
        button.Background = (Brush)Application.Current.Resources["SidebarSelectedBrush"];
        Brush activeBorderBrush = GetActiveBorderBrushForButton(button);
        button.BorderBrush = activeBorderBrush;
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var target = button.Tag as string;
            if (!string.IsNullOrEmpty(target))
            {
                _currentTarget = target;
                SetActiveButton(button);
                NavigationRequested?.Invoke(this, target);
            }
        }
    }

#if !__WASM__
    private void PIAButton_Click(object sender, RoutedEventArgs e)
    {
        PIAButtonClicked?.Invoke(this, EventArgs.Empty);
    }
#endif

    private void YoloOption_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_harvestFunctionalService == null)
        {
            return;
        }

        _harvestFunctionalService.SetYoloOptionEnabled(!_harvestFunctionalService.IsYoloOptionEnabled);
        ApplyYoloOptionVisualState();
        Log.Information("YOLO option toggled from sidebar: {Enabled}", _harvestFunctionalService.IsYoloOptionEnabled);
    }

    private void OnYoloOptionChanged(object? sender, bool enabled)
    {
        DispatcherQueue.TryEnqueue(ApplyYoloOptionVisualState);
    }

    private void ApplyYoloOptionVisualState()
    {
        bool enabled = _harvestFunctionalService?.IsYoloOptionEnabled == true;
        bool ready = _harvestFunctionalService?.IsYoloRuntimeReady == true;
        YoloOptionStatusDot.Fill = enabled
            ? (ready ? (Brush)Application.Current.Resources["SuccessBrush"] : (Brush)Application.Current.Resources["ErrorBrush"])
            : (Brush)Application.Current.Resources["MutedForegroundBrush"];
        YoloOptionLabel.Text = !enabled ? "Yolo Off" : ready ? "Yolo Active" : "Yolo Fallback";
        YoloOptionLabel.Foreground = enabled
            ? (ready ? (Brush)Application.Current.Resources["SuccessBrush"] : (Brush)Application.Current.Resources["ErrorBrush"])
            : (Brush)Application.Current.Resources["MutedForegroundBrush"];
    }

    private void SetActiveButton(Button button)
    {
        if (ReferenceEquals(_currentActiveButton, button))
        {
            return;
        }

        if (_currentActiveButton != null)
        {
            _currentActiveButton.ClearValue(Button.BackgroundProperty);
            _currentActiveButton.ClearValue(Button.BorderBrushProperty);

            UpdateActiveDot(_currentActiveButton, 0);
        }

        _currentActiveButton = button;

        button.Background = (Brush)Application.Current.Resources["SidebarSelectedBrush"];

        Brush activeBorderBrush = GetActiveBorderBrushForButton(button);
        button.BorderBrush = activeBorderBrush;

        UpdateActiveDot(button, 1);
    }

    private void UpdateActiveDot(Button button, double opacity)
    {
        if (button == FlightNavButton) FlightActiveDot.Opacity = opacity;
        else if (button == CameraNavButton) CameraActiveDot.Opacity = opacity;
        else if (button == MapNavButton) MapActiveDot.Opacity = opacity;
        else if (button == MissionPlannerNavButton) MissionPlannerActiveDot.Opacity = opacity;
        else if (button == StatsNavButton) StatsActiveDot.Opacity = opacity;
        else if (button == TlogNavButton) TlogActiveDot.Opacity = opacity;
        else if (button == DiagnosticsNavButton) DiagnosticsActiveDot.Opacity = opacity;
        else if (button == ParameterNavButton) ParameterActiveDot.Opacity = opacity;
#if !__WASM__
        else if (button == PIANavButton) PIAActiveDot.Opacity = opacity;
#endif
    }

    private Brush GetActiveBorderBrushForButton(Button button)
    {
        if (button == FlightNavButton) return (Brush)Application.Current.Resources["NavFlightBrush"];
        if (button == CameraNavButton) return (Brush)Application.Current.Resources["NavCameraBrush"];
        if (button == MapNavButton) return (Brush)Application.Current.Resources["NavMapBrush"];
        if (button == MissionPlannerNavButton) return (Brush)Application.Current.Resources["NavMapBrush"];
        if (button == StatsNavButton) return (Brush)Application.Current.Resources["NavStatsBrush"];
        if (button == TlogNavButton) return (Brush)Application.Current.Resources["NavTlogBrush"];
        if (button == DiagnosticsNavButton) return (Brush)Application.Current.Resources["NavDiagnosticsBrush"];
        if (button == ParameterNavButton) return (Brush)Application.Current.Resources["NavSettingsBrush"];
#if !__WASM__
        if (button == PIANavButton) return (Brush)Application.Current.Resources["PrimaryBrush"];
#endif

        return (Brush)Application.Current.Resources["PrimaryBrush"];
    }

    private Brush GetActiveBorderBrush(Button button)
    {
        return GetActiveBorderBrushForButton(button);
    }

    private async void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _themeService.ToggleThemeAsync();
            ApplyThemeToggleVisualState();
            Log.Information("Theme toggled from Sidebar");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle theme from Sidebar");
        }
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyThemeToggleVisualState();
            if (_currentActiveButton != null)
            {
                RefreshActiveButtonStyle(_currentActiveButton);
            }
        });
    }

    private void ApplyThemeToggleVisualState()
    {
        var resolvedTheme = _themeService.GetResolvedTheme();
        bool isDark = resolvedTheme == ThemeMode.Dark;
        var trackColor = isDark
            ? ((SolidColorBrush)Application.Current.Resources["ThemeToggleTrackDarkBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["ThemeToggleTrackLightBrush"]).Color;
        var thumbColor = isDark
            ? ((SolidColorBrush)Application.Current.Resources["ThemeToggleThumbDarkBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["PopoverBrush"]).Color;
        var iconColor = isDark
            ? ((SolidColorBrush)Application.Current.Resources["PrimaryForegroundBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["ThemeToggleSunBrush"]).Color;
        var glyph = isDark ? "\uE708" : "\uE706";

        ThemeToggleTrack.Background = new SolidColorBrush(trackColor);
        ThemeToggleThumb.Background = new SolidColorBrush(thumbColor);
        ThemeToggleIcon.Foreground = new SolidColorBrush(iconColor);
        ThemeToggleIcon.Glyph = glyph;
        ThemeToggleThumbTransform.X = isDark ? 2 : 28;
    }

    private void ModernSidebar_Unloaded(object sender, RoutedEventArgs e)
    {
        _themeService.ThemeChanged -= OnThemeChanged;
        if (_harvestFunctionalService != null)
        {
            _harvestFunctionalService.YoloOptionChanged -= OnYoloOptionChanged;
        }
        this.ActualThemeChanged -= OnActualThemeChanged;
    }
}
