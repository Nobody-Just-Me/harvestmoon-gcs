using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Model data untuk geofence (batas geografis virtual)
/// </summary>
public class GeofenceData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isActive;
    private GeofenceType _type;
    private double _centerLatitude;
    private double _centerLongitude;
    private double _radius;
    private double _maxAltitude;
    private List<GeofenceVertex> _vertices;
    private GeofenceStatus _status;
    private string _name;
    private bool _isViolated;
    private bool _isEnabled;

    public GeofenceData()
    {
        _vertices = new List<GeofenceVertex>();
        _type = GeofenceType.Circular;
        _radius = 500;
        _maxAltitude = 100;
        _status = GeofenceStatus.Inactive;
        _name = "Geofence";
        _isViolated = false;
        _isEnabled = false;
    }

    /// <summary>
    /// Apakah geofence aktif
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>
    /// Tipe geofence (Circular atau Polygon)
    /// </summary>
    public GeofenceType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// Latitude pusat geofence (untuk tipe Circular)
    /// </summary>
    public double CenterLatitude
    {
        get => _centerLatitude;
        set => SetProperty(ref _centerLatitude, value);
    }

    /// <summary>
    /// Longitude pusat geofence (untuk tipe Circular)
    /// </summary>
    public double CenterLongitude
    {
        get => _centerLongitude;
        set => SetProperty(ref _centerLongitude, value);
    }

    /// <summary>
    /// Radius geofence dalam meter (untuk tipe Circular)
    /// </summary>
    public double Radius
    {
        get => _radius;
        set => SetProperty(ref _radius, value);
    }

    /// <summary>
    /// Altitude maksimum dalam meter
    /// </summary>
    public double MaxAltitude
    {
        get => _maxAltitude;
        set => SetProperty(ref _maxAltitude, value);
    }

    /// <summary>
    /// Daftar vertex untuk geofence polygon
    /// </summary>
    public List<GeofenceVertex> Vertices
    {
        get => _vertices;
        set => SetProperty(ref _vertices, value);
    }

    /// <summary>
    /// Status geofence saat ini
    /// </summary>
    public GeofenceStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>
    /// Name of the geofence
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Whether the geofence is currently violated
    /// </summary>
    public bool IsViolated
    {
        get => _isViolated;
        set => SetProperty(ref _isViolated, value);
    }

    /// <summary>
    /// Whether the geofence is enabled (alias for IsActive for compatibility)
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Center point as GeoCoordinate
    /// </summary>
    public GeoCoordinate Center => new GeoCoordinate(_centerLatitude, _centerLongitude);

    /// <summary>
    /// Compatibility property: Points as GeoCoordinate list (for Avalonia compatibility)
    /// </summary>
    public List<GeoCoordinate> Points
    {
        get
        {
            var points = new List<GeoCoordinate>();
            if (_type == GeofenceType.Circular)
            {
                // For circular, return center point
                points.Add(new GeoCoordinate(_centerLatitude, _centerLongitude));
            }
            else
            {
                // For polygon, convert vertices to GeoCoordinates
                foreach (var vertex in _vertices)
                {
                    points.Add(new GeoCoordinate(vertex.Lat, vertex.Lon));
                }
            }
            return points;
        }
    }

    /// <summary>
    /// Validates the geofence data
    /// </summary>
    public bool IsValid()
    {
        if (_type == GeofenceType.Circular)
        {
            return _radius > 0 && _centerLatitude >= -90 && _centerLatitude <= 90 &&
                   _centerLongitude >= -180 && _centerLongitude <= 180;
        }
        else if (_type == GeofenceType.Polygon)
        {
            return _vertices.Count >= 3 && _vertices.All(v => 
                v.Lat >= -90 && v.Lat <= 90 && v.Lon >= -180 && v.Lon <= 180);
        }
        return false;
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Vertex untuk geofence polygon
/// </summary>
public class GeofenceVertex
{
    public int Index { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>
    /// Latitude formatted to 6 decimal places
    /// </summary>
    public string LatFormatted => Lat.ToString("F6");

    /// <summary>
    /// Longitude formatted to 6 decimal places
    /// </summary>
    public string LonFormatted => Lon.ToString("F6");

    public GeofenceVertex(int index, double lat, double lon)
    {
        Index = index;
        Lat = lat;
        Lon = lon;
    }
}

/// <summary>
/// Status geofence
/// </summary>
public enum GeofenceStatus
{
    Inactive,
    Drawing,
    Active
}
