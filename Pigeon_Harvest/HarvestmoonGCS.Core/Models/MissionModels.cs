using System;
using System.Collections.Generic;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Represents a mission waypoint
/// </summary>
public class MissionWaypoint
{
    public int Sequence { get; set; }
    public MavCommand Command { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public MavFrame Frame { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsAutoContinue { get; set; }
    public float Param1 { get; set; }
    public float Param2 { get; set; }
    public float Param3 { get; set; }
    public float Param4 { get; set; }
    
    /// <summary>
    /// Human-readable description of the waypoint
    /// </summary>
    public string Description => $"{Command} - {Latitude:F6}, {Longitude:F6}";
}

/// <summary>
/// MAVLink command types - Extended for ArduPilot support
/// Supports Fixed Wing, Quadcopter, Hybrid VTOL, and Payload Dropping
/// Reference: https://mavlink.io/en/messages/common.html
/// </summary>
public enum MavCommand
{
    // Navigation commands
    NavWaypoint = 16,
    NavLoiterUnlim = 17,
    NavLoiterTurns = 18,
    NavLoiterTime = 19,
    NavReturnToLaunch = 20,
    NavLand = 21,
    NavTakeoff = 22,
    NavContinueAndChangeAlt = 30,
    NavLoiterToAlt = 31,
    NavSplineWaypoint = 82,
    
    // VTOL specific commands
    NavVtolTakeoff = 84,        // Takeoff using VTOL mode and transition to forward flight
    NavVtolLand = 85,           // Land using VTOL mode
    
    // Guided mode commands
    NavGuided = 90,
    NavDelay = 93,
    NavPayloadPlace = 94,
    NavLast = 95,
    
    // Condition commands
    ConditionDelay = 112,
    ConditionChangeAlt = 113,
    ConditionDistance = 114,
    ConditionYaw = 115,
    ConditionLast = 159,
    
    // DO commands
    DoJump = 177,
    DoChangeSpeed = 178,
    DoSetHome = 179,
    DoSetParameter = 180,
    DoSetRelay = 181,
    DoRepeatRelay = 182,
    DoSetServo = 183,
    DoRepeatServo = 184,
    DoFlighttermination = 185,
    DoChangeAltitude = 186,
    DoLandStart = 189,
    DoRallyLand = 190,
    DoGoAround = 191,
    DoReposition = 192,
    DoPauseContinue = 193,
    DoSetReverse = 194,
    DoSetRoiLocation = 195,
    DoSetRoiWpnextOffset = 196,
    DoSetRoiNone = 197,
    DoSetRoi = 201,
    DoDigicamControl = 203,
    DoDigicamConfigure = 202,
    DoMountControl = 205,
    DoMountConfigure = 204,
    DoSetCamTriggDist = 206,
    DoFenceEnable = 207,
    DoParachute = 208,
    DoMotorTest = 209,
    DoInvertedFlight = 210,
    DoGripper = 211,
    DoAutotunEnable = 212,
    DoSetCamTriggInterval = 214,
    DoMountControlQuat = 220,
    DoGuidedMaster = 221,
    DoGuidedLimits = 222,
    DoEngineControl = 223,
    DoSetMissionCurrent = 224,
    DoLast = 240,
    
    // VTOL transition command
    DoVtolTransition = 3000,    // Request VTOL transition (MC to FW or FW to MC)
    
    // Payload/Gripper commands for dropping
    DoWinchRelativeLengthControl = 42600,
    
    // Preflight commands
    PreflightCalibration = 241,
    PreflightSetSensorOffsets = 242,
    PreflightUavcan = 243,
    PreflightStorage = 245,
    PreflightRebootShutdown = 246,
    
    // Mission commands
    MissionStart = 300,
    ComponentArmDisarm = 400,
    GetHomePosition = 410,
    StartRxPair = 500,
    GetMessageInterval = 510,
    SetMessageInterval = 511,
    RequestMessage = 512,
    RequestProtocolVersion = 519,
    RequestAutopilotCapabilities = 520,
    RequestCameraInformation = 521,
    RequestCameraSettings = 522,
    RequestStorageInformation = 525,
    StorageFormat = 526,
    RequestCameraCaptureStatus = 527,
    RequestFlightInformation = 528,
    ResetCameraSettings = 529,
    SetCameraMode = 530,
    SetCameraZoom = 531,
    SetCameraFocus = 532,
    JumpTag = 600,
    DoJumpTag = 601,
    
    // Image/Video commands
    ImageStartCapture = 2000,
    ImageStopCapture = 2001,
    RequestCameraImageCapture = 2002,
    DoTriggerControl = 2003,
    VideoStartCapture = 2500,
    VideoStopCapture = 2501,
    VideoStartStreaming = 2502,
    VideoStopStreaming = 2503,
    
    // Panorama
    PanoramaCreate = 2800,
    
    // Payload commands
    PayloadPrepareDeploy = 30001,
    PayloadControlDeploy = 30002,
    
    // Waypoint user actions
    WaypointUser1 = 31000,
    WaypointUser2 = 31001,
    WaypointUser3 = 31002,
    WaypointUser4 = 31003,
    WaypointUser5 = 31004
}

/// <summary>
/// MAVLink coordinate frame types
/// </summary>
public enum MavFrame
{
    Global = 0,
    LocalNed = 1,
    Mission = 2,
    GlobalRelativeAlt = 3,
    LocalEnu = 4,
    GlobalInt = 5,
    GlobalRelativeAltInt = 6,
    LocalOffsetNed = 7,
    BodyNed = 8,
    BodyOffsetNed = 9,
    GlobalTerrainAlt = 10,
    GlobalTerrainAltInt = 11
}

/// <summary>
/// Mission operation result
/// </summary>
public class MissionOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int ItemsProcessed { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Mission statistics
/// </summary>
public class MissionStatistics
{
    public int TotalWaypoints { get; set; }
    public double TotalDistance { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public double MaxAltitude { get; set; }
    public double MinAltitude { get; set; }
}

/// <summary>
/// Mission validation result
/// </summary>
public class MissionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// VTOL state enumeration
/// </summary>
public enum MavVtolState
{
    Undefined = 0,          // MAV is not configured as VTOL
    TransitionToFw = 1,     // VTOL is in transition from multicopter to fixed-wing
    TransitionToMc = 2,     // VTOL is in transition from fixed-wing to multicopter
    Mc = 3,                 // VTOL is in multicopter state
    Fw = 4                  // VTOL is in fixed-wing state
}

/// <summary>
/// Vehicle type enumeration
/// </summary>
public enum MavType
{
    Generic = 0,
    FixedWing = 1,
    Quadrotor = 2,
    Coaxial = 3,
    Helicopter = 4,
    AntennaTracker = 5,
    Gcs = 6,
    Airship = 7,
    FreeBalloon = 8,
    Rocket = 9,
    GroundRover = 10,
    SurfaceBoat = 11,
    Submarine = 12,
    Hexarotor = 13,
    Octorotor = 14,
    Tricopter = 15,
    FlappingWing = 16,
    Kite = 17,
    OnboardController = 18,
    VtolDuorotor = 19,      // Two-rotor VTOL (Tailsitter)
    VtolQuadrotor = 20,     // Quad-rotor VTOL (Tailsitter)
    VtolTiltrotor = 21,     // Tiltrotor VTOL
    VtolReserved2 = 22,
    VtolReserved3 = 23,
    VtolReserved4 = 24,
    VtolReserved5 = 25,
    Gimbal = 26,
    Adsb = 27,
    Parafoil = 28,
    Dodecarotor = 29,
    Camera = 30,
    ChargingStation = 31,
    Flarm = 32,
    Servo = 33,
    Odid = 34,
    Decarotor = 35,
    Battery = 36,
    Parachute = 37,
    Log = 38,
    Osd = 39,
    Imu = 40,
    Gps = 41,
    Winch = 42
}
