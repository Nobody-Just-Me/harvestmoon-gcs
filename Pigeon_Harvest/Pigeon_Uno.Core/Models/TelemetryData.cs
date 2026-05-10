using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Model data telemetri lengkap dari wahana
/// </summary>
public class TelemetryData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private DateTime _timestamp;
    private double _latitude;
    private double _longitude;
    private double _altitude;
    private double _relativeAltitude;
    private double _roll;
    private double _pitch;
    private double _yaw;
    private double _heading;
    private double _groundSpeed;
    private double _airSpeed;
    private double _verticalSpeed;
    private double _barometers;
    private double _batteryVoltage;
    private double _batteryCurrent;
    private int _batteryRemaining;
    private FlightMode _flightMode;
    private bool _isArmed;
    private int _satelliteCount;
    private double _hdop;
    private int _signalStrength;
    private int _throttlePercent;
    private double _batteryPercentage;
    private int _gpsFixType;
    private double _speed;

    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    public string AltitudeDisplay => $"{Altitude:F1} m";
    public string SpeedDisplay => $"{(Speed > 0 ? Speed : GroundSpeed):F1} m/s";
    public string RollDisplay => $"{Roll:F1}°";
    public string PitchDisplay => $"{Pitch:F1}°";
    public string HeadingDisplay => $"{Heading:F0}°";
    public string BarometersDisplay => $"{Barometers:F1} m";

    public int GPSFixType
    {
        get => _gpsFixType;
        set => SetProperty(ref _gpsFixType, value);
    }

    public double BatteryPercentage
    {
        get => _batteryPercentage;
        set => SetProperty(ref _batteryPercentage, value);
    }

    public double BatteryPercent => _batteryPercentage;

    public int ThrottlePercent
    {
        get => _throttlePercent;
        set => SetProperty(ref _throttlePercent, value);
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, value);
    }

    public double Altitude
    {
        get => _altitude;
        set => SetProperty(ref _altitude, value);
    }

    public double RelativeAltitude
    {
        get => _relativeAltitude;
        set => SetProperty(ref _relativeAltitude, value);
    }

    public double Roll
    {
        get => _roll;
        set => SetProperty(ref _roll, value);
    }

    public double Pitch
    {
        get => _pitch;
        set => SetProperty(ref _pitch, value);
    }

    public double Yaw
    {
        get => _yaw;
        set => SetProperty(ref _yaw, value);
    }

    public double Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    public double GroundSpeed
    {
        get => _groundSpeed;
        set => SetProperty(ref _groundSpeed, value);
    }

    public double AirSpeed
    {
        get => _airSpeed;
        set => SetProperty(ref _airSpeed, value);
    }

    public double VerticalSpeed
    {
        get => _verticalSpeed;
        set => SetProperty(ref _verticalSpeed, value);
    }

    /// <summary> Ketinggian dari barometer (meter). </summary>
    public double Barometers
    {
        get => _barometers;
        set => SetProperty(ref _barometers, value);
    }

    public double BatteryVoltage
    {
        get => _batteryVoltage;
        set => SetProperty(ref _batteryVoltage, value);
    }

    public double BatteryCurrent
    {
        get => _batteryCurrent;
        set => SetProperty(ref _batteryCurrent, value);
    }

    public int BatteryRemaining
    {
        get => _batteryRemaining;
        set => SetProperty(ref _batteryRemaining, value);
    }

    public FlightMode FlightMode
    {
        get => _flightMode;
        set => SetProperty(ref _flightMode, value);
    }

    public bool IsArmed
    {
        get => _isArmed;
        set => SetProperty(ref _isArmed, value);
    }

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => SetProperty(ref _satelliteCount, value);
    }

    public double HDOP
    {
        get => _hdop;
        set => SetProperty(ref _hdop, value);
    }

    // Legacy alias expected by older XAML bindings.
    public double Hdop
    {
        get => HDOP;
        set => HDOP = value;
    }

    public int SignalStrength
    {
        get => _signalStrength;
        set => SetProperty(ref _signalStrength, value);
    }

    // Legacy alias expected by older XAML bindings.
    public int Satellites
    {
        get => SatelliteCount;
        set => SatelliteCount = value;
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        NotifyDependentProperties(propertyName);
        return true;
    }

    private void NotifyDependentProperties(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(Altitude):
                OnPropertyChanged(nameof(AltitudeDisplay));
                break;
            case nameof(Speed):
            case nameof(GroundSpeed):
                OnPropertyChanged(nameof(SpeedDisplay));
                break;
            case nameof(Roll):
                OnPropertyChanged(nameof(RollDisplay));
                break;
            case nameof(Pitch):
                OnPropertyChanged(nameof(PitchDisplay));
                break;
            case nameof(Heading):
                OnPropertyChanged(nameof(HeadingDisplay));
                break;
            case nameof(Barometers):
                OnPropertyChanged(nameof(BarometersDisplay));
                break;
            case nameof(HDOP):
                OnPropertyChanged(nameof(Hdop));
                break;
            case nameof(SatelliteCount):
                OnPropertyChanged(nameof(Satellites));
                break;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
