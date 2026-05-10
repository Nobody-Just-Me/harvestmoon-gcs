using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pigeon_Uno.Controls.Avionics;

/// <summary>
/// Professional aviation-style Turn Coordinator.
/// Displays turn rate and slip/skid indication.
/// Renders using SkiaSharp for smooth 60 FPS performance.
/// </summary>
public sealed partial class TurnCoordinator : UserControl
{
    // Dependency property for turn rate (degrees per second)
    public static readonly DependencyProperty TurnRateProperty =
        DependencyProperty.Register(
            nameof(TurnRate),
            typeof(double),
            typeof(TurnCoordinator),
            new PropertyMetadata(0.0, OnTurnRateChanged));

    // Dependency property for slip/skid (lateral acceleration, -1 to 1)
    public static readonly DependencyProperty SlipSkidProperty =
        DependencyProperty.Register(
            nameof(SlipSkid),
            typeof(double),
            typeof(TurnCoordinator),
            new PropertyMetadata(0.0, OnSlipSkidChanged));

    public double TurnRate
    {
        get => (double)GetValue(TurnRateProperty);
        set => SetValue(TurnRateProperty, value);
    }

    public double SlipSkid
    {
        get => (double)GetValue(SlipSkidProperty);
        set => SetValue(SlipSkidProperty, value);
    }

    private const double MaxTurnRate = 30.0; // degrees per second (standard rate turn is 3°/s)
    private const double StandardRateTurn = 3.0; // 3°/s for standard rate turn

    public TurnCoordinator()
    {
        this.InitializeComponent();
    }

    private static void OnTurnRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TurnCoordinator tc)
        {
            tc.UpdateDisplay();
        }
    }

    private static void OnSlipSkidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TurnCoordinator tc)
        {
            tc.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        // Update digital readout
        TurnRateText.Text = $"{TurnRate:F1}°/s";
        
        // Trigger canvas redraw
        TurnCanvas?.Invalidate();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        canvas.Clear(new SKColor(40, 40, 40)); // Dark background
        
        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;
        var radius = Math.Min(info.Width, info.Height) / 2f - 20;
        
        // Draw background
        DrawBackground(canvas, info, centerX, centerY, radius);
        
        // Draw turn rate scale
        DrawTurnRateScale(canvas, centerX, centerY, radius);
        
        // Draw aircraft symbol (rotates with turn rate)
        DrawAircraftSymbol(canvas, centerX, centerY, radius);
        
        // Draw slip/skid ball
        DrawSlipSkidBall(canvas, info, centerX);
        
        // Draw border
        DrawBorder(canvas, info);
    }

    private void DrawBackground(SKCanvas canvas, SKImageInfo info, float centerX, float centerY, float radius)
    {
        // Draw outer circle
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(30, 30, 30),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        canvas.DrawCircle(centerX, centerY, radius, bgPaint);
        
        // Draw horizon line
        using var horizonPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        canvas.DrawLine(centerX - radius, centerY, centerX + radius, centerY, horizonPaint);
    }

    private void DrawTurnRateScale(SKCanvas canvas, float centerX, float centerY, float radius)
    {
        // Draw standard rate turn markers (L and R at 3°/s positions)
        var standardRateAngle = (float)(StandardRateTurn / MaxTurnRate * 90); // 90° max deflection
        
        using var markerPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 3,
            IsAntialias = true
        };
        
        // Left marker
        var leftAngle = -standardRateAngle * (float)Math.PI / 180f;
        var leftX = centerX + radius * 0.8f * (float)Math.Sin(leftAngle);
        var leftY = centerY - radius * 0.8f * (float)Math.Cos(leftAngle);
        canvas.DrawLine(leftX - 10, leftY, leftX + 10, leftY, markerPaint);
        
        // Right marker
        var rightAngle = standardRateAngle * (float)Math.PI / 180f;
        var rightX = centerX + radius * 0.8f * (float)Math.Sin(rightAngle);
        var rightY = centerY - radius * 0.8f * (float)Math.Cos(rightAngle);
        canvas.DrawLine(rightX - 10, rightY, rightX + 10, rightY, markerPaint);
        
        // Draw "L" and "R" labels
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 16,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        
        canvas.DrawText("L", leftX, leftY - 15, textPaint);
        canvas.DrawText("R", rightX, rightY - 15, textPaint);
        
        // Draw center marker (wings level)
        canvas.DrawLine(centerX - 15, centerY - radius * 0.8f, 
                       centerX + 15, centerY - radius * 0.8f, markerPaint);
    }

    private void DrawAircraftSymbol(SKCanvas canvas, float centerX, float centerY, float radius)
    {
        // Calculate rotation angle based on turn rate
        var clampedTurnRate = Math.Clamp(TurnRate, -MaxTurnRate, MaxTurnRate);
        var rotationAngle = (float)(clampedTurnRate / MaxTurnRate * 90); // 90° max deflection
        
        // Save canvas state
        canvas.Save();
        
        // Rotate around center
        canvas.RotateRadians(rotationAngle * (float)Math.PI / 180f, centerX, centerY);
        
        // Draw aircraft symbol (simplified top-down view)
        using var aircraftPaint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 4,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        
        var symbolSize = radius * 0.4f;
        
        // Fuselage
        canvas.DrawLine(centerX, centerY - symbolSize, centerX, centerY + symbolSize * 0.5f, aircraftPaint);
        
        // Wings
        canvas.DrawLine(centerX - symbolSize, centerY, centerX + symbolSize, centerY, aircraftPaint);
        
        // Tail
        canvas.DrawLine(centerX - symbolSize * 0.3f, centerY + symbolSize * 0.5f,
                       centerX + symbolSize * 0.3f, centerY + symbolSize * 0.5f, aircraftPaint);
        
        // Restore canvas state
        canvas.Restore();
    }

    private void DrawSlipSkidBall(SKCanvas canvas, SKImageInfo info, float centerX)
    {
        var ballY = info.Height - 40;
        var trackWidth = 80f;
        var trackHeight = 20f;
        
        // Draw ball track
        using var trackPaint = new SKPaint
        {
            Color = SKColors.Gray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        var trackRect = new SKRect(
            centerX - trackWidth / 2,
            ballY - trackHeight / 2,
            centerX + trackWidth / 2,
            ballY + trackHeight / 2);
        
        canvas.DrawRoundRect(trackRect, 10, 10, trackPaint);
        
        // Draw center markers
        using var markerPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        canvas.DrawLine(centerX - 2, ballY - 15, centerX - 2, ballY + 15, markerPaint);
        canvas.DrawLine(centerX + 2, ballY - 15, centerX + 2, ballY + 15, markerPaint);
        
        // Draw ball
        var clampedSlipSkid = Math.Clamp(SlipSkid, -1.0, 1.0);
        var ballX = centerX + (float)(clampedSlipSkid * (trackWidth / 2 - 10));
        
        using var ballPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        canvas.DrawCircle(ballX, ballY, 8, ballPaint);
        
        // Draw ball outline
        using var ballOutlinePaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        canvas.DrawCircle(ballX, ballY, 8, ballOutlinePaint);
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
