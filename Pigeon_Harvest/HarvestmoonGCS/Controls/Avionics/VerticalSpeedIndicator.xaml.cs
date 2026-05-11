using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace HarvestmoonGCS.Controls.Avionics;

/// <summary>
/// Professional aviation-style Vertical Speed Indicator (VSI).
/// Displays rate of climb/descent with needle and scale.
/// Renders using SkiaSharp for smooth 60 FPS performance.
/// </summary>
public sealed partial class VerticalSpeedIndicator : UserControl
{
    // Dependency property for vertical speed (m/s)
    public static readonly DependencyProperty VerticalSpeedProperty =
        DependencyProperty.Register(
            nameof(VerticalSpeed),
            typeof(double),
            typeof(VerticalSpeedIndicator),
            new PropertyMetadata(0.0, OnVerticalSpeedChanged));

    // Dependency property for warning threshold (m/s)
    public static readonly DependencyProperty WarningThresholdProperty =
        DependencyProperty.Register(
            nameof(WarningThreshold),
            typeof(double),
            typeof(VerticalSpeedIndicator),
            new PropertyMetadata(10.0));

    public double VerticalSpeed
    {
        get => (double)GetValue(VerticalSpeedProperty);
        set => SetValue(VerticalSpeedProperty, value);
    }

    public double WarningThreshold
    {
        get => (double)GetValue(WarningThresholdProperty);
        set => SetValue(WarningThresholdProperty, value);
    }

    private const double MaxVerticalSpeed = 20.0; // m/s (±20 m/s range)
    private const double MajorTickInterval = 5.0; // Major tick every 5 m/s
    private const double MinorTickInterval = 1.0; // Minor tick every 1 m/s

    public VerticalSpeedIndicator()
    {
        this.InitializeComponent();
    }

    private static void OnVerticalSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VerticalSpeedIndicator vsi)
        {
            vsi.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        // Update digital readout
        var sign = VerticalSpeed >= 0 ? "+" : "";
        VsiText.Text = $"{sign}{VerticalSpeed:F1}";
        
        // Trigger canvas redraw
        VsiCanvas?.Invalidate();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        canvas.Clear(new SKColor(40, 40, 40)); // Dark background
        
        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;
        
        // Draw background
        DrawBackground(canvas, info);
        
        // Draw scale
        DrawScale(canvas, info, centerX, centerY);
        
        // Draw needle
        DrawNeedle(canvas, info, centerX, centerY);
        
        // Draw center marker
        DrawCenterMarker(canvas, info, centerX, centerY);
        
        // Draw border
        DrawBorder(canvas, info);
    }

    private void DrawBackground(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(30, 30, 30),
            Style = SKPaintStyle.Fill
        };
        
        canvas.DrawRect(0, 0, info.Width, info.Height, paint);
    }

    private void DrawScale(SKCanvas canvas, SKImageInfo info, float centerX, float centerY)
    {
        var scaleHeight = info.Height - 40;
        var pixelsPerMps = scaleHeight / (2 * MaxVerticalSpeed);
        
        // Draw ticks and labels
        for (var vs = -MaxVerticalSpeed; vs <= MaxVerticalSpeed; vs += MinorTickInterval)
        {
            var yPos = centerY - (float)(vs * pixelsPerMps);
            
            var isMajorTick = Math.Abs(vs % MajorTickInterval) < 0.1;
            var isZero = Math.Abs(vs) < 0.1;
            
            // Determine color
            var isWarning = Math.Abs(vs) > WarningThreshold;
            var tickColor = isWarning ? new SKColor(255, 100, 100) : 
                           isZero ? SKColors.Yellow : SKColors.White;
            
            // Draw tick mark
            using (var tickPaint = new SKPaint
            {
                Color = tickColor,
                StrokeWidth = isMajorTick ? 2 : 1,
                IsAntialias = true
            })
            {
                var tickLength = isMajorTick ? 20f : 10f;
                canvas.DrawLine(centerX - tickLength, yPos, centerX + tickLength, yPos, tickPaint);
            }
            
            // Draw label for major ticks
            if (isMajorTick && !isZero)
            {
                using var textPaint = new SKPaint
                {
                    Color = tickColor,
                    TextSize = 12,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                    TextAlign = SKTextAlign.Center
                };
                
                var text = $"{Math.Abs(vs):F0}";
                canvas.DrawText(text, centerX, yPos - 5, textPaint);
            }
        }
        
        // Draw "UP" and "DOWN" labels
        using (var labelPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        })
        {
            canvas.DrawText("UP", centerX, 20, labelPaint);
            canvas.DrawText("DOWN", centerX, info.Height - 10, labelPaint);
        }
    }

    private void DrawNeedle(SKCanvas canvas, SKImageInfo info, float centerX, float centerY)
    {
        var scaleHeight = info.Height - 40;
        var pixelsPerMps = scaleHeight / (2 * MaxVerticalSpeed);
        
        // Clamp vertical speed to display range
        var clampedVs = Math.Clamp(VerticalSpeed, -MaxVerticalSpeed, MaxVerticalSpeed);
        var needleY = centerY - (float)(clampedVs * pixelsPerMps);
        
        // Determine needle color
        var isWarning = Math.Abs(VerticalSpeed) > WarningThreshold;
        var needleColor = isWarning ? new SKColor(255, 100, 100) : SKColors.Lime;
        
        // Draw needle
        using var needlePaint = new SKPaint
        {
            Color = needleColor,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        
        canvas.DrawLine(centerX - 30, needleY, centerX + 30, needleY, needlePaint);
        
        // Draw needle pointer
        using var path = new SKPath();
        path.MoveTo(centerX + 30, needleY);
        path.LineTo(centerX + 40, needleY - 5);
        path.LineTo(centerX + 40, needleY + 5);
        path.Close();
        
        using var fillPaint = new SKPaint
        {
            Color = needleColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        canvas.DrawPath(path, fillPaint);
    }

    private void DrawCenterMarker(SKCanvas canvas, SKImageInfo info, float centerX, float centerY)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        // Draw horizontal reference line at zero
        canvas.DrawLine(10, centerY, info.Width - 10, centerY, paint);
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
