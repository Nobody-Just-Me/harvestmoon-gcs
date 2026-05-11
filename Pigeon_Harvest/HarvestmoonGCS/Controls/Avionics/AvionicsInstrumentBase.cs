using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;

namespace HarvestmoonGCS.Controls.Avionics
{
    public enum RenderingQuality
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Base class for avionics instruments using SkiaSharp for cross-platform rendering
    /// OPTIMIZED: Implements caching for static elements and dirty region updates
    /// </summary>
    public abstract class AvionicsInstrumentBase : UserControl
    {
        protected SKXamlCanvas canvas;
        
        // OPTIMIZATION: Cache static background elements to avoid redrawing
        protected SKBitmap? _cachedBackground;
        protected bool _backgroundCacheValid = false;
        
        // OPTIMIZATION: Track if instrument needs redraw (dirty flag)
        protected bool _isDirty = true;

        // OPTIMIZATION: Throttling for Emulator Mode
        private DispatcherTimer? _throttleTimer;
        private bool _pendingRender = false;

        #region Dependency Properties

        public static readonly DependencyProperty EmulatorModeProperty =
            DependencyProperty.Register(
                nameof(EmulatorMode),
                typeof(bool),
                typeof(AvionicsInstrumentBase),
                new PropertyMetadata(false, OnEmulatorModeChanged));

        public static readonly DependencyProperty RenderingQualityProperty =
            DependencyProperty.Register(
                nameof(RenderingQuality),
                typeof(RenderingQuality),
                typeof(AvionicsInstrumentBase),
                new PropertyMetadata(RenderingQuality.High, OnRenderingQualityChanged));

        public bool EmulatorMode
        {
            get => (bool)GetValue(EmulatorModeProperty);
            set => SetValue(EmulatorModeProperty, value);
        }

        public RenderingQuality RenderingQuality
        {
            get => (RenderingQuality)GetValue(RenderingQualityProperty);
            set => SetValue(RenderingQualityProperty, value);
        }

