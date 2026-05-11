using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Model data untuk node LoRa dalam jaringan relay
/// </summary>
public class LoRaNodeData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _nodeId;
    private string _nodeName = string.Empty;
    private bool _isOnline;
    private int _rssi;
    private double _snr;
    private DateTime _lastSeen;
    private LoRaConfig? _configuration;
    private double _latitude;
    private double _longitude;
    private float _altitude;
    private float _temperature;
    private float _humidity;
    private float _pressure;
    private float _batteryVoltage;
    private int _batteryPercent;
    private int _satellites;
    private double _speed;
    private int _vibration;
    private long _packetNumber;

    public int NodeId
    {
        get => _nodeId;
        set => SetProperty(ref _nodeId, value);
    }

    public string NodeName
    {
        get => _nodeName;
        set
        {
            if (SetProperty(ref _nodeName, value))
                OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>Display name for UI (NodeName or default "Node N").</summary>
    public string Name => string.IsNullOrEmpty(_nodeName) ? $"Node {NodeId}" : _nodeName;

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public int RSSI
    {
        get => _rssi;
        set => SetProperty(ref _rssi, value);
    }

    public double SNR
    {
        get => _snr;
        set => SetProperty(ref _snr, value);
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set
        {
            SetProperty(ref _lastSeen, value);
            RefreshOnlineStatus();
        }
    }

    public LoRaConfig? Configuration
    {
        get => _configuration;
        set => SetProperty(ref _configuration, value);
    }

    public double Latitude
    {
        get => _latitude;
        set
        {
            if (SetProperty(ref _latitude, value))
            {
                OnPropertyChanged(nameof(LocationString));
                OnPropertyChanged(nameof(HasGpsFix));
            }
        }
    }

    public double Longitude
    {
        get => _longitude;
        set
        {
            if (SetProperty(ref _longitude, value))
            {
                OnPropertyChanged(nameof(LocationString));
                OnPropertyChanged(nameof(HasGpsFix));
            }
        }
    }

    public float Altitude
    {
        get => _altitude;
        set => SetProperty(ref _altitude, value);
    }

    public float Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, value);
    }

    public float Humidity
    {
        get => _humidity;
        set => SetProperty(ref _humidity, value);
    }

    public float Pressure
    {
        get => _pressure;
        set => SetProperty(ref _pressure, value);
    }

    public float BatteryVoltage
    {
        get => _batteryVoltage;
        set => SetProperty(ref _batteryVoltage, value);
    }

    public int BatteryPercent
    {
        get => _batteryPercent;
        set => SetProperty(ref _batteryPercent, value);
    }

    public int Satellites
    {
        get => _satellites;
        set => SetProperty(ref _satellites, value);
    }

    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    public int Vibration
    {
        get => _vibration;
        set => SetProperty(ref _vibration, value);
    }

    public long PacketNumber
    {
        get => _packetNumber;
        set => SetProperty(ref _packetNumber, value);
    }

    public string SignalQuality => $"{RSSI} dBm / {SNR:F1} dB";

    public string LocationString => HasGpsFix ? $"{Latitude:F6}, {Longitude:F6}" : "---, ---";

    public bool HasGpsFix => Math.Abs(Latitude) > double.Epsilon || Math.Abs(Longitude) > double.Epsilon;

    public void RefreshOnlineStatus()
    {
        IsOnline = LastSeen != default && (DateTime.Now - LastSeen).TotalSeconds < 90;
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
/// Pesan LoRa yang diterima
/// </summary>
public class LoRaMessage
{
    public int NodeId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int RSSI { get; set; }
    public double SNR { get; set; }
    public DateTime Timestamp { get; set; }
}
