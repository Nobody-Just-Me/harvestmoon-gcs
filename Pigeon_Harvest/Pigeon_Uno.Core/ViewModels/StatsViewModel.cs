using Pigeon_Uno.Core.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Pigeon_Uno.Services;
using Pigeon_Uno.Models;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using System;

namespace Pigeon_Uno.ViewModels;

public partial class StatsViewModel : ViewModelBase
{
    private readonly IMavLinkService _mavLinkService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IStatisticsService _statisticsService;
    private readonly IRealTimeDataService? _realTimeDataService;

    // Statistics properties
    [ObservableProperty]
    private string _flightTimeFormatted = "00:00:00";

    [ObservableProperty]
    private string _totalDistanceFormatted = "0.0 m";

    [ObservableProperty]
    private string _maxAltitudeFormatted = "0.0 m";

    [ObservableProperty]
    private string _maxSpeedFormatted = "0.0 m/s";

    [ObservableProperty]
    private string _avgSpeedFormatted = "0.0 m/s";

    [ObservableProperty]
    private double _currentYaw = 0.0;

    [ObservableProperty]
    private double _currentPitch = 0.0;

    [ObservableProperty]
    private double _currentRoll = 0.0;

    [ObservableProperty]
    private int _totalFlights = 0;

    [ObservableProperty]
    private double _totalBatteryUsed = 0.0;

    private TimePeriod _currentTimePeriod = TimePeriod.AllTime;

    public StatsViewModel(
        IMavLinkService mavLinkService, 
        IDispatcherService dispatcherService, 
        IStatisticsService statisticsService,
        IRealTimeDataService? realTimeDataService = null)
    {
        System.Diagnostics.Debug.WriteLine("[StatsViewModel] ========== CONSTRUCTOR CALLED ==========");
        Serilog.Log.Information("[StatsViewModel] ========== CONSTRUCTOR CALLED ==========");
        
        _mavLinkService = mavLinkService;
        _dispatcherService = dispatcherService;
        _statisticsService = statisticsService;
        _realTimeDataService = realTimeDataService;

        System.Diagnostics.Debug.WriteLine($"[StatsViewModel] Services injected - MavLink: {mavLinkService != null}, Dispatcher: {dispatcherService != null}, Stats: {statisticsService != null}, RealTime: {realTimeDataService != null}");
        
        _mavLinkService.TelemetryReceived += OnTelemetryReceived;
        _statisticsService.StatisticsUpdated += OnStatisticsUpdated;
        
        System.Diagnostics.Debug.WriteLine("[StatsViewModel] Subscribed to MavLinkService.TelemetryReceived");

        // Subscribe to real-time data service if available
        if (_realTimeDataService != null)
        {
            _realTimeDataService.TelemetryReceived += OnRealTimeTelemetryReceived;
            _realTimeDataService.ConnectionStatusChanged += OnRealTimeConnectionStatusChanged;
            _realTimeDataService.ErrorOccurred += OnRealTimeErrorOccurred;
            System.Diagnostics.Debug.WriteLine("[StatsViewModel] Real-time data service injected and subscribed");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[StatsViewModel] Real-time data service is NULL");
        }

        // Load statistics
        _ = LoadStatisticsAsync();
        
        System.Diagnostics.Debug.WriteLine("[StatsViewModel] Constructor completed");
        Serilog.Log.Information("[StatsViewModel] Constructor completed");
    }

    /// <summary>
    /// Handle real-time telemetry data from WebSocket
    /// </summary>
    private void OnRealTimeTelemetryReceived(object? sender, TelemetryData telemetryData)
    {
        _dispatcherService.Enqueue(() =>
        {
            // Update current IMU values
            CurrentYaw = ((telemetryData.Yaw % 360) + 360) % 360;
            CurrentPitch = telemetryData.Pitch;
            CurrentRoll = telemetryData.Roll;
            
            System.Diagnostics.Debug.WriteLine($"[StatsViewModel] Real-time data: Yaw={CurrentYaw:F2}°, Pitch={CurrentPitch:F2}°, Roll={CurrentRoll:F2}°");
        });
    }

    private void OnRealTimeConnectionStatusChanged(object? sender, bool isConnected)
    {
        System.Diagnostics.Debug.WriteLine($"[StatsViewModel] Real-time connection status: {isConnected}");
    }

    private void OnRealTimeErrorOccurred(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"[StatsViewModel] Real-time error: {error}");
    }

    private async System.Threading.Tasks.Task LoadStatisticsAsync()
    {
        await _statisticsService.LoadStatisticsAsync();
        UpdateStatisticsDisplay();
    }

    private void OnTelemetryReceived(object sender, FlightData data)
    {
        _dispatcherService.Enqueue(() =>
        {
            CurrentYaw = data.IMU.Yaw;
            CurrentPitch = data.IMU.Pitch;
            CurrentRoll = data.IMU.Roll;
            
            System.Diagnostics.Debug.WriteLine($"[StatsViewModel] OnTelemetryReceived: Yaw={CurrentYaw:F2}°, Pitch={CurrentPitch:F2}°, Roll={CurrentRoll:F2}°");
        });

        // Update statistics
        _statisticsService.UpdateStatistics(data);
    }

    private void OnStatisticsUpdated(object sender, FlightStatistics stats)
    {
        _dispatcherService.Enqueue(() =>
        {
            UpdateStatisticsDisplay();
        });
    }

    public void SetTimePeriod(TimePeriod period)
    {
        _currentTimePeriod = period;
        UpdateStatisticsDisplay();
    }

    public void ResetStatistics()
    {
        _statisticsService.ResetStatistics();
        UpdateStatisticsDisplay();
    }

    private void UpdateStatisticsDisplay()
    {
        var stats = _statisticsService.GetStatistics(_currentTimePeriod);

        FlightTimeFormatted = FormatTimeSpan(stats.FlightTime);
        TotalDistanceFormatted = FormatDistance(stats.TotalDistance);
        MaxAltitudeFormatted = $"{stats.MaxAltitude:F1} m";
        MaxSpeedFormatted = $"{stats.MaxSpeed:F1} m/s";
        AvgSpeedFormatted = $"{stats.AverageSpeed:F1} m/s";
        TotalFlights = stats.TotalFlights;
        TotalBatteryUsed = stats.TotalBatteryUsed;
    }

    private string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        else
        {
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }

    private string FormatDistance(double meters)
    {
        if (meters >= 1000)
        {
            return $"{meters / 1000:F2} km";
        }
        else
        {
            return $"{meters:F1} m";
        }
    }
}
