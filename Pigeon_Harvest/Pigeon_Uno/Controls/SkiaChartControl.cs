using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pigeon_Uno.Controls;

/// <summary>
/// Simple real-time line chart using SkiaSharp - Cross-platform compatible
/// </summary>
public class SkiaChartControl : UserControl
{
    private SKXamlCanvas _canvas;
    private List<ChartSeries> _series = new();
    private double _minY = -180;
    private double _maxY = 180;
    private int _maxDataPoints = 300;
    
    public string Title { get; set; } = "Chart";
    public string YAxisLabel { get; set; } = "Value";
    
    public SkiaChartControl()
    {
        _canvas = new SKXamlCanvas();
        _canvas.PaintSurface += OnPaintSurface;
        Content = _canvas;
        
        Loaded += (s, e) => _canvas.Invalidate();
    }
    
    public void AddSeries(string name, SKColor color)
    {
        _series.Add(new ChartSeries 
        { 
            Name = name, 
            Color = color, 
            Data = new List<double>() 
        });
    }
    
    public void AddDataPoint(string seriesName, double value)
    {
        var series = _series.FirstOrDefault(s => s.Name == seriesName);
        if (series != null)
        {
            series.Data.Add(value);
            
            // Keep only last N points
            if (series.Data.Count > _maxDataPoints)
            {
                series.Data.RemoveAt(0);
            }
            
            _canvas.Invalidate();
        }
    }
    
    public void Clear()
    {
        foreach (var series in _series)
        {
            series.Data.Clear();
        }
        _canvas.Invalidate();
    }
    
    public void SetYRange(double min, double max)
    {
        _minY = min;
        _maxY = max;
    }
    
    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        
        var info = e.Info;
        float width = info.Width;
        float height = info.Height;
        
        // Margins
        float marginLeft = 60;
        float marginRight = 20;
        float marginTop = 40;
        float marginBottom = 40;
        
        float chartWidth = width - marginLeft - marginRight;
        float chartHeight = height - marginTop - marginBottom;
        
        // Draw title
        using (var paint = new SKPaint())
        {
            paint.Color = SKColor.Parse("#0B7074");
            paint.TextSize = 20;
            paint.IsAntialias = true;
            paint.TextAlign = SKTextAlign.Center;
            canvas.DrawText(Title, width / 2, 25, paint);
        }
        
        // Draw chart background
        using (var paint = new SKPaint())
        {
            paint.Color = SKColor.Parse("#F5F5F5");
            canvas.DrawRect(marginLeft, marginTop, chartWidth, chartHeight, paint);
        }
        
        // Draw grid lines
        using (var paint = new SKPaint())
        {
            paint.Color = SKColor.Parse("#E0E0E0");
            paint.StrokeWidth = 1;
            paint.IsAntialias = true;
            
            // Horizontal grid lines
            for (int i = 0; i <= 4; i++)
            {
                float y = marginTop + (chartHeight / 4) * i;
                canvas.DrawLine(marginLeft, y, marginLeft + chartWidth, y, paint);
            }
            
            // Vertical grid lines
            for (int i = 0; i <= 10; i++)
            {
                float x = marginLeft + (chartWidth / 10) * i;
                canvas.DrawLine(x, marginTop, x, marginTop + chartHeight, paint);
            }
        }
        
        // Draw Y axis labels
        using (var paint = new SKPaint())
        {
            paint.Color = SKColors.Black;
            paint.TextSize = 12;
            paint.IsAntialias = true;
            paint.TextAlign = SKTextAlign.Right;
            
            for (int i = 0; i <= 4; i++)
            {
                float y = marginTop + (chartHeight / 4) * i;
                double value = _maxY - ((_maxY - _minY) / 4) * i;
                canvas.DrawText($"{value:F0}", marginLeft - 5, y + 5, paint);
            }
            
            // Y axis label
            paint.TextAlign = SKTextAlign.Center;
            canvas.Save();
            canvas.Translate(15, marginTop + chartHeight / 2);
            canvas.RotateDegrees(-90);
            canvas.DrawText(YAxisLabel, 0, 0, paint);
            canvas.Restore();
        }
        
        // Draw data series
        if (_series.Any() && _series.Any(s => s.Data.Count > 0))
        {
            int maxPoints = _series.Max(s => s.Data.Count);
            
            foreach (var series in _series)
            {
                if (series.Data.Count < 2) continue;
                
                using (var paint = new SKPaint())
                {
                    paint.Color = series.Color;
                    paint.StrokeWidth = 2;
                    paint.IsAntialias = true;
                    paint.Style = SKPaintStyle.Stroke;
                    
                    using (var path = new SKPath())
                    {
                        for (int i = 0; i < series.Data.Count; i++)
                        {
                            float x = marginLeft + (chartWidth * i / Math.Max(maxPoints - 1, 1));
                            float normalizedY = (float)((series.Data[i] - _minY) / (_maxY - _minY));
                            float y = marginTop + chartHeight - (normalizedY * chartHeight);
                            
                            // Clamp Y to chart bounds
                            y = Math.Max(marginTop, Math.Min(marginTop + chartHeight, y));
                            
                            if (i == 0)
                                path.MoveTo(x, y);
                            else
                                path.LineTo(x, y);
                        }
                        
                        canvas.DrawPath(path, paint);
                    }
                }
            }
        }
        
        // Draw legend
        float legendX = marginLeft + 10;
        float legendY = marginTop + 10;
        
        foreach (var series in _series)
        {
            using (var paint = new SKPaint())
            {
                // Draw color box
                paint.Color = series.Color;
                canvas.DrawRect(legendX, legendY, 15, 15, paint);
                
                // Draw series name
                paint.Color = SKColors.Black;
                paint.TextSize = 12;
                paint.IsAntialias = true;
                
                // Show current value
                string text = series.Data.Count > 0 
                    ? $"{series.Name}: {series.Data.Last():F1}°" 
                    : series.Name;
                canvas.DrawText(text, legendX + 20, legendY + 12, paint);
            }
            
            legendY += 20;
        }
        
        // Draw border
        using (var paint = new SKPaint())
        {
            paint.Color = SKColors.Gray;
            paint.StrokeWidth = 1;
            paint.Style = SKPaintStyle.Stroke;
            paint.IsAntialias = true;
            canvas.DrawRect(marginLeft, marginTop, chartWidth, chartHeight, paint);
        }
    }
    
    private class ChartSeries
    {
        public string Name { get; set; } = "";
        public SKColor Color { get; set; }
        public List<double> Data { get; set; } = new();
    }
}
