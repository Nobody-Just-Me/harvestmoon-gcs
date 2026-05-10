using System;
using System.Collections.Generic;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Core.Helpers;

/// <summary>
/// Auto waypoint generator for Hybrid VTOL missions
/// Supports automatic mission generation with VTOL-specific commands
/// </summary>
public class VtolMissionGenerator
{
    /// <summary>
    /// VTOL mission configuration
    /// </summary>
    public class VtolMissionConfig
    {
        // Takeoff settings
        public double TakeoffLatitude { get; set; }
        public double TakeoffLongitude { get; set; }
        public double TakeoffAltitude { get; set; } = 20; // meters
        public float TakeoffHeading { get; set; } = 0; // degrees
        
        // Transition settings
        public double TransitionAltitude { get; set; } = 50; // meters - altitude to transition to FW
        public double TransitionDistance { get; set; } = 100; // meters - distance from takeoff to transition
        
        // Survey/waypoint settings
        public List<(double Lat, double Lon)> SurveyPoints { get; set; } = new();
        public double CruiseAltitude { get; set; } = 100; // meters
        public float CruiseSpeed { get; set; } = 15; // m/s
        
        // Landing settings
        public double LandingLatitude { get; set; }
        public double LandingLongitude { get; set; }
        public double LandingAltitude { get; set; } = 0; // meters
        public double LandingApproachDistance { get; set; } = 150; // meters - distance before landing to transition back to MC
        
        // Mission type
        public VtolMissionType MissionType { get; set; } = VtolMissionType.SurveyWithTransition;
    }

    public enum VtolMissionType
    {
        /// <summary>
        /// Simple VTOL takeoff and landing without transition
        /// </summary>
        SimpleVtol,
        
        /// <summary>
        /// VTOL takeoff, transition to FW, cruise, transition to MC, VTOL landing
        /// </summary>
        SurveyWithTransition,
        
        /// <summary>
        /// Grid survey pattern with VTOL transitions
        /// </summary>
        GridSurvey,
        
        /// <summary>
        /// Corridor mapping with VTOL
        /// </summary>
        CorridorMapping
    }

    /// <summary>
    /// Generate a complete VTOL mission based on configuration
    /// </summary>
    public static List<MissionWaypoint> GenerateVtolMission(VtolMissionConfig config)
    {
        return config.MissionType switch
        {
            VtolMissionType.SimpleVtol => GenerateSimpleVtolMission(config),
            VtolMissionType.SurveyWithTransition => GenerateSurveyWithTransition(config),
            VtolMissionType.GridSurvey => GenerateGridSurvey(config),
            VtolMissionType.CorridorMapping => GenerateCorridorMapping(config),
            _ => GenerateSurveyWithTransition(config)
        };
    }

