using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Service for managing application themes and appearance
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme mode
    /// </summary>
    ThemeMode CurrentTheme { get; }

    /// <summary>
    /// Gets whether high contrast mode is enabled
    /// </summary>
    bool IsHighContrastEnabled { get; }

    /// <summary>
    /// Gets the current font size scale (1.0 = normal)
    /// </summary>
    double FontSizeScale { get; }

    /// <summary>
    /// Gets whether animations are enabled
    /// </summary>
    bool AnimationsEnabled { get; }

    /// <summary>
    /// Event raised when theme changes
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Sets the application theme
    /// </summary>
    Task SetThemeAsync(ThemeMode theme);

    /// <summary>
    /// Toggles between light and dark themes
    /// </summary>
    Task ToggleThemeAsync();

    /// <summary>
    /// Sets high contrast mode
    /// </summary>
    Task SetHighContrastAsync(bool enabled);

    /// <summary>
    /// Sets font size scale
    /// </summary>
    Task SetFontSizeScaleAsync(double scale);

    /// <summary>
    /// Sets whether animations are enabled
    /// </summary>
    Task SetAnimationsEnabledAsync(bool enabled);

    /// <summary>
    /// Applies custom theme colors
    /// </summary>
    Task ApplyCustomColorsAsync(ThemeColors colors);

    /// <summary>
    /// Resets theme to defaults
    /// </summary>
    Task ResetThemeAsync();

    /// <summary>
    /// Gets the resolved theme (converts System to actual Light/Dark based on OS setting)
    /// </summary>
    ThemeMode GetResolvedTheme();
}

/// <summary>
/// Theme mode enumeration
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    System
}

/// <summary>
/// Event args for theme changes
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public ThemeMode Theme { get; set; }
    public bool IsHighContrast { get; set; }
    public double FontSizeScale { get; set; }
}

/// <summary>
/// Custom theme colors
/// </summary>
public class ThemeColors
{
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? SurfaceColor { get; set; }
    public string? TextColor { get; set; }
    public string? AccentColor { get; set; }
}
