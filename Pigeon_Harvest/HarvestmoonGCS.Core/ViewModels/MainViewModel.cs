using HarvestmoonGCS.Core.ViewModels;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Services;
using System;

namespace HarvestmoonGCS.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IMavLinkService? _mavLinkService;
    private readonly IDispatcherService? _dispatcherService;
    private System.Timers.Timer? _flightTimer;
    private TimeSpan _flightTimeElapsed;
    private bool _isWahanaConnected;
    private bool _isTrackerConnected;
    private string _connectionStatus = "Disconnected";
    private int _signalStrength;
    private double _batteryVoltage;
    private double _batteryCurrent;
    private string _flightTime = "00:00:00";
    private string _currentLanguage = "en-US";

    public bool IsWahanaConnected
    {
        get => _isWahanaConnected;
        set => SetProperty(ref _isWahanaConnected, value);
    }

    public bool IsTrackerConnected
    {
        get => _isTrackerConnected;
        set => SetProperty(ref _isTrackerConnected, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public int SignalStrength
    {
        get => _signalStrength;
        set => SetProperty(ref _signalStrength, value);
    }

    public double BatteryVoltage
    {
        get => _batteryVoltage;
        set => SetProperty(ref _batteryVoltage, value);
    }

    public double BatteryCurrent
    {
        get => _batteryCurrent;
        set => SetProperty(ref _batteryCurrent, value);
    }

    public string FlightTime
    {
        get => _flightTime;
        set => SetProperty(ref _flightTime, value);
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetProperty(ref _currentLanguage, value);
    }

    public string SignalDisplay => _signalStrength > 0 ? $"{_signalStrength}%" : "N/A";
    public string BatteryDisplay => _batteryVoltage > 0 ? $"{_batteryVoltage:F1}V" : "N/A";

    public ICommand ChangeLanguageCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    public MainViewModel(IMavLinkService? mavLinkService = null, IDispatcherService? dispatcherService = null)
    {
        _mavLinkService = mavLinkService;
        _dispatcherService = dispatcherService;
        ChangeLanguageCommand = new RelayCommand<string>(ChangeLanguage);
        ExitApplicationCommand = new RelayCommand(ExitApplication);

        if (_mavLinkService != null)
        {
            _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _mavLinkService.TelemetryReceived += OnTelemetryReceived;
        }
    }

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        IsWahanaConnected = isConnected;
        ConnectionStatus = isConnected ? "Connected" : "Disconnected";
        if (isConnected)
            StartFlightTimer();
        else
        {
            StopFlightTimer();
            ResetFlightTimer();
        }
    }

    private void OnTelemetryReceived(object? sender, FlightData data)
    {
        void Update()
        {
            SignalStrength = data.Signal;
            BatteryVoltage = data.BatteryVolt;
            BatteryCurrent = data.BatteryCurr;
            OnPropertyChanged(nameof(SignalDisplay));
            OnPropertyChanged(nameof(BatteryDisplay));
        }
        if (_dispatcherService != null && !_dispatcherService.IsUIThread)
            _dispatcherService.Enqueue(Update);
        else
            Update();
    }

    private void ChangeLanguage(string? language)
    {
        if (!string.IsNullOrEmpty(language))
            CurrentLanguage = language;
    }

    private void ExitApplication()
    {
        // Implementation for exit
    }

    public void StartFlightTimer()
    {
        StopFlightTimer();
        _flightTimeElapsed = TimeSpan.Zero;
        FlightTime = "00:00:00";
        _flightTimer = new System.Timers.Timer(1000);
        _flightTimer.Elapsed += (s, e) =>
        {
            _flightTimeElapsed = _flightTimeElapsed.Add(TimeSpan.FromSeconds(1));
            var timeStr = _flightTimeElapsed.ToString(@"hh\:mm\:ss");
            void Update()
            {
                FlightTime = timeStr;
                OnPropertyChanged(nameof(FlightTime));
            }
            if (_dispatcherService != null && !_dispatcherService.IsUIThread)
                _dispatcherService.Enqueue(Update);
            else
                Update();
        };
        _flightTimer.Start();
    }

    public void StopFlightTimer()
    {
        _flightTimer?.Stop();
        _flightTimer?.Dispose();
        _flightTimer = null;
    }

    public void ResetFlightTimer()
    {
        _flightTimeElapsed = TimeSpan.Zero;
        FlightTime = "00:00:00";
        OnPropertyChanged(nameof(FlightTime));
    }
}
