using System;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Core.Services.AI;

public static class TelemetrySnapshotMapper
{
    public static TelemetrySnapshot ToSnapshot(this TelemetryData data, TelemetrySnapshot? previousSnapshot = null)
    {
        if (data == null)
            return new TelemetrySnapshot();

        var timestamp = data.Timestamp == default ? DateTime.UtcNow : data.Timestamp;
        var batteryVoltage = data.BatteryVoltage;
        var batteryPercent = data.BatteryPercent > 0
            ? data.BatteryPercent
            : EstimateBatteryPercent(batteryVoltage);
        var speed = data.Speed > 0 ? data.Speed : data.GroundSpeed;
        var heading = NormalizeDegrees(data.Heading);
        var linkQuality = EstimateLinkQualityPercent(data.SignalStrength);

        var snapshot = new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryVoltage = batteryVoltage,
            BatteryCurrent = data.BatteryCurrent,
            BatteryPercent = batteryPercent,
            GpsLatitude = data.Latitude,
            GpsLongitude = data.Longitude,
            GpsAltitude = data.Altitude,
            GpsSatellites = data.SatelliteCount,
            GpsFixType = data.GPSFixType > 0
                ? data.GPSFixType
                : EstimateGpsFixType(data.SatelliteCount, data.HDOP),
            GpsHdop = data.HDOP,
            RelativeAltitude = data.RelativeAltitude > 0 ? data.RelativeAltitude : data.Altitude,
            Altitude = data.Barometers,
            Speed = speed,
            GroundSpeed = data.GroundSpeed > 0 ? data.GroundSpeed : speed,
            AirSpeed = data.AirSpeed > 0 ? data.AirSpeed : speed,
            VerticalSpeed = data.VerticalSpeed,
            Heading = heading,
            Roll = data.Roll,
            Pitch = data.Pitch,
            Yaw = data.Yaw,
            FlightMode = data.FlightMode,
            Armed = data.IsArmed,
            ThrottlePercent = data.ThrottlePercent,
            LinkQualityPercent = linkQuality,
            PacketLossPercent = Math.Max(0, 100 - linkQuality)
        };

        if (previousSnapshot != null)
        {
            var timeDelta = (snapshot.Timestamp - previousSnapshot.Timestamp).TotalSeconds;
            if (timeDelta > 0)
            {
                var batteryDelta = previousSnapshot.BatteryPercent - snapshot.BatteryPercent;
                snapshot.BatteryDrainRate = batteryDelta / timeDelta;
                snapshot.RollRateDegPerSec = (snapshot.Roll - previousSnapshot.Roll) / timeDelta;
                snapshot.PitchRateDegPerSec = (snapshot.Pitch - previousSnapshot.Pitch) / timeDelta;
                snapshot.YawRateDegPerSec = CircularDeltaDegrees(snapshot.Yaw, previousSnapshot.Yaw) / timeDelta;
            }

            snapshot.HeadingErrorDeg = CircularDeltaDegrees(snapshot.Heading, previousSnapshot.Heading);
            snapshot.DistanceFromHomeMeters = previousSnapshot.DistanceFromHomeMeters +
                HaversineMeters(
                    previousSnapshot.GpsLatitude,
                    previousSnapshot.GpsLongitude,
                    snapshot.GpsLatitude,
                    snapshot.GpsLongitude);
        }

