using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pigeon_Uno.ViewModels;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Models;
using Pigeon_Uno.Services;

namespace Pigeon_Uno.Controls;

public sealed partial class TrackerControl : UserControl
{
    public TrackerViewModel? ViewModel => DataContext as TrackerViewModel;
    private IMavLinkService? _mavLinkService;
    private IRealTimeDataService? _realTimeDataService;
    private readonly ObservabilityService? _observabilityService;
    private DateTime _lastVehicleMapUpdateUtc = DateTime.MinValue;
    private const int VehicleMapUpdateIntervalMs = 200;
    private (double Lat, double Lon)? _lastRenderedVehiclePosition;

    public TrackerControl()
    {
        this.InitializeComponent();
        _observabilityService = App.Current.Services.GetService<ObservabilityService>();
        
        // DataContext akan di-set oleh XAML binding ke TrackerViewModel
        // Subscribe ke property changes setelah load
        this.Loaded += TrackerControl_Loaded;
        this.Unloaded += TrackerControl_Unloaded;
    }

    private void TrackerControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            // Fallback: get from DI if not set by binding
            DataContext = App.Current.Services.GetService<TrackerViewModel>();
        }
        
        if (ViewModel != null)
        {
            // Subscribe ke property changes
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.TrackPoints.CollectionChanged += TrackPoints_CollectionChanged;
            
            // Initialize tracker position dari ViewModel
            UpdateTrackerMarkerPosition();
        }

        _mavLinkService = App.Current.Services.GetService<IMavLinkService>();
        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived += OnMavLinkTelemetryReceived;
        }

        _realTimeDataService = App.Current.Services.GetService<IRealTimeDataService>();
        if (_realTimeDataService != null)
        {
            _realTimeDataService.TelemetryReceived += OnRealTimeTelemetryReceived;
        }
        
        // Initialize map centered on tracker position
        var vmTracker = ViewModel;
        var trackerLatCandidate = vmTracker?.TrackerLat ?? 0;
        var trackerLonCandidate = vmTracker?.TrackerLon ?? 0;
        var hasTrackerPosition = trackerLatCandidate != 0 && trackerLonCandidate != 0;
        double trackerLat = hasTrackerPosition ? trackerLatCandidate : -7.2754;
        double trackerLon = hasTrackerPosition ? trackerLonCandidate : 112.7947;
        SetCenter(trackerLat, trackerLon, 14);
        
        // Show tracker marker on map
        if (mapControl != null)
        {
            mapControl.SetShowTracker(true);
            mapControl.UpdateTrackerPosition(trackerLat, trackerLon);
        }
        
        // Initial vehicle position jika sudah ada data
        if (ViewModel is { } vm && vm.WahanaLat != 0 && vm.WahanaLon != 0)
        {
            UpdateVehiclePosition(vm.WahanaLat, vm.WahanaLon);
        }
    }

    private void TrackerControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup subscriptions
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.TrackPoints.CollectionChanged -= TrackPoints_CollectionChanged;
        }

        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived -= OnMavLinkTelemetryReceived;
            _mavLinkService = null;
        }

        if (_realTimeDataService != null)
        {
            _realTimeDataService.TelemetryReceived -= OnRealTimeTelemetryReceived;
            _realTimeDataService = null;
        }
    }

    public void SetCenter(double lat, double lon, double zoom)
    {
        if (mapControl != null)
        {
            mapControl.SetCenter(lat, lon, (int)zoom);
        }
    }

    /// <summary>
    /// Dipanggil saat halaman TrackerPage diaktifkan kembali dari cache
    /// </summary>
    public void OnControlActivated()
    {
        Serilog.Log.Information("[TrackerControl] OnControlActivated called");
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                var vmTracker = ViewModel;
                var trackerLatCandidate = vmTracker?.TrackerLat ?? 0;
                var trackerLonCandidate = vmTracker?.TrackerLon ?? 0;
                var hasTrackerPosition = trackerLatCandidate != 0 && trackerLonCandidate != 0;
                double trackerLat = hasTrackerPosition ? trackerLatCandidate : -7.2754;
                double trackerLon = hasTrackerPosition ? trackerLonCandidate : 112.7947;
                SetCenter(trackerLat, trackerLon, 14);
                if (mapControl != null)
                {
                    mapControl.SetShowTracker(true);
                    mapControl.UpdateTrackerPosition(trackerLat, trackerLon);
                }

                if (ViewModel is { } vm && vm.WahanaLat != 0 && vm.WahanaLon != 0)
                {
                    UpdateVehiclePosition(vm.WahanaLat, vm.WahanaLon);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[TrackerControl] OnControlActivated failed");
            }
        });
    }

    public void UpdateVehiclePosition(double lat, double lon)
    {
        if (mapControl != null)
        {
            mapControl.UpdateVehiclePosition(lat, lon);
        }
    }

    private void OnMavLinkTelemetryReceived(object? sender, FlightData data)
    {
        var lat = data.GPS.Latitude / 1e7;
        var lon = data.GPS.Longitude / 1e7;
        TryUpdateVehicleOnMap(lat, lon);
    }

    private void OnRealTimeTelemetryReceived(object? sender, Core.Models.TelemetryData telemetryData)
    {
        TryUpdateVehicleOnMap(telemetryData.Latitude, telemetryData.Longitude);
    }

    private void TryUpdateVehicleOnMap(double lat, double lon)
    {
        if (Math.Abs(lat) < 1e-6 && Math.Abs(lon) < 1e-6)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastVehicleMapUpdateUtc).TotalMilliseconds < VehicleMapUpdateIntervalMs)
        {
            return;
        }

        if (_lastRenderedVehiclePosition.HasValue)
        {
            var previous = _lastRenderedVehiclePosition.Value;
            if (Math.Abs(previous.Lat - lat) < 1e-6 && Math.Abs(previous.Lon - lon) < 1e-6)
            {
                return;
            }
        }

        _lastVehicleMapUpdateUtc = now;
        _lastRenderedVehiclePosition = (lat, lon);
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateVehiclePosition(lat, lon);
            _observabilityService?.Track("tracker.map.render");
        });
    }

    public void UpdateTrack(System.Collections.Generic.IEnumerable<Pigeon_Uno.Core.Models.WaypointData> points)
    {
        if (mapControl == null) return;
        mapControl.ClearWaypoints();
        foreach (var point in points)
        {
            mapControl.AddWaypointMarker(point.Sequence, point.Latitude, point.Longitude, point.Altitude, point.Command.ToString());
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;
        
        // Update tracker position on map
        if (e.PropertyName == nameof(ViewModel.TrackerLat) || 
            e.PropertyName == nameof(ViewModel.TrackerLon))
        {
            UpdateTrackerMarkerPosition();
        }
    }

    private void UpdateTrackerMarkerPosition()
    {
        if (ViewModel == null || mapControl == null) return;
        
        if (ViewModel.TrackerLat != 0 && ViewModel.TrackerLon != 0)
        {
            mapControl.UpdateTrackerPosition(ViewModel.TrackerLat, ViewModel.TrackerLon);
        }
    }

    private void TrackPoints_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel == null) return;
        UpdateTrack(ViewModel.TrackPoints);
    }
}
