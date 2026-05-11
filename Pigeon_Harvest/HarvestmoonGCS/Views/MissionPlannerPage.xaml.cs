using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using HarvestmoonGCS.Controls;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Views;

public sealed partial class MissionPlannerPage : Page
{
    private sealed class MissionWaypointItem
    {
        public int Sequence { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public string Coordinates => $"{Latitude:F6}, {Longitude:F6}";
        public string AltitudeText => $"ALT {Altitude:F0} m";
    }

    private readonly IMissionService _missionService;
    private readonly IMavLinkService _mavLinkService;
    private readonly List<MissionWaypointItem> _waypoints = new();
    private bool _initialized;
    private double _defaultLat = -6.9175;
    private double _defaultLon = 107.6191;

    public MissionPlannerPage()
    {
        this.InitializeComponent();

        _missionService = App.GetService<IMissionService>();
        _mavLinkService = App.GetService<IMavLinkService>();

        Loaded += MissionPlannerPage_Loaded;
        Unloaded += MissionPlannerPage_Unloaded;
    }

    public void OnPageActivated()
    {
        MissionMapControl?.InvalidateArrange();
    }

    private void MissionPlannerPage_Loaded(object sender, RoutedEventArgs e)
    {
        MissionMapControl.WaypointMoved -= MissionMapControl_WaypointMoved;
        MissionMapControl.WaypointMoved += MissionMapControl_WaypointMoved;

        if (!_initialized)
        {
            MapProviderComboBox.SelectedIndex = 0;
            MissionMapControl.SetCenter(_defaultLat, _defaultLon, 16);
            SeedInitialWaypoints();
            _initialized = true;
        }

        RenderMap();
    }

    private void MissionPlannerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        MissionMapControl.WaypointMoved -= MissionMapControl_WaypointMoved;
    }

    private void SeedInitialWaypoints()
    {
        if (_waypoints.Count > 0)
        {
            return;
        }

        _waypoints.Add(new MissionWaypointItem { Sequence = 1, Latitude = _defaultLat + 0.0012, Longitude = _defaultLon + 0.0009, Altitude = 150 });
        _waypoints.Add(new MissionWaypointItem { Sequence = 2, Latitude = _defaultLat + 0.0019, Longitude = _defaultLon + 0.0017, Altitude = 165 });
        _waypoints.Add(new MissionWaypointItem { Sequence = 3, Latitude = _defaultLat + 0.0010, Longitude = _defaultLon + 0.0024, Altitude = 160 });
    }

