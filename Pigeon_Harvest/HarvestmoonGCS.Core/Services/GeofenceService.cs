using MavLinkNet;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service untuk mengelola geofence (batas geografis virtual)
/// </summary>
public class GeofenceService : IGeofenceService
{
    private readonly ISettingsService _settingsService;
    private readonly IMavLinkService? _mavLinkService;
    private readonly GeofenceData _currentGeofence;

    public GeofenceData CurrentGeofence => _currentGeofence;

    public GeofenceService(ISettingsService settingsService, IMavLinkService? mavLinkService = null)
    {
        _settingsService = settingsService;
        _mavLinkService = mavLinkService;
        _currentGeofence = new GeofenceData();
    }

    public void SetGeofenceActive(bool isActive)
    {
        _currentGeofence.IsActive = isActive;
        _currentGeofence.IsEnabled = isActive;
        
        if (isActive)
        {
            _currentGeofence.Status = GeofenceStatus.Active;
        }
        else
        {
            _currentGeofence.Status = GeofenceStatus.Inactive;
        }
    }

    public void SetGeofenceCenter(double latitude, double longitude)
    {
        _currentGeofence.CenterLatitude = latitude;
        _currentGeofence.CenterLongitude = longitude;
    }

    public void SetGeofenceRadius(double radius)
    {
        _currentGeofence.Radius = radius;
    }

    public void SetMaxAltitude(double maxAltitude)
    {
        _currentGeofence.MaxAltitude = maxAltitude;
    }

    public void SetGeofenceType(GeofenceType type)
    {
        _currentGeofence.Type = type;
        
        // Clear vertices when switching to circular
        if (type == GeofenceType.Circular)
        {
            _currentGeofence.Vertices.Clear();
        }
    }

    public void AddPolygonVertex(double latitude, double longitude)
    {
        int index = _currentGeofence.Vertices.Count + 1;
        var vertex = new GeofenceVertex(index, latitude, longitude);
        _currentGeofence.Vertices.Add(vertex);
        
        // Set status to drawing if not already
        if (_currentGeofence.Status == GeofenceStatus.Inactive)
        {
            _currentGeofence.Status = GeofenceStatus.Drawing;
        }
    }

    public void ClearPolygonVertices()
    {
        _currentGeofence.Vertices.Clear();
        _currentGeofence.Status = GeofenceStatus.Inactive;
        _currentGeofence.IsActive = false;
    }

    public void CompletePolygon()
    {
        if (_currentGeofence.Vertices.Count >= 3)
        {
            _currentGeofence.Status = GeofenceStatus.Active;
            _currentGeofence.IsActive = true;
            _currentGeofence.IsEnabled = true;
        }
    }

    public double CalculateDistanceToBoundary(double latitude, double longitude, double altitude)
    {
        // Check altitude first
        if (altitude > _currentGeofence.MaxAltitude)
        {
            return -(altitude - _currentGeofence.MaxAltitude);
        }

        if (_currentGeofence.Type == GeofenceType.Circular)
        {
            return CalculateDistanceToCircularBoundary(latitude, longitude);
        }
        else
        {
            return CalculateDistanceToPolygonBoundary(latitude, longitude);
        }
    }

    private double CalculateDistanceToCircularBoundary(double latitude, double longitude)
    {
        // Calculate distance from center
        double distanceFromCenter = GeoMath.CalculateDistance(
            _currentGeofence.CenterLatitude,
            _currentGeofence.CenterLongitude,
            latitude,
            longitude);

        // Return distance to boundary (positive if inside, negative if outside)
        return _currentGeofence.Radius - distanceFromCenter;
    }

