using HarvestmoonGCS.Core.ViewModels;
using System.IO.Ports;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Helpers;

namespace HarvestmoonGCS.ViewModels;

public partial class TrackerViewModel : ViewModelBase
{
    private readonly IMavLinkService _mavLinkService;
    private readonly IDispatcherService _dispatcherService;

    [ObservableProperty] private double _trackerLat;
    [ObservableProperty] private double _trackerLon;
    [ObservableProperty] private double _trackerAlt;

    [ObservableProperty] private double _wahanaLat;
    [ObservableProperty] private double _wahanaLon;
    [ObservableProperty] private double _wahanaAlt;

    [ObservableProperty] private double _bearing;
    [ObservableProperty] private double _pitch;
    [ObservableProperty] private double _distance;

    [ObservableProperty] private bool _isTracking;
    [ObservableProperty] private double _latency;
    [ObservableProperty] private string _videoStatus = "N/A";
    [ObservableProperty] private string _batteryStatus = "N/A";

    public string TrackingIcon => IsTracking ? "⏸" : "▶";
    public string TrackingButtonText => IsTracking ? "Stop Tracking" : "Start Tracking";

    // Tracker-specific data (from TrackerData model when available)
    private DateTime _lastTelemetryReceived = DateTime.MinValue;
    private DateTime _lastTrackerDataReceived = DateTime.MinValue;

    [ObservableProperty] private bool _isTrackerConnected;
    [ObservableProperty] private string _trackerPortName = "/dev/ttyUSB0"; // Default for Linux
    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();

    [ObservableProperty] private ObservableCollection<WaypointData> _trackPoints = new();
    [ObservableProperty] private WaypointData? _selectedTrackPoint;

    private SerialPort? _serialPort;
    private System.Threading.Timer? _trackerDataTimer; // Timer untuk simulasi data tracker
    private bool _isTrackerDataSimulationEnabled = true; // Flag untuk enable/disable simulasi

    public TrackerViewModel(IMavLinkService mavLinkService, IDispatcherService dispatcherService)
    {
        _mavLinkService = mavLinkService;
        _dispatcherService = dispatcherService;

        _mavLinkService.TelemetryReceived += OnTelemetryReceived;
        
        // Start tracker data simulation timer untuk testing/debugging
        // Dalam production, ini akan diganti dengan actual tracker data receiver
        if (_isTrackerDataSimulationEnabled)
        {
            _trackerDataTimer = new System.Threading.Timer(
                OnTrackerDataTimerCallback, 
                null, 
                TimeSpan.FromSeconds(2), // Delay 2 detik pertama
                TimeSpan.FromSeconds(5)  // Update setiap 5 detik
            );
        }
        
        RefreshPorts();
    }

    private void OnTrackerDataTimerCallback(object? state)
    {
        // Simulasi tracker data untuk testing/debugging
        // Tracker position di-offset dari vehicle position dengan jarak tetap
        if (WahanaLat != 0 && WahanaLon != 0)
        {
            _dispatcherService.Enqueue(() =>
            {
                // Offset tracker position 0.001 derajat dari vehicle (sekitar 100m)
                TrackerLat = WahanaLat + 0.001;
                TrackerLon = WahanaLon + 0.001;
                TrackerAlt = WahanaAlt;
                
                // Simulasi battery voltage
                BatteryStatus = "12.5V";
                
                // Update status connected
                IsTrackerConnected = true;
                VideoStatus = "Connected";
                
                // Update latency
                UpdateLatency();
            });
        }
    }

    [RelayCommand]
    private void AddTrackPoint()
    {
        var newPoint = new WaypointData
        {
            Sequence = TrackPoints.Count + 1,
            Latitude = WahanaLat != 0 ? WahanaLat : -6.2,
            Longitude = WahanaLon != 0 ? WahanaLon : 106.8,
            Altitude = 100,
            Command = WaypointCommand.Waypoint
        };
        TrackPoints.Add(newPoint);
    }

    [RelayCommand]
    private void RemoveTrackPoint(WaypointData point)
    {
        if (point != null && TrackPoints.Contains(point))
        {
            TrackPoints.Remove(point);
            for (int i = 0; i < TrackPoints.Count; i++)
            {
                TrackPoints[i].Sequence = i + 1;
            }
        }
    }

