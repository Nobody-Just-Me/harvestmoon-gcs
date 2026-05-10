using Microsoft.UI.Xaml;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Controls.Avionics
{
    public class AttitudeIndicator : AvionicsInstrumentBase
    {
        #region Dependency Properties

        public static readonly DependencyProperty PitchAngleProperty =
            DependencyProperty.Register(
                nameof(PitchAngle),
                typeof(double),
                typeof(AttitudeIndicator),
                new PropertyMetadata(0.0, OnAngleChanged));

        public static readonly DependencyProperty RollAngleProperty =
            DependencyProperty.Register(
                nameof(RollAngle),
                typeof(double),
                typeof(AttitudeIndicator),
                new PropertyMetadata(0.0, OnAngleChanged));

        public double PitchAngle
        {
            get => (double)GetValue(PitchAngleProperty);
            set => SetValue(PitchAngleProperty, value);
        }

        public double RollAngle
        {
            get => (double)GetValue(RollAngleProperty);
            set => SetValue(RollAngleProperty, value);
        }

        private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AttitudeIndicator indicator)
            {
                indicator.InvalidateVisual();
            }
        }

        #endregion

        #region Fields

        private readonly SKPaint _framePaint;
        private readonly SKPaint _skyPaint;
        private readonly SKPaint _groundPaint;
        private readonly SKPaint _horizonPaint;
        private readonly SKPaint _crosshairPaint;
        private readonly SKPaint _scalePaint;
        private readonly SKPaint _textPaint;

        #endregion

        #region Constructor

        public AttitudeIndicator()
        {
            _framePaint = new SKPaint
            {
                Color = new SKColor(30, 30, 30),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
                IsAntialias = true
            };

            _skyPaint = new SKPaint
            {
                Color = SKColor.Parse("#38bdf8"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _groundPaint = new SKPaint
            {
                Color = SKColor.Parse("#8b5a2b"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _horizonPaint = new SKPaint
            {
                Color = SKColor.Parse("#FFFFFF80"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            _crosshairPaint = new SKPaint
            {
                Color = SKColor.Parse("#facc15"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
                IsAntialias = true
            };

            _scalePaint = new SKPaint
            {
                Color = SKColor.Parse("#FFFFFFB3"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };

            _textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 14,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
        }

        #endregion

        #region Rendering

        protected override void OnRender(SKCanvas canvas, SKImageInfo info)
        {
            float centerX = info.Width / 2f;
            float centerY = info.Height / 2f;
            float radius = Math.Min(info.Width, info.Height) / 2f - 10;

            canvas.DrawCircle(centerX, centerY, radius, _framePaint);

            var clipPath = new SKPath();
            clipPath.AddCircle(centerX, centerY, radius - 4);
            canvas.ClipPath(clipPath);

            canvas.Save();

            canvas.Translate(centerX, centerY);

            canvas.RotateDegrees((float)-RollAngle);

            float pitchOffset = (float)(PitchAngle * 2f);
            canvas.Translate(0, pitchOffset);

            float extension = radius * 3;

            var skyRect = new SKRect(-extension, -extension, extension, 0);
            canvas.DrawRect(skyRect, _skyPaint);

            var groundRect = new SKRect(-extension, 0, extension, extension);
            canvas.DrawRect(groundRect, _groundPaint);

            canvas.DrawLine(-extension, 0, extension, 0, _horizonPaint);

            for (int i = -90; i <= 90; i += 10)
            {
                if (i == 0) continue;
                
                float y = -(i * 2); 
                
                if (Math.Abs(y + pitchOffset) > radius * 1.5) continue;

                float lineWidth = (i % 20 == 0) ? 40 : 20;
                canvas.DrawLine(-lineWidth / 2, y, lineWidth / 2, y, _scalePaint);
            }

            canvas.Restore();

            canvas.DrawLine(centerX - 32, centerY, centerX + 32, centerY, _crosshairPaint);
            canvas.DrawLine(centerX, centerY - 8, centerX, centerY + 8, _crosshairPaint);
            canvas.DrawLine(centerX - 32, centerY, centerX - 28, centerY - 6, _crosshairPaint);
            canvas.DrawLine(centerX + 32, centerY, centerX + 28, centerY - 6, _crosshairPaint);

            var centerDotPaint = new SKPaint { Color = SKColor.Parse("#ef4444"), Style = SKPaintStyle.Fill, IsAntialias = true };
            var centerDotBorderPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawCircle(centerX, centerY, 4, centerDotPaint);
            canvas.DrawCircle(centerX, centerY, 4, centerDotBorderPaint);

            var topTickPaint = new SKPaint { Color = SKColor.Parse("#eab308"), Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRect(centerX - 2, centerY - radius + 8, 4, 12, topTickPaint);
        }

        #endregion

        #region Public Methods

        public void SetAttitudeIndicatorParameters(double aircraftPitchAngle, double aircraftRollAngle)
        {
            PitchAngle = aircraftPitchAngle;
            RollAngle = aircraftRollAngle;
        }

        #endregion
    }
}