    private double CalculateDistanceToPolygonBoundary(double latitude, double longitude)
    {
        if (_currentGeofence.Vertices.Count < 3)
        {
            return double.MaxValue; // No valid polygon
        }

        // Check if point is inside polygon using ray casting algorithm
        bool isInside = IsPointInPolygon(latitude, longitude);

        if (!isInside)
        {
            // Calculate distance to nearest edge
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < _currentGeofence.Vertices.Count; i++)
            {
                var v1 = _currentGeofence.Vertices[i];
                var v2 = _currentGeofence.Vertices[(i + 1) % _currentGeofence.Vertices.Count];
                
                double distance = CalculateDistanceToLineSegment(
                    latitude, longitude,
                    v1.Lat, v1.Lon,
                    v2.Lat, v2.Lon);
                
                minDistance = Math.Min(minDistance, distance);
            }
            
            return -minDistance; // Negative because outside
        }
        else
        {
            // Calculate distance to nearest edge (positive because inside)
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < _currentGeofence.Vertices.Count; i++)
            {
                var v1 = _currentGeofence.Vertices[i];
                var v2 = _currentGeofence.Vertices[(i + 1) % _currentGeofence.Vertices.Count];
                
                double distance = CalculateDistanceToLineSegment(
                    latitude, longitude,
                    v1.Lat, v1.Lon,
                    v2.Lat, v2.Lon);
                
                minDistance = Math.Min(minDistance, distance);
            }
            
