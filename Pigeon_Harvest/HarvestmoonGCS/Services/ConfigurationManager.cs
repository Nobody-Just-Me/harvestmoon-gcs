using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// Manages application configuration and settings categories
    /// Provides centralized access to all configuration settings
    /// </summary>
    public class ConfigurationManager
    {
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _logger;

        public ConfigurationManager(ISettingsService settingsService, ILoggingService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current application settings
        /// </summary>
        public AppSettings Settings => _settingsService.Settings;

        /// <summary>
        /// Connection configuration
        /// </summary>
        public ConnectionSettings Connection => Settings.Connection;

        /// <summary>
        /// Map configuration
        /// </summary>
        public MapSettings Map => Settings.Map;

        /// <summary>
        /// UI configuration
        /// </summary>
        public UiSettings Ui => Settings.Ui;

        /// <summary>
        /// Application language
        /// </summary>
        public string Language
        {
            get => Settings.Language;
            set => Settings.Language = value;
        }

        /// <summary>
        /// Map type
        /// </summary>
        public string MapType
        {
            get => Settings.MapType;
            set => Settings.MapType = value;
        }

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        public async Task<bool> LoadConfigurationAsync()
        {
            _logger.LogInfo("Loading configuration...", nameof(ConfigurationManager));
            var result = await _settingsService.LoadSettingsAsync();
            if (result)
            {
                _logger.LogInfo("Configuration loaded successfully", nameof(ConfigurationManager));
            }
            else
            {
                _logger.LogWarning("Failed to load configuration, using defaults", nameof(ConfigurationManager));
            }
            return result;
        }

        /// <summary>
        /// Saves settings to storage
        /// </summary>
        public async Task<bool> SaveConfigurationAsync()
        {
            _logger.LogInfo("Saving configuration...", nameof(ConfigurationManager));
            var result = await _settingsService.SaveSettingsAsync();
            if (result)
            {
                _logger.LogInfo("Configuration saved successfully", nameof(ConfigurationManager));
            }
            else
            {
                _logger.LogError("Failed to save configuration", nameof(ConfigurationManager));
            }
            return result;
        }

        /// <summary>
        /// Resets configuration to default values
        /// </summary>
        public async Task<bool> ResetToDefaultsAsync()
        {
            _logger.LogInfo("Resetting configuration to defaults...", nameof(ConfigurationManager));
            var result = await _settingsService.ResetToDefaultAsync();
            if (result)
            {
                _logger.LogInfo("Configuration reset to defaults", nameof(ConfigurationManager));
            }
            return result;
        }

        /// <summary>
        /// Gets a specific setting value
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue)
        {
            return _settingsService.GetSetting(key, defaultValue);
        }

        /// <summary>
        /// Sets a specific setting value
        /// </summary>
        public async Task<bool> SetSettingAsync<T>(string key, T value)
        {
            return await _settingsService.SetSettingAsync(key, value);
        }

        /// <summary>
        /// Updates connection settings
        /// </summary>
        public void UpdateConnectionSettings(Action<ConnectionSettings> updateAction)
        {
            updateAction(Settings.Connection);
        }

        /// <summary>
        /// Updates map settings
        /// </summary>
        public void UpdateMapSettings(Action<MapSettings> updateAction)
        {
            updateAction(Settings.Map);
        }

        /// <summary>
        /// Updates UI settings
        /// </summary>
        public void UpdateUiSettings(Action<UiSettings> updateAction)
        {
            updateAction(Settings.Ui);
        }
    }
}
