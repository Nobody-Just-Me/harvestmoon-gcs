namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Servo configuration model for calibration
/// </summary>
public class ServoConfig
{
    /// <summary>
    /// Servo channel number (1-16)
    /// </summary>
    public int Channel { get; set; }

    /// <summary>
    /// Whether servo output is reversed
    /// </summary>
    public bool Reverse { get; set; }

    /// <summary>
    /// Servo function assignment
    /// </summary>
    public int Function { get; set; }

    /// <summary>
    /// Minimum PWM value (0-2000)
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Trim/neutral PWM value (0-2000)
    /// </summary>
    public int Trim { get; set; }

    /// <summary>
    /// Maximum PWM value (0-2000)
    /// </summary>
    public int Max { get; set; }

    /// <summary>
    /// Current output percentage (0-100%)
    /// </summary>
    public int CurrentOutput { get; set; }
}

/// <summary>
/// PID parameters for flight control tuning
/// </summary>
public class PIDParameters
{
    /// <summary>
    /// Proportional gain
    /// </summary>
    public float P { get; set; }

    /// <summary>
    /// Integral gain
    /// </summary>
    public float I { get; set; }

    /// <summary>
    /// Derivative gain
    /// </summary>
    public float D { get; set; }

    /// <summary>
    /// Maximum integrator value
    /// </summary>
    public float IMAX { get; set; }

    /// <summary>
    /// Filter frequency for error signal
    /// </summary>
    public float FLTE { get; set; }

    /// <summary>
    /// Filter frequency for derivative signal
    /// </summary>
    public float FLTD { get; set; }

    /// <summary>
    /// Filter frequency for target signal
    /// </summary>
    public float FLTT { get; set; }
}

/// <summary>
/// Flight mode PWM range definition
/// </summary>
public class FlightModeRange
{
    /// <summary>
    /// Mode number (1-6)
    /// </summary>
    public int ModeNumber { get; set; }

    /// <summary>
    /// Minimum PWM value for this mode
    /// </summary>
    public int MinPWM { get; set; }

    /// <summary>
    /// Maximum PWM value for this mode
    /// </summary>
    public int MaxPWM { get; set; }

    /// <summary>
    /// Display label for this mode range
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Predefined flight mode ranges
    /// </summary>
    public static readonly FlightModeRange[] PredefinedRanges = new[]
    {
        new FlightModeRange { ModeNumber = 1, MinPWM = 0, MaxPWM = 1230, Label = "Mode 1" },
        new FlightModeRange { ModeNumber = 2, MinPWM = 1231, MaxPWM = 1360, Label = "Mode 2" },
        new FlightModeRange { ModeNumber = 3, MinPWM = 1361, MaxPWM = 1490, Label = "Mode 3" },
        new FlightModeRange { ModeNumber = 4, MinPWM = 1491, MaxPWM = 1620, Label = "Mode 4" },
        new FlightModeRange { ModeNumber = 5, MinPWM = 1621, MaxPWM = 1749, Label = "Mode 5" },
        new FlightModeRange { ModeNumber = 6, MinPWM = 1750, MaxPWM = 2200, Label = "Mode 6" }
    };
}

/// <summary>
/// Compass device information
/// </summary>
public class CompassDevice
{
    /// <summary>
    /// Compass number (1-3)
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Device ID
    /// </summary>
    public int DevID { get; set; }

    /// <summary>
    /// Device type description
    /// </summary>
    public string DevType { get; set; } = string.Empty;
}