    private void MapProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MapProviderComboBox.SelectedIndex < 0)
        {
            return;
        }

        var provider = MapProviderComboBox.SelectedIndex switch
        {
            0 => SkiaMapControl.MapTileProvider.ArcGISTopographic,
            1 => SkiaMapControl.MapTileProvider.ArcGISImagery,
            2 => SkiaMapControl.MapTileProvider.ArcGISStreetMap,
            3 => SkiaMapControl.MapTileProvider.GoogleMap,
            4 => SkiaMapControl.MapTileProvider.GoogleSatellite,
            5 => SkiaMapControl.MapTileProvider.GoogleTerrain,
            6 => SkiaMapControl.MapTileProvider.GoogleHybrid,
            _ => SkiaMapControl.MapTileProvider.OpenStreetMap
        };

        MissionMapControl.SetTileProvider(provider);
        RenderMap();
    }

    private void MissionMapControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var tapPoint = e.GetPosition(MissionMapControl);
        var geo = MissionMapControl.GetLatLonFromClick(tapPoint);

        AddWaypoint(geo.Lat, geo.Lon, 150);
        RenderMap();
    }

    private void AddWaypointButton_Click(object sender, RoutedEventArgs e)
    {
        if (_waypoints.Count == 0)
        {
            AddWaypoint(_defaultLat, _defaultLon, 150);
            RenderMap();
            return;
        }

        var last = _waypoints[^1];
        AddWaypoint(last.Latitude + 0.0008, last.Longitude + 0.0008, last.Altitude);
        RenderMap();
    }

    private void ClearWaypointButton_Click(object sender, RoutedEventArgs e)
    {
        _waypoints.Clear();
        RenderMap();
    }

    private async void UploadMissionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_waypoints.Count == 0)
        {
            WaypointSummaryText.Text = "No waypoints to upload";
            return;
        }

        if (!_mavLinkService.IsConnected)
        {
            WaypointSummaryText.Text = "Vehicle not connected";
            return;
        }

        UploadMissionButton.IsEnabled = false;
        WaypointSummaryText.Text = "Uploading mission...";

        try
        {
            var missionWaypoints = _waypoints
                .OrderBy(w => w.Sequence)
                .Select((wp, index) => new MissionWaypoint
                {
                    Sequence = index,
                    Command = MavCommand.NavWaypoint,
                    Latitude = wp.Latitude,
                    Longitude = wp.Longitude,
                    Altitude = wp.Altitude,
                    Frame = MavFrame.GlobalRelativeAlt,
                    IsAutoContinue = true
                })
                .ToList();

            var success = await _missionService.UploadMissionAsync(missionWaypoints);
            WaypointSummaryText.Text = success
                ? $"Uploaded {missionWaypoints.Count} waypoints"
                : "Mission upload failed";
        }
        catch (Exception ex)
        {
            WaypointSummaryText.Text = $"Upload error: {ex.Message}";
        }
        finally
        {
            UploadMissionButton.IsEnabled = true;
        }
    }

    private void RemoveWaypointButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var sequence = button.Tag switch
        {
            int value => value,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => -1
        };

        if (sequence < 0)
        {
            return;
        }

        var target = _waypoints.FirstOrDefault(x => x.Sequence == sequence);
        if (target == null)
        {
            return;
        }

        _waypoints.Remove(target);
        ResequenceWaypoints();
        RenderMap();
    }

    private void GeofenceRadiusSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        GeofenceLabel.Text = $"{e.NewValue:F0} m";
        ApplyGeofence();
    }

    private void MissionMapControl_WaypointMoved(object? sender, SkiaMapControl.WaypointMovedEventArgs e)
    {
        var waypoint = _waypoints.FirstOrDefault(w => w.Sequence == e.Sequence);
        if (waypoint == null)
        {
            return;
        }

        waypoint.Latitude = e.NewLat;
        waypoint.Longitude = e.NewLon;
        RefreshWaypointList();
    }

    private void AddWaypoint(double lat, double lon, double altitude)
    {
        _waypoints.Add(new MissionWaypointItem
        {
            Sequence = _waypoints.Count + 1,
            Latitude = lat,
            Longitude = lon,
            Altitude = altitude
        });

        if (IsValidCoordinate(lat, lon))
        {
            _defaultLat = lat;
            _defaultLon = lon;
        }
    }

    private void ResequenceWaypoints()
    {
        for (int i = 0; i < _waypoints.Count; i++)
        {
            _waypoints[i].Sequence = i + 1;
        }
    }

    private void RenderMap()
    {
        MissionMapControl.ClearWaypoints();

        foreach (var wp in _waypoints.OrderBy(w => w.Sequence))
        {
            MissionMapControl.AddWaypointMarker(wp.Sequence, wp.Latitude, wp.Longitude, wp.Altitude, "WP");
        }

        if (_waypoints.Count > 0)
        {
            var first = _waypoints[0];
            MissionMapControl.SetCenter(first.Latitude, first.Longitude, 16);
        }

        ApplyGeofence();
        RefreshWaypointList();
    }

    private void ApplyGeofence()
    {
        if (_waypoints.Count == 0)
        {
            MissionMapControl.ClearGeofence();
            return;
        }

        var center = _waypoints[0];
        MissionMapControl.SetGeofence(true, center.Latitude, center.Longitude, GeofenceRadiusSlider.Value);
    }

    private void RefreshWaypointList()
    {
        WaypointListView.ItemsSource = null;
        WaypointListView.ItemsSource = _waypoints.OrderBy(w => w.Sequence).ToList();
        WaypointSummaryText.Text = $"{_waypoints.Count} waypoints";
    }

    private static bool IsValidCoordinate(double lat, double lon)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon))
        {
            return false;
        }

        if (lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            return false;
        }

        return Math.Abs(lat) > 0.000001 || Math.Abs(lon) > 0.000001;
    }
}
