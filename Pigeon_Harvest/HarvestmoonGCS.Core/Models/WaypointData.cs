using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Model data waypoint untuk mission planning
/// </summary>
public class WaypointData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _sequence;
    private double _latitude;
    private double _longitude;
    private double _altitude;
    private WaypointCommand _command;
    private double _param1;
    private double _param2;
    private double _param3;
    private double _param4;
    private bool _isCurrent;

    public int Sequence
    {
        get => _sequence;
        set => SetProperty(ref _sequence, value);
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

    public WaypointCommand Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }

    public double Param1
    {
        get => _param1;
        set => SetProperty(ref _param1, value);
    }

    public double Param2
    {
        get => _param2;
        set => SetProperty(ref _param2, value);
    }

    public double Param3
    {
        get => _param3;
        set => SetProperty(ref _param3, value);
    }

    public double Param4
    {
        get => _param4;
        set => SetProperty(ref _param4, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
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
/// Enum untuk tipe perintah waypoint
/// </summary>
public enum WaypointCommand
{
    Waypoint = 16,
    Loiter = 17,
    LoiterUnlimited = 17,
    LoiterTurns = 18,
    LoiterTime = 19,
    ReturnToLaunch = 20,
    Land = 21,
    TakeOff = 22,
    NavSplineWaypoint = 82,
    NavVtolTakeoff = 84,
    NavVtolLand = 85,
    Continue = 31,
    SetHome = 179,
    DoJump = 177,
    DoChangeSpeed = 178,
    DoSetHome = 179,
    DoSetRelay = 181,
    DoRepeatRelay = 182,
    DoSetServo = 183,
    DoRepeatServo = 184,
    DoDigicamControl = 203,
    DoMountControl = 205,
    DoVtolTransition = 3000
}
