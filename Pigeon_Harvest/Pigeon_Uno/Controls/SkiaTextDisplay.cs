using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pigeon_Uno.Controls;

/// <summary>
/// Custom text display using SkiaSharp for reliable rendering on Linux/Skia
/// </summary>
public class SkiaTextDisplay : SKXamlCanvas
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(SkiaTextDisplay),
            new PropertyMetadata("N/A", OnTextChanged));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(float),
            typeof(SkiaTextDisplay),
            new PropertyMetadata(24f, OnPropertyChanged));

    public static readonly DependencyProperty TextColorProperty =
        DependencyProperty.Register(
            nameof(TextColor),
            typeof(SKColor),
            typeof(SkiaTextDisplay),
            new PropertyMetadata(SKColors.Blue, OnPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public float FontSize
    {
        get => (float)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public SKColor TextColor
    {
        get => (SKColor)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public SkiaTextDisplay()
    {
        PaintSurface += OnPaintSurface;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkiaTextDisplay control)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaTextDisplay] Text changed to: {e.NewValue}");
            control.Invalidate(); // Force redraw
        }
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkiaTextDisplay control)
        {
            control.Invalidate(); // Force redraw
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            Color = TextColor,
            TextSize = FontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };

        // Measure text to center it
        var textBounds = new SKRect();
        paint.MeasureText(Text, ref textBounds);

        // Draw text centered
        var x = (e.Info.Width - textBounds.Width) / 2;
        var y = (e.Info.Height + textBounds.Height) / 2;

        canvas.DrawText(Text, x, y, paint);
    }
}
