using System.ComponentModel;
using System.Runtime.CompilerServices;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Model untuk menyimpan pengaturan aplikasi
/// </summary>
public class AppSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _language = "en";
    private string _mapType = "ArcGISTopographic";
    private ConnectionSettings _connection = new ConnectionSettings();
    private MapSettings _map = new MapSettings();
    private UiSettings _ui = new UiSettings();
    private AISettings _ai = new AISettings();

    /// <summary>
    /// Bahasa aplikasi (en = English, id = Indonesia)
    /// </summary>
    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    /// <summary>
    /// Jenis peta yang dipilih
    /// </summary>
    public string MapType
    {
        get => _mapType;
        set => SetProperty(ref _mapType, value);
    }

    /// <summary>
    /// Pengaturan koneksi
    /// </summary>
    public ConnectionSettings Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    /// <summary>
    /// Pengaturan peta
    /// </summary>
    public MapSettings Map
    {
        get => _map;
        set => SetProperty(ref _map, value);
    }

    /// <summary>
    /// Pengaturan UI
    /// </summary>
    public UiSettings Ui
    {
        get => _ui;
        set => SetProperty(ref _ui, value);
    }

    /// <summary>
    /// Pengaturan AI
    /// </summary>
    public AISettings AI
    {
        get => _ai;
        set => SetProperty(ref _ai, value);
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
/// Pengaturan koneksi MAVLink
/// </summary>
public class ConnectionSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _connectionType = "TCP";
    private string _ipAddress = "127.0.0.1";
    private int _port = 5760;
    private string _serialPort = "COM1";
    private int _baudRate = 57600;
    private bool _autoConnect = false;

    /// <summary>
    /// Tipe koneksi (TCP, UDP, Serial)
    /// </summary>
    public string ConnectionType
    {
        get => _connectionType;
        set => SetProperty(ref _connectionType, value);
    }

    /// <summary>
    /// Alamat IP untuk koneksi TCP/UDP
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    /// <summary>
    /// Port untuk koneksi TCP/UDP
    /// </summary>
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    /// <summary>
    /// Nama port serial (COM1, /dev/ttyUSB0, dll)
    /// </summary>
    public string SerialPort
    {
        get => _serialPort;
        set => SetProperty(ref _serialPort, value);
    }

    /// <summary>
    /// Baud rate untuk koneksi serial
    /// </summary>
    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    /// <summary>
    /// Otomatis connect saat aplikasi dimulai
    /// </summary>
    public bool AutoConnect
    {
        get => _autoConnect;
        set => SetProperty(ref _autoConnect, value);
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
/// Pengaturan peta
/// </summary>
public class MapSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _followVehicle = true;
    private float _defaultWaypointRadius = 50.0f;
    private float _defaultWaypointAltitude = 100.0f;
    private bool _showGeofence = false;
    private float _geofenceRadius = 1000.0f;

    /// <summary>
    /// Otomatis ikuti posisi wahana di peta
    /// </summary>
    public bool FollowVehicle
    {
        get => _followVehicle;
        set => SetProperty(ref _followVehicle, value);
    }

    /// <summary>
    /// Radius default untuk waypoint baru (meter)
    /// </summary>
    public float DefaultWaypointRadius
    {
        get => _defaultWaypointRadius;
        set => SetProperty(ref _defaultWaypointRadius, value);
    }

    /// <summary>
    /// Altitude default untuk waypoint baru (meter)
    /// </summary>
    public float DefaultWaypointAltitude
    {
        get => _defaultWaypointAltitude;
        set => SetProperty(ref _defaultWaypointAltitude, value);
    }

    /// <summary>
    /// Tampilkan geofence di peta
    /// </summary>
    public bool ShowGeofence
    {
        get => _showGeofence;
        set => SetProperty(ref _showGeofence, value);
    }

    /// <summary>
    /// Radius geofence (meter)
    /// </summary>
    public float GeofenceRadius
    {
        get => _geofenceRadius;
        set => SetProperty(ref _geofenceRadius, value);
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
/// Pengaturan UI
/// </summary>
public class UiSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _theme = "Dark";
    private bool _showAdvancedOptions = false;
    private bool _enableSoundAlerts = true;
    private bool _enableVoiceAlerts = false;

    /// <summary>
    /// Tema aplikasi (Light, Dark)
    /// </summary>
    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    /// <summary>
    /// Tampilkan opsi advanced
    /// </summary>
    public bool ShowAdvancedOptions
    {
        get => _showAdvancedOptions;
        set => SetProperty(ref _showAdvancedOptions, value);
    }

    /// <summary>
    /// Aktifkan alert suara
    /// </summary>
    public bool EnableSoundAlerts
    {
        get => _enableSoundAlerts;
        set => SetProperty(ref _enableSoundAlerts, value);
    }

    /// <summary>
    /// Aktifkan alert suara (voice)
    /// </summary>
    public bool EnableVoiceAlerts
    {
        get => _enableVoiceAlerts;
        set => SetProperty(ref _enableVoiceAlerts, value);
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