        private static void OnEmulatorModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvionicsInstrumentBase instrument)
            {
                instrument.UpdateThrottleTimer();
                instrument.InvalidateVisual();
            }
        }

        private static void OnRenderingQualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvionicsInstrumentBase instrument)
            {
                instrument.InvalidateVisual();
            }
        }

        #endregion
        
        public AvionicsInstrumentBase()
        {
            canvas = new SKXamlCanvas();
            canvas.PaintSurface += OnPaintSurface;
            
            // Ensure transparent background
            canvas.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            
            Content = canvas;
            
            // Set default size
            Width = 300;
            Height = 300;
            
            // Invalidate cache when size changes
            SizeChanged += (s, e) =>
            {
                _backgroundCacheValid = false;
                _isDirty = true;
            };

            // Initialize throttle timer
            _throttleTimer = new DispatcherTimer();
            _throttleTimer.Tick += OnThrottleTimerTick;
        }

        private void UpdateThrottleTimer()
        {
            if (EmulatorMode)
            {
                _throttleTimer.Interval = TimeSpan.FromMilliseconds(100); // 10 FPS
                _throttleTimer.Start();
            }
            else
            {
                _throttleTimer.Stop();
            }
        }

        private void OnThrottleTimerTick(object? sender, object e)
        {
            if (_pendingRender)
            {
                _pendingRender = false;
                _isDirty = true;
                canvas?.Invalidate();
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // OPTIMIZATION: Skip rendering if not dirty (no changes)
            if (!_isDirty) return;
            
            var surface = e.Surface;
            var canvas = surface.Canvas;
            
            canvas.Clear(SKColors.Transparent);
            
            // Call derived class rendering
            OnRender(canvas, e.Info);
            
            _isDirty = false;
        }

        /// <summary>
        /// Override this method to implement custom rendering
        /// </summary>
        protected abstract void OnRender(SKCanvas canvas, SKImageInfo info);

        /// <summary>
        /// Request a redraw of the instrument
        /// </summary>
        protected void InvalidateVisual()
        {
            if (EmulatorMode)
            {
                _pendingRender = true;
                // Timer will pick it up
            }
            else
            {
                _isDirty = true;
                canvas?.Invalidate();
            }
        }
        
        /// <summary>
        /// Invalidate the background cache (call when static elements change)
        /// </summary>
        protected void InvalidateBackgroundCache()
        {
            _backgroundCacheValid = false;
            _cachedBackground?.Dispose();
            _cachedBackground = null;
            InvalidateVisual();
        }

        #region Helper Methods

        /// <summary>
        /// Load a bitmap from embedded resources
        /// </summary>
        protected SKBitmap LoadBitmap(string resourceName)
        {
            try
            {
                var assembly = GetType().Assembly;
                var resourcePath = $"HarvestmoonGCS.Assets.Avionics.{resourceName}";
                
                using (var stream = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (stream != null)
                    {
                        return SKBitmap.Decode(stream);
                    }
                }
                
                // Fallback: try to load from file system
                var filePath = $"ms-appx:///Assets/avionics/{resourceName}";
                // For now, return null - we'll handle file loading differently
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Rotate and translate an image
        /// </summary>
        protected void RotateAndTranslate(SKCanvas canvas, SKBitmap bitmap, 
            double rotationAngle, double translationAngle, 
            SKPoint imagePoint, int translationPx, 
            SKPoint rotationPoint, float scale, SKPaint? paint = null)
        {
            if (bitmap == null) return;

            canvas.Save();
            
            // Move to rotation point
            canvas.Translate(rotationPoint.X, rotationPoint.Y);
            
            // Apply rotation
            canvas.RotateRadians((float)rotationAngle);
            
            // Apply translation
            float deltaX = (float)(translationPx * Math.Sin(translationAngle));
            float deltaY = (float)(-translationPx * Math.Cos(translationAngle));
            
            // Draw the bitmap
            var destRect = new SKRect(
                (imagePoint.X + deltaX - rotationPoint.X) * scale,
                (imagePoint.Y + deltaY - rotationPoint.Y) * scale,
                (imagePoint.X + deltaX - rotationPoint.X + bitmap.Width) * scale,
                (imagePoint.Y + deltaY - rotationPoint.Y + bitmap.Height) * scale
            );
            
            canvas.DrawBitmap(bitmap, destRect, paint);
            
            canvas.Restore();
        }

        /// <summary>
        /// Rotate an image around a point
        /// </summary>
        protected void RotateImage(SKCanvas canvas, SKBitmap bitmap, 
            double angle, SKPoint imagePoint, SKPoint rotationPoint, float scale, SKPaint? paint = null)
        {
            if (bitmap == null) return;

            canvas.Save();
            
            // Move to rotation point
            canvas.Translate(rotationPoint.X * scale, rotationPoint.Y * scale);
            
            // Apply rotation
            canvas.RotateRadians((float)angle);
            
            // Draw the bitmap
            var destRect = new SKRect(
                (imagePoint.X - rotationPoint.X) * scale,
                (imagePoint.Y - rotationPoint.Y) * scale,
                (imagePoint.X - rotationPoint.X + bitmap.Width) * scale,
                (imagePoint.Y - rotationPoint.Y + bitmap.Height) * scale
            );
            
            canvas.DrawBitmap(bitmap, destRect, paint);
            
            canvas.Restore();
        }

        /// <summary>
        /// Convert physical value to angle in radians
        /// </summary>
        protected float InterpolPhyToAngle(float phyVal, float minPhy, float maxPhy, 
            float minAngle, float maxAngle)
        {
            if (phyVal < minPhy)
            {
                return (float)(minAngle * Math.PI / 180);
            }
            else if (phyVal > maxPhy)
            {
                return (float)(maxAngle * Math.PI / 180);
            }
            else
            {
                float a = (maxAngle - minAngle) / (maxPhy - minPhy);
                float b = (float)(0.5 * (maxAngle + minAngle - a * (maxPhy + minPhy)));
                float y = a * phyVal + b;
                return (float)(y * Math.PI / 180);
            }
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        protected double FromDegToRad(double degAngle)
        {
            return degAngle * Math.PI / 180;
        }

        #endregion
    }
}
