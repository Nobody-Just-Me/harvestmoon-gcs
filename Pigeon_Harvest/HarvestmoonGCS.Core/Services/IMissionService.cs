using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Helpers;

namespace HarvestmoonGCS.Core.Services;

public interface IMissionService
{
    // Events
    event EventHandler<int> UploadProgressChanged;
    event EventHandler<int> DownloadProgressChanged;
    event EventHandler<MissionOperationResult> OperationCompleted;
    event EventHandler<string> ErrorOccurred;

    // Mission operations
    Task<bool> UploadMissionAsync(IEnumerable<MissionWaypoint> waypoints);
    Task<List<MissionWaypoint>> DownloadMissionAsync();
    Task<bool> ClearMissionAsync();
    
    // File operations
    Task<List<MissionWaypoint>> ImportMissionAsync(string filePath);
    Task<bool> ExportMissionAsync(IEnumerable<MissionWaypoint> waypoints, string filePath);
    
    // Validation and statistics
    Task<MissionValidationResult> ValidateMissionAsync(IEnumerable<MissionWaypoint> waypoints);
    Task<MissionStatistics> CalculateStatisticsAsync(IEnumerable<MissionWaypoint> waypoints);
    
    // Waypoint creation
    MissionWaypoint CreateWaypoint(double latitude, double longitude, double altitude);
    
    // VTOL Auto Waypoint Generation
    List<MissionWaypoint> GenerateVtolAutoMission(
        double takeoffLat, double takeoffLon,
        double landingLat, double landingLon,
        List<(double Lat, double Lon)> surveyPoints = null,
        VtolMissionGenerator.VtolMissionType missionType = VtolMissionGenerator.VtolMissionType.SurveyWithTransition);
    
    List<MissionWaypoint> GenerateVtolAutoMissionWithConfig(VtolMissionGenerator.VtolMissionConfig config);
    
    List<MissionWaypoint> GenerateVtolGridSurvey(
        double centerLat, double centerLon,
        int rows, int cols, double spacing,
        double altitude = 100);
    
    List<MissionWaypoint> GenerateVtolCorridorMapping(
        double startLat, double startLon,
        double endLat, double endLon,
        double altitude = 100);
    
    // Mission Planner-style Mission Generation
    List<MissionWaypoint> GenerateSurveyMission(
        List<(double Lat, double Lon)> polygonArea,
        double lineSpacing,
        double altitude,
        double angle = 0,
        double overshoot = 0,
        double cameraFootprint = 0);
    
    List<MissionWaypoint> GenerateCorridorMission(
        List<(double Lat, double Lon)> centerLine,
        double corridorWidth,
        double altitude,
        double lineSpacing);
    
    List<MissionWaypoint> GenerateCircularMission(
        double centerLat,
        double centerLon,
        double radius,
        double altitude,
        int numPoints = 8,
        bool isSpiral = false,
        double spiralTurns = 3);
    
    List<MissionWaypoint> GenerateStructureScanMission(
        double centerLat,
        double centerLon,
        double radius,
        double minAltitude,
        double maxAltitude,
        int numLayers = 3,
        int pointsPerLayer = 8);
}
