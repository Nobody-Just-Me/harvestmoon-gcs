using System;
using System.Collections.Generic;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Helpers
{
    /// <summary>
    /// Helper class for MAVLink geofence operations
    /// Provides utilities for converting geofence data to MAVLink messages
    /// </summary>
    public static class GeofenceMavLinkHelper
    {
        /// <summary>
        /// Converts a GeofenceData to MAVLink fence points
        /// </summary>
        /// <param name="geofence">Geofence to convert</param>
        /// <returns>List of fence point data (lat, lon, index)</returns>
        public static List<FencePoint> ConvertToMavLinkFencePoints(GeofenceData geofence)
        {
            var fencePoints = new List<FencePoint>();

            if (geofence == null || !geofence.IsValid())
                return fencePoints;

            if (geofence.Type == GeofenceType.Circular)
            {
                // For circular geofence, send center point with radius
                if (geofence.Points.Count > 0)
                {
                    var center = geofence.Points[0];
                    fencePoints.Add(new FencePoint
                    {
                        Index = 0,
                        Latitude = center.Latitude,
                        Longitude = center.Longitude,
                        IsCircle = true,
                        Radius = (float)geofence.Radius
                    });
                }
            }
            else if (geofence.Type == GeofenceType.Polygon)
            {
                // For polygon geofence, send all boundary points
                for (int i = 0; i < geofence.Points.Count; i++)
                {
                    var point = geofence.Points[i];
                    fencePoints.Add(new FencePoint
                    {
                        Index = i,
                        Latitude = point.Latitude,
                        Longitude = point.Longitude,
                        IsCircle = false,
                        Radius = 0
                    });
                }
            }

            return fencePoints;
        }

        /// <summary>
        /// Gets the MAVLink fence action for a geofence action type
        /// </summary>
        /// <param name="action">Geofence action</param>
        /// <returns>MAVLink fence action value</returns>
        public static int GetMavLinkFenceAction(GeofenceAction action)
        {
            return action switch
            {
                GeofenceAction.None => 0,      // FENCE_ACTION_NONE
                GeofenceAction.Warning => 0,   // FENCE_ACTION_NONE (just warning)
                GeofenceAction.RTL => 1,       // FENCE_ACTION_RTL
                GeofenceAction.Land => 2,      // FENCE_ACTION_LAND
                GeofenceAction.Hold => 4,      // FENCE_ACTION_BRAKE (hold position)
                _ => 0
            };
        }

        /// <summary>
        /// Validates geofence data for MAVLink upload
        /// </summary>
        /// <param name="geofence">Geofence to validate</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if valid for upload</returns>
        public static bool ValidateForMavLinkUpload(GeofenceData geofence, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (geofence == null)
            {
                errorMessage = "Geofence is null";
                return false;
            }

            if (!geofence.IsValid())
            {
                errorMessage = "Geofence is not valid";
                return false;
            }

            if (geofence.Type == GeofenceType.Circular)
            {
                if (geofence.Points.Count == 0)
                {
                    errorMessage = "Circular geofence has no center point";
                    return false;
                }

                if (geofence.Radius <= 0)
                {
                    errorMessage = "Circular geofence has invalid radius";
                    return false;
                }

                // Check if radius is within reasonable limits (e.g., 10m to 10km)
                if (geofence.Radius < 10 || geofence.Radius > 10000)
                {
                    errorMessage = $"Circular geofence radius ({geofence.Radius}m) is outside reasonable limits (10m - 10km)";
                    return false;
                }
            }
            else if (geofence.Type == GeofenceType.Polygon)
            {
                if (geofence.Points.Count < 3)
                {
                    errorMessage = "Polygon geofence must have at least 3 points";
                    return false;
                }

                // Check if polygon has too many points (MAVLink limit is typically 84 points)
                if (geofence.Points.Count > 84)
                {
                    errorMessage = $"Polygon geofence has too many points ({geofence.Points.Count}). Maximum is 84.";
                    return false;
                }

                // Validate all points are within valid lat/lon ranges
                foreach (var point in geofence.Points)
                {
                    if (!point.IsValid())
                    {
                        errorMessage = $"Polygon contains invalid point: ({point.Latitude}, {point.Longitude})";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates the total number of fence points needed for MAVLink upload
        /// </summary>
        /// <param name="geofence">Geofence to calculate for</param>
        /// <returns>Number of fence points</returns>
        public static int GetFencePointCount(GeofenceData geofence)
        {
            if (geofence == null || !geofence.IsValid())
                return 0;

            if (geofence.Type == GeofenceType.Circular)
            {
                return 1; // Only center point with radius
            }
            else if (geofence.Type == GeofenceType.Polygon)
            {
                return geofence.Points.Count;
            }

            return 0;
        }
    }

    /// <summary>
    /// Represents a MAVLink fence point
    /// </summary>
    public class FencePoint
    {
        public int Index { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsCircle { get; set; }
        public float Radius { get; set; }
    }
}
