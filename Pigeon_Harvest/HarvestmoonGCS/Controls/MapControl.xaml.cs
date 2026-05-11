using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using HarvestmoonGCS.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using HarvestmoonGCS.Helpers;

namespace HarvestmoonGCS.Controls;

public sealed partial class MapControl : UserControl
{
    public MapViewModel ViewModel => (MapViewModel)DataContext;
    private bool _isFollowingVehicle = false;
    private List<(double Lat, double Lon)> _undoStack = new();
    private List<(double Lat, double Lon)> _redoStack = new();
    private bool _isWPDockHidden = true;
    private int _lastMapProviderIndex = -1;
    private NotifyCollectionChangedEventHandler? _waypointsCollectionChangedHandler;

    private Border? _geofencePointsBorder;
    private Button? _btnCompleteGeofence;
    private TextBlock? _geofenceStatusText;
    private Border? _geofenceStatusBorder;
    private ToggleButton? _btnGeofenceMode;
    private Button? _btnSendGeofence;
    private Button? _btnClearGeofence;
    private TextBlock? _tbTotalDistance;
    private ItemsControl? _geofencePointsList;
    private TextBox? _tbGeofenceRadius;
    private TextBox? _tbGeofenceAltitude;
    private ComboBox? _cbGeofenceType;
    private StackPanel? _wpDockStack;
    private StackPanel? _wpDock;
    private Button? _wpDockButton;
    private Border? _followVehicleBorder;
    private Border? _geofencePanel;
    private TextBox? _tbGeofenceLat;
    private TextBox? _tbGeofenceLon;

    private Border geofencePointsBorder => _geofencePointsBorder ??= (Border)FindName("geofence_points_border");
    private Button btnCompleteGeofence => _btnCompleteGeofence ??= (Button)FindName("btn_complete_geofence");
    private TextBlock geofenceStatusText => _geofenceStatusText ??= (TextBlock)FindName("geofence_status_text");
    private Border geofenceStatusBorder => _geofenceStatusBorder ??= (Border)FindName("geofence_status_border");
    private ToggleButton btnGeofenceMode => _btnGeofenceMode ??= (ToggleButton)FindName("btn_geofence_mode");
    private Button btnSendGeofence => _btnSendGeofence ??= (Button)FindName("btn_send_geofence");
    private Button btnClearGeofence => _btnClearGeofence ??= (Button)FindName("btn_clear_geofence");
    private TextBlock tbTotalDistance => _tbTotalDistance ??= (TextBlock)FindName("tb_total_distance");
    private ItemsControl geofencePointsList => _geofencePointsList ??= (ItemsControl)FindName("geofence_points_list");
    private TextBox tbGeofenceRadius => _tbGeofenceRadius ??= (TextBox)FindName("tb_geofence_radius");
    private TextBox tbGeofenceAltitude => _tbGeofenceAltitude ??= (TextBox)FindName("tb_geofence_altitude");
    private ComboBox cbGeofenceType => _cbGeofenceType ??= (ComboBox)FindName("cb_geofence_type");
    private StackPanel wpDockStack => _wpDockStack ??= (StackPanel)FindName("wp_dock_stack");
    private StackPanel wpDock => _wpDock ??= (StackPanel)FindName("wp_dock");
    private Button wpDockButton => _wpDockButton ??= (Button)FindName("wp_dock_btn");
    private Border followVehicleBorder => _followVehicleBorder ??= (Border)FindName("follow_wahana_border");
    private Border geofencePanel => _geofencePanel ??= (Border)FindName("geofence_panel");
    private TextBox tbGeofenceLat => _tbGeofenceLat ??= (TextBox)FindName("tb_geofence_lat");
    private TextBox tbGeofenceLon => _tbGeofenceLon ??= (TextBox)FindName("tb_geofence_lon");

    public MapControl()
    {
        Serilog.Log.Information("[MapControl] Constructor started");
        
        this.InitializeComponent();
        var viewModel = App.Current.Services.GetService<MapViewModel>();
        DataContext = viewModel;
        
        Serilog.Log.Information("[MapPage] ViewModel initialized, calling mapControl.SetCenter");
        
        // Subscribe to ViewModel property changes
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _waypointsCollectionChangedHandler ??= (_, __) => RebuildWaypointDock();
            ViewModel.Waypoints.CollectionChanged += _waypointsCollectionChangedHandler;
        }

