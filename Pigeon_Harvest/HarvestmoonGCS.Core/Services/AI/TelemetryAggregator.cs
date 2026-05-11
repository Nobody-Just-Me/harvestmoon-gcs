using System;
using System.Collections.Generic;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Aggregates telemetry snapshots into statistical summaries including min/max/avg/stddev
/// for all telemetry fields, battery drain rate, GPS dropout count, and altitude trend analysis.
/// </summary>
public static class TelemetryAggregator
{
    /// <summary>
    /// Computes statistical summary from a collection of telemetry snapshots.
    /// </summary>
    /// <param name="snapshots">Collection of telemetry snapshots to analyze</param>
    /// <returns>TelemetrySummary with computed statistics for all fields</returns>
    public static TelemetrySummary Summarize(IEnumerable<TelemetrySnapshot> snapshots)
    {
        var summary = new TelemetrySummary();

        var list = snapshots as List<TelemetrySnapshot> ?? new List<TelemetrySnapshot>(snapshots);
        int count = list.Count;
        summary.SnapshotCount = count;

        if (count == 0)
            return summary;

        DateTime firstTs = list[0].Timestamp;
        DateTime lastTs = list[count - 1].Timestamp;
        summary.WindowStart = firstTs;
        summary.WindowEnd = lastTs;

        var batteryStats = new RunningStats();
        var batteryVoltageStats = new RunningStats();
        var batteryCurrentStats = new RunningStats();
        var batteryTempStats = new RunningStats();

        var altitudeStats = new RunningStats();
        var relativeAltitudeStats = new RunningStats();
        var verticalSpeedStats = new RunningStats();
        var speedStats = new RunningStats();
        var groundSpeedStats = new RunningStats();
        var airSpeedStats = new RunningStats();

        var headingStats = new RunningStats();
        var headingErrorStats = new RunningStats();
        var rollStats = new RunningStats();
        var pitchStats = new RunningStats();
        var yawStats = new RunningStats();

        var vibXStats = new RunningStats();
        var vibYStats = new RunningStats();
        var vibZStats = new RunningStats();
        var vibrationMagnitudeStats = new RunningStats();

        var windStats = new RunningStats();
        var windGustStats = new RunningStats();

        var gpsHdopStats = new RunningStats();
        var gpsVdopStats = new RunningStats();
        var distanceFromHomeStats = new RunningStats();
        var linkQualityStats = new RunningStats();
        var packetLossStats = new RunningStats();
        var throttleStats = new RunningStats();
        var batteryDrainRateStats = new RunningStats();
        var climbRateStats = new RunningStats();
        var descentRateStats = new RunningStats();

        double prevBattery = double.NaN;
        DateTime prevTimestamp = default;
        FlightMode? prevMode = null;
        int dropoutCount = 0;
        int modeChanges = 0;
        int linkDegradedCount = 0;
        int highVibrationCount = 0;
        int highWindCount = 0;
        int gpsFixLostCount = 0;
        int missionWaypointChanges = 0;
        int previousMissionWaypoint = -1;
        double distanceTravelledMeters = 0;
        TelemetrySnapshot? previousSnapshot = null;

        for (int i = 0; i < count; i++)
        {
            var s = list[i];

            batteryStats.Add(s.BatteryPercent);
            batteryVoltageStats.Add(s.BatteryVoltage);
            batteryCurrentStats.Add(s.BatteryCurrent);
            batteryTempStats.Add(s.BatteryTemperatureC);
            altitudeStats.Add(s.Altitude);
            relativeAltitudeStats.Add(s.RelativeAltitude);
            verticalSpeedStats.Add(s.VerticalSpeed);
            speedStats.Add(s.Speed);
            groundSpeedStats.Add(s.GroundSpeed);
            airSpeedStats.Add(s.AirSpeed);
            headingStats.Add(s.Heading);
            headingErrorStats.Add(Math.Abs(s.HeadingErrorDeg));
            rollStats.Add(s.Roll);
            pitchStats.Add(s.Pitch);
            yawStats.Add(s.Yaw);
            vibXStats.Add(s.VibrationX);
            vibYStats.Add(s.VibrationY);
            vibZStats.Add(s.VibrationZ);
            vibrationMagnitudeStats.Add(s.VibrationMagnitude);
            windStats.Add(s.WindSpeed);
            windGustStats.Add(s.WindGustSpeed);
            gpsHdopStats.Add(s.GpsHdop);
            gpsVdopStats.Add(s.GpsVdop);
            distanceFromHomeStats.Add(s.DistanceFromHomeMeters);
            linkQualityStats.Add(s.LinkQualityPercent);
            packetLossStats.Add(s.PacketLossPercent);
            throttleStats.Add(s.ThrottlePercent);
            if (s.VerticalSpeed > 0)
            {
                climbRateStats.Add(s.VerticalSpeed);
            }
            else if (s.VerticalSpeed < 0)
            {
                descentRateStats.Add(Math.Abs(s.VerticalSpeed));
            }

            if (prevMode.HasValue && s.FlightMode != prevMode.Value)
                modeChanges++;
            prevMode = s.FlightMode;

            if (s.GpsSatellites == 0 || s.GpsHdop > 10)
                dropoutCount++;
            if (s.GpsFixType <= 1)
                gpsFixLostCount++;
            if (s.LinkQualityPercent > 0 && s.LinkQualityPercent < 50)
                linkDegradedCount++;
            if (s.VibrationMagnitude > 60)
                highVibrationCount++;
            if (s.WindSpeed > 10 || s.WindGustSpeed > 12)
                highWindCount++;
            if (previousMissionWaypoint >= 0 && s.MissionCurrentWaypoint != previousMissionWaypoint)
                missionWaypointChanges++;
            previousMissionWaypoint = s.MissionCurrentWaypoint;

            if (!double.IsNaN(prevBattery) && prevTimestamp != default)
            {
                var timeDelta = (s.Timestamp - prevTimestamp).TotalMinutes;
                if (timeDelta > 0)
                {
                    var drainRate = (prevBattery - s.BatteryPercent) / timeDelta;
                    if (drainRate > 0)
                    {
                        batteryDrainRateStats.Add(drainRate);
                    }

                    if (drainRate > summary.BatteryDrainRate || double.IsNaN(summary.BatteryDrainRate))
                        summary.BatteryDrainRate = drainRate;
                }
            }

            if (previousSnapshot != null)
            {
                distanceTravelledMeters += HaversineMeters(
                    previousSnapshot.GpsLatitude,
                    previousSnapshot.GpsLongitude,
                    s.GpsLatitude,
                    s.GpsLongitude);
            }

            prevBattery = s.BatteryPercent;
            prevTimestamp = s.Timestamp;
            previousSnapshot = s;
        }

        summary.BatteryMin = batteryStats.Min;
        summary.BatteryMax = batteryStats.Max;
        summary.BatteryAvg = batteryStats.Mean;
        summary.BatteryStdDev = batteryStats.StdDev;
        summary.BatteryVoltageMin = batteryVoltageStats.Min;
        summary.BatteryVoltageMax = batteryVoltageStats.Max;
        summary.BatteryVoltageAvg = batteryVoltageStats.Mean;
        summary.BatteryCurrentMin = batteryCurrentStats.Min;
        summary.BatteryCurrentMax = batteryCurrentStats.Max;
        summary.BatteryCurrentAvg = batteryCurrentStats.Mean;
        summary.BatteryTempMin = batteryTempStats.Min;
        summary.BatteryTempMax = batteryTempStats.Max;
        summary.BatteryTempAvg = batteryTempStats.Mean;
        summary.BatteryDrainRateAvg = batteryDrainRateStats.Mean;

        summary.AltitudeMin = altitudeStats.Min;
        summary.AltitudeMax = altitudeStats.Max;
        summary.AltitudeAvg = altitudeStats.Mean;
        summary.AltitudeStdDev = altitudeStats.StdDev;
        summary.RelativeAltitudeMin = relativeAltitudeStats.Min;
        summary.RelativeAltitudeMax = relativeAltitudeStats.Max;
        summary.RelativeAltitudeAvg = relativeAltitudeStats.Mean;
        summary.VerticalSpeedMin = verticalSpeedStats.Min;
        summary.VerticalSpeedMax = verticalSpeedStats.Max;
        summary.VerticalSpeedAvg = verticalSpeedStats.Mean;
        summary.ClimbRateAvg = climbRateStats.Mean;
        summary.DescentRateAvg = descentRateStats.Mean;

        summary.SpeedMin = speedStats.Min;
        summary.SpeedMax = speedStats.Max;
        summary.SpeedAvg = speedStats.Mean;
        summary.SpeedStdDev = speedStats.StdDev;
        summary.GroundSpeedMin = groundSpeedStats.Min;
        summary.GroundSpeedMax = groundSpeedStats.Max;
        summary.GroundSpeedAvg = groundSpeedStats.Mean;
        summary.AirSpeedMin = airSpeedStats.Min;
        summary.AirSpeedMax = airSpeedStats.Max;
        summary.AirSpeedAvg = airSpeedStats.Mean;

        summary.HeadingMin = headingStats.Min;
        summary.HeadingMax = headingStats.Max;
        summary.HeadingAvg = headingStats.Mean;
        summary.HeadingStdDev = headingStats.StdDev;
        summary.HeadingErrorAvg = headingErrorStats.Mean;
        summary.HeadingErrorMax = headingErrorStats.Max;
        summary.RollMin = rollStats.Min;
        summary.RollMax = rollStats.Max;
        summary.RollAvg = rollStats.Mean;
        summary.PitchMin = pitchStats.Min;
        summary.PitchMax = pitchStats.Max;
        summary.PitchAvg = pitchStats.Mean;
        summary.YawMin = yawStats.Min;
        summary.YawMax = yawStats.Max;
        summary.YawAvg = yawStats.Mean;

        summary.VibrationXMin = vibXStats.Min;
        summary.VibrationXMax = vibXStats.Max;
        summary.VibrationXAvg = vibXStats.Mean;
        summary.VibrationXStdDev = vibXStats.StdDev;

        summary.VibrationYMin = vibYStats.Min;
        summary.VibrationYMax = vibYStats.Max;
        summary.VibrationYAvg = vibYStats.Mean;
        summary.VibrationYStdDev = vibYStats.StdDev;

        summary.VibrationZMin = vibZStats.Min;
        summary.VibrationZMax = vibZStats.Max;
        summary.VibrationZAvg = vibZStats.Mean;
        summary.VibrationZStdDev = vibZStats.StdDev;
        summary.VibrationMagnitudeMin = vibrationMagnitudeStats.Min;
        summary.VibrationMagnitudeMax = vibrationMagnitudeStats.Max;
        summary.VibrationMagnitudeAvg = vibrationMagnitudeStats.Mean;

        summary.WindSpeedMin = windStats.Min;
        summary.WindSpeedMax = windStats.Max;
        summary.WindSpeedAvg = windStats.Mean;
        summary.WindSpeedStdDev = windStats.StdDev;
        summary.WindGustMax = windGustStats.Max;
        summary.GpsHdopAvg = gpsHdopStats.Mean;
        summary.GpsHdopMax = gpsHdopStats.Max;
        summary.GpsVdopAvg = gpsVdopStats.Mean;
        summary.GpsVdopMax = gpsVdopStats.Max;
        summary.DistanceFromHomeMax = distanceFromHomeStats.Max;
        summary.DistanceFromHomeAvg = distanceFromHomeStats.Mean;
        summary.DistanceTravelledMeters = distanceTravelledMeters;
        summary.LinkQualityMin = linkQualityStats.Min;
        summary.LinkQualityMax = linkQualityStats.Max;
        summary.LinkQualityAvg = linkQualityStats.Mean;
        summary.PacketLossAvg = packetLossStats.Mean;
        summary.PacketLossMax = packetLossStats.Max;
        summary.ThrottleAvg = throttleStats.Mean;
        summary.ThrottleMax = throttleStats.Max;

        summary.DropoutCount = dropoutCount;
        summary.ModeChanges = modeChanges;
        summary.LinkDegradedCount = linkDegradedCount;
        summary.HighVibrationCount = highVibrationCount;
        summary.HighWindCount = highWindCount;
        summary.GpsFixLostCount = gpsFixLostCount;
        summary.MissionWaypointChanges = missionWaypointChanges;
        summary.StabilityScore = ComputeStabilityScore(summary);

        summary.AltitudeTrend = ComputeTrend(list, s => s.Altitude, 30);

        return summary;
    }

