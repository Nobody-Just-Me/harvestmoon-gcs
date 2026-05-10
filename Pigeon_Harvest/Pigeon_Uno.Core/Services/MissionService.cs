using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Helpers;
using MavLinkNet;

namespace Pigeon_Uno.Core.Services;

public class MissionService : IMissionService
{
    private readonly IMavLinkService _mavLinkService;

    public event EventHandler<int> UploadProgressChanged;
    public event EventHandler<int> DownloadProgressChanged;
    public event EventHandler<MissionOperationResult> OperationCompleted;
    public event EventHandler<string> ErrorOccurred;

    public MissionService(IMavLinkService mavLinkService)
    {
        _mavLinkService = mavLinkService;
    }

    public async Task<bool> UploadMissionAsync(IEnumerable<MissionWaypoint> waypoints)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to vehicle");
                return false;
            }

            var wps = waypoints.ToList();

            if (wps.Count == 0)
            {
                ErrorOccurred?.Invoke(this, "No waypoints to upload");
                return false;
            }

            var uploadWaypoints = wps.Select(wp => new WaypointData
            {
                Sequence = wp.Sequence,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                Altitude = wp.Altitude,
                Command = ConvertMissionCommandToWaypointCommand(wp.Command),
                Param1 = wp.Param1,
                Param2 = wp.Param2,
                Param3 = wp.Param3,
                Param4 = wp.Param4,
                IsCurrent = wp.IsCurrent
            }).ToList();

            UploadProgressChanged?.Invoke(this, 0);
            var uploadSuccess = await _mavLinkService.UploadMissionAsync(uploadWaypoints);
            UploadProgressChanged?.Invoke(this, uploadWaypoints.Count);

            if (!uploadSuccess)
            {
                OperationCompleted?.Invoke(this, new MissionOperationResult
                {
                    Success = false,
                    Message = "Mission upload failed",
                    ItemsProcessed = 0
                });
                return false;
            }

            OperationCompleted?.Invoke(this, new MissionOperationResult
            {
                Success = true,
                Message = $"Uploaded {wps.Count} waypoints successfully",
                ItemsProcessed = wps.Count
            });

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            OperationCompleted?.Invoke(this, new MissionOperationResult
            {
                Success = false,
                Message = $"Upload failed: {ex.Message}",
                Error = ex
            });
            return false;
        }
    }

    public async Task<List<MissionWaypoint>> DownloadMissionAsync()
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to vehicle");
                return new List<MissionWaypoint>();
            }

            DownloadProgressChanged?.Invoke(this, 0);

            var downloaded = await _mavLinkService.DownloadMissionAsync();
            var waypoints = downloaded.Select((wp, idx) => new MissionWaypoint
            {
                Sequence = wp.Sequence != 0 ? wp.Sequence : idx,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                Altitude = wp.Altitude,
                Command = ConvertWaypointCommandToMissionCommand(wp.Command),
                Frame = Pigeon_Uno.Core.Models.MavFrame.GlobalRelativeAlt,
                IsCurrent = wp.IsCurrent,
                IsAutoContinue = true,
                Param1 = (float)wp.Param1,
                Param2 = (float)wp.Param2,
                Param3 = (float)wp.Param3,
                Param4 = (float)wp.Param4
            }).ToList();

            DownloadProgressChanged?.Invoke(this, waypoints.Count);

            OperationCompleted?.Invoke(this, new MissionOperationResult
            {
                Success = true,
                Message = $"Downloaded {waypoints.Count} waypoints",
                ItemsProcessed = waypoints.Count
            });

            return waypoints;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            return new List<MissionWaypoint>();
        }
    }

    public async Task<bool> ClearMissionAsync()
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ErrorOccurred?.Invoke(this, "Not connected to vehicle");
                return false;
            }

            var clearAll = new UasMissionClearAll
            {
                TargetSystem = 1,
                TargetComponent = 0
            };
            _mavLinkService.SendMessage(clearAll);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            List<WaypointData> remaining;
            do
            {
                await Task.Delay(200);
                remaining = await _mavLinkService.DownloadMissionAsync();
            }
            while (remaining.Count > 0 && DateTime.UtcNow < deadline);

            var cleared = remaining.Count == 0;

            if (!cleared)
            {
                OperationCompleted?.Invoke(this, new MissionOperationResult
                {
                    Success = false,
                    Message = "Mission clear not acknowledged by vehicle",
                    ItemsProcessed = remaining.Count
                });
                return false;
            }

            OperationCompleted?.Invoke(this, new MissionOperationResult
            {
                Success = true,
                Message = "Mission cleared successfully"
            });

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            return false;
        }
    }

    public async Task<List<MissionWaypoint>> ImportMissionAsync(string filePath)
    {
        try
        {
            var waypoints = new List<MissionWaypoint>();
            var lines = await File.ReadAllLinesAsync(filePath);
            
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length >= 4)
                {
                    waypoints.Add(new MissionWaypoint
                    {
                        Sequence = waypoints.Count,
                        Command = MavCommand.NavWaypoint,
                        Latitude = double.Parse(parts[0]),
                        Longitude = double.Parse(parts[1]),
                        Altitude = double.Parse(parts[2]),
                        Frame = Pigeon_Uno.Core.Models.MavFrame.GlobalRelativeAlt,
                        IsAutoContinue = true
                    });
                }
            }

            return waypoints;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Import failed: {ex.Message}");
            return new List<MissionWaypoint>();
        }
    }

    public async Task<bool> ExportMissionAsync(IEnumerable<MissionWaypoint> waypoints, string filePath)
    {
        try
        {
            var lines = new List<string> { "Latitude,Longitude,Altitude,Command" };
            
            foreach (var wp in waypoints)
            {
                lines.Add($"{wp.Latitude},{wp.Longitude},{wp.Altitude},{wp.Command}");
            }

            await File.WriteAllLinesAsync(filePath, lines);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Export failed: {ex.Message}");
            return false;
        }
    }

    public Task<MissionValidationResult> ValidateMissionAsync(IEnumerable<MissionWaypoint> waypoints)
    {
        var result = new MissionValidationResult { IsValid = true };
        var wps = waypoints.ToList();

        if (!wps.Any())
        {
            result.IsValid = false;
            result.Errors.Add("Mission must contain at least one waypoint");
        }

        foreach (var wp in wps)
        {
            if (wp.Latitude < -90 || wp.Latitude > 90)
            {
                result.IsValid = false;
                result.Errors.Add($"Waypoint {wp.Sequence}: Invalid latitude {wp.Latitude}");
            }

            if (wp.Longitude < -180 || wp.Longitude > 180)
            {
                result.IsValid = false;
                result.Errors.Add($"Waypoint {wp.Sequence}: Invalid longitude {wp.Longitude}");
            }

            if (wp.Altitude < 0 || wp.Altitude > 500)
            {
                result.Warnings.Add($"Waypoint {wp.Sequence}: Altitude {wp.Altitude}m may be unsafe");
            }
        }

        return Task.FromResult(result);
    }

    public Task<MissionStatistics> CalculateStatisticsAsync(IEnumerable<MissionWaypoint> waypoints)
    {
        var wps = waypoints.ToList();
        var stats = new MissionStatistics
        {
            TotalWaypoints = wps.Count
        };

        if (wps.Count < 2)
            return Task.FromResult(stats);

        double totalDistance = 0;
        for (int i = 1; i < wps.Count; i++)
        {
            totalDistance += CalculateDistance(
                wps[i - 1].Latitude, wps[i - 1].Longitude,
                wps[i].Latitude, wps[i].Longitude);
        }

        stats.TotalDistance = totalDistance;
        stats.MaxAltitude = wps.Max(w => w.Altitude);
        stats.MinAltitude = wps.Min(w => w.Altitude);
        
        var avgSpeed = 15.0; // m/s
        stats.EstimatedDuration = TimeSpan.FromSeconds(totalDistance / avgSpeed);

        return Task.FromResult(stats);
    }

    public MissionWaypoint CreateWaypoint(double latitude, double longitude, double altitude)
    {
        return new MissionWaypoint
        {
            Sequence = 0,
            Command = MavCommand.NavWaypoint,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            Frame = Pigeon_Uno.Core.Models.MavFrame.GlobalRelativeAlt,
            IsAutoContinue = true
        };
    }

    /// <summary>
    /// Generate auto waypoint mission for Hybrid VTOL
    /// </summary>
    public List<MissionWaypoint> GenerateVtolAutoMission(
        double takeoffLat, double takeoffLon,
        double landingLat, double landingLon,
        List<(double Lat, double Lon)> surveyPoints = null,
        VtolMissionGenerator.VtolMissionType missionType = VtolMissionGenerator.VtolMissionType.SurveyWithTransition)
    {
        var config = new VtolMissionGenerator.VtolMissionConfig
        {
            TakeoffLatitude = takeoffLat,
            TakeoffLongitude = takeoffLon,
            TakeoffAltitude = 20,
            TakeoffHeading = 0,
            
            TransitionAltitude = 50,
            TransitionDistance = 100,
            
            SurveyPoints = surveyPoints ?? new List<(double, double)>(),
            CruiseAltitude = 100,
            CruiseSpeed = 15,
            
            LandingLatitude = landingLat,
            LandingLongitude = landingLon,
            LandingAltitude = 0,
            LandingApproachDistance = 150,
            
            MissionType = missionType
        };

        return VtolMissionGenerator.GenerateVtolMission(config);
    }

    /// <summary>
    /// Generate auto waypoint mission for Hybrid VTOL with custom configuration
    /// </summary>
    public List<MissionWaypoint> GenerateVtolAutoMissionWithConfig(VtolMissionGenerator.VtolMissionConfig config)
    {
        return VtolMissionGenerator.GenerateVtolMission(config);
    }

    /// <summary>
    /// Generate grid survey mission for VTOL
    /// </summary>
    public List<MissionWaypoint> GenerateVtolGridSurvey(
        double centerLat, double centerLon,
        int rows, int cols, double spacing,
        double altitude = 100)
    {
        var config = new VtolMissionGenerator.VtolMissionConfig
        {
            TakeoffLatitude = centerLat,
            TakeoffLongitude = centerLon,
            TakeoffAltitude = 20,
            
            LandingLatitude = centerLat,
            LandingLongitude = centerLon,
            
            CruiseAltitude = altitude,
            CruiseSpeed = 15,
            
            MissionType = VtolMissionGenerator.VtolMissionType.GridSurvey
        };

        return VtolMissionGenerator.GenerateVtolMission(config);
    }

    /// <summary>
    /// Generate corridor mapping mission for VTOL
    /// </summary>
    public List<MissionWaypoint> GenerateVtolCorridorMapping(
        double startLat, double startLon,
        double endLat, double endLon,
        double altitude = 100)
    {
        var config = new VtolMissionGenerator.VtolMissionConfig
        {
            TakeoffLatitude = startLat,
            TakeoffLongitude = startLon,
            TakeoffAltitude = 20,
            
            LandingLatitude = endLat,
            LandingLongitude = endLon,
            
            CruiseAltitude = altitude,
            CruiseSpeed = 15,
            
            MissionType = VtolMissionGenerator.VtolMissionType.CorridorMapping
        };

        return VtolMissionGenerator.GenerateVtolMission(config);
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
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

    /// <summary>
    /// Generate survey mission with grid pattern (Mission Planner style)
    /// </summary>
    public List<MissionWaypoint> GenerateSurveyMission(
        List<(double Lat, double Lon)> polygonArea,
        double lineSpacing,
        double altitude,
        double angle = 0,
        double overshoot = 0,
        double cameraFootprint = 0)
    {
        return MissionPlannerGenerator.GenerateSurveyMission(
            polygonArea, lineSpacing, altitude, angle, overshoot, cameraFootprint);
    }

    /// <summary>
    /// Generate corridor mapping mission (Mission Planner style)
    /// </summary>
    public List<MissionWaypoint> GenerateCorridorMission(
        List<(double Lat, double Lon)> centerLine,
        double corridorWidth,
        double altitude,
        double lineSpacing)
    {
        return MissionPlannerGenerator.GenerateCorridorMission(
            centerLine, corridorWidth, altitude, lineSpacing);
    }

    /// <summary>
    /// Generate circular or spiral mission (Mission Planner style)
    /// </summary>
    public List<MissionWaypoint> GenerateCircularMission(
        double centerLat,
        double centerLon,
        double radius,
        double altitude,
        int numPoints = 8,
        bool isSpiral = false,
        double spiralTurns = 3)
    {
        return MissionPlannerGenerator.GenerateCircularMission(
            centerLat, centerLon, radius, altitude, numPoints, isSpiral, spiralTurns);
    }

    /// <summary>
    /// Generate structure scan mission (Mission Planner style)
    /// </summary>
    public List<MissionWaypoint> GenerateStructureScanMission(
        double centerLat,
        double centerLon,
        double radius,
        double minAltitude,
        double maxAltitude,
        int numLayers = 3,
        int pointsPerLayer = 8)
    {
        return MissionPlannerGenerator.GenerateStructureScanMission(
            centerLat, centerLon, radius, minAltitude, maxAltitude, numLayers, pointsPerLayer);
    }

    private static WaypointCommand ConvertMissionCommandToWaypointCommand(MavCommand command)
    {
        var raw = (int)command;
        return Enum.IsDefined(typeof(WaypointCommand), raw)
            ? (WaypointCommand)raw
            : WaypointCommand.Waypoint;
    }

    private static MavCommand ConvertWaypointCommandToMissionCommand(WaypointCommand command)
    {
        var raw = (int)command;
        return Enum.IsDefined(typeof(MavCommand), raw)
            ? (MavCommand)raw
            : MavCommand.NavWaypoint;
    }
}