        this.Unloaded += MapControl_Unloaded;
        
        // Initialize map with default center (Surabaya)
        mapControl.SetCenter(-7.2754, 112.7947, 15);
        
        Serilog.Log.Information("[MapPage] Map center set, initializing waypoint dock");
        
        // Initial waypoint dock state
        wpDock.Height = 25;
        wpDockButton.Content = "Markers ▲";
        RebuildWaypointDock();
        
        Serilog.Log.Information("[MapPage] Constructor completed successfully");
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.VehiclePosition))
        {
            // Update vehicle position on map
            if (ViewModel.VehiclePosition != null)
            {
                mapControl.UpdateVehiclePosition(
                    ViewModel.VehiclePosition.Latitude,
                    ViewModel.VehiclePosition.Longitude);
                
                // Auto-follow if enabled
                if (_isFollowingVehicle)
                {
                    mapControl.SetCenter(
                        ViewModel.VehiclePosition.Latitude,
                        ViewModel.VehiclePosition.Longitude);
                }
            }
        }
        else if (e.PropertyName == nameof(ViewModel.TotalDistance))
        {
            // Sinkronkan label total jarak dengan ViewModel
            tbTotalDistance.Text = $"{ViewModel.TotalDistance:F2} km";
        }
    }

    private void MapControl_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // TODO: Implement when map control is available
        // For now, simulate adding waypoint at center of map area
        var simulatedLat = -7.2754; // Surabaya coordinates as example
        var simulatedLon = 112.7947;
        
        // Check if in geofence drawing mode
        if (ViewModel.IsDrawingGeofence && ViewModel.GeofenceType == HarvestmoonGCS.Core.Models.GeofenceType.Polygon)
        {
            // Add geofence vertex
            if (ViewModel.AddGeofenceVertexCommand.CanExecute((simulatedLat, simulatedLon)))
            {
                ViewModel.AddGeofenceVertexCommand.Execute((simulatedLat, simulatedLon));
                UpdateGeofenceVerticesList();
            }
        }
        else
        {
            // Add Waypoint on double-click
            if (ViewModel.AddWaypointCommand.CanExecute((simulatedLat, simulatedLon)))
            {
                ViewModel.AddWaypointCommand.Execute((simulatedLat, simulatedLon));
                _undoStack.Add((simulatedLat, simulatedLon));
                _redoStack.Clear();
                UpdateTotalDistance();
            }
        }
    }

    private void ChooseMap(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && mapControl != null)
        {
            var index = comboBox.SelectedIndex;
            if (index == _lastMapProviderIndex)
            {
                return;
            }

            _lastMapProviderIndex = index;
            UpdateMapLayer(index);
        }
    }

    private void UpdateMapLayer(int selectedIndex)
    {
        // Map ComboBox index to MapTileProvider enum
        var provider = selectedIndex switch
        {
            0 => Controls.SkiaMapControl.MapTileProvider.ArcGISTopographic,
            1 => Controls.SkiaMapControl.MapTileProvider.ArcGISImagery,
            2 => Controls.SkiaMapControl.MapTileProvider.ArcGISStreetMap,
            3 => Controls.SkiaMapControl.MapTileProvider.GoogleMap,
            4 => Controls.SkiaMapControl.MapTileProvider.GoogleSatellite,
            5 => Controls.SkiaMapControl.MapTileProvider.GoogleTerrain,
            6 => Controls.SkiaMapControl.MapTileProvider.GoogleHybrid,
            _ => Controls.SkiaMapControl.MapTileProvider.OpenStreetMap
        };
        
        mapControl.SetTileProvider(provider);
        
        Serilog.Log.Information("[MapPage] Map layer changed to: {Provider}", provider);
    }

    private void FollowVehicle_Click(object sender, RoutedEventArgs e)
    {
        _isFollowingVehicle = !_isFollowingVehicle;
        
        if (_isFollowingVehicle)
        {
            followVehicleBorder.Background = new SolidColorBrush(
                Color.FromArgb(255, 0, 255, 0)); // Green
            
            // Center on vehicle if position is available
            if (ViewModel.VehiclePosition != null)
            {
                CenterMapOnPosition(ViewModel.VehiclePosition.Latitude, ViewModel.VehiclePosition.Longitude);
            }
        }
        else
        {
            followVehicleBorder.Background = new SolidColorBrush(
                Color.FromArgb(0xFF, 0x53, 0xDF, 0xFA)); // Original blue
        }
    }

    private void CenterMapOnPosition(double latitude, double longitude)
    {
        mapControl.SetCenter(latitude, longitude);
    }

    private void SendWaypointCommand(object sender, RoutedEventArgs e)
    {
        if (ViewModel.UploadMissionCommand.CanExecute(null))
        {
            ViewModel.UploadMissionCommand.Execute(null);
        }
    }

    private void ToggleGeofencePanel_Click(object sender, RoutedEventArgs e)
    {
        geofencePanel.Visibility = geofencePanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SetGeofenceCenter_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(tbGeofenceLat.Text, out double lat) &&
            double.TryParse(tbGeofenceLon.Text, out double lon))
        {
            CenterMapOnPosition(lat, lon);
            
            if (ViewModel.SetGeofenceCenterCommand.CanExecute((lat, lon)))
            {
                ViewModel.SetGeofenceCenterCommand.Execute((lat, lon));
            }
        }
    }

    private void ToggleGeofenceMode_Click(object sender, RoutedEventArgs e)
    {
        if (btnGeofenceMode.IsChecked == true)
        {
            // Start drawing mode
            ViewModel.StartDrawingGeofence();
            btnCompleteGeofence.Visibility = Visibility.Visible;
            geofenceStatusText.Text = "Drawing";
            geofenceStatusBorder.Background = new SolidColorBrush(
                Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00)); // Orange

            // Show polygon points list if polygon type
            if (cbGeofenceType.SelectedIndex == 1) // Polygon
            {
                geofencePointsBorder.Visibility = Visibility.Visible;
            }
        }
        else
        {
            // Stop drawing mode
            ViewModel.IsDrawingGeofence = false;
            btnCompleteGeofence.Visibility = Visibility.Collapsed;
            geofenceStatusText.Text = ViewModel.IsGeofenceActive ? "Active" : "Inactive";
            geofenceStatusBorder.Background = new SolidColorBrush(
                ViewModel.IsGeofenceActive
                    ? Color.FromArgb(0xFF, 0x28, 0xA7, 0x45) // Green
                    : Color.FromArgb(0xFF, 0x6C, 0x75, 0x7D)); // Gray
        }
    }

    private void CompleteGeofence_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CompleteGeofenceCommand.CanExecute(null))
        {
            ViewModel.CompleteGeofenceCommand.Execute(null);
        }
        
        btnCompleteGeofence.Visibility = Visibility.Collapsed;
        // btnGeofenceMode.IsChecked = false;
        btnSendGeofence.IsEnabled = true;
        btnClearGeofence.IsEnabled = true;
        geofenceStatusText.Text = "Active";
        geofenceStatusBorder.Background = new SolidColorBrush(
            Color.FromArgb(0xFF, 0x28, 0xA7, 0x45)); // Green
    }

    private void SendGeofence_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SendGeofenceCommand.CanExecute(null))
        {
            ViewModel.SendGeofenceCommand.Execute(null);
        }
    }

    private void ClearGeofence_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClearGeofenceCommand.CanExecute(null))
        {
            ViewModel.ClearGeofenceCommand.Execute(null);
        }
        
        btnSendGeofence.IsEnabled = false;
        btnClearGeofence.IsEnabled = false;
        // btnGeofenceMode.IsChecked = false;
        geofencePointsBorder.Visibility = Visibility.Collapsed;
        geofenceStatusText.Text = "Inactive";
        geofenceStatusBorder.Background = new SolidColorBrush(
            Color.FromArgb(0xFF, 0x6C, 0x75, 0x7D)); // Gray
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0 && ViewModel.Waypoints.Count > 0)
        {
            var lastWaypoint = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(lastWaypoint);
            
            var waypointToRemove = ViewModel.Waypoints[ViewModel.Waypoints.Count - 1];
            ViewModel.DeleteWaypointCommand.Execute(waypointToRemove);
            UpdateTotalDistance();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count > 0)
        {
            var waypoint = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(waypoint);
            
            ViewModel.AddWaypointCommand.Execute((waypoint.Lat, waypoint.Lon));
            UpdateTotalDistance();
        }
    }

    private void ResetMarkers_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClearMissionCommand.CanExecute(null))
        {
            ViewModel.ClearMissionCommand.Execute(null);
            _undoStack.Clear();
            _redoStack.Clear();
            UpdateTotalDistance();
        }
    }

    private void ExportWaypoints_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SaveMissionCommand.CanExecute(null))
        {
            ViewModel.SaveMissionCommand.Execute(null);
        }
    }

    private void ImportWaypoints_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LoadMissionCommand.CanExecute(null))
        {
            ViewModel.LoadMissionCommand.Execute(null);
            UpdateTotalDistance();
        }
    }

    private void UpdateTotalDistance()
    {
        if (ViewModel.Waypoints.Count < 2)
        {
            tbTotalDistance.Text = "0.00 km";
            return;
        }

        double totalDistance = 0;
        for (int i = 0; i < ViewModel.Waypoints.Count - 1; i++)
        {
            var wp1 = ViewModel.Waypoints[i];
            var wp2 = ViewModel.Waypoints[i + 1];
            totalDistance += GeoMath.CalculateDistance(
                wp1.Latitude, wp1.Longitude,
                wp2.Latitude, wp2.Longitude);
        }

        tbTotalDistance.Text = $"{(totalDistance / 1000):F2} km";
    }

    private void UpdateGeofenceVerticesList()
    {
        geofencePointsList.ItemsSource = ViewModel.GeofenceVertices;
    }

    private void UpdateGeofenceParameters()
    {
        // Update geofence parameters from UI controls
        if (double.TryParse(tbGeofenceRadius.Text, out double radius))
        {
            ViewModel.SetGeofenceRadius(radius);
        }

        if (double.TryParse(tbGeofenceAltitude.Text, out double altitude))
        {
            ViewModel.SetGeofenceMaxAltitude(altitude);
        }

        // Update geofence type
        var selectedType = cbGeofenceType.SelectedIndex == 0 
            ? HarvestmoonGCS.Core.Models.GeofenceType.Circular 
            : HarvestmoonGCS.Core.Models.GeofenceType.Polygon;
        
        if (selectedType != ViewModel.GeofenceType)
        {
            ViewModel.SetGeofenceType(selectedType);
            
            // Show/hide polygon points list
            geofencePointsBorder.Visibility = selectedType == HarvestmoonGCS.Core.Models.GeofenceType.Polygon
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void GeofenceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;
        
        var selectedType = cbGeofenceType.SelectedIndex == 0 
            ? HarvestmoonGCS.Core.Models.GeofenceType.Circular 
            : HarvestmoonGCS.Core.Models.GeofenceType.Polygon;
        
        ViewModel.SetGeofenceType(selectedType);
        
        // Show/hide polygon points list
        geofencePointsBorder.Visibility = selectedType == HarvestmoonGCS.Core.Models.GeofenceType.Polygon
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void GeofenceRadius_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null) return;
        
        if (double.TryParse(tbGeofenceRadius.Text, out double radius))
        {
            ViewModel.SetGeofenceRadius(radius);
        }
    }

    private void GeofenceAltitude_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel == null) return;
        
        if (double.TryParse(tbGeofenceAltitude.Text, out double altitude))
        {
            ViewModel.SetGeofenceMaxAltitude(altitude);
        }
    }

    private void ToggleWPDock(object sender, RoutedEventArgs e)
    {
        // Toggle waypoint dock height & label (mirip WPF)
        var nextHeight = _isWPDockHidden ? 300 : 25;
        wpDockButton.Content = _isWPDockHidden ? "Markers ▼" : "Markers ▲";
        _isWPDockHidden = !_isWPDockHidden;
        wpDock.Height = nextHeight;
    }

    private void RebuildWaypointDock()
    {
        if (ViewModel == null || wpDockStack == null) return;

        wpDockStack.Children.Clear();
        foreach (var wp in ViewModel.Waypoints.OrderBy(w => w.Sequence))
        {
            var item = new WaypointItem(wp);
            wpDockStack.Children.Add(item);
        }

        UpdateTotalDistance();
    }

    private void MapControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            if (_waypointsCollectionChangedHandler != null)
            {
                ViewModel.Waypoints.CollectionChanged -= _waypointsCollectionChangedHandler;
            }
        }

        this.Unloaded -= MapControl_Unloaded;
    }
}
