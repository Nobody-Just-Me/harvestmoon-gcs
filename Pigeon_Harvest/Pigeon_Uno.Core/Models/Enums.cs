namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Daftar perintah untuk dikirim ke wahana
/// </summary>
public enum Command
{
    /// <summary>
    /// Perintah ARM (siapkan motor)
    /// </summary>
    ARM,

    /// <summary>
    /// Perintah DISARM (matikan motor)
    /// </summary>
    DISARM,

    /// <summary>
    /// Perintah auto take off
    /// </summary>
    TAKE_OFF = 0xAA,

    /// <summary>
    /// Perintah auto landing
    /// </summary>
    LAND = 0xCC,

    /// <summary>
    /// Perintah Return to Launch (kembali ke home)
    /// </summary>
    RTL,

    /// <summary>
    /// Perintah pause mission
    /// </summary>
    PAUSE,

    /// <summary>
    /// Perintah continue mission
    /// </summary>
    CONTINUE,

    /// <summary>
    /// Perintah membatalkan auto take-off
    /// </summary>
    BATALKAN = 0xBB
}

/// <summary>
/// Daftar identifier protokol komunikasi
/// </summary>
public enum BufferHeader
{
    /// <summary>
    /// Protokol prorietary EFALCON 4.0
    /// </summary>
    EFALCON4 = 'W',

    /// <summary>
    /// Protokol MAVLINK Versi 1.0
    /// </summary>
    MAVLINK1 = 0xFE,

    /// <summary>
    /// Protokol MAVLINK Versi 2.0
    /// </summary>
    MAVLINK2 = 0xFD,

    /// <summary>
    /// Protokol prorietary TRITON
    /// </summary>
    TRITON = 'T'
}

/// <summary>
/// Daftar mode penerbangan
/// </summary>
public enum FlightMode
{
    /// <summary>
    /// Mode tidak diketahui
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// Terbang secara manual
    /// </summary>
    DISARMED,

    /// <summary>
    /// Terbang secara manual
    /// </summary>
    ARMED,

    /// <summary>
    /// Terbang secara manual
    /// </summary>
    MANUAL,

    /// <summary>
    /// Terbang secara manual
    /// </summary>
    HOLD_ALTITUDE,

    /// <summary>
    /// Terbang dengan stabilisasi
    /// </summary>
    STABILIZER,

    /// <summary>
    /// Terbang mengitari tempat
    /// </summary>
    LOITER,

    /// <summary>
    /// Terbang secara autopilot
    /// </summary>
    AUTO,

    /// <summary>
    /// Sedang takeoff
    /// </summary>
    TAKEOFF,

    /// <summary>
    /// Sedang landing
    /// </summary>
    LAND,

    /// <summary>
    /// kembali ke titik awal
    /// </summary>
    RTL,

    /// <summary>
    /// Mode brake (berhenti di tempat)
    /// </summary>
    BRAKE,

    /// <summary>
    /// QStabilize mode, untuk wahana VTOL
    /// </summary>
    Q_Stabilize,

    /// <summary>
    /// QHover mode, untuk wahana VTOL
    /// </summary>
    Q_Hover,

    /// <summary>
    /// QLand mode, untuk wahana VTOL
    /// </summary>
    Q_Land,

    /// <summary>
    /// FBWA mode, untuk wahana VTOL
    /// </summary>
    FBWA,

}

/// <summary>
/// Identifier penggunaan efalcon
/// </summary>
public enum TipeDevice
{
    /// <summary>
    /// Antenna Tracker (Triton)
    /// </summary>
    TRACKER,

    /// <summary>
    /// Wahana UAV
    /// </summary>
    WAHANA
}

public enum ConnType
{
    Internet,
    WIFI,
    SerialPort,
    UDP
}

