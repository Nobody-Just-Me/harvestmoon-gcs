using System;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Configurable telemetry sampler using interval and significant-change rules.
/// </summary>
public sealed class TelemetrySampler : ITelemetrySampler
{
    private readonly AISettings _settings;
    private readonly object _lock = new();
    private TelemetrySnapshot? _lastSampled;
    private DateTime _lastSampledAt = DateTime.MinValue;

    public TelemetrySampler(AISettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool ShouldSample(TelemetrySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        lock (_lock)
        {
            var config = _settings.TelemetrySampling ?? new TelemetrySamplingConfig();
            var now = snapshot.Timestamp == default ? DateTime.UtcNow : snapshot.Timestamp;

            var minIntervalMs = Math.Clamp(config.MinIntervalMs, 50, 10_000);
            var forceIntervalMs = Math.Clamp(config.ForceIntervalMs, minIntervalMs, 60_000);

            if (_lastSampled == null)
            {
                SaveLastSampled(snapshot, now);
                return true;
            }

            var elapsedMs = Math.Max(0, (now - _lastSampledAt).TotalMilliseconds);

            if (elapsedMs >= forceIntervalMs)
            {
                SaveLastSampled(snapshot, now);
                return true;
            }

            var stateChanged =
                (config.SampleOnArmedStateChange && snapshot.Armed != _lastSampled.Armed) ||
                (config.SampleOnFlightModeChange && snapshot.FlightMode != _lastSampled.FlightMode);

            if (stateChanged)
            {
                SaveLastSampled(snapshot, now);
                return true;
            }

            if (elapsedMs < minIntervalMs)
            {
                return false;
            }

            if (IsSignificantChange(snapshot, _lastSampled, config))
            {
                SaveLastSampled(snapshot, now);
                return true;
            }

            return false;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _lastSampled = null;
            _lastSampledAt = DateTime.MinValue;
        }
    }

    private static bool IsSignificantChange(
        TelemetrySnapshot current,
        TelemetrySnapshot previous,
        TelemetrySamplingConfig config)
    {
        var batteryDelta = Math.Abs(current.BatteryPercent - previous.BatteryPercent);
        if (batteryDelta >= Math.Max(0.1, config.BatteryDeltaPercent))
        {
            return true;
        }

        var altitudeDelta = Math.Abs(current.Altitude - previous.Altitude);
        if (altitudeDelta >= Math.Max(0.2, config.AltitudeDeltaMeters))
        {
            return true;
        }

        var speedDelta = Math.Abs(current.Speed - previous.Speed);
        if (speedDelta >= Math.Max(0.1, config.SpeedDeltaMps))
        {
            return true;
        }

        var groundSpeedDelta = Math.Abs(current.GroundSpeed - previous.GroundSpeed);
        if (groundSpeedDelta >= Math.Max(0.1, config.SpeedDeltaMps))
        {
            return true;
        }

        var airSpeedDelta = Math.Abs(current.AirSpeed - previous.AirSpeed);
        if (airSpeedDelta >= Math.Max(0.1, config.SpeedDeltaMps))
        {
            return true;
        }

        var verticalSpeedDelta = Math.Abs(current.VerticalSpeed - previous.VerticalSpeed);
        if (verticalSpeedDelta >= Math.Max(0.1, config.VerticalSpeedDeltaMps))
        {
            return true;
        }

        var headingDelta = CircularDeltaDegrees(current.Heading, previous.Heading);
        if (headingDelta >= Math.Max(1.0, config.HeadingDeltaDeg))
        {
            return true;
        }

        var gpsDelta = HaversineMeters(
            current.GpsLatitude, current.GpsLongitude,
            previous.GpsLatitude, previous.GpsLongitude);

        if (gpsDelta >= Math.Max(0.5, config.GpsDistanceDeltaMeters))
        {
            return true;
        }

        var gpsHdopDelta = Math.Abs(current.GpsHdop - previous.GpsHdop);
        if (gpsHdopDelta >= Math.Max(0.05, config.GpsHdopDelta))
        {
            return true;
        }

        var batteryVoltageDelta = Math.Abs(current.BatteryVoltage - previous.BatteryVoltage);
        if (batteryVoltageDelta >= Math.Max(0.01, config.BatteryVoltageDeltaVolts))
        {
            return true;
        }

        var batteryCurrentDelta = Math.Abs(current.BatteryCurrent - previous.BatteryCurrent);
        if (batteryCurrentDelta >= Math.Max(0.05, config.BatteryCurrentDeltaAmp))
        {
            return true;
        }

        var rollDelta = Math.Abs(current.Roll - previous.Roll);
        if (rollDelta >= Math.Max(0.2, config.RollDeltaDeg))
        {
            return true;
        }

        var pitchDelta = Math.Abs(current.Pitch - previous.Pitch);
        if (pitchDelta >= Math.Max(0.2, config.PitchDeltaDeg))
        {
            return true;
        }

        var vibrationMagnitudeDelta = Math.Abs(current.VibrationMagnitude - previous.VibrationMagnitude);
        if (vibrationMagnitudeDelta >= Math.Max(0.5, config.VibrationMagnitudeDelta))
        {
            return true;
        }

        var linkQualityDelta = Math.Abs(current.LinkQualityPercent - previous.LinkQualityPercent);
        if (linkQualityDelta >= Math.Max(1.0, config.LinkQualityDeltaPercent))
        {
            return true;
        }

        if (current.MissionCurrentWaypoint != previous.MissionCurrentWaypoint ||
            current.MissionTotalWaypoints != previous.MissionTotalWaypoints)
        {
            return true;
        }

        return false;
    }

    private void SaveLastSampled(TelemetrySnapshot snapshot, DateTime sampledAt)
    {
        _lastSampled = snapshot;
        _lastSampledAt = sampledAt;
    }

    private static double CircularDeltaDegrees(double current, double previous)
    {
        var diff = Math.Abs(current - previous) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
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