        return snapshot;
    }

    public static TelemetrySnapshot ToSnapshot(this FlightData data, TelemetrySnapshot? previousSnapshot = null, DateTime? timestamp = null)
    {
        if (data == null)
            return new TelemetrySnapshot();

        var gps = data.GPS ?? new GPSData();
        var imu = data.IMU ?? new Inertial();

        var snapshotTimestamp = timestamp ?? DateTime.UtcNow;
        var batteryVoltage = data.MavlinkMiliVolt > 0
            ? data.MavlinkMiliVolt / 1000.0
            : data.BatteryVolt > 0
                ? data.BatteryVolt / 1000.0
                : 0.0;
        var batteryCurrent = data.MavlinkCentiAmp > 0
            ? data.MavlinkCentiAmp / 100.0
            : data.BatteryCurr > 0
                ? data.BatteryCurr / 100.0
                : 0.0;
        var batteryPercent = EstimateBatteryPercent(batteryVoltage);

        var altitude = data.Barometers > 0 ? data.Barometers : data.AltitudeFloat;
        var verticalSpeed = 0.0;
        var heading = NormalizeDegrees(imu.Yaw);
        var speed = data.Speed;
        var linkQuality = EstimateLinkQualityPercent(data.Signal);
        var currentWaypoint = Math.Max(0, data.ModeChannel);
        var totalWaypoints = Math.Max(0, data.ModeCh1);

        if (previousSnapshot != null)
        {
            var timeDeltaSeconds = (snapshotTimestamp - previousSnapshot.Timestamp).TotalSeconds;
            if (timeDeltaSeconds > 0)
            {
                verticalSpeed = (altitude - previousSnapshot.Altitude) / timeDeltaSeconds;
            }
        }

        var snapshot = new TelemetrySnapshot
        {
            Timestamp = snapshotTimestamp,
            BatteryVoltage = batteryVoltage,
            BatteryCurrent = batteryCurrent,
            BatteryPercent = batteryPercent,
            GpsLatitude = gps.Latitude / 1e7,
            GpsLongitude = gps.Longitude / 1e7,
            GpsAltitude = data.AltitudeFloat,
            GpsSatellites = data.Sats > 0 ? data.Sats : gps.Sats,
            GpsFixType = EstimateGpsFixType(data.Sats > 0 ? data.Sats : gps.Sats, data.Hdop > 0 ? data.Hdop / 100.0 : gps.Hdop),
            GpsHdop = data.Hdop > 0 ? data.Hdop / 100.0 : gps.Hdop,
            RelativeAltitude = data.AltitudeFloat,
            Altitude = altitude,
            Speed = speed,
            GroundSpeed = speed,
            AirSpeed = speed,
            VerticalSpeed = verticalSpeed,
            Heading = heading,
            Roll = imu.Roll,
            Pitch = imu.Pitch,
            Yaw = imu.Yaw,
            FlightMode = data.FlightMode,
            Armed = data.FlightMode != FlightMode.DISARMED,
            ThrottlePercent = data.ThrottlePercent,
            LinkQualityPercent = linkQuality,
            PacketLossPercent = Math.Max(0, 100 - linkQuality),
            MissionCurrentWaypoint = currentWaypoint,
            MissionTotalWaypoints = totalWaypoints,
            CompassCalibrationProgress1 = data.Compass_Progress1,
            CompassCalibrationProgress2 = data.Compass_Progress2,
            RcChannel1 = data.ModeCh1,
            RcChannel2 = data.ModeCh2,
            RcChannel3 = data.ModeCh3,
            RcChannel4 = data.ModeCh4,
            RcChannel5 = data.ModeCh5,
            RcChannel6 = data.ModeCh6,
            RcChannel7 = data.ModeCh7PWM,
            RcChannel8 = data.ModeCh8PWM,
            ServoChannel1 = data.ServoCh1,
            ServoChannel2 = data.ServoCh2,
            ServoChannel3 = data.ServoCh3,
            ServoChannel4 = data.ServoCh4,
            ServoChannel5 = data.ServoCh5,
            ServoChannel6 = data.ServoCh6,
            ServoChannel7 = data.ServoCh7,
            ServoChannel8 = data.ServoCh8
        };

        if (previousSnapshot != null)
        {
            var timeDelta = (snapshot.Timestamp - previousSnapshot.Timestamp).TotalSeconds;
            if (timeDelta > 0)
            {
                var batteryDelta = previousSnapshot.BatteryPercent - snapshot.BatteryPercent;
                snapshot.BatteryDrainRate = batteryDelta / timeDelta;
                snapshot.RollRateDegPerSec = (snapshot.Roll - previousSnapshot.Roll) / timeDelta;
                snapshot.PitchRateDegPerSec = (snapshot.Pitch - previousSnapshot.Pitch) / timeDelta;
                snapshot.YawRateDegPerSec = CircularDeltaDegrees(snapshot.Yaw, previousSnapshot.Yaw) / timeDelta;
            }

            snapshot.HeadingErrorDeg = CircularDeltaDegrees(snapshot.Heading, previousSnapshot.Heading);
            snapshot.DistanceFromHomeMeters = previousSnapshot.DistanceFromHomeMeters +
                HaversineMeters(
                    previousSnapshot.GpsLatitude,
                    previousSnapshot.GpsLongitude,
                    snapshot.GpsLatitude,
                    snapshot.GpsLongitude);
        }

        return snapshot;
    }

    private static double EstimateBatteryPercent(double voltage)
    {
        if (voltage <= 0)
            return 0;

        const double fullVoltage = 16.8;
        const double emptyVoltage = 14.0;
        var percent = ((voltage - emptyVoltage) / (fullVoltage - emptyVoltage)) * 100.0;
        return Math.Clamp(percent, 0.0, 100.0);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static int EstimateGpsFixType(int sats, double hdop)
    {
        if (sats >= 10 && hdop is > 0 and <= 1.5)
            return 3;
        if (sats >= 6)
            return 2;
        return sats > 0 ? 1 : 0;
    }

    private static double EstimateLinkQualityPercent(int signal)
    {
        if (signal <= 0)
            return 0;

        if (signal <= 100)
            return signal;

        return Math.Clamp(signal / 255.0 * 100.0, 0.0, 100.0);
    }

    private static double EstimateLinkQualityPercent(byte signal) => EstimateLinkQualityPercent((int)signal);

    private static double CircularDeltaDegrees(double current, double previous)
    {
        var diff = Math.Abs(current - previous) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        if (lat1 == 0 && lon1 == 0)
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
