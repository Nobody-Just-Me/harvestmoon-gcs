using System;

namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Represents flight statistics data
/// </summary>
public class FlightStatistics
{
    /// <summary>
    /// Total flight time
    /// </summary>
    public TimeSpan FlightTime { get; set; }

    /// <summary>
    /// Total distance traveled in meters
    /// </summary>
    public double TotalDistance { get; set; }

    /// <summary>
    /// Maximum altitude reached in meters
    /// </summary>
    public double MaxAltitude { get; set; }

    /// <summary>
    /// Maximum speed reached in m/s
    /// </summary>
    public double MaxSpeed { get; set; }

    /// <summary>
    /// Average speed in m/s
    /// </summary>
    public double AverageSpeed { get; set; }

    /// <summary>
    /// Number of waypoints completed
    /// </summary>
    public int WaypointsCompleted { get; set; }

    /// <summary>
    /// Date when the statistics were recorded
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of flights
    /// </summary>
    public int FlightCount { get; set; }

    /// <summary>
    /// Total flights (alias for FlightCount)
    /// </summary>
    public int TotalFlights
    {
        get => FlightCount;
        set => FlightCount = value;
    }

    /// <summary>
    /// Total battery used in mAh
    /// </summary>
    public double TotalBatteryUsed { get; set; }

    public FlightStatistics()
    {
        Date = DateTime.Now;
    }

    /// <summary>
    /// Reset all statistics to zero
    /// </summary>
    public void Reset()
    {
        FlightTime = TimeSpan.Zero;
        TotalDistance = 0;
        MaxAltitude = 0;
        MaxSpeed = 0;
        AverageSpeed = 0;
        WaypointsCompleted = 0;
        FlightCount = 0;
        TotalBatteryUsed = 0;
        Date = DateTime.Now;
    }

    /// <summary>
    /// Merge another statistics object into this one
    /// </summary>
    public void Merge(FlightStatistics other)
    {
        FlightTime += other.FlightTime;
        TotalDistance += other.TotalDistance;
        MaxAltitude = Math.Max(MaxAltitude, other.MaxAltitude);
        MaxSpeed = Math.Max(MaxSpeed, other.MaxSpeed);
        FlightCount += other.FlightCount;
        
        // Recalculate average speed
        if (FlightTime.TotalSeconds > 0)
        {
            AverageSpeed = TotalDistance / FlightTime.TotalSeconds;
        }
    }
}

/// <summary>
/// Time period for filtering statistics
/// </summary>
public enum TimePeriod
{
    Today,
    ThisWeek,
    ThisMonth,
    AllTime
}
