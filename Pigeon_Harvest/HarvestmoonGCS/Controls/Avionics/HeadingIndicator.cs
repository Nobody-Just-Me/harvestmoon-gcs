using Microsoft.UI.Xaml;
using SkiaSharp;
using System;

namespace HarvestmoonGCS.Controls.Avionics
{
    /// <summary>
    /// Heading Indicator instrument control showing aircraft heading
    /// </summary>
    public class HeadingIndicator : AvionicsInstrumentBase
    {
        #region Dependency Properties

        public static readonly DependencyProperty HeadingProperty =
            DependencyProperty.Register(
                nameof(Heading),
                typeof(int),
                typeof(HeadingIndicator),
                new PropertyMetadata(0, OnHeadingChanged));

        /// <summary>
        /// Aircraft heading in degrees (0-360)
        /// </summary>
        public int Heading
        {
            get => (int)GetValue(HeadingProperty);
            set => SetValue(HeadingProperty, value);
        }

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeadingIndicator indicator)
            {
                indicator.InvalidateVisual();
            }
        }

        #endregion

        #region Fields

        private readonly SKPaint _bezelPaint;
        private readonly SKPaint _tickPaint;
        private readonly SKPaint _textPaint;
        private readonly SKPaint _redTextPaint;
        private readonly SKPaint _arrowPaint;
        private readonly SKPath _arrowPath;

        #endregion

        #region Constructor

        public HeadingIndicator()
        {
            _bezelPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = new SKColor(20, 20, 20),
                IsAntialias = true
            };

            _tickPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse("#64748b"),
                IsAntialias = true
            };

            _textPaint = new SKPaint
            {
                Color = SKColor.Parse("#64748b"),
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            _redTextPaint = new SKPaint
            {
                Color = SKColor.Parse("#ef4444"),
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            _arrowPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse("#facc15"),
                IsAntialias = true
            };

            _arrowPath = new SKPath();
        }

        #endregion

        #region Rendering

        protected override void OnRender(SKCanvas canvas, SKImageInfo info)
        {
            float centerX = info.Width / 2f;
            float centerY = info.Height / 2f;
            float radius = Math.Min(info.Width, info.Height) / 2f - 10f;

            // Dynamically adjust text sizes based on available radius
            _textPaint.TextSize = radius * 0.20f;
            _redTextPaint.TextSize = radius * 0.20f;

            // Draw circular bezel (background)
            canvas.DrawCircle(centerX, centerY, radius, _bezelPaint);

            canvas.Save();
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(-Heading);

            // Draw tick marks and letters
            for (int i = 0; i < 360; i += 5)
            {
                bool isMajor = i % 30 == 0;
                bool isCardinal = i % 90 == 0;
                
                float tickLength = isMajor ? radius * 0.12f : radius * 0.06f;
                _tickPaint.StrokeWidth = isMajor ? 3f : 1.5f;

                canvas.DrawLine(0, -radius, 0, -radius + tickLength, _tickPaint);

                if (isCardinal)
                {
                    string letter = i switch
                    {
                        0 => "N",
                        90 => "E",
                        180 => "S",
                        270 => "W",
                        _ => ""
                    };

                    float textY = -radius + tickLength + _textPaint.TextSize * 0.8f;
                    
                    if (i == 0)
                    {
                        canvas.DrawText(letter, 0, textY, _redTextPaint);
                    }
                    else
                    {
                        canvas.DrawText(letter, 0, textY, _textPaint);
                    }
                }
                else if (isMajor)
                {
                    string num = (i / 10).ToString();
                    float textY = -radius + tickLength + _textPaint.TextSize * 0.8f;
                    canvas.DrawText(num, 0, textY, _textPaint);
                }

                canvas.RotateDegrees(5);
            }

            canvas.Restore();

            var centerCirclePaint = new SKPaint { Color = SKColor.Parse("#f1f5f980"), Style = SKPaintStyle.Fill, IsAntialias = true };
            var centerCircleBorderPaint = new SKPaint { Color = SKColor.Parse("#ffffff80"), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawCircle(centerX, centerY, 24, centerCirclePaint);
            canvas.DrawCircle(centerX, centerY, 24, centerCircleBorderPaint);

            _arrowPath.Reset();
            float arrowSize = 12;
            _arrowPath.MoveTo(centerX, centerY + arrowSize);
            _arrowPath.LineTo(centerX - arrowSize / 2, centerY);
            _arrowPath.LineTo(centerX + arrowSize / 2, centerY);
            _arrowPath.Close();
            
            canvas.DrawPath(_arrowPath, _arrowPaint);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set heading indicator parameters
        /// </summary>
        /// <param name="aircraftHeading">Heading in degrees (0-360)</param>
        public void SetHeadingIndicatorParameters(int aircraftHeading)
        {
            Heading = aircraftHeading;
        }

        #endregion
    }
}