    /// <summary>
    /// Computes the trend direction (Increasing, Decreasing, Stable, Oscillating)
    /// for a telemetry field over a sliding window using linear regression.
    /// </summary>
    private static Trend ComputeTrend(List<TelemetrySnapshot> snapshots, Func<TelemetrySnapshot, double> selector, int windowSeconds)
    {
        if (snapshots.Count < 3)
            return Trend.Stable;

        DateTime cutoff = snapshots[snapshots.Count - 1].Timestamp.AddSeconds(-windowSeconds);

        int windowStart = 0;
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].Timestamp >= cutoff)
            {
                windowStart = i;
                break;
            }
        }

        int windowCount = snapshots.Count - windowStart;
        if (windowCount < 3)
            return Trend.Stable;

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < windowCount; i++)
        {
            double x = i;
            double y = selector(snapshots[windowStart + i]);
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        double denominator = windowCount * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < 1e-10)
            return Trend.Stable;

        double slope = (windowCount * sumXY - sumX * sumY) / denominator;

        double meanY = sumY / windowCount;
        if (Math.Abs(slope) / Math.Max(1, Math.Abs(meanY)) < 0.01)
            return Trend.Stable;

        int rise = 0, fall = 0;
        for (int i = 1; i < windowCount; i++)
        {
            double prev = selector(snapshots[windowStart + i - 1]);
            double curr = selector(snapshots[windowStart + i]);
            if (curr > prev) rise++;
            else if (curr < prev) fall++;
        }

        double totalDeltas = rise + fall;
        if (totalDeltas == 0)
            return Trend.Stable;

        double oscillationRatio = Math.Min(rise, fall) / totalDeltas;
        if (oscillationRatio > 0.35)
            return Trend.Oscillating;

        return slope > 0 ? Trend.Increasing : Trend.Decreasing;
    }

    private static double ComputeStabilityScore(TelemetrySummary summary)
    {
        // Weighted heuristic score 0..100 (higher = more stable).
        var attitudePenalty = Math.Clamp((summary.RollMax - summary.RollMin) * 0.2, 0, 35);
        var pitchPenalty = Math.Clamp((summary.PitchMax - summary.PitchMin) * 0.2, 0, 25);
        var vibrationPenalty = Math.Clamp(summary.VibrationMagnitudeAvg * 0.25, 0, 25);
        var windPenalty = Math.Clamp(summary.WindSpeedAvg * 0.6, 0, 15);

        var score = 100.0 - attitudePenalty - pitchPenalty - vibrationPenalty - windPenalty;
        return Math.Clamp(score, 0, 100);
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        if ((lat1 == 0 && lon1 == 0) || (lat2 == 0 && lon2 == 0))
            return 0;

        const double earthRadius = 6_371_000.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadius * c;
    }

    private static double ToRadians(double value) => value * (Math.PI / 180.0);
}
