using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using HarvestmoonGCS.Core.Services;
using Windows.UI;
using IThemeService = HarvestmoonGCS.Core.Services.IThemeService;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Implementation of theme management service
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private ThemeMode _currentTheme;
    private bool _isHighContrastEnabled;
    private double _fontSizeScale = 1.0;
    private bool _animationsEnabled = true;

    public ThemeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadThemeSettings();
    }

    public ThemeMode CurrentTheme => _currentTheme;
    public bool IsHighContrastEnabled => _isHighContrastEnabled;
    public double FontSizeScale => _fontSizeScale;
    public bool AnimationsEnabled => _animationsEnabled;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public async Task SetThemeAsync(ThemeMode theme)
    {
        _currentTheme = theme;
        await _settingsService.SetSettingAsync("Theme", theme.ToString());
        ApplyTheme();
        RaiseThemeChanged();
    }

    public async Task ToggleThemeAsync()
    {
        ThemeMode newTheme = _currentTheme == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        await SetThemeAsync(newTheme);
    }

    public async Task SetHighContrastAsync(bool enabled)
    {
        _isHighContrastEnabled = enabled;
        await _settingsService.SetSettingAsync("HighContrast", enabled);
        ApplyHighContrast();
        RaiseThemeChanged();
    }

    public async Task SetFontSizeScaleAsync(double scale)
    {
        _fontSizeScale = Math.Clamp(scale, 0.8, 2.0);
        await _settingsService.SetSettingAsync("FontSizeScale", _fontSizeScale);
        ApplyFontSizeScale();
        RaiseThemeChanged();
    }

    public async Task SetAnimationsEnabledAsync(bool enabled)
    {
        _animationsEnabled = enabled;
        await _settingsService.SetSettingAsync("AnimationsEnabled", enabled);
        ApplyAnimationSettings();
    }

    public async Task ApplyCustomColorsAsync(ThemeColors colors)
    {
        if (colors == null) return;

        var resources = Application.Current.Resources;

        if (!string.IsNullOrEmpty(colors.PrimaryColor))
        {
            resources["PrimaryColor"] = new SolidColorBrush(ParseColor(colors.PrimaryColor));
        }

        if (!string.IsNullOrEmpty(colors.SecondaryColor))
        {
            resources["SecondaryColor"] = new SolidColorBrush(ParseColor(colors.SecondaryColor));
        }

        if (!string.IsNullOrEmpty(colors.BackgroundColor))
        {
            resources["DarkBackgroundColor"] = new SolidColorBrush(ParseColor(colors.BackgroundColor));
        }

        if (!string.IsNullOrEmpty(colors.AccentColor))
        {
            resources["AccentColor"] = new SolidColorBrush(ParseColor(colors.AccentColor));
        }

        await _settingsService.SetSettingAsync("CustomColors", colors);
    }

    public async Task ResetThemeAsync()
    {
        await SetThemeAsync(ThemeMode.Dark);
        await SetHighContrastAsync(false);
        await SetFontSizeScaleAsync(1.0);
        await SetAnimationsEnabledAsync(true);
    }

    private void LoadThemeSettings()
    {
        var themeStr = _settingsService.GetSetting<string>("Theme", "Dark");
        _currentTheme = Enum.TryParse<ThemeMode>(themeStr, out var theme) ? theme : ThemeMode.Dark;

        _isHighContrastEnabled = _settingsService.GetSetting<bool>("HighContrast", false);
        _fontSizeScale = _settingsService.GetSetting<double>("FontSizeScale", 1.0);
        _animationsEnabled = _settingsService.GetSetting<bool>("AnimationsEnabled", true);

        ApplyTheme();
        ApplyHighContrast();
        ApplyFontSizeScale();
        ApplyAnimationSettings();
    }

    private void ApplyTheme()
    {
        if (App.MainWindow == null) return;

        var elementTheme = _currentTheme == ThemeMode.Light 
            ? ElementTheme.Light 
            : ElementTheme.Dark;

        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = elementTheme;
        }

        
        ForceThemeRefresh();
    }

    public ThemeMode GetResolvedTheme()
    {
        return _currentTheme;
    }

    private void ForceThemeRefresh()
    {
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            // Just update layout, RequestedTheme is already set in ApplyTheme
            root.UpdateLayout();
        }
    }

    private void ApplyHighContrast()
    {
        if (!_isHighContrastEnabled) return;

        var resources = Application.Current.Resources;

        // High contrast colors
        if (_currentTheme == ThemeMode.Light)
        {
            resources["PrimaryTextColor"] = new SolidColorBrush(Colors.Black);
            resources["DarkBackgroundColor"] = new SolidColorBrush(Colors.White);
            resources["BorderColor"] = new SolidColorBrush(Colors.Black);
        }
        else
        {
            resources["PrimaryTextColor"] = new SolidColorBrush(Colors.White);
            resources["DarkBackgroundColor"] = new SolidColorBrush(Colors.Black);
            resources["BorderColor"] = new SolidColorBrush(Colors.White);
        }
    }

    private void ApplyFontSizeScale()
    {
        var resources = Application.Current.Resources;

        // Scale font sizes
        if (resources.ContainsKey("BaseFontSize"))
        {
            var baseFontSize = (double)resources["BaseFontSize"];
            resources["ScaledFontSize"] = baseFontSize * _fontSizeScale;
        }
    }

    private void ApplyAnimationSettings()
    {
        // This would require modifying animation durations throughout the app
        // For now, we'll just store the setting
    }

    private void RaiseThemeChanged()
    {
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs
        {
            Theme = _currentTheme,
            IsHighContrast = _isHighContrastEnabled,
            FontSizeScale = _fontSizeScale
        });
    }

    private Color ParseColor(string colorString)
    {
        if (string.IsNullOrEmpty(colorString)) return Colors.Transparent;

        // Remove # if present
        colorString = colorString.TrimStart('#');

        if (colorString.Length == 6)
        {
            // RGB format
            var r = Convert.ToByte(colorString.Substring(0, 2), 16);
            var g = Convert.ToByte(colorString.Substring(2, 2), 16);
            var b = Convert.ToByte(colorString.Substring(4, 2), 16);
            return Color.FromArgb(255, r, g, b);
        }
        else if (colorString.Length == 8)
        {
            // ARGB format
            var a = Convert.ToByte(colorString.Substring(0, 2), 16);
            var r = Convert.ToByte(colorString.Substring(2, 2), 16);
            var g = Convert.ToByte(colorString.Substring(4, 2), 16);
            var b = Convert.ToByte(colorString.Substring(6, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }

        return Colors.Transparent;
    }
}
