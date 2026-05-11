using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// Manages settings UI interactions and validation
    /// Acts as a bridge between ViewModels and ConfigurationManager
    /// </summary>
    public class SettingsManager
    {
        private readonly ConfigurationManager _configManager;
        private readonly ILoggingService _logger;
        private readonly IDialogService _dialogService;

        public SettingsManager(
            ConfigurationManager configManager,
            ILoggingService logger,
            IDialogService dialogService)
        {
            _configManager = configManager;
            _logger = logger;
            _dialogService = dialogService;
        }

        /// <summary>
        /// Current application settings
        /// </summary>
        public AppSettings Settings => _configManager.Settings;

        /// <summary>
        /// Validates connection settings
        /// </summary>
        public bool ValidateConnectionSettings()
        {
            var connection = Settings.Connection;

            // Validate IP address format for TCP/UDP
            if (connection.ConnectionType == "TCP" || connection.ConnectionType == "UDP")
            {
                if (string.IsNullOrWhiteSpace(connection.IpAddress))
                {
                    _dialogService.ShowAlertAsync("IP Address cannot be empty", "Validation Error");
                    return false;
                }

                if (connection.Port <= 0 || connection.Port > 65535)
                {
                    _dialogService.ShowAlertAsync("Port must be between 1 and 65535", "Validation Error");
                    return false;
                }
            }

            // Validate serial port settings
            if (connection.ConnectionType == "Serial")
            {
                if (string.IsNullOrWhiteSpace(connection.SerialPort))
                {
                    _dialogService.ShowAlertAsync("Serial Port cannot be empty", "Validation Error");
                    return false;
                }

                if (connection.BaudRate <= 0)
                {
                    _dialogService.ShowAlertAsync("Baud Rate must be positive", "Validation Error");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates map settings
        /// </summary>
        public bool ValidateMapSettings()
        {
            var map = Settings.Map;

            if (map.DefaultWaypointRadius < 0)
            {
                _dialogService.ShowAlertAsync("Waypoint radius cannot be negative", "Validation Error");
                return false;
            }

            if (map.DefaultWaypointAltitude < 0)
            {
                _dialogService.ShowAlertAsync("Waypoint altitude cannot be negative", "Validation Error");
                return false;
            }

            if (map.GeofenceRadius < 0)
            {
                _dialogService.ShowAlertAsync("Geofence radius cannot be negative", "Validation Error");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Saves all settings after validation
        /// </summary>
        public async Task<bool> SaveSettingsAsync()
        {
            if (!ValidateConnectionSettings())
                return false;

            if (!ValidateMapSettings())
                return false;

            _logger.LogInfo("Saving all settings...", nameof(SettingsManager));
            return await _configManager.SaveConfigurationAsync();
        }

        /// <summary>
        /// Resets settings to defaults with confirmation
        /// </summary>
        public async Task<bool> ResetSettingsWithConfirmationAsync()
        {
            var confirmed = await _dialogService.ShowConfirmAsync(
                "Are you sure you want to reset all settings to default values?",
                "Reset Settings");

            if (!confirmed)
                return false;

            _logger.LogInfo("Resetting settings to defaults...", nameof(SettingsManager));
            return await _configManager.ResetToDefaultsAsync();
        }

        /// <summary>
        /// Applies a theme change
        /// </summary>
        public void ApplyTheme(string theme)
        {
            Settings.Ui.Theme = theme;
            _logger.LogInfo($"Theme changed to: {theme}", nameof(SettingsManager));
        }

        /// <summary>
        /// Changes application language
        /// </summary>
        public void ChangeLanguage(string languageCode)
        {
            Settings.Language = languageCode;
            _logger.LogInfo($"Language changed to: {languageCode}", nameof(SettingsManager));
        }

        /// <summary>
        /// Toggles sound alerts
        /// </summary>
        public void ToggleSoundAlerts(bool enabled)
        {
            Settings.Ui.EnableSoundAlerts = enabled;
            _logger.LogInfo($"Sound alerts {(enabled ? "enabled" : "disabled")}", nameof(SettingsManager));
        }

        /// <summary>
        /// Toggles voice alerts
        /// </summary>
        public void ToggleVoiceAlerts(bool enabled)
        {
            Settings.Ui.EnableVoiceAlerts = enabled;
            _logger.LogInfo($"Voice alerts {(enabled ? "enabled" : "disabled")}", nameof(SettingsManager));
        }

        /// <summary>
        /// Updates connection type and validates
        /// </summary>
        public void UpdateConnectionType(string connectionType)
        {
            Settings.Connection.ConnectionType = connectionType;
            _logger.LogInfo($"Connection type changed to: {connectionType}", nameof(SettingsManager));
        }

        /// <summary>
        /// Gets available connection types
        /// </summary>
        public string[] GetAvailableConnectionTypes()
        {
            return new[] { "TCP", "UDP", "Serial" };
        }

        /// <summary>
        /// Gets available themes
        /// </summary>
        public string[] GetAvailableThemes()
        {
            return new[] { "Light", "Dark" };
        }

        /// <summary>
        /// Gets available languages
        /// </summary>
        public (string Code, string Name)[] GetAvailableLanguages()
        {
            return new[]
            {
                ("en", "English"),
                ("id", "Bahasa Indonesia")
            };
        }

        /// <summary>
        /// Gets available map types
        /// </summary>
        public string[] GetAvailableMapTypes()
        {
            return new[]
            {
                "ArcGISTopographic",
                "ArcGISImagery",
                "OpenStreetMap",
                "BingMapsRoad",
                "BingMapsAerial"
            };
        }
    }
}