public enum ServoFunction
{
    Disabled = 0,
    RCPassThru = 1,
    Flap = 2,
    FlapAuto = 3,
    Aileron = 4,
    MountPan = 6,
    MountTilt = 7,
    MountRoll = 8,
    MountOpen = 9,
    CameraTrigger = 10,
    Release = 11,
    Mount2Pan = 12,
    Mount2Tilt = 13,
    Mount2Roll = 14,
    Mount2Open = 15,
    DifferentialSpoilerLeft1 = 16,
    DifferentialSpoilerRight1 = 17,
    DifferentialSpoilerLeft2 = 86,
    DifferentialSpoilerRight2 = 87,
    Elevator = 19,
    Rudder = 21,
    FlaperonLeft = 24,
    FlaperonRight = 25,
    GroundSteering = 26,
    Parachute = 27,
    EPM = 28,
    LandingGear = 29,
    EngineRunEnable = 30,
    HeliRSC = 31,
    HeliTailRSC = 32,
    Motor1 = 33,
    Motor2 = 34,
    Motor3 = 35,
    Motor4 = 36,
    Motor5 = 37,
    Motor6 = 38,
    Motor7 = 39,
    Motor8 = 40,
    MotorTilt = 41,
    RCIN1 = 51,
    RCIN2 = 52,
    RCIN3 = 53,
    RCIN4 = 54,
    RCIN5 = 55,
    RCIN6 = 56,
    RCIN7 = 57,
    RCIN8 = 58,
    RCIN9 = 59,
    RCIN10 = 60,
    RCIN11 = 61,
    RCIN12 = 62,
    RCIN13 = 63,
    RCIN14 = 64,
    RCIN15 = 65,
    RCIN16 = 66,
    Ignition = 67,
    Choke = 68,
    Starter = 69,
    Throttle = 70,
    TrackerYaw = 71,
    TrackerPitch = 72,
    ThrottleLeft = 73,
    ThrottleRight = 74,
    TiltMotorLeft = 75,
    TiltMotorRight = 76,
    ElevonLeft = 77,
    ElevonRight = 78,
    VTailLeft = 79,
    VTailRight = 80,
    BoostThrottle = 81,
    Motor9 = 82,
    Motor10 = 83,
    Motor11 = 84,
    Motor12 = 85,
    Winch = 88
}

/// <summary>
/// Tipe koneksi komunikasi
/// </summary>
public enum ConnectionType
{
    TCP,
    UDP,
    Serial,
    LoRa,
    WebSocket
}

/// <summary>
/// Tipe kalibrasi sensor
/// </summary>
public enum CalibrationType
{
    Accelerometer,
    Compass,
    Gyroscope
}

/// <summary>
/// Hasil kalibrasi
/// </summary>
public enum CalibrationResult
{
    None,
    Success,
    Failed
}

/// <summary>
/// Geofence boundary types
/// </summary>
public enum GeofenceType
{
    /// <summary>
    /// Circular geofence with center point and radius
    /// </summary>
    Circular,

    /// <summary>
    /// Polygon geofence with multiple boundary points
    /// </summary>
    Polygon
}

/// <summary>
/// Actions to take when geofence is violated
/// </summary>
public enum GeofenceAction
{
    /// <summary>
    /// No action, just log the violation
    /// </summary>
    None,

    /// <summary>
    /// Display warning to operator
    /// </summary>
    Warning,

    /// <summary>
    /// Return to launch point
    /// </summary>
    RTL,

    /// <summary>
    /// Land immediately
    /// </summary>
    Land,

    /// <summary>
    /// Hold position (loiter)
    /// </summary>
    Hold
}

/// <summary>
/// Geofence violation severity levels
/// </summary>
public enum GeofenceViolationType
{
    /// <summary>
    /// Vehicle is approaching the boundary (warning zone)
    /// </summary>
    Approaching,

    /// <summary>
    /// Vehicle has breached the boundary
    /// </summary>
    Breach,

    /// <summary>
    /// Vehicle is far outside the boundary (critical)
    /// </summary>
    Critical
}

/// <summary>
/// Copter flight modes (ArduCopter)
/// </summary>
public enum CopterMode
{
    Stabilize = 0,
    Acro = 1,
    AltHold = 2,
    Auto = 3,
    Guided = 4,
    Loiter = 5,
    RTL = 6,
    Circle = 7,
    Land = 9,
    Drift = 11,
    Sport = 13,
    Flip = 14,
    AutoTune = 15,
    PosHold = 16,
    Brake = 17,
    Throw = 18,
    AvoidADSB = 19,
    GuidedNoGPS = 20,
    SmartRTL = 21,
    FlowHold = 22,
    Follow = 23,
    ZigZag = 24,
    SystemID = 25,
    AutoRotate = 26,
    AutoRTL = 27
}

/// <summary>
/// Plane flight modes (ArduPlane)
/// </summary>
public enum PlaneMode
{
    Manual = 0,
    Circle = 1,
    Stabilize = 2,
    Training = 3,
    Acro = 4,
    FlyByWireA = 5,
    FlyByWireB = 6,
    Cruise = 7,
    Autotune = 8,
    Auto = 10,
    RTL = 11,
    Loiter = 12,
    Takeoff = 13,
    AvoidADSB = 14,
    Guided = 15,
    Initializing = 16,
    QStabilize = 17,
    QHover = 18,
    QLoiter = 19,
    QLand = 20,
    QRTL = 21,
    QAutotune = 22,
    QAcro = 23,
    Thermal = 24
}
