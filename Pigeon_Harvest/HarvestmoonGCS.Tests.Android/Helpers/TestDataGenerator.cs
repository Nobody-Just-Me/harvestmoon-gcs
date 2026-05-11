using FsCheck;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HarvestmoonGCS.Tests.Android.Helpers;

/// <summary>
/// Generates test data for property-based testing using FsCheck
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Generates random valid waypoints
    /// </summary>
    public static Gen<MockWaypoint> WaypointGenerator()
    {
        return from lat in Gen.Choose(-90, 90).Select(x => x + Gen.Choose(0, 999999).Sample(0, 1).First() / 1000000.0)
               from lon in Gen.Choose(-180, 180).Select(x => x + Gen.Choose(0, 999999).Sample(0, 1).First() / 1000000.0)
               from alt in Gen.Choose(0, 5000).Select(x => (double)x)
               from idx in Gen.Choose(0, 1000)
               select new MockWaypoint
               {
                   Index = idx,
                   Latitude = lat,
                   Longitude = lon,
                   Altitude = alt
               };
    }

    /// <summary>
    /// Generates random telemetry data
    /// </summary>
    public static Gen<MockTelemetryData> TelemetryGenerator()
    {
        return from lat in Gen.Choose(-90, 90).Select(x => (double)x)
               from lon in Gen.Choose(-180, 180).Select(x => (double)x)
               from alt in Gen.Choose(0, 10000).Select(x => (double)x)
               from speed in Gen.Choose(0, 100).Select(x => (double)x)
               from heading in Gen.Choose(0, 359)
               from battery in Gen.Choose(0, 100)
               from armed in Arb.Generate<bool>()
               select new MockTelemetryData
               {
                   Latitude = lat,
                   Longitude = lon,
                   Altitude = alt,
                   Speed = speed,
                   Heading = heading,
                   BatteryPercent = battery,
                   FlightMode = "GUIDED",
                   Armed = armed
               };
    }

    /// <summary>
    /// Generates random missions with valid waypoints
    /// </summary>
    public static Gen<List<MockWaypoint>> MissionGenerator(int minWaypoints = 2, int maxWaypoints = 50)
    {
        return from count in Gen.Choose(minWaypoints, maxWaypoints)
               from waypoints in Gen.ListOf(count, WaypointGenerator())
               select waypoints.Select((wp, idx) => new MockWaypoint
               {
                   Index = idx,
                   Latitude = wp.Latitude,
                   Longitude = wp.Longitude,
                   Altitude = wp.Altitude
               }).ToList();
    }

    /// <summary>
    /// Generates random geofence boundaries
    /// </summary>
    public static Gen<List<GeoPoint>> GeofenceGenerator(int minPoints = 3, int maxPoints = 20)
    {
        return from count in Gen.Choose(minPoints, maxPoints)
               from centerLat in Gen.Choose(-90, 90).Select(x => (double)x)
               from centerLon in Gen.Choose(-180, 180).Select(x => (double)x)
               from radius in Gen.Choose(100, 5000).Select(x => (double)x)
               select GeneratePolygonPoints(centerLat, centerLon, radius, count);
    }

    /// <summary>
    /// Generates random alert data
    /// </summary>
    public static Gen<MockAlert> AlertGenerator()
    {
        var severities = new[] { "INFO", "WARNING", "ERROR", "CRITICAL" };
        var messages = new[]
        {
            "Low battery",
            "GPS signal lost",
            "Connection timeout",
            "Geofence breach",
            "Altitude limit exceeded"
        };

        return from severity in Gen.Elements(severities)
               from message in Gen.Elements(messages)
               from timestamp in Arb.Generate<DateTime>()
               select new MockAlert
               {
                   Severity = severity,
                   Message = message,
                   Timestamp = timestamp
               };
    }

    /// <summary>
    /// Generates random MAVLink-like message bytes
    /// </summary>
    public static Gen<byte[]> MavLinkMessageGenerator()
    {
        return from length in Gen.Choose(10, 263) // MAVLink v1 max payload is 255 + 8 header
               from bytes in Gen.ArrayOf(length, Arb.Generate<byte>())
               select bytes;
    }

    /// <summary>
    /// Generates random non-empty text for speech testing
    /// </summary>
    public static Gen<string> NonEmptyTextGenerator()
    {
        return from text in Arb.Generate<NonEmptyString>()
               select text.Get;
    }

    /// <summary>
    /// Helper to generate polygon points around a center
    /// </summary>
    private static List<GeoPoint> GeneratePolygonPoints(double centerLat, double centerLon, double radius, int pointCount)
    {
        var points = new List<GeoPoint>();
        var angleStep = 360.0 / pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            var angle = i * angleStep * Math.PI / 180.0;
            var latOffset = radius * Math.Cos(angle) / 111320.0; // Approximate meters to degrees
            var lonOffset = radius * Math.Sin(angle) / (111320.0 * Math.Cos(centerLat * Math.PI / 180.0));

            points.Add(new GeoPoint
            {
                Latitude = centerLat + latOffset,
                Longitude = centerLon + lonOffset
            });
        }

        return points;
    }
}

public class GeoPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class MockAlert
{
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
