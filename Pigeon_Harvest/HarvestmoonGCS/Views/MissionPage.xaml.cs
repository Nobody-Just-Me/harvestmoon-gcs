using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Models;
using Windows.Storage;
using Windows.Storage.Pickers;
using HarvestmoonGCS.Controls;

namespace HarvestmoonGCS.Views;

public sealed partial class MissionPage : Page
{
    private readonly IMissionService _missionService;
    private readonly IMavLinkService _mavLinkService;
    private readonly IGeofenceService _geofenceService;
    private List<MissionWaypoint> _waypoints = new();
    
    // Waypoint UI tracking
    private class WaypointMarker
    {
        public int Sequence { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public Border MarkerElement { get; set; }
    }
    private List<WaypointMarker> _waypointMarkers = new();
    
    // Undo/Redo stacks
    private Stack<List<MissionWaypoint>> _undoStack = new();
    private Stack<List<MissionWaypoint>> _redoStack = new();
    private int _lastMapProviderIndex = -1;
    private DateTime _lastStatusMessageAt = DateTime.MinValue;
    private DispatcherTimer? _messageClearTimer;
    private int _messageSequence;
    private int _scheduledMessageSequence;
    private bool _handlersAttached;
    
    public MissionPage()
    {
        this.InitializeComponent();
        
        // Get services from DI container
        _missionService = App.GetService<IMissionService>();
        _mavLinkService = App.GetService<IMavLinkService>();
        _geofenceService = App.GetService<IGeofenceService>();
        
        Loaded += MissionPage_Loaded;
        Unloaded += MissionPage_Unloaded;
        
        // Initialize UI
        UpdateConnectionStatus();
        UpdateStatistics();
        
        AttachHandlersIfNeeded();
        
        // Subscribe to vehicle position updates (if available)
        SubscribeToVehicleUpdates();
    }

    private void MissionPage_Loaded(object sender, RoutedEventArgs e)
    {
        AttachHandlersIfNeeded();
        UpdateConnectionStatus();
    }

    private void MissionPage_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachHandlersIfNeeded();
    }

    private void AttachHandlersIfNeeded()
    {
        if (_handlersAttached)
        {
            return;
        }

        _missionService.UploadProgressChanged += OnUploadProgressChanged;
        _missionService.DownloadProgressChanged += OnDownloadProgressChanged;
        _missionService.OperationCompleted += OnOperationCompleted;
        _missionService.ErrorOccurred += OnErrorOccurred;
        _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _mavLinkService.TelemetryReceived += OnTelemetryReceived;
        missionMapControl.WaypointDragged += OnWaypointDragged;
        _handlersAttached = true;
    }

