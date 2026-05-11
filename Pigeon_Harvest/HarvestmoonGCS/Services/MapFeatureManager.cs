using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.Layers;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Helpers;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// Manages map features including waypoints, tracks, and geofences
    /// Coordinates between ViewModels and map visualization
    /// </summary>
    public class MapFeatureManager
    {
        private readonly MapsuiHelper _mapsuiHelper;
        private readonly IWaypointService _waypointService;
        private readonly IGeofenceService _geofenceService;
        private readonly ILoggingService _logger;
        private Map _map;

        /// <summary>
        /// Event fired when map features are updated
        /// </summary>
        public event EventHandler<EventArgs>? FeaturesUpdated;

        public MapFeatureManager(
            MapsuiHelper mapsuiHelper,
            IWaypointService waypointService,
            IGeofenceService geofenceService,
            ILoggingService logger)
        {
            _mapsuiHelper = mapsuiHelper;
            _waypointService = waypointService;
            _geofenceService = geofenceService;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the map with all feature layers
        /// </summary>
        public void InitializeMap(Map map)
        {
            _map = map;

            // Add feature layers
            _mapsuiHelper.AddWaypointLayer(map, "Waypoints");
            _mapsuiHelper.AddTrackLayer(map, "Tracks");
            _mapsuiHelper.AddGeofenceLayer(map, "Geofences");
            _mapsuiHelper.AddWaypointLayer(map, "Vehicle");

            _logger.LogInfo("Map feature layers initialized", nameof(MapFeatureManager));
        }

        /// <summary>
        /// Updates waypoint features on map
        /// </summary>
        public async Task UpdateWaypointsAsync()
        {
            if (_map == null) return;

            try
            {
                var waypoints = await _waypointService.GetWaypointsAsync();
                
                // TODO: Implement waypoint rendering when Mapsui.Uno.WinUI is available
                // For now, just log the update
                _logger.LogInfo($"Updated {waypoints.Count} waypoints on map", nameof(MapFeatureManager));
                
                // FeaturesUpdated?.Invoke((object)this, System.EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update waypoints: {ex.Message}", nameof(MapFeatureManager));
            }
        }

        /// <summary>
        /// Updates geofence features on map
        /// </summary>
        public async Task UpdateGeofencesAsync()
        {
            if (_map == null) return;

            try
            {
                var geofences = await _geofenceService.GetActiveGeofencesAsync();
                var geofenceList = geofences.ToList();
                
                // TODO: Implement geofence rendering when Mapsui.Uno.WinUI is available
                // For now, just log the update
                _logger.LogInfo($"Updated {geofenceList.Count} geofences on map", "MapFeatureManager");
                
                // FeaturesUpdated?.Invoke((object)this, System.EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update geofences: {ex.Message}", "MapFeatureManager");
            }
        }

        /// <summary>
        /// Updates vehicle position on map
        /// </summary>
        public void UpdateVehiclePosition(GeoCoordinate position, double heading)
        {
            if (_map == null) return;

            try
            {
                // TODO: Implement vehicle position rendering when Mapsui.Uno.WinUI is available
                // For now, just log the update (using LogInfo instead of LogDebug)
                // _logger.LogInfo($"Vehicle position updated: {position.Latitude}, {position.Longitude}", nameof(MapFeatureManager));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update vehicle position: {ex.Message}", nameof(MapFeatureManager));
            }
        }

        /// <summary>
        /// Clears all features from map
        /// </summary>
        public void ClearAllFeatures()
        {
            _mapsuiHelper.ClearLayer("Waypoints");
            _mapsuiHelper.ClearLayer("Tracks");
            _mapsuiHelper.ClearLayer("Geofences");
            _mapsuiHelper.ClearLayer("Vehicle");
            
            _logger.LogInfo("All map features cleared", nameof(MapFeatureManager));
        }
    }
}
