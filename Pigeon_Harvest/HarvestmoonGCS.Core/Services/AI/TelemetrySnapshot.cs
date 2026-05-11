using System;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services.AI;

public class TelemetrySnapshot
{
    public DateTime Timestamp { get; set; }
    public double BatteryVoltage { get; set; }
    public double BatteryCurrent { get; set; }
    public double BatteryPercent { get; set; }
    public double BatteryTemperatureC { get; set; }
    public double EscTemperatureC { get; set; }
    public double EstimatedRemainingMinutes { get; set; }
    public double GpsLatitude { get; set; }
    public double GpsLongitude { get; set; }
    public double GpsAltitude { get; set; }
    public int GpsSatellites { get; set; }
    public int GpsFixType { get; set; }
    public double GpsHdop { get; set; }
    public double GpsVdop { get; set; }
    public double Altitude { get; set; }
    public double RelativeAltitude { get; set; }
    public double DistanceFromHomeMeters { get; set; }
    public double Speed { get; set; }
    public double GroundSpeed { get; set; }
    public double AirSpeed { get; set; }
    public double VerticalSpeed { get; set; }
    public double Heading { get; set; }
    public double HeadingErrorDeg { get; set; }
    public double Roll { get; set; }
    public double Pitch { get; set; }
    public double Yaw { get; set; }
    public double RollRateDegPerSec { get; set; }
    public double PitchRateDegPerSec { get; set; }
    public double YawRateDegPerSec { get; set; }
    public FlightMode FlightMode { get; set; }
    public bool Armed { get; set; }
    public double ThrottlePercent { get; set; }
    public double LinkQualityPercent { get; set; }
    public double PacketLossPercent { get; set; }
    public double VibrationX { get; set; }
    public double VibrationY { get; set; }
    public double VibrationZ { get; set; }
    public double VibrationMagnitude => Math.Sqrt(
        (VibrationX * VibrationX) +
        (VibrationY * VibrationY) +
        (VibrationZ * VibrationZ));
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double WindGustSpeed { get; set; }
    public int MissionCurrentWaypoint { get; set; }
    public int MissionTotalWaypoints { get; set; }
    public int CompassCalibrationProgress1 { get; set; }
    public int CompassCalibrationProgress2 { get; set; }
    public double CpuLoadPercent { get; set; }
    public double RamUsagePercent { get; set; }
    public double ControllerTemperatureC { get; set; }
    public int RcChannel1 { get; set; }
    public int RcChannel2 { get; set; }
    public int RcChannel3 { get; set; }
    public int RcChannel4 { get; set; }
    public int RcChannel5 { get; set; }
    public int RcChannel6 { get; set; }
    public int RcChannel7 { get; set; }
    public int RcChannel8 { get; set; }
    public int ServoChannel1 { get; set; }
    public int ServoChannel2 { get; set; }
    public int ServoChannel3 { get; set; }
    public int ServoChannel4 { get; set; }
    public int ServoChannel5 { get; set; }
    public int ServoChannel6 { get; set; }
    public int ServoChannel7 { get; set; }
    public int ServoChannel8 { get; set; }
    public double? BatteryDrainRate { get; set; }
}
