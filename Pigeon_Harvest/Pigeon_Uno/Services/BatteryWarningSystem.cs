using System;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services
{
    /// <summary>
    /// Monitors battery level and triggers warnings when battery is low
    /// Provides configurable thresholds and audio/visual alerts
    /// </summary>
    public class BatteryWarningSystem
    {
        private readonly ILoggingService _logger;
        private readonly IDialogService? _dialogService;
        private readonly ISpeechService? _speechService;
        
        private double _warningThreshold = 30.0;
        private double _criticalThreshold = 15.0;
        private bool _warningTriggered;
        private bool _criticalTriggered;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Event fired when battery warning is triggered
        /// </summary>
        public event EventHandler<BatteryWarningEventArgs>? BatteryWarning;

        /// <summary>
        /// Event fired when battery reaches critical level
        /// </summary>
        public event EventHandler<BatteryWarningEventArgs>? BatteryCritical;

        /// <summary>
        /// Gets or sets the warning threshold percentage (default: 30%)
        /// </summary>
        public double WarningThreshold
        {
            get => _warningThreshold;
            set => _warningThreshold = Math.Clamp(value, 5.0, 50.0);
        }

        /// <summary>
        /// Gets or sets the critical threshold percentage (default: 15%)
        /// </summary>
        public double CriticalThreshold
        {
            get => _criticalThreshold;
            set => _criticalThreshold = Math.Clamp(value, 5.0, 25.0);
        }

        /// <summary>
        /// Gets whether audio alerts are enabled
        /// </summary>
        public bool AudioAlertsEnabled { get; set; } = true;

        public BatteryWarningSystem(
            ILoggingService logger,
            IDialogService? dialogService = null,
            ISpeechService? speechService = null)
        {
            _logger = logger;
            _dialogService = dialogService;
            _speechService = speechService;
        }

        /// <summary>
        /// Checks battery level and triggers appropriate warnings
        /// </summary>
        public void CheckBatteryLevel(BatteryStatus status)
        {
            if (status == null) return;

            var percentage = status.Percentage;

            // Check critical level
            if (percentage <= _criticalThreshold)
            {
                if (!_criticalTriggered)
                {
                    _criticalTriggered = true;
                    TriggerCriticalWarning(status);
                }
            }
            // Check warning level
            else if (percentage <= _warningThreshold)
            {
                if (!_warningTriggered && CanTriggerWarning())
                {
                    _warningTriggered = true;
                    _lastWarningTime = DateTime.Now;
                    TriggerWarning(status);
                }
            }
            else
            {
                // Reset triggers when battery recovers
                _warningTriggered = false;
                _criticalTriggered = false;
            }
        }

        /// <summary>
        /// Triggers battery warning
        /// </summary>
        private void TriggerWarning(BatteryStatus status)
        {
            var message = $"Battery low: {status.Percentage:F0}%";
            _logger.LogWarning(message, nameof(BatteryWarningSystem));

            // Fire event
            BatteryWarning?.Invoke(this, new BatteryWarningEventArgs
            {
                Percentage = status.Percentage,
                Message = message,
                Level = BatteryLevel.Warning
            });

            // Show dialog alert
            _ = Task.Run(async () =>
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(message, "Battery Warning");
                }
            });

            // Play audio alert
            if (AudioAlertsEnabled && _speechService != null)
            {
                _speechService.SpeakAsync("Battery level low. Please land soon.");
            }
        }

        /// <summary>
        /// Triggers critical battery warning
        /// </summary>
        private void TriggerCriticalWarning(BatteryStatus status)
        {
            var message = $"Battery CRITICAL: {status.Percentage:F0}% - Land immediately!";
            _logger.LogError(message, nameof(BatteryWarningSystem));

            // Fire event
            BatteryCritical?.Invoke(this, new BatteryWarningEventArgs
            {
                Percentage = status.Percentage,
                Message = message,
                Level = BatteryLevel.Critical
            });

            // Show critical dialog
            _ = Task.Run(async () =>
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowAlertAsync(message, "CRITICAL BATTERY");
                }
            });

            // Play critical audio alert
            if (AudioAlertsEnabled && _speechService != null)
            {
                _speechService.SpeakAsync("Critical battery level. Land immediately.");
            }
        }

        /// <summary>
        /// Checks if enough time has passed since last warning
        /// </summary>
        private bool CanTriggerWarning()
        {
            return DateTime.Now - _lastWarningTime >= _warningCooldown;
        }

        /// <summary>
        /// Resets warning triggers
        /// </summary>
        public void ResetTriggers()
        {
            _warningTriggered = false;
            _criticalTriggered = false;
            _lastWarningTime = DateTime.MinValue;
            _logger.LogInfo("Battery warning triggers reset", nameof(BatteryWarningSystem));
        }
    }

    /// <summary>
    /// Event args for battery warning events
    /// </summary>
    public class BatteryWarningEventArgs : EventArgs
    {
        public double Percentage { get; set; }
        public string Message { get; set; } = string.Empty;
        public BatteryLevel Level { get; set; }
    }

    /// <summary>
    /// Battery warning levels
    /// </summary>
    public enum BatteryLevel
    {
        Normal,
        Warning,
        Critical
    }
}
