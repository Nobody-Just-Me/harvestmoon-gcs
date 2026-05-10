using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Services;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Manages audio alerts and text-to-speech announcements.
/// Queues alerts and processes them sequentially.
/// </summary>
public class AlertManager : IAlertManager
{
    private readonly ISpeechService _speechService;
    private readonly Queue<Alert> _alertQueue;
    private readonly SemaphoreSlim _queueSemaphore;
    private readonly SemaphoreSlim _processingSemaphore;
    private bool _isProcessing;
    private AlertSettings _settings;

    public AlertManager(ISpeechService speechService)
    {
        _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
        _alertQueue = new Queue<Alert>();
        _queueSemaphore = new SemaphoreSlim(1, 1);
        _processingSemaphore = new SemaphoreSlim(1, 1);
        _settings = new AlertSettings();
    }

    /// <summary>
    /// Gets or sets alert settings.
    /// </summary>
    public AlertSettings Settings
    {
        get => _settings;
        set => _settings = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Initializes the alert manager.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _speechService.InitializeAsync();
    }

    /// <summary>
    /// Queues a battery warning alert.
    /// </summary>
    public async Task QueueBatteryWarningAsync(int batteryPercent)
    {
        if (!_settings.BatteryWarningEnabled)
            return;

        var alert = new Alert
        {
            Type = AlertType.BatteryWarning,
            Priority = AlertPriority.High,
            Message = $"Battery low: {batteryPercent} percent",
            SoundFile = "battery_warning.wav"
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues a GPS lost alert.
    /// </summary>
    public async Task QueueGpsLostAsync()
    {
        if (!_settings.GpsLostEnabled)
            return;

        var alert = new Alert
        {
            Type = AlertType.GpsLost,
            Priority = AlertPriority.Critical,
            Message = "GPS signal lost",
            SoundFile = "gps_warning.wav"
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues a connection lost alert.
    /// </summary>
    public async Task QueueConnectionLostAsync()
    {
        if (!_settings.ConnectionLostEnabled)
            return;

        var alert = new Alert
        {
            Type = AlertType.ConnectionLost,
            Priority = AlertPriority.Critical,
            Message = "Connection lost",
            SoundFile = "connection_lost.wav"
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues a geofence violation alert.
    /// </summary>
    public async Task QueueGeofenceViolationAsync()
    {
        if (!_settings.GeofenceViolationEnabled)
            return;

        var alert = new Alert
        {
            Type = AlertType.GeofenceViolation,
            Priority = AlertPriority.High,
            Message = "Geofence violation",
            SoundFile = "geofence_warning.wav"
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues a flight mode change alert.
    /// </summary>
    public async Task QueueFlightModeChangeAsync(string modeName)
    {
        if (!_settings.FlightModeChangeEnabled)
            return;

        var alert = new Alert
        {
            Type = AlertType.FlightModeChange,
            Priority = AlertPriority.Normal,
            Message = $"Flight mode: {modeName}",
            SoundFile = null // No sound for mode changes
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues a custom alert.
    /// </summary>
    public async Task QueueCustomAlertAsync(string message, AlertPriority priority = AlertPriority.Normal)
    {
        var alert = new Alert
        {
            Type = AlertType.Custom,
            Priority = priority,
            Message = message,
            SoundFile = null
        };

        await QueueAlertAsync(alert);
    }

    /// <summary>
    /// Queues an alert for processing.
    /// </summary>
    private async Task QueueAlertAsync(Alert alert)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            // Check if similar alert is already in queue (prevent spam)
            if (IsDuplicateAlert(alert))
            {
                return;
            }

            _alertQueue.Enqueue(alert);
        }
        finally
        {
            _queueSemaphore.Release();
        }

        await ProcessQueueAsync();
    }

    /// <summary>
    /// Checks if a similar alert is already in the queue.
    /// </summary>
    private bool IsDuplicateAlert(Alert newAlert)
    {
        foreach (var existingAlert in _alertQueue)
        {
            if (existingAlert.Type == newAlert.Type &&
                existingAlert.Message == newAlert.Message)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Processes alerts from the queue.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        await _processingSemaphore.WaitAsync();
        try
        {
            while (true)
            {
                Alert alert = null;

                await _queueSemaphore.WaitAsync();
                try
                {
                    if (_alertQueue.Count > 0)
                    {
                        alert = _alertQueue.Dequeue();
                    }
                    else
                    {
                        return;
                    }
                }
                finally
                {
                    _queueSemaphore.Release();
                }

                await ProcessAlertAsync(alert);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    /// <summary>
    /// Processes a single alert.
    /// </summary>
    private async Task ProcessAlertAsync(Alert alert)
    {
        try
        {
            // Play sound if specified
            if (!string.IsNullOrEmpty(alert.SoundFile) && _settings.SoundEnabled)
            {
                await PlaySoundAsync(alert.SoundFile);
            }

            // Speak message if TTS is enabled
            if (_settings.TtsEnabled && !string.IsNullOrEmpty(alert.Message))
            {
                // Critical alerts interrupt current speech
                bool shouldInterrupt = alert.Priority == AlertPriority.Critical;
                await _speechService.SpeakAsync(alert.Message, shouldInterrupt);
            }

            // Wait a bit between alerts
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            // Log error but continue processing
            System.Diagnostics.Debug.WriteLine($"Error processing alert: {ex.Message}");
        }
    }

    /// <summary>
    /// Plays an alert sound.
    /// </summary>
    private async Task PlaySoundAsync(string soundFile)
    {
        try
        {
            // Platform-specific sound playback
            // For now, use a simple beep or system sound
            // In a full implementation, this would load and play the actual sound file
            
            System.Diagnostics.Debug.WriteLine($"[AlertManager] Playing sound: {soundFile}");
            
            // Note: Actual sound playback would require platform-specific implementations:
            // - Windows: Use MediaPlayer or SoundPlayer
            // - Android: Use MediaPlayer or SoundPool
            // - iOS: Use AVAudioPlayer
            // - WebAssembly: Use HTML5 Audio API
            
            // For now, just log the sound file name
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlertManager] Error playing sound: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all pending alerts.
    /// </summary>
    public async Task ClearAlertsAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _alertQueue.Clear();
        }
        finally
        {
            _queueSemaphore.Release();
        }

        await _speechService.StopAsync();
    }

    /// <summary>
    /// Stops the alert manager.
    /// </summary>
    public async Task StopAsync()
    {
        await ClearAlertsAsync();
    }
}

/// <summary>
/// Represents an alert.
/// </summary>
public class Alert
{
    public AlertType Type { get; set; }
    public AlertPriority Priority { get; set; }
    public string Message { get; set; }
    public string SoundFile { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Alert types.
/// </summary>
public enum AlertType
{
    BatteryWarning,
    GpsLost,
    ConnectionLost,
    GeofenceViolation,
    FlightModeChange,
    Custom
}

/// <summary>
/// Alert priorities.
/// </summary>
public enum AlertPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Alert settings.
/// </summary>
public class AlertSettings
{
    public bool TtsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool BatteryWarningEnabled { get; set; } = true;
    public bool GpsLostEnabled { get; set; } = true;
    public bool ConnectionLostEnabled { get; set; } = true;
    public bool GeofenceViolationEnabled { get; set; } = true;
    public bool FlightModeChangeEnabled { get; set; } = true;
    public int BatteryWarningThreshold { get; set; } = 20; // Percent
}