    [RelayCommand]
    private void ClearTrackPoints()
    {
        TrackPoints.Clear();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPortHelper.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }
        if (AvailablePorts.Count > 0 && string.IsNullOrEmpty(TrackerPortName))
            TrackerPortName = AvailablePorts[0];
    }

    [RelayCommand]
    private void ConnectTracker()
    {
        try
        {
            if (_serialPort == null)
            {
                _serialPort = new SerialPort(TrackerPortName, 57600); // Standard baudrate
                _serialPort.DataReceived += OnTrackerSerialDataReceived;
                _serialPort.Open();
                IsTrackerConnected = true;
                VideoStatus = "Connected";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tracker Connect Error: {ex.Message}");
            IsTrackerConnected = false;
            VideoStatus = "N/A";
        }
    }

    [RelayCommand]
    private void DisconnectTracker()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.DataReceived -= OnTrackerSerialDataReceived;
            _serialPort.Close();
        }
        _serialPort = null;
        IsTrackerConnected = false;
        VideoStatus = "N/A";
    }

    /// <summary>
    /// Handle serial data received from tracker
    /// Tracker may send status information including battery, GPS, orientation
    /// Format depends on tracker firmware (e.g., "#,pitch,yaw" for commands, or status strings)
    /// </summary>
    private void OnTrackerSerialDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            var data = _serialPort.ReadExisting();
            
            // Parse tracker status data
            // This is a placeholder for actual tracker protocol parsing
            // Different tracker firmwares may send different formats
            // Example formats:
            // - "BAT:12.5V" for battery voltage
            // - "GPS:lat,lon,alt" for GPS data
            // - "ORI:yaw,pitch,roll" for orientation
            
            _dispatcherService.Enqueue(() =>
            {
                // For now, just update latency when we receive data
                UpdateLatency();
                
                // TODO: Parse actual tracker status messages when protocol is defined
                // Example parsing (commented out):
                // if (data.StartsWith("BAT:"))
                // {
                //     var voltage = ParseBatteryVoltage(data);
                //     BatteryStatus = $"{voltage:F1}V";
                // }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tracker Serial Data Error: {ex.Message}");
        }
    }

    private void OnTelemetryReceived(object sender, FlightData data)
    {
        _dispatcherService.Enqueue(() =>
        {
            WahanaLat = data.GPS.Latitude / 1e7; // Convert from int to decimal degrees
            WahanaLon = data.GPS.Longitude / 1e7; // Convert from int to decimal degrees
            WahanaAlt = data.Altitude / 1000.0; // Convert from mm to meters

            // Update latency based on telemetry reception
            UpdateLatency();

            CalculateTracking();
        });
    }

    /// <summary>
    /// Update latency calculation based on telemetry reception timing
    /// </summary>
    private void UpdateLatency()
    {
        var now = DateTime.Now;
        if (_lastTelemetryReceived != DateTime.MinValue)
        {
            var timeSinceLastUpdate = (now - _lastTelemetryReceived).TotalMilliseconds;
            // Latency is the time between telemetry updates
            // Smooth the value with a simple moving average
            Latency = (Latency * 0.7) + (timeSinceLastUpdate * 0.3);
        }
        _lastTelemetryReceived = now;
    }

    /// <summary>
    /// Update tracker-specific data (GPS, battery, orientation)
    /// This method will be called when tracker telemetry is received via MAVLink
    /// Currently, tracker data is set manually or from serial port
    /// </summary>
    /// <param name="trackerData">Tracker telemetry data</param>
    public void UpdateTrackerData(TrackerData trackerData)
    {
        _dispatcherService.Enqueue(() =>
        {
            // Update tracker GPS position
            if (trackerData.GPSTrack.IsValid)
            {
                TrackerLat = trackerData.GPSTrack.Latitude;
                TrackerLon = trackerData.GPSTrack.Longitude;
                TrackerAlt = trackerData.Altitude;
            }

            // Update battery status
            if (trackerData.Battery > 0)
            {
                BatteryStatus = $"{trackerData.Battery:F1}V";
            }

            // Update orientation (if available)
            // Note: Tracker orientation is typically shown as Yaw in the TrackerData model
            if (trackerData.IMU != null)
            {
                // Orientation data available but not currently displayed in UI
                // Could be added to UI in future if needed
            }

            // Update video status based on tracker connection
            // In the WPF version, this is not actually implemented
            // For now, we show connection status
            VideoStatus = IsTrackerConnected ? "Connected" : "N/A";

            _lastTrackerDataReceived = DateTime.Now;
        });
    }

    private void CalculateTracking()
    {
        Distance = GeoMath.Distance(TrackerLat, TrackerLon, WahanaLat, WahanaLon);
        Bearing = GeoMath.Bearing(TrackerLat, TrackerLon, WahanaLat, WahanaLon);
        Pitch = GeoMath.Pitch(Distance, TrackerAlt, WahanaAlt);

        if (IsTracking)
        {
            string cmd = $"#,{Pitch:F0},{Bearing:F0}\r\n";
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(cmd);

            if (IsTrackerConnected && _serialPort != null && _serialPort.IsOpen)
            {
                try { _serialPort.Write(bytes, 0, bytes.Length); } catch { }
            }
            else
            {
                // Fallback or specific mode
                try
                {
                    _mavLinkService.SendRawBytes(bytes);
                }
                catch
                {
                }
            }
        }
    }

    [RelayCommand]
    private void ToggleTracking()
    {
        IsTracking = !IsTracking;
        OnPropertyChanged(nameof(TrackingIcon));
        OnPropertyChanged(nameof(TrackingButtonText));
    }

    [RelayCommand]
    private void SetHome()
    {
        TrackerLat = WahanaLat;
        TrackerLon = WahanaLon;
        TrackerAlt = WahanaAlt;
    }
}