    /// <summary>
    /// Generate simple VTOL mission (takeoff, waypoints, landing) without FW transition
    /// </summary>
    private static List<MissionWaypoint> GenerateSimpleVtolMission(VtolMissionConfig config)
    {
        var waypoints = new List<MissionWaypoint>();
        int seq = 0;

        // 1. VTOL Takeoff
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavVtolTakeoff,
            Latitude = config.TakeoffLatitude,
            Longitude = config.TakeoffLongitude,
            Altitude = config.TakeoffAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            Param4 = config.TakeoffHeading,
            IsAutoContinue = true
        });

        // 2. Survey waypoints in MC mode
        foreach (var point in config.SurveyPoints)
        {
            waypoints.Add(new MissionWaypoint
            {
                Sequence = seq++,
                Command = MavCommand.NavWaypoint,
                Latitude = point.Lat,
                Longitude = point.Lon,
                Altitude = config.CruiseAltitude,
                Frame = MavFrame.GlobalRelativeAlt,
                IsAutoContinue = true
            });
        }

        // 3. VTOL Land
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavVtolLand,
            Latitude = config.LandingLatitude,
            Longitude = config.LandingLongitude,
            Altitude = config.LandingAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        return waypoints;
    }

    /// <summary>
    /// Generate survey mission with VTOL to FW transition
    /// </summary>
    private static List<MissionWaypoint> GenerateSurveyWithTransition(VtolMissionConfig config)
    {
        var waypoints = new List<MissionWaypoint>();
        int seq = 0;

        // 1. VTOL Takeoff
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavVtolTakeoff,
            Latitude = config.TakeoffLatitude,
            Longitude = config.TakeoffLongitude,
            Altitude = config.TakeoffAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            Param4 = config.TakeoffHeading,
            IsAutoContinue = true
        });

        // 2. Climb to transition altitude
        var transitionPoint = CalculatePointAtDistance(
            config.TakeoffLatitude, 
            config.TakeoffLongitude,
            config.TakeoffHeading,
            config.TransitionDistance);

        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavWaypoint,
            Latitude = transitionPoint.Lat,
            Longitude = transitionPoint.Lon,
            Altitude = config.TransitionAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        // 3. Transition to Fixed Wing mode
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.DoVtolTransition,
            Param1 = 4, // 4 = Fixed Wing mode
            Frame = MavFrame.Mission,
            IsAutoContinue = true
        });

        // 4. Set cruise speed for FW mode
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.DoChangeSpeed,
            Param1 = 1, // 1 = Ground speed
            Param2 = config.CruiseSpeed,
            Param3 = -1, // -1 = no throttle change
            Frame = MavFrame.Mission,
            IsAutoContinue = true
        });

        // 5. Survey waypoints in FW mode
        foreach (var point in config.SurveyPoints)
        {
            waypoints.Add(new MissionWaypoint
            {
                Sequence = seq++,
                Command = MavCommand.NavWaypoint,
                Latitude = point.Lat,
                Longitude = point.Lon,
                Altitude = config.CruiseAltitude,
                Frame = MavFrame.GlobalRelativeAlt,
                IsAutoContinue = true
            });
        }

        // 6. Approach point before landing (transition back to MC)
        var approachPoint = CalculatePointAtDistance(
            config.LandingLatitude,
            config.LandingLongitude,
            GetBearingToPoint(
                config.SurveyPoints.Count > 0 ? config.SurveyPoints[^1].Lat : config.TakeoffLatitude,
                config.SurveyPoints.Count > 0 ? config.SurveyPoints[^1].Lon : config.TakeoffLongitude,
                config.LandingLatitude,
                config.LandingLongitude),
            -config.LandingApproachDistance); // Negative distance = before landing point

        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavWaypoint,
            Latitude = approachPoint.Lat,
            Longitude = approachPoint.Lon,
            Altitude = config.TransitionAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        // 7. Transition back to Multicopter mode
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.DoVtolTransition,
            Param1 = 3, // 3 = Multicopter mode
            Frame = MavFrame.Mission,
            IsAutoContinue = true
        });

        // 8. VTOL Land
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavVtolLand,
            Latitude = config.LandingLatitude,
            Longitude = config.LandingLongitude,
            Altitude = config.LandingAltitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        return waypoints;
    }

    /// <summary>
    /// Generate grid survey pattern for VTOL
    /// </summary>
    private static List<MissionWaypoint> GenerateGridSurvey(VtolMissionConfig config)
    {
        // Generate grid points if not provided
        if (config.SurveyPoints.Count == 0)
        {
            config.SurveyPoints = GenerateGridPoints(
                config.TakeoffLatitude,
                config.TakeoffLongitude,
                5, // 5 rows
                5, // 5 columns
                50 // 50 meters spacing
            );
        }

        return GenerateSurveyWithTransition(config);
    }

    /// <summary>
    /// Generate corridor mapping mission
    /// </summary>
    private static List<MissionWaypoint> GenerateCorridorMapping(VtolMissionConfig config)
    {
        // Generate corridor points if not provided
        if (config.SurveyPoints.Count < 2)
        {
            // Create a simple corridor from takeoff to landing
            var bearing = GetBearingToPoint(
                config.TakeoffLatitude, config.TakeoffLongitude,
                config.LandingLatitude, config.LandingLongitude);

            var distance = CalculateDistance(
                config.TakeoffLatitude, config.TakeoffLongitude,
                config.LandingLatitude, config.LandingLongitude);

            // Create waypoints every 100m along the corridor
            config.SurveyPoints.Clear();
            for (double d = 100; d < distance; d += 100)
            {
                var point = CalculatePointAtDistance(
                    config.TakeoffLatitude,
                    config.TakeoffLongitude,
                    bearing,
                    d);
                config.SurveyPoints.Add((point.Lat, point.Lon));
            }
        }

        return GenerateSurveyWithTransition(config);
    }

    /// <summary>
    /// Generate grid survey points
    /// </summary>
    private static List<(double Lat, double Lon)> GenerateGridPoints(
        double centerLat, double centerLon, int rows, int cols, double spacing)
    {
        var points = new List<(double, double)>();
        var metersPerDegreeLat = 111320.0; // approximate
        var metersPerDegreeLon = 111320.0 * Math.Cos(centerLat * Math.PI / 180);

        var startLat = centerLat - (rows / 2.0 * spacing / metersPerDegreeLat);
        var startLon = centerLon - (cols / 2.0 * spacing / metersPerDegreeLon);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var lat = startLat + (row * spacing / metersPerDegreeLat);
                var lon = startLon + (col * spacing / metersPerDegreeLon);
                
                // Alternate direction for efficient coverage (lawnmower pattern)
                if (row % 2 == 1)
                {
                    lon = startLon + ((cols - 1 - col) * spacing / metersPerDegreeLon);
                }
                
                points.Add((lat, lon));
            }
        }

        return points;
    }

    /// <summary>
    /// Calculate a point at a given distance and bearing from a start point
    /// </summary>
    private static (double Lat, double Lon) CalculatePointAtDistance(
        double lat, double lon, float bearing, double distance)
    {
        const double R = 6371000; // Earth radius in meters
        var bearingRad = bearing * Math.PI / 180;
        var latRad = lat * Math.PI / 180;
        var lonRad = lon * Math.PI / 180;

        var newLatRad = Math.Asin(
            Math.Sin(latRad) * Math.Cos(distance / R) +
            Math.Cos(latRad) * Math.Sin(distance / R) * Math.Cos(bearingRad));

        var newLonRad = lonRad + Math.Atan2(
            Math.Sin(bearingRad) * Math.Sin(distance / R) * Math.Cos(latRad),
            Math.Cos(distance / R) - Math.Sin(latRad) * Math.Sin(newLatRad));

        return (newLatRad * 180 / Math.PI, newLonRad * 180 / Math.PI);
    }

    /// <summary>
    /// Calculate bearing from point 1 to point 2
    /// </summary>
    private static float GetBearingToPoint(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;

        var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x) * 180 / Math.PI;
        return (float)((bearing + 360) % 360);
    }

    /// <summary>
    /// Calculate distance between two points in meters
    /// </summary>
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
