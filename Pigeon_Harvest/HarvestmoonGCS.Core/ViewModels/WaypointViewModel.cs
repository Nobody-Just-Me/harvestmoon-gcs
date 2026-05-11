using HarvestmoonGCS.Core.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System;

namespace HarvestmoonGCS.ViewModels;

/// <summary>
/// ViewModel for Waypoint management - handles waypoint operations and mission execution
/// </summary>
public partial class WaypointViewModel : ViewModelBase
{
    private readonly IMavLinkService _mavLinkService;
    private readonly IMissionService _missionService;
    private readonly IDialogService _dialogService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private int _sequence;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;

    [ObservableProperty]
    private float _altitude = 20;

    [ObservableProperty]
    private float _speed;

    [ObservableProperty]
    private float _radius;
    
    [ObservableProperty]
    private string _command = "WAYPOINT";

    [ObservableProperty]
    private double _distanceToNext;

    // Collection of waypoints for mission management
    [ObservableProperty]
    private ObservableCollection<WaypointData> _waypoints = new();

    [ObservableProperty]
    private bool _isMissionRunning;

    [ObservableProperty]
    private bool _canStartMission;

    [ObservableProperty]
    private string _missionStatus = "Ready";

    public WaypointViewModel()
    {
        // Parameterless constructor for design-time support
        _mavLinkService = null!;
        _missionService = null!;
        _dialogService = null!;
        _fileService = null!;
    }

