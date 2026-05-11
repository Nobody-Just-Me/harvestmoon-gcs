using System;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Trend direction for telemetry parameters.
/// </summary>
public enum Trend
{
    Stable,
    Increasing,
    Decreasing,
    Oscillating
}

/// <summary>
/// Aggregated statistical summary of telemetry snapshots for LLM analysis prompts.
/// Computed by TelemetryAggregator from TelemetrySnapshot[].
/// </summary>
public class TelemetrySummary
{
    // Battery statistics
    public double BatteryMin { get; set; }
    public double BatteryMax { get; set; }
    public double BatteryAvg { get; set; }
    public double BatteryStdDev { get; set; }
    public double BatteryDrainRate { get; set; } // %/min
    public double BatteryDrainRateAvg { get; set; } // %/min
    public double BatteryVoltageMin { get; set; }
    public double BatteryVoltageMax { get; set; }
    public double BatteryVoltageAvg { get; set; }
    public double BatteryCurrentMin { get; set; }
    public double BatteryCurrentMax { get; set; }
    public double BatteryCurrentAvg { get; set; }
    public double BatteryTempMin { get; set; }
    public double BatteryTempMax { get; set; }
    public double BatteryTempAvg { get; set; }

    // Altitude statistics
    public double AltitudeMin { get; set; }
    public double AltitudeMax { get; set; }
    public double AltitudeAvg { get; set; }
    public double AltitudeStdDev { get; set; }
    public Trend AltitudeTrend { get; set; } = Trend.Stable;
    public double RelativeAltitudeMin { get; set; }
    public double RelativeAltitudeMax { get; set; }
    public double RelativeAltitudeAvg { get; set; }
    public double VerticalSpeedMin { get; set; }
    public double VerticalSpeedMax { get; set; }
    public double VerticalSpeedAvg { get; set; }
    public double ClimbRateAvg { get; set; }
    public double DescentRateAvg { get; set; }

    // Speed statistics
    public double SpeedMin { get; set; }
    public double SpeedMax { get; set; }
    public double SpeedAvg { get; set; }
    public double SpeedStdDev { get; set; }
    public double GroundSpeedMin { get; set; }
    public double GroundSpeedMax { get; set; }
    public double GroundSpeedAvg { get; set; }
    public double AirSpeedMin { get; set; }
    public double AirSpeedMax { get; set; }
    public double AirSpeedAvg { get; set; }

    // Heading statistics
    public double HeadingMin { get; set; }
    public double HeadingMax { get; set; }
    public double HeadingAvg { get; set; }
    public double HeadingStdDev { get; set; }
    public double HeadingErrorAvg { get; set; }
    public double HeadingErrorMax { get; set; }

    // Attitude statistics
    public double RollMin { get; set; }
    public double RollMax { get; set; }
    public double RollAvg { get; set; }
    public double PitchMin { get; set; }
    public double PitchMax { get; set; }
    public double PitchAvg { get; set; }
    public double YawMin { get; set; }
    public double YawMax { get; set; }
    public double YawAvg { get; set; }

    // Vibration statistics
    public double VibrationXMin { get; set; }
    public double VibrationXMax { get; set; }
    public double VibrationXAvg { get; set; }
    public double VibrationXStdDev { get; set; }

    public double VibrationYMin { get; set; }
    public double VibrationYMax { get; set; }
    public double VibrationYAvg { get; set; }
    public double VibrationYStdDev { get; set; }

    public double VibrationZMin { get; set; }
    public double VibrationZMax { get; set; }
    public double VibrationZAvg { get; set; }
    public double VibrationZStdDev { get; set; }
    public double VibrationMagnitudeMin { get; set; }
    public double VibrationMagnitudeMax { get; set; }
    public double VibrationMagnitudeAvg { get; set; }

    // Wind statistics
    public double WindSpeedMin { get; set; }
    public double WindSpeedMax { get; set; }
    public double WindSpeedAvg { get; set; }
    public double WindSpeedStdDev { get; set; }
    public double WindGustMax { get; set; }

    // GPS / link statistics
    public double GpsHdopAvg { get; set; }
    public double GpsHdopMax { get; set; }
    public double GpsVdopAvg { get; set; }
    public double GpsVdopMax { get; set; }
    public double DistanceFromHomeMax { get; set; }
    public double DistanceFromHomeAvg { get; set; }
    public double DistanceTravelledMeters { get; set; }
    public double LinkQualityMin { get; set; }
    public double LinkQualityMax { get; set; }
    public double LinkQualityAvg { get; set; }
    public double PacketLossAvg { get; set; }
    public double PacketLossMax { get; set; }
    public double ThrottleAvg { get; set; }
    public double ThrottleMax { get; set; }

    // Event counts
    public int DropoutCount { get; set; } // GPS lost events (satellites == 0 or HDOP > 10)
    public int ModeChanges { get; set; } // Flight mode changes
    public int LinkDegradedCount { get; set; }
    public int HighVibrationCount { get; set; }
    public int HighWindCount { get; set; }
    public int GpsFixLostCount { get; set; }
    public int MissionWaypointChanges { get; set; }
    public double StabilityScore { get; set; }

    // Sample window info
    public int SnapshotCount { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
}
