using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace HarvestmoonGCS.Controls.Avionics;

/// <summary>
/// Professional aviation-style altimeter with moving tape display.
/// Renders using SkiaSharp for smooth 60 FPS performance.
/// </summary>
public sealed partial class Altimeter : UserControl
{
    // Dependency property for altitude
    public static readonly DependencyProperty AltitudeProperty =
        DependencyProperty.Register(
            nameof(Altitude),
            typeof(double),
            typeof(Altimeter),
            new PropertyMetadata(0.0, OnAltitudeChanged));

    // Dependency property for warning threshold
    public static readonly DependencyProperty WarningThresholdProperty =
        DependencyProperty.Register(
            nameof(WarningThreshold),
            typeof(double),
            typeof(Altimeter),
            new PropertyMetadata(500.0));

    public double Altitude
    {
        get => (double)GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    public double WarningThreshold
    {
        get => (double)GetValue(WarningThresholdProperty);
        set => SetValue(WarningThresholdProperty, value);
    }

    private const double PixelsPerMeter = 5.0; // Scale factor
    private const double MajorTickInterval = 100.0; // Major tick every 100m
    private const double MinorTickInterval = 20.0; // Minor tick every 20m

    public Altimeter()
    {
        this.InitializeComponent();
    }

    private static void OnAltitudeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Altimeter altimeter)
        {
            altimeter.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        // Update digital readout
        AltitudeText.Text = $"{Altitude:F1}m";
        
        // Trigger canvas redraw
        AltimeterCanvas?.Invalidate();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        canvas.Clear(new SKColor(40, 40, 40)); // Dark background
        
        // Calculate visible range
        var centerY = info.Height / 2f;
        var visibleRange = (info.Height / 2f) / (float)PixelsPerMeter;
        var minAlt = Altitude - visibleRange;
        var maxAlt = Altitude + visibleRange;
        
        // Draw tape background
        DrawTapeBackground(canvas, info);
        
        // Draw altitude tape
        DrawAltitudeTape(canvas, info, minAlt, maxAlt, centerY);
        
        // Draw center reference marker
        DrawCenterMarker(canvas, info, centerY);
        
        // Draw border
        DrawBorder(canvas, info);
    }

    private void DrawTapeBackground(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(30, 30, 30),
            Style = SKPaintStyle.Fill
        };
        
        canvas.DrawRect(0, 0, info.Width, info.Height, paint);
    }

    private void DrawAltitudeTape(SKCanvas canvas, SKImageInfo info, double minAlt, double maxAlt, float centerY)
    {
        // Calculate starting altitude (rounded to nearest minor tick)
        var startAlt = Math.Floor(minAlt / MinorTickInterval) * MinorTickInterval;
        var endAlt = Math.Ceiling(maxAlt / MinorTickInterval) * MinorTickInterval;
        
        // Draw ticks and labels
        for (var alt = startAlt; alt <= endAlt; alt += MinorTickInterval)
        {
            var yPos = centerY - (float)((alt - Altitude) * PixelsPerMeter);
            
            // Skip if outside visible area
            if (yPos < -50 || yPos > info.Height + 50)
                continue;
            
            var isMajorTick = Math.Abs(alt % MajorTickInterval) < 0.1;
            
            // Determine color based on altitude and warning threshold
            var isWarning = alt > WarningThreshold;
            var tickColor = isWarning ? new SKColor(255, 100, 100) : SKColors.White;
            
            // Draw tick mark
            using (var tickPaint = new SKPaint
            {
                Color = tickColor,
                StrokeWidth = isMajorTick ? 2 : 1,
                IsAntialias = true
            })
            {
                var tickStart = isMajorTick ? 10f : 20f;
                var tickEnd = isMajorTick ? 50f : 35f;
                
                canvas.DrawLine(tickStart, yPos, tickEnd, yPos, tickPaint);
            }
            
            // Draw label for major ticks
            if (isMajorTick)
            {
                using var textPaint = new SKPaint
                {
                    Color = tickColor,
                    TextSize = 16,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };
                
                var text = $"{alt:F0}";
                var textBounds = new SKRect();
                textPaint.MeasureText(text, ref textBounds);
                
                canvas.DrawText(text, 55, yPos + textBounds.Height / 2, textPaint);
            }
        }
    }

    private void DrawCenterMarker(SKCanvas canvas, SKImageInfo info, float centerY)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 3,
            IsAntialias = true
        };
        
        // Draw horizontal reference line
        canvas.DrawLine(0, centerY, info.Width, centerY, paint);
        
        // Draw triangle pointer
        using var path = new SKPath();
        path.MoveTo(info.Width - 20, centerY);
        path.LineTo(info.Width - 5, centerY - 8);
        path.LineTo(info.Width - 5, centerY + 8);
        path.Close();
        
        using var fillPaint = new SKPaint
        {
            Color = SKColors.Yellow,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        canvas.DrawPath(path, fillPaint);
    }

    private void DrawBorder(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        
        canvas.DrawRect(1, 1, info.Width - 2, info.Height - 2, paint);
    }
}