    public WaypointViewModel(
        IMavLinkService mavLinkService,
        IMissionService missionService,
        IDialogService dialogService,
        IFileService fileService)
    {
        _mavLinkService = mavLinkService;
        _missionService = missionService;
        _dialogService = dialogService;
        _fileService = fileService;

        // Subscribe to MAVLink connection changes
        _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        UpdateCanStartMission();
    }

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        UpdateCanStartMission();
    }

    partial void OnWaypointsChanged(ObservableCollection<WaypointData> value)
    {
        UpdateCanStartMission();
    }

    private void UpdateCanStartMission()
    {
        CanStartMission = _mavLinkService?.IsConnected == true && Waypoints.Count > 0 && !IsMissionRunning;
    }

    /// <summary>
    /// Load mission from file - imports waypoints from .waypoints, .txt, or .csv files
    /// </summary>
    [RelayCommand]
    public async Task LoadMission()
    {
        try
        {
            var filePath = await _fileService.PickFileAsync(new[] { ".waypoints", ".txt", ".csv" });
            
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            bool isMissionPlannerFormat = lines.Length > 0 && lines[0].StartsWith("QGC WPL");
            
            var importedWaypoints = new List<WaypointData>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("QGC"))
                    continue;
                
                var parts = line.Split(new char[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (isMissionPlannerFormat && parts.Length >= 11)
                {
                    // Mission Planner format: seq current frame command p1 p2 p3 p4 x y z autocontinue
                    if (int.TryParse(parts[0], out int seq) &&
                        double.TryParse(parts[8], out double lat) &&
                        double.TryParse(parts[9], out double lon) &&
                        double.TryParse(parts[10], out double alt))
                    {
                        var waypoint = new WaypointData
                        {
                            Sequence = seq,
                            Latitude = lat,
                            Longitude = lon,
                            Altitude = alt,
                            Command = WaypointCommand.Waypoint
                        };
                        
                        // Parse command if available
                        if (int.TryParse(parts[3], out int cmdInt))
                        {
                            waypoint.Command = (WaypointCommand)cmdInt;
                        }
                        
                        importedWaypoints.Add(waypoint);
                    }
                }
                else if (!isMissionPlannerFormat && parts.Length >= 3)
                {
                    // Simple CSV format: lat, lon, alt
                    if (double.TryParse(parts[0], out double lat) &&
                        double.TryParse(parts[1], out double lon) &&
                        double.TryParse(parts[2], out double alt))
                    {
                        var waypoint = new WaypointData
                        {
                            Sequence = importedWaypoints.Count,
                            Latitude = lat,
                            Longitude = lon,
                            Altitude = alt,
                            Command = WaypointCommand.Waypoint
                        };
                        importedWaypoints.Add(waypoint);
                    }
                }
            }
            
            // Resequence waypoints
            for (int i = 0; i < importedWaypoints.Count; i++)
            {
                importedWaypoints[i].Sequence = i;
            }

            if (importedWaypoints.Count > 0)
            {
                Waypoints.Clear();
                foreach (var wp in importedWaypoints)
                {
                    Waypoints.Add(wp);
                }
                
                MissionStatus = $"Loaded {importedWaypoints.Count} waypoints";
                await _dialogService.ShowAlertAsync($"Successfully imported {importedWaypoints.Count} waypoints from file.", "Import Complete");
            }
            else
            {
                await _dialogService.ShowAlertAsync("No valid waypoints found in the file.", "Import Failed");
            }
            
            UpdateCanStartMission();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"Error loading mission: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Upload mission to the vehicle - sends waypoints to the autopilot
    /// </summary>
    [RelayCommand]
    public async Task UploadMission()
    {
        try
        {
            if (Waypoints.Count == 0)
            {
                await _dialogService.ShowAlertAsync("No waypoints to upload. Please add waypoints first.", "Upload Mission");
                return;
            }

            if (!_mavLinkService.IsConnected)
            {
                await _dialogService.ShowAlertAsync("Vehicle not connected. Please connect to the vehicle first.", "Upload Mission");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmAsync(
                $"Upload {Waypoints.Count} waypoints to the vehicle?",
                "Confirm Upload");

            if (!confirmed) return;

            MissionStatus = "Uploading mission...";

            // Convert WaypointData to MissionWaypoint format expected by IMissionService
            var missionWaypoints = Waypoints.Select(wp => new MissionWaypoint
            {
                Sequence = wp.Sequence,
                Latitude = wp.Latitude,
                Longitude = wp.Longitude,
                Altitude = wp.Altitude,
                Command = (MavCommand)wp.Command,
                Frame = MavFrame.GlobalRelativeAlt,
                IsAutoContinue = true
            }).ToList();

            var success = await _missionService.UploadMissionAsync(missionWaypoints);

            if (success)
            {
                MissionStatus = $"Uploaded {Waypoints.Count} waypoints";
                await _dialogService.ShowAlertAsync($"Successfully uploaded {Waypoints.Count} waypoints to the vehicle.", "Upload Complete");
            }
            else
            {
                MissionStatus = "Upload failed";
                await _dialogService.ShowAlertAsync("Failed to upload mission. Please check the connection and try again.", "Upload Failed");
            }
            
            UpdateCanStartMission();
        }
        catch (Exception ex)
        {
            MissionStatus = "Upload error";
            await _dialogService.ShowAlertAsync($"Error uploading mission: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Start mission execution - sends the start mission command to the vehicle
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartMission))]
    public async Task StartMission()
    {
        try
        {
            if (Waypoints.Count == 0)
            {
                await _dialogService.ShowAlertAsync("No waypoints to execute. Please upload a mission first.", "Start Mission");
                return;
            }

            if (!_mavLinkService.IsConnected)
            {
                await _dialogService.ShowAlertAsync("Vehicle not connected", "Start Mission");
                return;
            }

            // Confirm before starting mission
            var confirmed = await _dialogService.ShowConfirmAsync(
                $"Start mission execution with {Waypoints.Count} waypoints?\n\nThe vehicle will switch to AUTO mode and begin executing the mission.",
                "Start Mission");

            if (!confirmed) return;

            MissionStatus = "Starting mission...";

            // Start mission (first item = 0, last item = 0 means run all)
            var success = await _mavLinkService.StartMissionAsync(0, 0);

            if (success)
            {
                IsMissionRunning = true;
                MissionStatus = "Mission running - AUTO mode";
                await _dialogService.ShowAlertAsync("Mission started successfully! Vehicle is now in AUTO mode.", "Mission Started");
            }
            else
            {
                MissionStatus = "Failed to start mission";
                await _dialogService.ShowAlertAsync("Failed to start mission. Check connection and try again.", "Error");
            }
            
            UpdateCanStartMission();
        }
        catch (Exception ex)
        {
            MissionStatus = "Start mission error";
            await _dialogService.ShowAlertAsync($"Error starting mission: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Pause the current mission
    /// </summary>
    [RelayCommand]
    public async Task PauseMission()
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                await _dialogService.ShowAlertAsync("Vehicle not connected", "Pause Mission");
                return;
            }

            MissionStatus = "Pausing mission...";

            var success = await _mavLinkService.PauseMissionAsync();

            if (success)
            {
                MissionStatus = "Mission paused";
                await _dialogService.ShowAlertAsync("Mission paused successfully.", "Mission Paused");
            }
            else
            {
                MissionStatus = "Failed to pause mission";
                await _dialogService.ShowAlertAsync("Failed to pause mission.", "Error");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"Error pausing mission: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Resume the paused mission
    /// </summary>
    [RelayCommand]
    public async Task ResumeMission()
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                await _dialogService.ShowAlertAsync("Vehicle not connected", "Resume Mission");
                return;
            }

            MissionStatus = "Resuming mission...";

            var success = await _mavLinkService.ResumeMissionAsync();

            if (success)
            {
                IsMissionRunning = true;
                MissionStatus = "Mission running - AUTO mode";
                await _dialogService.ShowAlertAsync("Mission resumed successfully.", "Mission Resumed");
            }
            else
            {
                MissionStatus = "Failed to resume mission";
                await _dialogService.ShowAlertAsync("Failed to resume mission.", "Error");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync($"Error resuming mission: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Clear all waypoints
    /// </summary>
    [RelayCommand]
    public void ClearWaypoints()
    {
        Waypoints.Clear();
        MissionStatus = "Waypoints cleared";
        UpdateCanStartMission();
    }

    /// <summary>
    /// Add a new waypoint
    /// </summary>
    public void AddWaypoint(double latitude, double longitude, double altitude = 100)
    {
        var waypoint = new WaypointData
        {
            Sequence = Waypoints.Count,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude,
            Command = WaypointCommand.Waypoint
        };
        Waypoints.Add(waypoint);
        UpdateCanStartMission();
    }

    /// <summary>
    /// Remove a waypoint
    /// </summary>
    public void RemoveWaypoint(WaypointData waypoint)
    {
        Waypoints.Remove(waypoint);
        // Renumber remaining waypoints
        for (int i = 0; i < Waypoints.Count; i++)
        {
            Waypoints[i].Sequence = i;
        }
        UpdateCanStartMission();
    }
}
