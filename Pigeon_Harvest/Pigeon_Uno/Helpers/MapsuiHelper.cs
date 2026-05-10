using System;
using System.Collections.Generic;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.UI;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using MapsuiBrush = Mapsui.Styles.Brush;

namespace Pigeon_Uno.Helpers
{
    /// <summary>
    /// Helper class for Mapsui map operations
    /// Manages layers, tile sources, and coordinate conversions
    /// </summary>
    public class MapsuiHelper
    {
        private readonly ILoggingService _logger;
        private readonly Dictionary<string, ILayer> _layers;

        public MapsuiHelper(ILoggingService logger)
        {
            _logger = logger;
            _layers = new Dictionary<string, ILayer>();
        }

        /// <summary>
        /// Creates a new map with default settings
        /// </summary>
        public Map CreateMap()
        {
            var map = new Map();
            map.CRS = "EPSG:3857";
            return map;
        }

        /// <summary>
        /// Adds a tile layer to the map
        /// </summary>
        public void AddTileLayer(Map map, string layerName, string urlTemplate)
        {
            try
            {
                // Simplified implementation - tile layers will be added when Mapsui.Uno.WinUI is available
                // For now, just log the request
                _logger.LogInfo($"Tile layer requested: {layerName} (not yet implemented)", nameof(MapsuiHelper));
                
                // TODO: Implement when Mapsui.Uno.WinUI package is available
                // var tileSource = new HttpTileSource(...);
                // var tileLayer = new TileLayer(tileSource) { Name = layerName };
                // map.Layers.Add(tileLayer);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add tile layer: {ex.Message}", nameof(MapsuiHelper));
            }
        }

        /// <summary>
        /// Adds a vector layer for waypoints
        /// </summary>
        public void AddWaypointLayer(Map map, string layerName)
        {
            var layer = new Layer(layerName)
            {
                Style = new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    SymbolScale = 0.5,
                    Fill = new MapsuiBrush(Color.Red),
                    Outline = new Pen(Color.Black, 2)
                }
            };

            map.Layers.Add(layer);
            _layers[layerName] = layer;
        }

        /// <summary>
        /// Adds a vector layer for tracks
        /// </summary>
        public void AddTrackLayer(Map map, string layerName)
        {
            var layer = new Layer(layerName)
            {
                Style = new VectorStyle
                {
                    Line = new Pen(Color.Blue, 3),
                    Fill = null
                }
            };

            map.Layers.Add(layer);
            _layers[layerName] = layer;
        }

        /// <summary>
        /// Adds a vector layer for geofence
        /// </summary>
        public void AddGeofenceLayer(Map map, string layerName)
        {
            var layer = new Layer(layerName)
            {
                Style = new VectorStyle
                {
                    Line = new Pen(Color.Yellow, 2),
                    Fill = new MapsuiBrush(new Color(255, 255, 0, 128))
                }
            };

            map.Layers.Add(layer);
            _layers[layerName] = layer;
        }

        /// <summary>
        /// Converts GeoCoordinate to Mapsui MPoint
        /// </summary>
        public MPoint ToMPoint(GeoCoordinate coordinate)
        {
            var spherical = SphericalMercator.FromLonLat(
                coordinate.Longitude, 
                coordinate.Latitude);
            return new MPoint(spherical.x, spherical.y);
        }

        /// <summary>
        /// Converts Mapsui MPoint to GeoCoordinate
        /// </summary>
        public GeoCoordinate ToGeoCoordinate(MPoint point)
        {
            var (lon, lat) = SphericalMercator.ToLonLat(point.X, point.Y);
            return new GeoCoordinate { Latitude = lat, Longitude = lon };
        }

        /// <summary>
        /// Centers map on coordinate
        /// </summary>
        public void CenterOn(Map map, GeoCoordinate coordinate, double? resolution = null)
        {
            var point = ToMPoint(coordinate);
            // Use Navigator instead of HomeNavigator
            map.Navigator?.CenterOn(point);
            
            if (resolution.HasValue)
            {
                map.Navigator?.ZoomTo(resolution.Value);
            }
        }

        /// <summary>
        /// Gets layer by name
        /// </summary>
        public ILayer GetLayer(string layerName)
        {
            return _layers.TryGetValue(layerName, out var layer) ? layer : null;
        }

        /// <summary>
        /// Removes layer from map
        /// </summary>
        public void RemoveLayer(Map map, string layerName)
        {
            if (_layers.TryGetValue(layerName, out var layer))
            {
                map.Layers.Remove(layer);
                _layers.Remove(layerName);
                _logger.LogInfo($"Removed layer: {layerName}", nameof(MapsuiHelper));
            }
        }

        /// <summary>
        /// Clears all features from a layer
        /// </summary>
        public void ClearLayer(string layerName)
        {
            if (_layers.TryGetValue(layerName, out var layer))
            {
                if (layer is Layer l && l.DataSource != null)
                {
                    // Clear features
                    l.DataSource = null;
                }
            }
        }
    }
}
