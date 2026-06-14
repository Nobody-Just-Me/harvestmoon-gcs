using System;
using System.Collections.Generic;
using System.Linq;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Helpers;

public sealed record MissionProgressSnapshot(
    double ProgressPercent,
    int CurrentWaypoint,
    int TotalWaypoints,
    double DistanceToCurrentMeters,
    double RemainingDistanceMeters);

public static class MissionProgressCalculator
{
    public static MissionProgressSnapshot Calculate(
        TelemetryData? telemetry,
        IReadOnlyCollection<WaypointData>? waypoints,
        double waypointRadiusMeters)
    {
        var orderedWaypoints = waypoints?
            .OrderBy(waypoint => waypoint.Sequence <= 0 ? int.MaxValue : waypoint.Sequence)
            .ToList() ?? new List<WaypointData>();

        if (orderedWaypoints.Count == 0 || telemetry == null ||
            !IsValidCoordinate(telemetry.Latitude, telemetry.Longitude))
        {
            return new MissionProgressSnapshot(0, 0, orderedWaypoints.Count, 0, 0);
        }

        var nearestIndex = 0;
        var nearestDistance = double.MaxValue;
        for (var i = 0; i < orderedWaypoints.Count; i++)
        {
            var distance = GeoMath.CalculateDistance(
                telemetry.Latitude,
                telemetry.Longitude,
                orderedWaypoints[i].Latitude,
                orderedWaypoints[i].Longitude);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        var currentWaypoint = Math.Clamp(nearestIndex + 1, 1, orderedWaypoints.Count);
        var completedWaypoints = nearestDistance <= Math.Max(1, waypointRadiusMeters)
            ? currentWaypoint
            : Math.Max(0, currentWaypoint - 1);

        var progress = orderedWaypoints.Count == 0
            ? 0
            : Math.Clamp(completedWaypoints * 100.0 / orderedWaypoints.Count, 0, 100);

        var remaining = nearestDistance;
        for (var i = nearestIndex; i < orderedWaypoints.Count - 1; i++)
        {
            remaining += GeoMath.CalculateDistance(
                orderedWaypoints[i].Latitude,
                orderedWaypoints[i].Longitude,
                orderedWaypoints[i + 1].Latitude,
                orderedWaypoints[i + 1].Longitude);
        }

        return new MissionProgressSnapshot(
            progress,
            currentWaypoint,
            orderedWaypoints.Count,
            nearestDistance,
            remaining);
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
    {
        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180 &&
               Math.Abs(latitude) > 0.000001 &&
               Math.Abs(longitude) > 0.000001;
    }
}