            return minDistance;
        }
    }

    private bool IsPointInPolygon(double latitude, double longitude)
    {
        // Ray casting algorithm
        bool inside = false;
        int j = _currentGeofence.Vertices.Count - 1;
        
        for (int i = 0; i < _currentGeofence.Vertices.Count; i++)
        {
            var vi = _currentGeofence.Vertices[i];
            var vj = _currentGeofence.Vertices[j];
            
            if ((vi.Lon > longitude) != (vj.Lon > longitude) &&
                latitude < (vj.Lat - vi.Lat) * (longitude - vi.Lon) / (vj.Lon - vi.Lon) + vi.Lat)
            {
                inside = !inside;
            }
            
            j = i;
        }
        
        return inside;
    }

    private double CalculateDistanceToLineSegment(
        double px, double py,
        double x1, double y1,
        double x2, double y2)
    {
        // Calculate distance from point (px, py) to line segment (x1, y1) - (x2, y2)
        double A = px - x1;
        double B = py - y1;
        double C = x2 - x1;
        double D = y2 - y1;

        double dot = A * C + B * D;
        double lenSq = C * C + D * D;
        double param = -1;

        if (lenSq != 0)
        {
            param = dot / lenSq;
        }

        double xx, yy;

        if (param < 0)
        {
            xx = x1;
            yy = y1;
        }
        else if (param > 1)
        {
            xx = x2;
            yy = y2;
        }
        else
        {
            xx = x1 + param * C;
            yy = y1 + param * D;
        }

        // Calculate actual distance using GeoMath
        return GeoMath.CalculateDistance(px, py, xx, yy);
    }

    public bool IsInsideGeofence(double latitude, double longitude, double altitude)
    {
        // Check altitude
        if (altitude > _currentGeofence.MaxAltitude)
        {
            return false;
        }

        if (_currentGeofence.Type == GeofenceType.Circular)
        {
            double distanceFromCenter = GeoMath.CalculateDistance(
                _currentGeofence.CenterLatitude,
                _currentGeofence.CenterLongitude,
                latitude,
                longitude);
            
            return distanceFromCenter <= _currentGeofence.Radius;
        }
        else
        {
            return IsPointInPolygon(latitude, longitude);
        }
    }

    public async Task SaveGeofenceParametersAsync()
    {
        await _settingsService.SetSettingAsync("Geofence.IsActive", _currentGeofence.IsActive);
        await _settingsService.SetSettingAsync("Geofence.Type", _currentGeofence.Type.ToString());
        await _settingsService.SetSettingAsync("Geofence.CenterLatitude", _currentGeofence.CenterLatitude);
        await _settingsService.SetSettingAsync("Geofence.CenterLongitude", _currentGeofence.CenterLongitude);
        await _settingsService.SetSettingAsync("Geofence.Radius", _currentGeofence.Radius);
        await _settingsService.SetSettingAsync("Geofence.MaxAltitude", _currentGeofence.MaxAltitude);
        
        // Save vertices as JSON string
        if (_currentGeofence.Type == GeofenceType.Polygon && _currentGeofence.Vertices.Count > 0)
        {
            var verticesJson = System.Text.Json.JsonSerializer.Serialize(_currentGeofence.Vertices);
            await _settingsService.SetSettingAsync("Geofence.Vertices", verticesJson);
        }
        
        await _settingsService.SaveSettingsAsync();
    }

    public async Task LoadGeofenceParametersAsync()
    {
        await _settingsService.LoadSettingsAsync();
        
        _currentGeofence.IsActive = _settingsService.GetSetting("Geofence.IsActive", false);
        _currentGeofence.IsEnabled = _currentGeofence.IsActive;
        
        var typeString = _settingsService.GetSetting("Geofence.Type", "Circular");
        _currentGeofence.Type = Enum.TryParse<GeofenceType>(typeString, out var type) 
            ? type 
            : GeofenceType.Circular;
        
        _currentGeofence.CenterLatitude = _settingsService.GetSetting("Geofence.CenterLatitude", 0.0);
        _currentGeofence.CenterLongitude = _settingsService.GetSetting("Geofence.CenterLongitude", 0.0);
        _currentGeofence.Radius = _settingsService.GetSetting("Geofence.Radius", 500.0);
        _currentGeofence.MaxAltitude = _settingsService.GetSetting("Geofence.MaxAltitude", 100.0);
        
        // Load vertices if polygon type
        if (_currentGeofence.Type == GeofenceType.Polygon)
        {
            var verticesJson = _settingsService.GetSetting("Geofence.Vertices", "");
            if (!string.IsNullOrEmpty(verticesJson))
            {
                try
                {
                    var vertices = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<GeofenceVertex>>(verticesJson);
                    if (vertices != null)
                    {
                        _currentGeofence.Vertices.Clear();
                        _currentGeofence.Vertices.AddRange(vertices);
                    }
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }
        }
        
        // Update status based on loaded data
        if (_currentGeofence.IsActive)
        {
            _currentGeofence.Status = GeofenceStatus.Active;
        }
    }

    public async Task SendGeofenceToVehicleAsync()
    {
        if (_mavLinkService == null)
        {
            throw new InvalidOperationException("MAVLink service is required to send geofence to vehicle.");
        }

        if (!_mavLinkService.IsConnected && !_mavLinkService.IsInPlaybackMode)
        {
            throw new InvalidOperationException("Cannot send geofence because MAVLink is not connected.");
        }

        if (!_currentGeofence.IsActive)
        {
            await SetVehicleParameterAsync("FENCE_ENABLE", 0);
            return;
        }

        if (!GeofenceMavLinkHelper.ValidateForMavLinkUpload(_currentGeofence, out var errorMessage))
        {
            throw new InvalidOperationException($"Geofence cannot be uploaded: {errorMessage}");
        }

        await SetVehicleParameterAsync("FENCE_ENABLE", 0);
        await SetVehicleParameterAsync("FENCE_ALT_MAX", (float)_currentGeofence.MaxAltitude);

        if (_currentGeofence.Type == GeofenceType.Circular)
        {
            await SetVehicleParameterAsync("FENCE_TYPE", 3);
            await SetVehicleParameterAsync("FENCE_RADIUS", (float)_currentGeofence.Radius);
        }
        else
        {
            var fencePoints = GeofenceMavLinkHelper.ConvertToMavLinkFencePoints(_currentGeofence);
            await SetVehicleParameterAsync("FENCE_TYPE", 5);
            await SetVehicleParameterAsync("FENCE_TOTAL", fencePoints.Count);

            foreach (var point in fencePoints)
            {
                _mavLinkService.SendMessage(new UasFencePoint
                {
                    TargetSystem = 1,
                    TargetComponent = 0,
                    Idx = (byte)(point.Index + 1),
                    Count = (byte)fencePoints.Count,
                    Lat = (float)point.Latitude,
                    Lng = (float)point.Longitude
                });
            }
        }

        await SetVehicleParameterAsync("FENCE_ACTION", GeofenceMavLinkHelper.GetMavLinkFenceAction(GeofenceAction.RTL));
        await SetVehicleParameterAsync("FENCE_ENABLE", 1);
    }

    private async Task SetVehicleParameterAsync(string name, float value)
    {
        var success = await _mavLinkService!.SetParameterAsync(name, value);
        if (!success)
        {
            throw new InvalidOperationException($"Failed to set vehicle geofence parameter '{name}' to {value}.");
        }
    }

    public async Task<System.Collections.Generic.List<GeofenceData>> GetActiveGeofencesAsync()
    {
        await Task.CompletedTask;
        
        var geofences = new System.Collections.Generic.List<GeofenceData>();
        
        // Return current geofence if active
        if (_currentGeofence.IsActive && _currentGeofence.IsValid())
        {
            geofences.Add(_currentGeofence);
        }
        
        return geofences;
    }
}