    private void DetachHandlersIfNeeded()
    {
        if (!_handlersAttached)
        {
            return;
        }

        _missionService.UploadProgressChanged -= OnUploadProgressChanged;
        _missionService.DownloadProgressChanged -= OnDownloadProgressChanged;
        _missionService.OperationCompleted -= OnOperationCompleted;
        _missionService.ErrorOccurred -= OnErrorOccurred;
        _mavLinkService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _mavLinkService.TelemetryReceived -= OnTelemetryReceived;
        missionMapControl.WaypointDragged -= OnWaypointDragged;
        _handlersAttached = false;
    }
    
    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateConnectionStatus();
        });
    }
    
    private void UpdateConnectionStatus()
    {
        if (_mavLinkService.IsConnected)
        {
            StatusTextBlock.Text = $"Connected - {_mavLinkService.ConnectionString}";
            DownloadButton.IsEnabled = true;
            UploadButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
        }
        else
        {
            StatusTextBlock.Text = "Not connected";
            DownloadButton.IsEnabled = false;
            UploadButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
        }
    }
    
    private void OnUploadProgressChanged(object? sender, int progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var total = _waypoints.Count;
            var percentage = total > 0 ? (progress * 100.0 / total) : 0;
            OperationProgressBar.Value = percentage;
            MessageTextBlock.Text = $"Uploading waypoint {progress} of {total}...";
        });
    }
    
    private void OnDownloadProgressChanged(object? sender, int progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            MessageTextBlock.Text = $"Downloading waypoint {progress}...";
        });
    }
    
    private void OnOperationCompleted(object? sender, MissionOperationResult result)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            OperationProgressBar.Visibility = Visibility.Collapsed;
            ShowMessage(result.Message, !result.Success);
        });
    }
    
    private void OnErrorOccurred(object? sender, string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowMessage(error, true);
            OperationProgressBar.Visibility = Visibility.Collapsed;
        });
    }
    
    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            OperationProgressBar.Visibility = Visibility.Visible;
            OperationProgressBar.Value = 0;
            MessageTextBlock.Text = "Downloading mission...";
            
            var waypoints = await DownloadMissionFromAutopilot();
            
            if (waypoints != null && waypoints.Count > 0)
            {
                SaveState(); // Save for undo
                
                // Clear existing waypoints
                _waypoints.Clear();
                missionMapControl.ClearWaypointMarkers();
                _waypointMarkers.Clear();
                
                // Add downloaded waypoints
                _waypoints = waypoints.ToList();
                
                // Rebuild UI
                RebuildWaypointMarkers();
                RefreshWaypointList();
                await UpdateStatistics();
                UpdateTotalDistance();
                
                ShowMessage($"Downloaded {waypoints.Count} waypoints", false);
            }
            else
            {
                ShowMessage("No waypoints found on autopilot", false);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Download failed: {ex.Message}", true);
            OperationProgressBar.Visibility = Visibility.Collapsed;
        }
    }
    
    private async Task<List<MissionWaypoint>> DownloadMissionFromAutopilot()
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Starting mission download");

            var downloaded = await _missionService.DownloadMissionAsync();
            if (downloaded == null || downloaded.Count == 0)
            {
                Serilog.Log.Information("[MissionPage] No mission items on autopilot");
                return new List<MissionWaypoint>();
            }

            Serilog.Log.Information("[MissionPage] Mission download completed: {Count} waypoints", downloaded.Count);
            return downloaded;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error during mission download");
            throw;
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                OperationProgressBar.Visibility = Visibility.Collapsed;
            });
        }
    }
    
    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_waypoints.Count == 0)
            {
                ShowMessage("No waypoints to upload", true);
                return;
            }
            
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            // Validate mission first
            var validation = await _missionService.ValidateMissionAsync(_waypoints);
            
            if (!validation.IsValid)
            {
                var dialog = new ContentDialog
                {
                    Title = "Mission Validation Failed",
                    Content = string.Join("\n", validation.Errors),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }
            
            if (validation.Warnings.Count > 0)
            {
                var dialog = new ContentDialog
                {
                    Title = "Mission Warnings",
                    Content = string.Join("\n", validation.Warnings) + "\n\nContinue with upload?",
                    PrimaryButtonText = "Upload",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                    return;
            }
            
            OperationProgressBar.Visibility = Visibility.Visible;
            OperationProgressBar.Value = 0;
            MessageTextBlock.Text = "Uploading mission...";
            
            // Upload mission using enhanced method
            var success = await UploadMissionToAutopilot(_waypoints);
            
            if (success)
            {
                ShowMessage($"Uploaded {_waypoints.Count} waypoints successfully", false);
                
                // Enable Start Mission button after successful upload
                btn_start_mission.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Upload failed: {ex.Message}", true);
            OperationProgressBar.Visibility = Visibility.Collapsed;
        }
    }
    
    private async Task<bool> UploadMissionToAutopilot(List<MissionWaypoint> waypoints)
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Starting mission upload: {Count} waypoints", waypoints.Count);

            var uploadSuccess = await _missionService.UploadMissionAsync(waypoints);
            
            if (uploadSuccess)
            {
                Serilog.Log.Information("[MissionPage] Mission upload completed successfully");
                return true;
            }
            else
            {
                Serilog.Log.Warning("[MissionPage] Mission upload failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error during mission upload");
            return false;
        }
    }
    
    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Mission",
            Content = "Are you sure you want to clear the mission on the drone?",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };
        
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                if (!_mavLinkService.IsConnected)
                {
                    ShowMessage("Vehicle not connected", true);
                    return;
                }
                
                OperationProgressBar.Visibility = Visibility.Visible;
                OperationProgressBar.Value = 0;
                MessageTextBlock.Text = "Clearing mission on autopilot...";
                
                var success = await ClearMissionOnAutopilot();
                
                if (success)
                {
                    SaveState(); // Save for undo
                    
                    _waypoints.Clear();
                    missionMapControl.ClearWaypointMarkers();
                    _waypointMarkers.Clear();
                    RefreshWaypointList();
                    await UpdateStatistics();
                    UpdateTotalDistance();
                    
                    ShowMessage("Mission cleared on autopilot", false);
                }
                else
                {
                    ShowMessage("Failed to clear mission on autopilot", true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Clear failed: {ex.Message}", true);
            }
            finally
            {
                OperationProgressBar.Visibility = Visibility.Collapsed;
            }
        }
    }
    
    private async Task<bool> ClearMissionOnAutopilot()
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Clearing mission on autopilot");

            var ackReceived = await _missionService.ClearMissionAsync();
            
            if (ackReceived)
            {
                Serilog.Log.Information("[MissionPage] Mission cleared successfully on autopilot");
                return true;
            }
            else
            {
                Serilog.Log.Warning("[MissionPage] Failed to clear mission on autopilot");
                return false;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error clearing mission on autopilot");
            return false;
        }
    }
    
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".waypoints");
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".csv");
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            
            if (file != null)
            {
                SaveState(); // Save for undo
                
                var importedWaypoints = await ImportWaypointsFromFile(file.Path);
                
                if (importedWaypoints.Count > 0)
                {
                    // Clear existing waypoints
                    _waypoints.Clear();
                    missionMapControl.ClearWaypointMarkers();
                    _waypointMarkers.Clear();
                    
                    // Add imported waypoints
                    _waypoints.AddRange(importedWaypoints);
                    
                    // Rebuild markers and UI
                    RebuildWaypointMarkers();
                    RefreshWaypointList();
                    await UpdateStatistics();
                    UpdateTotalDistance();
                    
                    ShowMessage($"Imported {importedWaypoints.Count} waypoints from {file.Name}", false);
                }
                else
                {
                    ShowMessage("No valid waypoints found in file", true);
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Import error: {ex.Message}", true);
        }
    }
    
    private async Task<List<MissionWaypoint>> ImportWaypointsFromFile(string filePath)
    {
        var waypoints = new List<MissionWaypoint>();
        
        try
        {
            var lines = await System.IO.File.ReadAllLinesAsync(filePath);
            bool isMissionPlannerFormat = lines.Length > 0 && lines[0].StartsWith("QGC WPL");
            
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
                        var waypoint = _missionService.CreateWaypoint(lat, lon, alt);
                        waypoint.Sequence = seq;
                        
                        // Parse command if available
                        if (int.TryParse(parts[3], out int cmdInt))
                        {
                            waypoint.Command = (MavCommand)cmdInt;
                        }
                        
                        waypoints.Add(waypoint);
                    }
                }
                else if (!isMissionPlannerFormat && parts.Length >= 3)
                {
                    // Simple CSV format: lat, lon, alt
                    if (double.TryParse(parts[0], out double lat) &&
                        double.TryParse(parts[1], out double lon) &&
                        double.TryParse(parts[2], out double alt))
                    {
                        var waypoint = _missionService.CreateWaypoint(lat, lon, alt);
                        waypoint.Sequence = waypoints.Count;
                        waypoints.Add(waypoint);
                    }
                }
            }
            
            // Resequence waypoints
            for (int i = 0; i < waypoints.Count; i++)
            {
                waypoints[i].Sequence = i;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error importing waypoints from {FilePath}", filePath);
            throw;
        }
        
        return waypoints;
    }
    
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_waypoints.Count == 0)
            {
                ShowMessage("No waypoints to export", true);
                return;
            }
            
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Mission Planner Waypoint File", new List<string> { ".waypoints" });
            picker.FileTypeChoices.Add("CSV File", new List<string> { ".csv" });
            picker.SuggestedFileName = $"mission_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSaveFileAsync();
            
            if (file != null)
            {
                await ExportWaypointsToFile(file.Path, _waypoints);
                ShowMessage($"Exported {_waypoints.Count} waypoints to {file.Name}", false);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Export error: {ex.Message}", true);
        }
    }
    
    private async Task ExportWaypointsToFile(string filePath, List<MissionWaypoint> waypoints)
    {
        try
        {
            var lines = new List<string>();
            bool isMissionPlannerFormat = filePath.EndsWith(".waypoints", StringComparison.OrdinalIgnoreCase);
            
            if (isMissionPlannerFormat)
            {
                // Mission Planner format header
                lines.Add("QGC WPL 110");
                
                for (int i = 0; i < waypoints.Count; i++)
                {
                    var wp = waypoints[i];
                    
                    // Format: seq current frame command p1 p2 p3 p4 x y z autocontinue
                    var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                        i,                              // seq
                        i == 0 ? 1 : 0,               // current (first waypoint is current)
                        (int)wp.Frame,                 // frame
                        (int)wp.Command,               // command
                        0.0,                           // p1
                        10.0,                          // p2 (acceptance radius)
                        0.0,                           // p3
                        0.0,                           // p4
                        wp.Latitude,                   // x (lat)
                        wp.Longitude,                  // y (lon)
                        wp.Altitude,                   // z (alt)
                        wp.IsAutoContinue ? 1 : 0      // autocontinue
                    );
                    lines.Add(line);
                }
            }
            else
            {
                // Simple CSV format
                lines.Add("Latitude,Longitude,Altitude,Command,Frame");
                
                foreach (var wp in waypoints)
                {
                    var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4}",
                        wp.Latitude,
                        wp.Longitude,
                        wp.Altitude,
                        wp.Command,
                        wp.Frame
                    );
                    lines.Add(line);
                }
            }
            
            await System.IO.File.WriteAllLinesAsync(filePath, lines);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error exporting waypoints to {FilePath}", filePath);
            throw;
        }
    }
    
    private async void AddWaypointButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowWaypointEditorAsync(null);
    }
    
    private async void GenerateVtolButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowMissionPlannerGeneratorAsync();
    }
    
    private async void GenerateMissionPlannerButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowMissionPlannerGeneratorAsync();
    }
    
    private async Task ShowMissionPlannerGeneratorAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Generate VTOL Mission",
            PrimaryButtonText = "Generate",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        
        var stackPanel = new StackPanel { Spacing = 10 };
        
        // Mission Type
        var missionTypeCombo = new ComboBox
        {
            Header = "Mission Type",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        missionTypeCombo.Items.Add("Simple VTOL (No Transition)");
        missionTypeCombo.Items.Add("Survey with Transition (Recommended)");
        missionTypeCombo.Items.Add("Grid Survey");
        missionTypeCombo.Items.Add("Corridor Mapping");
        missionTypeCombo.SelectedIndex = 1; // Default to Survey with Transition
        stackPanel.Children.Add(missionTypeCombo);
        
        // Takeoff Location
        stackPanel.Children.Add(new TextBlock { Text = "Takeoff Location", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        var takeoffLatTextBox = new TextBox
        {
            Header = "Latitude",
            Text = "-6.200000",
            PlaceholderText = "e.g., -6.200000"
        };
        stackPanel.Children.Add(takeoffLatTextBox);
        
        var takeoffLonTextBox = new TextBox
        {
            Header = "Longitude",
            Text = "106.816666",
            PlaceholderText = "e.g., 106.816666"
        };
        stackPanel.Children.Add(takeoffLonTextBox);
        
        // Landing Location
        stackPanel.Children.Add(new TextBlock { Text = "Landing Location", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        var landingLatTextBox = new TextBox
        {
            Header = "Latitude",
            Text = "-6.210000",
            PlaceholderText = "e.g., -6.210000"
        };
        stackPanel.Children.Add(landingLatTextBox);
        
        var landingLonTextBox = new TextBox
        {
            Header = "Longitude",
            Text = "106.826666",
            PlaceholderText = "e.g., 106.826666"
        };
        stackPanel.Children.Add(landingLonTextBox);
        
        // Altitudes
        stackPanel.Children.Add(new TextBlock { Text = "Altitude Settings", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        var takeoffAltTextBox = new TextBox
        {
            Header = "Takeoff Altitude (m)",
            Text = "20",
            PlaceholderText = "20"
        };
        stackPanel.Children.Add(takeoffAltTextBox);
        
        var cruiseAltTextBox = new TextBox
        {
            Header = "Cruise Altitude (m)",
            Text = "100",
            PlaceholderText = "100"
        };
        stackPanel.Children.Add(cruiseAltTextBox);
        
        var transitionAltTextBox = new TextBox
        {
            Header = "Transition Altitude (m)",
            Text = "50",
            PlaceholderText = "50"
        };
        stackPanel.Children.Add(transitionAltTextBox);
        
        // Speed
        var cruiseSpeedTextBox = new TextBox
        {
            Header = "Cruise Speed (m/s)",
            Text = "15",
            PlaceholderText = "15"
        };
        stackPanel.Children.Add(cruiseSpeedTextBox);
        
        // Grid settings (only for grid survey)
        var gridSettingsPanel = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        gridSettingsPanel.Children.Add(new TextBlock { Text = "Grid Settings", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        
        var gridRowsTextBox = new TextBox
        {
            Header = "Rows",
            Text = "5",
            PlaceholderText = "5"
        };
        gridSettingsPanel.Children.Add(gridRowsTextBox);
        
        var gridColsTextBox = new TextBox
        {
            Header = "Columns",
            Text = "5",
            PlaceholderText = "5"
        };
        gridSettingsPanel.Children.Add(gridColsTextBox);
        
        var gridSpacingTextBox = new TextBox
        {
            Header = "Spacing (m)",
            Text = "50",
            PlaceholderText = "50"
        };
        gridSettingsPanel.Children.Add(gridSpacingTextBox);
        
        stackPanel.Children.Add(gridSettingsPanel);
        
        // Show/hide grid settings based on mission type
        missionTypeCombo.SelectionChanged += (s, e) =>
        {
            gridSettingsPanel.Visibility = missionTypeCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        };
        
        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            MaxHeight = 500
        };
        
        dialog.Content = scrollViewer;
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var takeoffLat = double.Parse(takeoffLatTextBox.Text);
                var takeoffLon = double.Parse(takeoffLonTextBox.Text);
                var landingLat = double.Parse(landingLatTextBox.Text);
                var landingLon = double.Parse(landingLonTextBox.Text);
                var takeoffAlt = double.Parse(takeoffAltTextBox.Text);
                var cruiseAlt = double.Parse(cruiseAltTextBox.Text);
                var transitionAlt = double.Parse(transitionAltTextBox.Text);
                var cruiseSpeed = float.Parse(cruiseSpeedTextBox.Text);
                
                List<MissionWaypoint> waypoints;
                
                switch (missionTypeCombo.SelectedIndex)
                {
                    case 0: // Simple VTOL
                        waypoints = _missionService.GenerateVtolAutoMission(
                            takeoffLat, takeoffLon,
                            landingLat, landingLon,
                            null,
                            VtolMissionGenerator.VtolMissionType.SimpleVtol);
                        break;
                    
                    case 1: // Survey with Transition
                        var config = new VtolMissionGenerator.VtolMissionConfig
                        {
                            TakeoffLatitude = takeoffLat,
                            TakeoffLongitude = takeoffLon,
                            TakeoffAltitude = takeoffAlt,
                            LandingLatitude = landingLat,
                            LandingLongitude = landingLon,
                            CruiseAltitude = cruiseAlt,
                            TransitionAltitude = transitionAlt,
                            CruiseSpeed = cruiseSpeed,
                            MissionType = VtolMissionGenerator.VtolMissionType.SurveyWithTransition
                        };
                        waypoints = _missionService.GenerateVtolAutoMissionWithConfig(config);
                        break;
                    
                    case 2: // Grid Survey
                        var rows = int.Parse(gridRowsTextBox.Text);
                        var cols = int.Parse(gridColsTextBox.Text);
                        var spacing = double.Parse(gridSpacingTextBox.Text);
                        waypoints = _missionService.GenerateVtolGridSurvey(
                            takeoffLat, takeoffLon,
                            rows, cols, spacing, cruiseAlt);
                        break;
                    
                    case 3: // Corridor Mapping
                        waypoints = _missionService.GenerateVtolCorridorMapping(
                            takeoffLat, takeoffLon,
                            landingLat, landingLon,
                            cruiseAlt);
                        break;
                    
                    default:
                        waypoints = new List<MissionWaypoint>();
                        break;
                }
                
                _waypoints = waypoints;
                ResequenceWaypoints();
                RefreshWaypointList();
                await UpdateStatistics();
                
                ShowMessage($"Generated {waypoints.Count} waypoints for VTOL mission", false);
            }
            catch (Exception ex)
            {
                ShowMessage($"Generation failed: {ex.Message}", true);
            }
        }
    }
    
    private async void EditWaypoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && index < _waypoints.Count)
        {
            var waypoint = _waypoints[index];
            await ShowWaypointEditorAsync(waypoint);
        }
    }
    
    private void DeleteWaypoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && index < _waypoints.Count)
        {
            SaveState(); // Save for undo
            
            var waypoint = _waypoints[index];
            _waypoints.RemoveAt(index);
            
            // Remove from map
            missionMapControl.RemoveWaypointMarker(index);
            
            // Resequence remaining waypoints
            for (int i = 0; i < _waypoints.Count; i++)
            {
                _waypoints[i].Sequence = i;
            }
            
            // Rebuild markers with new sequence numbers
            RebuildWaypointMarkers();
            
            // Update UI
            RefreshWaypointList();
            _ = UpdateStatistics();
            UpdateTotalDistance();
            
            ShowMessage($"Waypoint {index + 1} deleted", false);
        }
    }

    private void WaypointListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection if needed for map highlighting
    }
    
    private void MissionMap_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try
        {
            var point = e.GetCurrentPoint(missionMapControl);
            if (!point.Properties.IsRightButtonPressed)
            {
                return;
            }

            var position = point.Position;
            var (lat, lon) = missionMapControl.ScreenToLatLon(position.X, position.Y);

            SaveState();

            var waypoint = _missionService.CreateWaypoint(lat, lon, 50);
            waypoint.Sequence = _waypoints.Count;
            _waypoints.Add(waypoint);

            AddWaypointMarker(waypoint.Sequence, lat, lon);
            RefreshWaypointList();
            _ = UpdateStatistics();
            UpdateTotalDistance();

            ShowMessage($"Waypoint {waypoint.Sequence + 1} added at {lat:F6}, {lon:F6}", false);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to add waypoint: {ex.Message}", true);
        }
    }
    
    private void OnWaypointDragged(object sender, SkiaMapControl.WaypointDraggedEventArgs e)
    {
        try
        {
            // Save state for undo
            SaveState();
            
            // Update waypoint data
            if (e.Sequence < _waypoints.Count)
            {
                _waypoints[e.Sequence].Latitude = e.NewLat;
                _waypoints[e.Sequence].Longitude = e.NewLon;
                
                // Update marker tracking
                var marker = _waypointMarkers.FirstOrDefault(m => m.Sequence == e.Sequence);
                if (marker != null)
                {
                    marker.Lat = e.NewLat;
                    marker.Lon = e.NewLon;
                }
                
                // Update UI
                RefreshWaypointList();
                UpdateStatistics();
                UpdateTotalDistance();
                
                ShowMessage($"Waypoint {e.Sequence + 1} moved to {e.NewLat:F6}, {e.NewLon:F6}", false);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error updating waypoint: {ex.Message}", true);
        }
    }
    
    private void SubscribeToVehicleUpdates()
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Subscribed to vehicle position updates");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error subscribing to vehicle updates");
        }
    }

    private void OnTelemetryReceived(object? sender, HarvestmoonGCS.Models.FlightData data)
    {
        var lat = data.GPS.Latitude / 1e7;
        var lon = data.GPS.Longitude / 1e7;

        if (Math.Abs(lat) < 0.000001 && Math.Abs(lon) < 0.000001)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            OnVehiclePositionChanged(this, new VehiclePositionEventArgs
            {
                Latitude = lat,
                Longitude = lon,
                Altitude = data.AltitudeFloat,
                Heading = data.IMU.Yaw
            });
        });
    }
    
    private void OnVehiclePositionChanged(object sender, VehiclePositionEventArgs e)
    {
        try
        {
            // Update vehicle position on map
            missionMapControl.UpdateVehiclePosition(e.Latitude, e.Longitude);
            
            Serilog.Log.Debug("[MissionPage] Vehicle position updated: {Lat}, {Lon}", e.Latitude, e.Longitude);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error updating vehicle position");
        }
    }
    
    // Placeholder event args class
    public class VehiclePositionEventArgs : EventArgs
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public float Heading { get; set; }
    }
    
    // Geofencing functionality
    private bool _geofenceActive = false;
    private double _geofenceCenterLat = 0;
    private double _geofenceCenterLon = 0;
    private double _geofenceRadius = 500;
    private double _geofenceMaxAltitude = 100;
    
    private void ToggleGeofencePanel_Click(object sender, RoutedEventArgs e)
    {
        if (geofence_panel.Visibility == Visibility.Collapsed)
        {
            geofence_panel.Visibility = Visibility.Visible;
            btn_geofence_toggle.Background = ResolveThemeBrush("SuccessBrush", Microsoft.UI.Colors.Green);
            btn_geofence_toggle.Content = "✓ Geofence";
        }
        else
        {
            geofence_panel.Visibility = Visibility.Collapsed;
            btn_geofence_toggle.Background = ResolveThemeBrush("InfoBrush", Windows.UI.Color.FromArgb(255, 45, 90, 138));
            btn_geofence_toggle.Content = "🔲 Geofence";
        }
    }
    
    private void CreateGeofence_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get geofence parameters
            if (!double.TryParse(tb_geofence_radius.Text, out double radius))
            {
                ShowMessage("Invalid radius value", true);
                return;
            }
            
            if (!double.TryParse(tb_geofence_altitude.Text, out double maxAlt))
            {
                ShowMessage("Invalid altitude value", true);
                return;
            }
            
            // Use map center as geofence center
            _geofenceCenterLat = missionMapControl.Latitude;
            _geofenceCenterLon = missionMapControl.Longitude;
            _geofenceRadius = radius;
            _geofenceMaxAltitude = maxAlt;
            
            // Create geofence visualization
            CreateGeofenceVisualization();
            
            // Enable geofence
            _geofenceActive = true;
            btn_send_geofence.IsEnabled = true;
            btn_clear_geofence.IsEnabled = true;
            
            UpdateGeofenceStatus("Ready", "WarningBrush", Windows.UI.Color.FromArgb(255, 255, 165, 0));
            
            ShowMessage($"Geofence created at {_geofenceCenterLat:F6}, {_geofenceCenterLon:F6} with {radius}m radius", false);
        }
        catch (Exception ex)
        {
            ShowMessage($"Error creating geofence: {ex.Message}", true);
        }
    }
    
    private void CreateGeofenceVisualization()
    {
        missionMapControl.SetGeofence(
            isCircular: true,
            centerLat: _geofenceCenterLat,
            centerLon: _geofenceCenterLon,
            radius: _geofenceRadius);

        Serilog.Log.Information("[MissionPage] Creating geofence visualization at {Lat}, {Lon} with radius {Radius}m", 
            _geofenceCenterLat, _geofenceCenterLon, _geofenceRadius);
    }
    
    private async void SendGeofence_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            if (!_geofenceActive)
            {
                ShowMessage("No geofence to send", true);
                return;
            }
            
            UpdateGeofenceStatus("Sending...", "WarningBrush", Windows.UI.Color.FromArgb(255, 255, 255, 0));
            
            var success = await SendGeofenceToAutopilot();
            
            if (success)
            {
                UpdateGeofenceStatus("Active ✓", "SuccessBrush", Windows.UI.Color.FromArgb(255, 0, 128, 0));
                ShowMessage($"Geofence sent to vehicle successfully", false);
            }
            else
            {
                UpdateGeofenceStatus("Error", "ErrorBrush", Windows.UI.Color.FromArgb(255, 255, 0, 0));
                ShowMessage("Failed to send geofence to vehicle", true);
            }
        }
        catch (Exception ex)
        {
            UpdateGeofenceStatus("Error", "ErrorBrush", Windows.UI.Color.FromArgb(255, 255, 0, 0));
            ShowMessage($"Error sending geofence: {ex.Message}", true);
        }
    }
    
    private async Task<bool> SendGeofenceToAutopilot()
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Sending geofence to autopilot");

            _geofenceService.SetGeofenceType(GeofenceType.Circular);
            _geofenceService.SetGeofenceCenter(_geofenceCenterLat, _geofenceCenterLon);
            _geofenceService.SetGeofenceRadius(_geofenceRadius);
            _geofenceService.SetMaxAltitude(_geofenceMaxAltitude);
            _geofenceService.SetGeofenceActive(true);

            var radiusSet = await _mavLinkService.SetParameterAsync("FENCE_RADIUS", (float)_geofenceRadius);
            var altitudeSet = await _mavLinkService.SetParameterAsync("FENCE_ALT_MAX", (float)_geofenceMaxAltitude);
            var enabledSet = await _mavLinkService.SetParameterAsync("FENCE_ENABLE", 1f);

            _mavLinkService.SendMessage(new MavLinkNet.UasFencePoint
            {
                TargetSystem = 1,
                TargetComponent = 0,
                Idx = 0,
                Count = 1,
                Lat = (float)_geofenceCenterLat,
                Lng = (float)_geofenceCenterLon
            });

            await _mavLinkService.SendCommandLongAsync((int)MavCommand.DoFenceEnable, 1, 0, 0, 0, 0, 0, 0);

            return radiusSet && altitudeSet && enabledSet;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error sending geofence to autopilot");
            return false;
        }
    }
    
    private async void ClearGeofence_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Clear geofence
            _geofenceActive = false;
            btn_send_geofence.IsEnabled = false;
            btn_clear_geofence.IsEnabled = false;
            
            // Clear visualization
            ClearGeofenceVisualization();

            _geofenceService.SetGeofenceActive(false);
            _geofenceService.SetGeofenceRadius(_geofenceRadius);
            _geofenceService.SetMaxAltitude(_geofenceMaxAltitude);

            if (_mavLinkService.IsConnected)
            {
                await _mavLinkService.SetParameterAsync("FENCE_ENABLE", 0f);
                await _mavLinkService.SendCommandLongAsync((int)MavCommand.DoFenceEnable, 0, 0, 0, 0, 0, 0, 0);
            }
            
            UpdateGeofenceStatus("Inactive", "SecondaryBrush", Windows.UI.Color.FromArgb(255, 128, 128, 128));
            
            ShowMessage("Geofence cleared", false);
        }
        catch (Exception ex)
        {
            ShowMessage($"Error clearing geofence: {ex.Message}", true);
        }
    }
    
    private void ClearGeofenceVisualization()
    {
        missionMapControl.ClearGeofence();
        Serilog.Log.Information("[MissionPage] Clearing geofence visualization");
    }
    
    private void UpdateGeofenceStatus(string status, string brushResourceKey, Windows.UI.Color fallbackColor)
    {
        geofence_status_text.Text = status;
        geofence_status_border.Background = ResolveThemeBrush(brushResourceKey, fallbackColor);
    }
    
    private async Task ShowWaypointEditorAsync(MissionWaypoint? waypoint)
    {
        var isNew = waypoint == null;
        if (isNew)
        {
            waypoint = _missionService.CreateWaypoint(0, 0, 100);
            waypoint.Sequence = _waypoints.Count;
        }
        
        var dialog = new ContentDialog
        {
            Title = isNew ? "Add Waypoint" : $"Edit Waypoint {waypoint.Sequence}",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        
        var stackPanel = new StackPanel { Spacing = 10 };
        
        // Command type
        var commandCombo = new ComboBox
        {
            Header = "Command Type",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = waypoint.Command
        };
        commandCombo.Items.Add(MavCommand.NavWaypoint);
        commandCombo.Items.Add(MavCommand.NavTakeoff);
        commandCombo.Items.Add(MavCommand.NavVtolTakeoff);
        commandCombo.Items.Add(MavCommand.NavLand);
        commandCombo.Items.Add(MavCommand.NavVtolLand);
        commandCombo.Items.Add(MavCommand.NavReturnToLaunch);
        commandCombo.Items.Add(MavCommand.NavLoiterTime);
        commandCombo.Items.Add(MavCommand.NavSplineWaypoint);
        commandCombo.Items.Add(MavCommand.DoVtolTransition);
        commandCombo.SelectedItem = waypoint.Command;
        stackPanel.Children.Add(commandCombo);
        
        // Latitude
        var latTextBox = new TextBox
        {
            Header = "Latitude",
            Text = waypoint.Latitude.ToString(),
            PlaceholderText = "e.g., -6.200000"
        };
        stackPanel.Children.Add(latTextBox);
        
        // Longitude
        var lonTextBox = new TextBox
        {
            Header = "Longitude",
            Text = waypoint.Longitude.ToString(),
            PlaceholderText = "e.g., 106.816666"
        };
        stackPanel.Children.Add(lonTextBox);
        
        // Altitude
        var altTextBox = new TextBox
        {
            Header = "Altitude (meters)",
            Text = waypoint.Altitude.ToString(),
            PlaceholderText = "e.g., 100"
        };
        stackPanel.Children.Add(altTextBox);
        
        // Frame
        var frameCombo = new ComboBox
        {
            Header = "Coordinate Frame",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        frameCombo.Items.Add(MavFrame.GlobalRelativeAlt);
        frameCombo.Items.Add(MavFrame.Global);
        frameCombo.Items.Add(MavFrame.GlobalTerrainAlt);
        frameCombo.SelectedItem = waypoint.Frame;
        stackPanel.Children.Add(frameCombo);

        var param1TextBox = new TextBox
        {
            Header = "Param1",
            Text = waypoint.Param1.ToString(),
            PlaceholderText = "e.g., VTOL_TRANSITION state: 3 (MC) or 4 (FW)"
        };
        stackPanel.Children.Add(param1TextBox);

        var param2TextBox = new TextBox
        {
            Header = "Param2",
            Text = waypoint.Param2.ToString(),
            PlaceholderText = "Command-specific parameter"
        };
        stackPanel.Children.Add(param2TextBox);

        var param3TextBox = new TextBox
        {
            Header = "Param3",
            Text = waypoint.Param3.ToString(),
            PlaceholderText = "Command-specific parameter"
        };
        stackPanel.Children.Add(param3TextBox);

        var param4TextBox = new TextBox
        {
            Header = "Param4",
            Text = waypoint.Param4.ToString(),
            PlaceholderText = "Yaw / command-specific parameter"
        };
        stackPanel.Children.Add(param4TextBox);
        
        // Auto-continue
        var autoContinueCheck = new CheckBox
        {
            Content = "Auto Continue",
            IsChecked = waypoint.IsAutoContinue
        };
        stackPanel.Children.Add(autoContinueCheck);
        
        dialog.Content = stackPanel;
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                waypoint.Command = (MavCommand)commandCombo.SelectedItem;
                waypoint.Latitude = double.Parse(latTextBox.Text);
                waypoint.Longitude = double.Parse(lonTextBox.Text);
                waypoint.Altitude = double.Parse(altTextBox.Text);
                waypoint.Frame = (MavFrame)frameCombo.SelectedItem;
                waypoint.IsAutoContinue = autoContinueCheck.IsChecked ?? true;
                waypoint.Param1 = float.Parse(param1TextBox.Text);
                waypoint.Param2 = float.Parse(param2TextBox.Text);
                waypoint.Param3 = float.Parse(param3TextBox.Text);
                waypoint.Param4 = float.Parse(param4TextBox.Text);

                if (waypoint.Command == MavCommand.DoVtolTransition && waypoint.Param1 != 3 && waypoint.Param1 != 4)
                {
                    throw new InvalidOperationException("DO_VTOL_TRANSITION Param1 must be 3 (MC) or 4 (FW)");
                }
                
                if (isNew)
                {
                    _waypoints.Add(waypoint);
                }
                
                ResequenceWaypoints();
                RefreshWaypointList();
                await UpdateStatistics();
                
                ShowMessage(isNew ? "Waypoint added" : "Waypoint updated", false);
            }
            catch (Exception ex)
            {
                ShowMessage($"Invalid input: {ex.Message}", true);
            }
        }
    }
    
    private void ResequenceWaypoints()
    {
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence = i;
            _waypoints[i].IsCurrent = (i == 0);
        }
    }
    
    private void RefreshWaypointList()
    {
        WaypointListView.ItemsSource = _waypoints;
        
        // Also populate the waypoint dock
        PopulateWaypointDock();
    }
    
    private void PopulateWaypointDock()
    {
        // Clear existing items
        wp_dock_stack.Children.Clear();
        
        // Add waypoint items to dock
        for (int i = 0; i < _waypoints.Count; i++)
        {
            var waypoint = _waypoints[i];
            var waypointItem = CreateWaypointDockItem(waypoint, i);
            wp_dock_stack.Children.Add(waypointItem);
        }
    }
    
    private Border CreateWaypointDockItem(MissionWaypoint waypoint, int index)
    {
        var border = new Border
        {
            Background = ResolveThemeBrush("SecondaryBrush", Microsoft.UI.Colors.DarkSlateGray),
            BorderBrush = ResolveThemeBrush("BorderBrush", Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(3),
            Tag = index
        };
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        
        // Waypoint number
        var numberText = new TextBlock
        {
            Text = $"WP{index + 1}",
            Foreground = ResolveThemeBrush("WarningBrush", Microsoft.UI.Colors.Orange),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        Grid.SetColumn(numberText, 0);
        grid.Children.Add(numberText);
        
        // Coordinates and command
        var infoStack = new StackPanel { Orientation = Orientation.Vertical };
        
        var coordText = new TextBlock
        {
            Text = $"{waypoint.Latitude:F6}, {waypoint.Longitude:F6}",
            Foreground = ResolveThemeBrush("ForegroundBrush", Microsoft.UI.Colors.White),
            FontSize = 10,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
        };
        infoStack.Children.Add(coordText);
        
        var commandText = new TextBlock
        {
            Text = $"{waypoint.Command} | Alt: {waypoint.Altitude}m",
            Foreground = ResolveThemeBrush("MutedForegroundBrush", Microsoft.UI.Colors.LightGray),
            FontSize = 9
        };
        infoStack.Children.Add(commandText);
        
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);
        
        // Edit button
        var editButton = new Button
        {
            Content = "✏",
            Width = 25,
            Height = 25,
            FontSize = 12,
            Background = ResolveThemeBrush("InfoBrush", Microsoft.UI.Colors.DarkBlue),
            Foreground = ResolveThemeBrush("PrimaryForegroundBrush", Microsoft.UI.Colors.White),
            Margin = new Thickness(2, 0, 2, 0),
            Tag = index
        };
        editButton.Click += EditWaypoint_Click;
        Grid.SetColumn(editButton, 2);
        grid.Children.Add(editButton);
        
        // Delete button
        var deleteButton = new Button
        {
            Content = "✕",
            Width = 25,
            Height = 25,
            FontSize = 12,
            Background = ResolveThemeBrush("ErrorBrush", Microsoft.UI.Colors.DarkRed),
            Foreground = ResolveThemeBrush("PrimaryForegroundBrush", Microsoft.UI.Colors.White),
            Tag = index
        };
        deleteButton.Click += DeleteWaypoint_Click;
        Grid.SetColumn(deleteButton, 3);
        grid.Children.Add(deleteButton);
        
        border.Child = grid;
        return border;
    }
    
    private async Task UpdateStatistics()
    {
        try
        {
            var stats = await _missionService.CalculateStatisticsAsync(_waypoints);
            
            WaypointCountText.Text = $"Waypoints: {stats.TotalWaypoints}";
            DistanceText.Text = $"Distance: {stats.TotalDistance:F0} m";
            DurationText.Text = $"Est. Duration: {stats.EstimatedDuration:mm\\:ss}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating statistics: {ex.Message}");
        }
    }
    
    private void ShowMessage(string message, bool isError)
    {
        var now = DateTime.UtcNow;
        if (!isError && (now - _lastStatusMessageAt).TotalMilliseconds < 300)
        {
            return;
        }

        _lastStatusMessageAt = now;
        MessageTextBlock.Text = message;
        MessageTextBlock.Foreground = isError
            ? ResolveThemeBrush("ErrorBrush", Microsoft.UI.Colors.Red)
            : ResolveThemeBrush("SuccessBrush", Microsoft.UI.Colors.Green);

        _scheduledMessageSequence = ++_messageSequence;
        _messageClearTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _messageClearTimer.Tick -= MessageClearTimer_Tick;
        _messageClearTimer.Tick += MessageClearTimer_Tick;
        _messageClearTimer.Stop();
        _messageClearTimer.Start();
    }

    private void MessageClearTimer_Tick(object? sender, object e)
    {
        _messageClearTimer?.Stop();
        if (_scheduledMessageSequence == _messageSequence)
        {
            MessageTextBlock.Text = string.Empty;
        }
    }
    
    // WPF-style event handlers
    private void ChooseMap(object sender, SelectionChangedEventArgs e)
    {
        if (cb_map_type == null || missionMapControl == null)
            return;

        if (cb_map_type.SelectedIndex == _lastMapProviderIndex)
        {
            return;
        }
             
        var providers = new[]
        {
            SkiaMapControl.MapTileProvider.ArcGISTopographic,
            SkiaMapControl.MapTileProvider.ArcGISImagery,
            SkiaMapControl.MapTileProvider.ArcGISStreetMap,
            SkiaMapControl.MapTileProvider.GoogleMap,
            SkiaMapControl.MapTileProvider.GoogleSatellite,
            SkiaMapControl.MapTileProvider.GoogleTerrain,
            SkiaMapControl.MapTileProvider.GoogleHybrid
        };
        
        if (cb_map_type.SelectedIndex >= 0 && cb_map_type.SelectedIndex < providers.Length)
        {
            _lastMapProviderIndex = cb_map_type.SelectedIndex;
            missionMapControl.SetTileProvider(providers[cb_map_type.SelectedIndex]);
        }
    }
    
    private async void SendwaypointCommand(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            if (_waypoints.Count == 0)
            {
                ShowMessage("No waypoints to send", true);
                return;
            }
            
            // Get WP_RADIUS parameter value
            if (double.TryParse(tb_radius.Text, out double radius))
            {
                // Send WP_RADIUS parameter
                var radiusSet = await SendWaypointRadiusParameter(radius);
                if (!radiusSet)
                {
                    ShowMessage("Failed to set WP_RADIUS parameter", true);
                    return;
                }
            }
            
            // Send current waypoint command (simplified)
            if (_waypoints.Count > 0)
            {
                var currentWaypoint = _waypoints.FirstOrDefault(w => w.IsCurrent) ?? _waypoints[0];
                var gotoSent = await SendGotoWaypointCommand(currentWaypoint);
                if (!gotoSent)
                {
                    ShowMessage("Failed to set current waypoint", true);
                    return;
                }
                
                ShowMessage($"Sent waypoint command: WP{currentWaypoint.Sequence + 1} at {currentWaypoint.Latitude:F6}, {currentWaypoint.Longitude:F6}", false);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to send waypoint command: {ex.Message}", true);
        }
    }
    
    private async Task<bool> SendWaypointRadiusParameter(double radius)
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Sending WP_RADIUS parameter: {Radius}", radius);

            return await _mavLinkService.SetParameterAsync("WP_RADIUS", (float)radius);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error sending WP_RADIUS parameter");
            return false;
        }
    }
    
    private async Task<bool> SendGotoWaypointCommand(MissionWaypoint waypoint)
    {
        try
        {
            Serilog.Log.Information("[MissionPage] Sending GOTO waypoint command: {Sequence} at {Lat}, {Lon}, {Alt}", 
                waypoint.Sequence, waypoint.Latitude, waypoint.Longitude, waypoint.Altitude);

            var setCurrentSuccess = await _mavLinkService.SetCurrentWaypointAsync(waypoint.Sequence);
            if (!setCurrentSuccess)
            {
                return false;
            }

            await _mavLinkService.SendCommandLongAsync(
                (int)MavCommand.DoSetMissionCurrent,
                waypoint.Sequence,
                0,
                0,
                0,
                (float)waypoint.Latitude,
                (float)waypoint.Longitude,
                (float)waypoint.Altitude);

            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[MissionPage] Error sending GOTO waypoint command");
            return false;
        }
    }
    
    private async void StartMission_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_waypoints.Count == 0)
            {
                ShowMessage("No waypoints to execute. Please upload a mission first.", true);
                return;
            }
            
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            // Confirm before starting mission
            var dialog = new ContentDialog
            {
                Title = "Start Mission",
                Content = $"Start mission execution with {_waypoints.Count} waypoints?\n\nThe vehicle will switch to AUTO mode and begin executing the mission.",
                PrimaryButtonText = "Start Mission",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                MessageTextBlock.Text = "Starting mission...";
                
                // Start mission (first item = 0, last item = 0 means run all)
                var success = await _mavLinkService.StartMissionAsync(0, 0);
                
                if (success)
                {
                    ShowMessage("Mission started successfully! Vehicle is now in AUTO mode.", false);
                    
                    // Enable pause button, disable start button
                    btn_start_mission.IsEnabled = false;
                    btn_pause_mission.IsEnabled = true;
                    btn_resume_mission.IsEnabled = false;
                }
                else
                {
                    ShowMessage("Failed to start mission. Check connection and try again.", true);
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error starting mission: {ex.Message}", true);
        }
    }
    
    private async void PauseMission_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            MessageTextBlock.Text = "Pausing mission...";
            
            var success = await _mavLinkService.PauseMissionAsync();
            
            if (success)
            {
                ShowMessage("Mission paused", false);
                
                // Enable resume button, disable pause button
                btn_pause_mission.IsEnabled = false;
                btn_resume_mission.IsEnabled = true;
            }
            else
            {
                ShowMessage("Failed to pause mission", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error pausing mission: {ex.Message}", true);
        }
    }
    
    private async void ResumeMission_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                ShowMessage("Vehicle not connected", true);
                return;
            }
            
            MessageTextBlock.Text = "Resuming mission...";
            
            var success = await _mavLinkService.ResumeMissionAsync();
            
            if (success)
            {
                ShowMessage("Mission resumed", false);
                
                // Enable pause button, disable resume button
                btn_pause_mission.IsEnabled = true;
                btn_resume_mission.IsEnabled = false;
            }
            else
            {
                ShowMessage("Failed to resume mission", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error resuming mission: {ex.Message}", true);
        }
    }
    
    private void IkutiWahana_Clicked(object sender, RoutedEventArgs e)
    {
        // Toggle follow vehicle mode
        bool isFollowing = missionMapControl.IsFollowingVehicle;
        missionMapControl.SetFollowVehicle(!isFollowing);
        
        if (!isFollowing)
        {
            // Started following
            ShowMessage("Follow vehicle mode enabled", false);
            
            // Update button appearance (optional - could change color/text)
            if (sender is Button button)
            {
                button.Background = ResolveThemeBrush("SuccessBrush", Microsoft.UI.Colors.Green);
            }
        }
        else
        {
            // Stopped following
            ShowMessage("Follow vehicle mode disabled", false);
            
            // Reset button appearance
            if (sender is Button button)
            {
                button.Background = ResolveThemeBrush("InfoBrush", Microsoft.UI.Colors.DeepSkyBlue);
            }
        }
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush ResolveThemeBrush(string key, Windows.UI.Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var brushObj) == true &&
            brushObj is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            return brush;
        }

        return new Microsoft.UI.Xaml.Media.SolidColorBrush(fallback);
    }
    
    private void ToggleWPDock(object sender, RoutedEventArgs e)
    {
        if (wp_dock_container.Visibility == Visibility.Collapsed)
        {
            wp_dock_container.Visibility = Visibility.Visible;
            wp_dock_btn.Content = "Markers ▼";
        }
        else
        {
            wp_dock_container.Visibility = Visibility.Collapsed;
            wp_dock_btn.Content = "Markers ▲";
        }
    }
    
    private void ResetWaypoints_Click(object sender, RoutedEventArgs e)
    {
        if (_waypoints.Count == 0)
        {
            ShowMessage("No waypoints to reset", false);
            return;
        }
        
        SaveState(); // Save for undo
        
        _waypoints.Clear();
        missionMapControl.ClearWaypointMarkers();
        _waypointMarkers.Clear();
        RefreshWaypointList();
        _ = UpdateStatistics();
        UpdateTotalDistance();
        
        ShowMessage("All waypoints cleared", false);
    }
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0)
        {
            ShowMessage("Nothing to undo", false);
            return;
        }
        
        // Save current state to redo stack
        _redoStack.Push(new List<MissionWaypoint>(_waypoints));
        
        // Restore previous state
        _waypoints = _undoStack.Pop();
        
        // Update UI
        RefreshWaypointList();
        UpdateStatistics();
        UpdateTotalDistance();
        RebuildWaypointMarkers();
        
        ShowMessage("Undo successful", false);
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count == 0)
        {
            ShowMessage("Nothing to redo", false);
            return;
        }
        
        // Save current state to undo stack
        _undoStack.Push(new List<MissionWaypoint>(_waypoints));
        
        // Restore next state
        _waypoints = _redoStack.Pop();
        
        // Update UI
        RefreshWaypointList();
        UpdateStatistics();
        UpdateTotalDistance();
        RebuildWaypointMarkers();
        
        ShowMessage("Redo successful", false);
    }
    
    private void RebuildWaypointMarkers()
    {
        if (missionMapControl == null)
        {
            return;
        }

        // Clear existing markers from map
        missionMapControl.ClearWaypointMarkers();
        _waypointMarkers.Clear();
        
        // Rebuild from waypoints list
        for (int i = 0; i < _waypoints.Count; i++)
        {
            AddWaypointMarker(i, _waypoints[i].Latitude, _waypoints[i].Longitude);
        }
    }
    
    private void MissionMap_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        try
        {
            // Get click position
            var position = e.GetPosition(missionMapControl);
            
            // Convert to lat/lon
            var (lat, lon) = missionMapControl.ScreenToLatLon(position.X, position.Y);
            
            // Save state for undo
            SaveState();
            
            // Create waypoint
            var waypoint = _missionService.CreateWaypoint(lat, lon, 50); // Default 50m altitude
            waypoint.Sequence = _waypoints.Count;
            _waypoints.Add(waypoint);
            
            // Add marker to map
            AddWaypointMarker(waypoint.Sequence, lat, lon);
            
            // Update UI
            RefreshWaypointList();
            UpdateStatistics();
            UpdateTotalDistance();
            
            ShowMessage($"Waypoint {waypoint.Sequence + 1} added at {lat:F6}, {lon:F6}", false);
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to add waypoint: {ex.Message}", true);
        }
    }
    
    private void AddWaypointMarker(int sequence, double lat, double lon)
    {
        // Add marker to map overlay
        missionMapControl.AddWaypointMarker(sequence, lat, lon);
        
        // Track in our list (simplified since SkiaMapControl handles the visual)
        var waypointMarker = new WaypointMarker
        {
            Sequence = sequence,
            Lat = lat,
            Lon = lon,
            MarkerElement = null // SkiaMapControl manages the visual element
        };
        
        _waypointMarkers.Add(waypointMarker);
    }
    
    private void SaveState()
    {
        _undoStack.Push(new List<MissionWaypoint>(_waypoints.Select(w => new MissionWaypoint
        {
            Sequence = w.Sequence,
            Latitude = w.Latitude,
            Longitude = w.Longitude,
            Altitude = w.Altitude,
            Command = w.Command,
            Frame = w.Frame,
            IsAutoContinue = w.IsAutoContinue,
            IsCurrent = w.IsCurrent
        })));
        _redoStack.Clear();
    }
    
    private void UpdateTotalDistance()
    {
        double totalMeters = 0;
        for (int i = 1; i < _waypoints.Count; i++)
        {
            totalMeters += CalculateDistance(
                _waypoints[i - 1].Latitude, _waypoints[i - 1].Longitude,
                _waypoints[i].Latitude, _waypoints[i].Longitude);
        }
        tb_total_distance.Text = $"{totalMeters / 1000:F2} km";
    }
    
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        double phi1 = lat1 * Math.PI / 180.0;
        double phi2 = lat2 * Math.PI / 180.0;
        double deltaPhi = (lat2 - lat1) * Math.PI / 180.0;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180.0;

        double a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
