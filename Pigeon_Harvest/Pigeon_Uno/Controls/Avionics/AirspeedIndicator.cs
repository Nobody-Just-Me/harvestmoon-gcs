using Microsoft.UI.Xaml;
using SkiaSharp;
using System;

namespace Pigeon_Uno.Controls.Avionics
{
    public class AirspeedIndicator : AvionicsInstrumentBase
    {
        #region Dependency Properties

        public static readonly DependencyProperty AirspeedProperty =
            DependencyProperty.Register(
                nameof(Airspeed),
                typeof(int),
                typeof(AirspeedIndicator),
                new PropertyMetadata(0, OnAirspeedChanged));

        public int Airspeed
        {
            get => (int)GetValue(AirspeedProperty);
            set => SetValue(AirspeedProperty, value);
        }

        private static void OnAirspeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AirspeedIndicator indicator)
            {
                indicator.InvalidateVisual();
            }
        }

        #endregion

        #region Cached Paints

        private readonly SKPaint _backgroundPaint = new SKPaint
        {
            Color = new SKColor(30, 30, 30),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private readonly SKPaint _bezelPaint = new SKPaint
        {
            Color = new SKColor(15, 15, 15),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 12,
            IsAntialias = true
        };

        private readonly SKPaint _tickPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        private readonly SKPaint _majorTickPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            IsAntialias = true
        };

        private readonly SKPaint _textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 16,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        private readonly SKPaint _needlePaint = new SKPaint
        {
            Color = SKColor.Parse("#ef4444"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        private readonly SKPaint _digitalTextPaint = new SKPaint
        {
            Color = SKColor.Parse("#10b981"),
            TextSize = 24,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        
        private readonly SKPaint _centerDotPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        #endregion

        #region Constructor

        public AirspeedIndicator()
        {
        }

        #endregion

        #region Rendering

        protected override void OnRender(SKCanvas canvas, SKImageInfo info)
        {
            float width = info.Width;
            float height = info.Height;
            float centerX = width / 2f;
            float centerY = height / 2f;
            float radius = Math.Min(width, height) / 2f - 10f;

            canvas.DrawCircle(centerX, centerY, radius, _backgroundPaint);
            canvas.DrawCircle(centerX, centerY, radius, _bezelPaint);

            const int maxSpeed = 60;
            const float startAngle = -135f;
            const float sweepAngle = 270f;
            
            for (int i = 0; i < 8; i++)
            {
                float angle = startAngle + (i * 45f);
                double rad = angle * Math.PI / 180.0;
                float tickLength = 12f;
                float innerRadius = radius - tickLength;
                
                float x1 = centerX + (float)(radius * Math.Cos(rad));
                float y1 = centerY + (float)(radius * Math.Sin(rad));
                float x2 = centerX + (float)(innerRadius * Math.Cos(rad));
                float y2 = centerY + (float)(innerRadius * Math.Sin(rad));
                
                canvas.DrawLine(x1, y1, x2, y2, _majorTickPaint);
            }
            
            canvas.DrawText("m/s", centerX, centerY - radius * 0.3f, _textPaint);

            float currentSpeed = Math.Max(0, Math.Min(Airspeed, maxSpeed));
            float scaleFactor = sweepAngle / maxSpeed;
            float needleAngle = startAngle + (currentSpeed * scaleFactor);

            canvas.Save();
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(needleAngle);
            
            using (var path = new SKPath())
            {
                path.MoveTo(radius * 0.85f, 0);
                path.LineTo(-15, -2);
                path.LineTo(-15, 2);
                path.Close();
                canvas.DrawPath(path, _needlePaint);
            }
            
            canvas.Restore();
            
            var pivotHubPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            var pivotHubBorderPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
            canvas.DrawCircle(centerX, centerY, 6, pivotHubPaint);
            canvas.DrawCircle(centerX, centerY, 6, pivotHubBorderPaint);
            
            canvas.DrawText($"{Airspeed}", centerX, centerY + radius * 0.5f, _digitalTextPaint);
        }

        #endregion

        #region Public Methods

        public void SetAirspeedIndicatorParameters(int aircraftAirspeed)
        {
            Airspeed = aircraftAirspeed;
        }

        #endregion
    }
}
