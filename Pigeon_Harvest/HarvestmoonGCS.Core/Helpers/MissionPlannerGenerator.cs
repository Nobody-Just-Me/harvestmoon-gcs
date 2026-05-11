using System;
using System.Collections.Generic;
using System.Linq;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Helpers;

public static class MissionPlannerGenerator
{
    public static List<MissionWaypoint> GenerateGridSurveyMission(
        double centerLat,
        double centerLon,
        double widthMeters,
        double heightMeters,
        double spacingMeters,
        double altitude,
        double speed = 5)
    {
        var waypoints = new List<MissionWaypoint>();
        if (widthMeters <= 0 || heightMeters <= 0 || spacingMeters <= 0 || altitude <= 0)
        {
            return waypoints;
        }

        var halfWidth = widthMeters / 2.0;
        var halfHeight = heightMeters / 2.0;
        var metersPerDegreeLat = 111_320.0;
        var metersPerDegreeLon = 111_320.0 * Math.Cos(centerLat * Math.PI / 180.0);
        var lineCount = Math.Max(2, (int)Math.Ceiling(widthMeters / spacingMeters) + 1);
        var seq = 0;

        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavTakeoff,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = altitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        for (var line = 0; line < lineCount; line++)
        {
            var xMeters = -halfWidth + Math.Min(line * spacingMeters, widthMeters);
            var startY = line % 2 == 0 ? -halfHeight : halfHeight;
            var endY = -startY;

            AddGridWaypoint(waypoints, seq++, centerLat, centerLon, xMeters, startY, altitude, metersPerDegreeLat, metersPerDegreeLon);
            AddGridWaypoint(waypoints, seq++, centerLat, centerLon, xMeters, endY, altitude, metersPerDegreeLat, metersPerDegreeLon);
        }

        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq,
            Command = MavCommand.NavReturnToLaunch,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = 0,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });

        return waypoints;
    }

    private static void AddGridWaypoint(
        List<MissionWaypoint> waypoints,
        int sequence,
        double centerLat,
        double centerLon,
        double offsetEastMeters,
        double offsetNorthMeters,
        double altitude,
        double metersPerDegreeLat,
        double metersPerDegreeLon)
    {
        waypoints.Add(new MissionWaypoint
        {
            Sequence = sequence,
            Command = MavCommand.NavWaypoint,
            Latitude = centerLat + offsetNorthMeters / metersPerDegreeLat,
            Longitude = centerLon + offsetEastMeters / metersPerDegreeLon,
            Altitude = altitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
    }

    public static List<MissionWaypoint> GenerateSurveyMission(
        List<(double Lat, double Lon)> polygonArea,
        double lineSpacing,
        double altitude,
        double angle = 0,
        double overshoot = 0,
        double cameraFootprint = 0)
    {
        var waypoints = new List<MissionWaypoint>();
        if (polygonArea.Count < 3) return waypoints;
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = 0,
            Command = MavCommand.NavTakeoff,
            Latitude = polygonArea[0].Lat,
            Longitude = polygonArea[0].Lon,
            Altitude = 20,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = 1,
            Command = MavCommand.NavWaypoint,
            Latitude = polygonArea[0].Lat + 0.001,
            Longitude = polygonArea[0].Lon,
            Altitude = altitude,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = 2,
            Command = MavCommand.NavReturnToLaunch,
            Latitude = polygonArea[0].Lat,
            Longitude = polygonArea[0].Lon,
            Altitude = 0,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        return waypoints;
    }

    public static List<MissionWaypoint> GenerateCorridorMission(
        List<(double Lat, double Lon)> centerLine,
        double corridorWidth,
        double altitude,
        double lineSpacing)
    {
        var waypoints = new List<MissionWaypoint>();
        if (centerLine.Count < 2) return waypoints;
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = 0,
            Command = MavCommand.NavTakeoff,
            Latitude = centerLine[0].Lat,
            Longitude = centerLine[0].Lon,
            Altitude = 20,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        for (int i = 0; i < centerLine.Count; i++)
        {
            waypoints.Add(new MissionWaypoint
            {
                Sequence = i + 1,
                Command = MavCommand.NavWaypoint,
                Latitude = centerLine[i].Lat,
                Longitude = centerLine[i].Lon,
                Altitude = altitude,
                Frame = MavFrame.GlobalRelativeAlt,
                IsAutoContinue = true
            });
        }
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = waypoints.Count,
            Command = MavCommand.NavReturnToLaunch,
            Latitude = centerLine[0].Lat,
            Longitude = centerLine[0].Lon,
            Altitude = 0,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        return waypoints;
    }

    public static List<MissionWaypoint> GenerateCircularMission(
        double centerLat,
        double centerLon,
        double radius,
        double altitude,
        int numPoints = 8,
        bool isSpiral = false,
        double spiralTurns = 3)
    {
        var waypoints = new List<MissionWaypoint>();
        int seq = 0;
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavTakeoff,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = 20,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        double angleStep = 360.0 / numPoints;
        for (int i = 0; i < numPoints; i++)
        {
            double angle = i * angleStep * Math.PI / 180;
            double lat = centerLat + (radius / 111000.0) * Math.Cos(angle);
            double lon = centerLon + (radius / (111000.0 * Math.Cos(centerLat * Math.PI / 180))) * Math.Sin(angle);
            
            waypoints.Add(new MissionWaypoint
            {
                Sequence = seq++,
                Command = MavCommand.NavWaypoint,
                Latitude = lat,
                Longitude = lon,
                Altitude = altitude,
                Frame = MavFrame.GlobalRelativeAlt,
                IsAutoContinue = true
            });
        }
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavReturnToLaunch,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = 0,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        return waypoints;
    }

    public static List<MissionWaypoint> GenerateStructureScanMission(
        double centerLat,
        double centerLon,
        double radius,
        double minAltitude,
        double maxAltitude,
        int numLayers = 3,
        int pointsPerLayer = 8)
    {
        var waypoints = new List<MissionWaypoint>();
        int seq = 0;
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavTakeoff,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = 20,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        double altitudeStep = (maxAltitude - minAltitude) / Math.Max(1, numLayers - 1);
        double currentAlt = minAltitude;
        
        for (int layer = 0; layer < numLayers; layer++)
        {
            double angleStep = 360.0 / pointsPerLayer;
            for (int i = 0; i < pointsPerLayer; i++)
            {
                double angle = i * angleStep * Math.PI / 180;
                double lat = centerLat + (radius / 111000.0) * Math.Cos(angle);
                double lon = centerLon + (radius / (111000.0 * Math.Cos(centerLat * Math.PI / 180))) * Math.Sin(angle);
                
                waypoints.Add(new MissionWaypoint
                {
                    Sequence = seq++,
                    Command = MavCommand.NavWaypoint,
                    Latitude = lat,
                    Longitude = lon,
                    Altitude = currentAlt,
                    Frame = MavFrame.GlobalRelativeAlt,
                    Param1 = 2,
                    IsAutoContinue = true
                });
            }
            currentAlt += altitudeStep;
        }
        
        waypoints.Add(new MissionWaypoint
        {
            Sequence = seq++,
            Command = MavCommand.NavReturnToLaunch,
            Latitude = centerLat,
            Longitude = centerLon,
            Altitude = 0,
            Frame = MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        });
        
        return waypoints;
    }
}
