using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pigeon_Uno.Controls;

/// <summary>
/// Custom SkiaSharp control for displaying telemetry data
/// This bypasses Uno Platform TextBlock rendering issues on Linux
/// </summary>
public sealed class SkiaTelemetryDisplay : UserControl
{
    private SKXamlCanvas? _canvas;
    private Grid? _container;

    // Telemetry data properties
    private string _rollValue = "0.00°";
    private string _pitchValue = "0.00°";
    private string _yawValue = "0.00°";
    private string _altitudeValue = "N/A";
    private string _speedValue = "N/A";
    private string _flightMode = "N/A";

    public string RollValue
    {
        get => _rollValue;
        set
        {
            if (_rollValue != value)
            {
                _rollValue = value;
                InvalidateCanvas();
            }
        }
    }

    public string PitchValue
    {
        get => _pitchValue;
        set
        {
            if (_pitchValue != value)
            {
                _pitchValue = value;
                InvalidateCanvas();
            }
        }
    }

    public string YawValue
    {
        get => _yawValue;
        set
        {
            if (_yawValue != value)
            {
                _yawValue = value;
                InvalidateCanvas();
            }
        }
    }

    public string AltitudeValue
    {
        get => _altitudeValue;
        set
        {
            if (_altitudeValue != value)
            {
                _altitudeValue = value;
                InvalidateCanvas();
            }
        }
    }

    public string SpeedValue
    {
        get => _speedValue;
        set
        {
            if (_speedValue != value)
            {
                _speedValue = value;
                InvalidateCanvas();
            }
        }
    }

    public string FlightMode
    {
        get => _flightMode;
        set
        {
            if (_flightMode != value)
            {
                _flightMode = value;
                InvalidateCanvas();
            }
        }
    }

    public SkiaTelemetryDisplay()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Create container grid with transparent background
        _container = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        
        // Create SkiaSharp canvas with transparent background
        _canvas = new SKXamlCanvas
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        _canvas.PaintSurface += OnPaintSurface;
        
        // Add canvas to container
        _container.Children.Add(_canvas);
        
        // Set as content with transparent background
        Content = _container;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        
        // Set compact size to fit content exactly
        Width = 280;
        Height = 200;
        
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        
        // Clear canvas with transparent background
        canvas.Clear(SKColors.Transparent);
        
        // Create paints
        var headerPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 20,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        
        var labelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas")
        };
        
        var valuePaint = new SKPaint
        {
            Color = SKColor.Parse("#FF15008B"),
            TextSize = 18,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        
        var backgroundPaint = new SKPaint
        {
            Color = SKColor.Parse("#FF474747"),
            IsAntialias = true
        };
        
        var itemBackgroundPaint = new SKPaint
        {
            Color = SKColors.Gainsboro,
            IsAntialias = true
        };

        float y = 5;
        float itemHeight = 28;
        float padding = 8;
        
        // Draw flight mode header - more compact
        var flightModeRect = new SKRect(0, y, info.Width, y + 35);
        canvas.DrawRect(flightModeRect, backgroundPaint);
        
        var flightModeText = _flightMode.ToUpper();
        var flightModeX = info.Width / 2 - headerPaint.MeasureText(flightModeText) / 2;
        canvas.DrawText(flightModeText, flightModeX, y + 25, headerPaint);
        
        y += 38;
        
        // Draw telemetry items - more compact spacing
        DrawTelemetryItem(canvas, "HEADING", _yawValue, 0, y, info.Width, itemHeight, labelPaint, valuePaint, itemBackgroundPaint, padding);
        y += itemHeight + 1;
        
        DrawTelemetryItem(canvas, "PITCH", _pitchValue, 0, y, info.Width, itemHeight, labelPaint, valuePaint, itemBackgroundPaint, padding);
        y += itemHeight + 1;
        
        DrawTelemetryItem(canvas, "ROLL", _rollValue, 0, y, info.Width, itemHeight, labelPaint, valuePaint, itemBackgroundPaint, padding);
        y += itemHeight + 1;
        
        DrawTelemetryItem(canvas, "SPEED", _speedValue, 0, y, info.Width, itemHeight, labelPaint, valuePaint, itemBackgroundPaint, padding);
        y += itemHeight + 1;
        
        DrawTelemetryItem(canvas, "ALTITUDE", _altitudeValue, 0, y, info.Width, itemHeight, labelPaint, valuePaint, itemBackgroundPaint, padding);
        
        // Dispose paints
        headerPaint.Dispose();
        labelPaint.Dispose();
        valuePaint.Dispose();
        backgroundPaint.Dispose();
        itemBackgroundPaint.Dispose();
        
    }
    
    private void DrawTelemetryItem(SKCanvas canvas, string label, string value, float x, float y, float width, float height, 
        SKPaint labelPaint, SKPaint valuePaint, SKPaint backgroundPaint, float padding)
    {
        // Draw background
        var rect = new SKRect(x, y, x + width, y + height);
        canvas.DrawRect(rect, backgroundPaint);
        
        // Draw label
        canvas.DrawText(label, x + padding, y + height - 10, labelPaint);
        
        // Draw value (right aligned)
        var valueWidth = valuePaint.MeasureText(value);
        canvas.DrawText(value, x + width - valueWidth - padding, y + height - 8, valuePaint);
    }
    
    private void InvalidateCanvas()
    {
        try
        {
            _canvas?.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaTelemetryDisplay] Error invalidating canvas: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Update all telemetry values at once
    /// </summary>
    public void UpdateTelemetry(string roll, string pitch, string yaw, string altitude, string speed, string flightMode)
    {
        bool changed = false;
        
        if (_rollValue != roll) { _rollValue = roll; changed = true; }
        if (_pitchValue != pitch) { _pitchValue = pitch; changed = true; }
        if (_yawValue != yaw) { _yawValue = yaw; changed = true; }
        if (_altitudeValue != altitude) { _altitudeValue = altitude; changed = true; }
        if (_speedValue != speed) { _speedValue = speed; changed = true; }
        if (_flightMode != flightMode) { _flightMode = flightMode; changed = true; }
        
        if (changed)
        {
            InvalidateCanvas();
        }
    }
}
