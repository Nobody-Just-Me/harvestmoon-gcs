using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Helpers
{
    /// <summary>
    /// Monitors geofence violations and triggers alerts
    /// </summary>
    public class GeofenceMonitor
    {
        private readonly IGeofenceService _geofenceService;
        private readonly IDialogService _dialogService;
        private readonly ILoggingService _logger;
        private readonly List<GeofenceData> _activeGeofences;

        /// <summary>
        /// Event fired when geofence violation is detected
        /// </summary>
        public event EventHandler<GeofenceViolationEventArgs>? GeofenceViolated;

        /// <summary>
        /// Event fired when vehicle re-enters geofence
        /// </summary>
        public event EventHandler<GeofenceViolationEventArgs>? GeofenceRestored;

        public GeofenceMonitor(
            IGeofenceService geofenceService,
            IDialogService dialogService,
            ILoggingService logger)
        {
            _geofenceService = geofenceService;
            _dialogService = dialogService;
            _logger = logger;
            _activeGeofences = new List<GeofenceData>();
        }

        /// <summary>
        /// Checks if position violates any active geofences
        /// </summary>
        public async Task CheckGeofenceViolationAsync(GeoCoordinate currentPosition)
        {
            var geofences = await _geofenceService.GetActiveGeofencesAsync();

            foreach (var geofence in geofences)
            {
                var distance = CalculateDistance(currentPosition, geofence.Center);
                var isInside = distance <= geofence.Radius;
                var wasViolated = geofence.IsViolated;

                if (!isInside && !wasViolated)
                {
                    // New violation
                    geofence.IsViolated = true;
                    await TriggerViolationAlert(geofence, currentPosition);
                }
                else if (isInside && wasViolated)
                {
                    // Restored
                    geofence.IsViolated = false;
                    await TriggerRestoredAlert(geofence, currentPosition);
                }
            }
        }

        /// <summary>
        /// Calculates distance between two coordinates in meters
        /// </summary>
        private double CalculateDistance(GeoCoordinate pos1, GeoCoordinate pos2)
        {
            const double R = 6371000; // Earth radius in meters
            
            var lat1 = ToRadians(pos1.Latitude);
            var lat2 = ToRadians(pos2.Latitude);
            var deltaLat = ToRadians(pos2.Latitude - pos1.Latitude);
            var deltaLon = ToRadians(pos2.Longitude - pos1.Longitude);

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private async Task TriggerViolationAlert(GeofenceData geofence, GeoCoordinate position)
        {
            var message = $"GEOFENCE VIOLATION: Vehicle left {geofence.Name}";
            _logger.LogError(message, nameof(GeofenceMonitor));

            GeofenceViolated?.Invoke(this, new GeofenceViolationEventArgs
            {
                Geofence = geofence,
                Position = position,
                Message = message
            });

            await _dialogService.ShowAlertAsync(message, "Geofence Alert");
        }

        private async Task TriggerRestoredAlert(GeofenceData geofence, GeoCoordinate position)
        {
            var message = $"Vehicle re-entered {geofence.Name}";
            _logger.LogInfo(message, nameof(GeofenceMonitor));

            GeofenceRestored?.Invoke(this, new GeofenceViolationEventArgs
            {
                Geofence = geofence,
                Position = position,
                Message = message
            });

            await _dialogService.ShowAlertAsync(message, "Geofence Restored");
        }
    }

    /// <summary>
    /// Event args for geofence violations
    /// </summary>
    public class GeofenceViolationEventArgs : EventArgs
    {
        public GeofenceData Geofence { get; set; } = null!;
        public GeoCoordinate Position { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
    }
}
