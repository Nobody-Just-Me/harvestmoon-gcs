using System;
using System.Collections.Generic;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using SkiaSharp;

namespace Pigeon_Uno.Helpers
{
    /// <summary>
    /// Renders geofence visualization on map using SkiaSharp
    /// </summary>
    public class GeofenceRenderer
    {
        private readonly IGeofenceService _geofenceService;
        private readonly ILoggingService _logger;

        public GeofenceRenderer(IGeofenceService geofenceService, ILoggingService logger)
        {
            _geofenceService = geofenceService;
            _logger = logger;
        }

        /// <summary>
        /// Renders geofence circle on canvas
        /// </summary>
        public void RenderGeofence(SKCanvas canvas, GeofenceData geofence, float centerX, float centerY, float scale)
        {
            if (geofence == null || !geofence.IsEnabled) return;

            try
            {
                var radius = geofence.Radius * scale;
                
                using var paint = new SKPaint
                {
                    Color = geofence.IsViolated ? SKColors.Red : SKColors.Yellow,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 3,
                    IsAntialias = true
                };

                // Draw geofence circle
                canvas.DrawCircle((float)centerX, (float)centerY, (float)radius, paint);

                // Draw fill with transparency
                using var fillPaint = new SKPaint
                {
                    Color = geofence.IsViolated 
                        ? new SKColor(255, 0, 0, 30) 
                        : new SKColor(255, 255, 0, 30),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawCircle((float)centerX, (float)centerY, (float)radius, fillPaint);

                // Draw center point
                using var centerPaint = new SKPaint
                {
                    Color = SKColors.Orange,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawCircle(centerX, centerY, 5, centerPaint);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error rendering geofence: {ex.Message}", nameof(GeofenceRenderer));
            }
        }

        /// <summary>
        /// Renders multiple geofences
        /// </summary>
        public void RenderGeofences(SKCanvas canvas, IEnumerable<GeofenceData> geofences, 
            Func<GeoCoordinate, (float x, float y)> coordinateToScreen)
        {
            foreach (var geofence in geofences)
            {
                if (!geofence.IsEnabled) continue;

                var (x, y) = coordinateToScreen(geofence.Center);
                RenderGeofence(canvas, geofence, x, y, 1.0f);
            }
        }
    }
}
