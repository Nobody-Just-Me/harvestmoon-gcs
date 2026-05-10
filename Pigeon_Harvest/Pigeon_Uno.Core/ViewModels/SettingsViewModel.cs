using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// Manages application configuration and preferences.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private const string SettingsFileName = "app_settings.json";
    private string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pigeon_Uno",
        SettingsFileName);

    #region Connection Settings

    private int _connectionTypeIndex = 0;
    public int ConnectionTypeIndex
    {
        get => _connectionTypeIndex;
        set => SetProperty(ref _connectionTypeIndex, value);
    }

    private int _baudRateIndex = 3; // 57600
    public int BaudRateIndex
    {
        get => _baudRateIndex;
        set => SetProperty(ref _baudRateIndex, value);
    }

    private bool _autoReconnect = true;
    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => SetProperty(ref _autoReconnect, value);
    }

    private int _connectionTimeout = 10;
    public int ConnectionTimeout
    {
        get => _connectionTimeout;
        set => SetProperty(ref _connectionTimeout, value);
    }

    #endregion

    #region Map Settings

    private int _mapProviderIndex = 0; // OpenStreetMap
    public int MapProviderIndex
    {
        get => _mapProviderIndex;
        set => SetProperty(ref _mapProviderIndex, value);
    }

    private int _defaultZoomLevel = 15;
    public int DefaultZoomLevel
    {
        get => _defaultZoomLevel;
        set => SetProperty(ref _defaultZoomLevel, value);
    }

    private bool _showMapGrid = false;
    public bool ShowMapGrid
    {
        get => _showMapGrid;
        set => SetProperty(ref _showMapGrid, value);
    }

    private bool _showFlightTrack = true;
    public bool ShowFlightTrack
    {
        get => _showFlightTrack;
        set => SetProperty(ref _showFlightTrack, value);
    }

    private bool _centerMapOnDrone = true;
    public bool CenterMapOnDrone
    {
        get => _centerMapOnDrone;
        set => SetProperty(ref _centerMapOnDrone, value);
    }

    private int _trackColorIndex = 0; // Red
    public int TrackColorIndex
    {
        get => _trackColorIndex;
        set => SetProperty(ref _trackColorIndex, value);
    }

    #endregion

    #region Alert Settings

    private bool _masterAlertEnabled = true;
    public bool MasterAlertEnabled
    {
        get => _masterAlertEnabled;
        set => SetProperty(ref _masterAlertEnabled, value);
    }

    private int _alertVolume = 80;
    public int AlertVolume
    {
        get => _alertVolume;
        set => SetProperty(ref _alertVolume, value);
    }

    private bool _useTTS = true;
    public bool UseTTS
    {
        get => _useTTS;
        set => SetProperty(ref _useTTS, value);
    }

    private bool _playAlertSound = true;
    public bool PlayAlertSound
    {
        get => _playAlertSound;
        set => SetProperty(ref _playAlertSound, value);
    }

    private bool _batteryAlertEnabled = true;
    public bool BatteryAlertEnabled
    {
        get => _batteryAlertEnabled;
        set => SetProperty(ref _batteryAlertEnabled, value);
    }

    private bool _gpsAlertEnabled = true;
    public bool GpsAlertEnabled
    {
        get => _gpsAlertEnabled;
        set => SetProperty(ref _gpsAlertEnabled, value);
    }

    private bool _connectionAlertEnabled = true;
    public bool ConnectionAlertEnabled
    {
        get => _connectionAlertEnabled;
        set => SetProperty(ref _connectionAlertEnabled, value);
    }

    private bool _geofenceAlertEnabled = true;
    public bool GeofenceAlertEnabled
    {
        get => _geofenceAlertEnabled;
        set => SetProperty(ref _geofenceAlertEnabled, value);
    }

    private bool _flightModeAlertEnabled = true;
    public bool FlightModeAlertEnabled
    {
        get => _flightModeAlertEnabled;
        set => SetProperty(ref _flightModeAlertEnabled, value);
    }

    #endregion

    #region UI Settings

    private int _themeIndex = 0; // Dark
    public int ThemeIndex
    {
        get => _themeIndex;
        set => SetProperty(ref _themeIndex, value);
    }

    private int _languageIndex = 0; // English
    public int LanguageIndex
    {
        get => _languageIndex;
        set => SetProperty(ref _languageIndex, value);
    }

    private int _fontSizeIndex = 1; // Medium
    public int FontSizeIndex
    {
        get => _fontSizeIndex;
        set => SetProperty(ref _fontSizeIndex, value);
    }

    private int _unitSystemIndex = 0; // Metric
    public int UnitSystemIndex
    {
        get => _unitSystemIndex;
        set => SetProperty(ref _unitSystemIndex, value);
    }

    private bool _showTooltips = true;
    public bool ShowTooltips
    {
        get => _showTooltips;
        set => SetProperty(ref _showTooltips, value);
    }

    private bool _highContrastMode = false;
    public bool HighContrastMode
    {
        get => _highContrastMode;
        set => SetProperty(ref _highContrastMode, value);
    }

    private bool _enableAnimations = true;
    public bool EnableAnimations
    {
        get => _enableAnimations;
        set => SetProperty(ref _enableAnimations, value);
    }

    #endregion

    #region Performance Settings

    private int _telemetryUpdateRate = 10;
    public int TelemetryUpdateRate
    {
        get => _telemetryUpdateRate;
        set => SetProperty(ref _telemetryUpdateRate, value);
    }

    private int _mapQualityIndex = 2; // High
    public int MapQualityIndex
    {
        get => _mapQualityIndex;
        set => SetProperty(ref _mapQualityIndex, value);
    }

    private bool _enableCaching = true;
    public bool EnableCaching
    {
        get => _enableCaching;
        set => SetProperty(ref _enableCaching, value);
    }

    private bool _hardwareAcceleration = true;
    public bool HardwareAcceleration
    {
        get => _hardwareAcceleration;
        set => SetProperty(ref _hardwareAcceleration, value);
    }

    private bool _lowPowerMode = false;
    public bool LowPowerMode
    {
        get => _lowPowerMode;
        set => SetProperty(ref _lowPowerMode, value);
    }

    private int _maxMemoryUsage = 512;
    public int MaxMemoryUsage
    {
        get => _maxMemoryUsage;
        set => SetProperty(ref _maxMemoryUsage, value);
    }

    #endregion

    #region Data and Storage Settings

    private bool _enableLogging = true;
    public bool EnableLogging
    {
        get => _enableLogging;
        set => SetProperty(ref _enableLogging, value);
    }

    private bool _autoSaveLogs = true;
    public bool AutoSaveLogs
    {
        get => _autoSaveLogs;
        set => SetProperty(ref _autoSaveLogs, value);
    }

    private int _logRetentionDays = 30;
    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set => SetProperty(ref _logRetentionDays, value);
    }

    #endregion

    #region Constructor

    public SettingsViewModel()
    {
        LoadSettingsFromFile();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Save settings to file.
    /// </summary>
    public async Task<bool> SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                // Connection
                ConnectionTypeIndex = ConnectionTypeIndex,
                BaudRateIndex = BaudRateIndex,
                AutoReconnect = AutoReconnect,
                ConnectionTimeout = ConnectionTimeout,

                // Map
                MapProviderIndex = MapProviderIndex,
                DefaultZoomLevel = DefaultZoomLevel,
                ShowMapGrid = ShowMapGrid,
                ShowFlightTrack = ShowFlightTrack,
                CenterMapOnDrone = CenterMapOnDrone,
                TrackColorIndex = TrackColorIndex,

                // Alerts
                MasterAlertEnabled = MasterAlertEnabled,
                AlertVolume = AlertVolume,
                UseTTS = UseTTS,
                PlayAlertSound = PlayAlertSound,
                BatteryAlertEnabled = BatteryAlertEnabled,
                GpsAlertEnabled = GpsAlertEnabled,
                ConnectionAlertEnabled = ConnectionAlertEnabled,
                GeofenceAlertEnabled = GeofenceAlertEnabled,
                FlightModeAlertEnabled = FlightModeAlertEnabled,

                // UI
                ThemeIndex = ThemeIndex,
                LanguageIndex = LanguageIndex,
                FontSizeIndex = FontSizeIndex,
                UnitSystemIndex = UnitSystemIndex,
                ShowTooltips = ShowTooltips,
                HighContrastMode = HighContrastMode,
                EnableAnimations = EnableAnimations,

                // Performance
                TelemetryUpdateRate = TelemetryUpdateRate,
                MapQualityIndex = MapQualityIndex,
                EnableCaching = EnableCaching,
                HardwareAcceleration = HardwareAcceleration,
                LowPowerMode = LowPowerMode,
                MaxMemoryUsage = MaxMemoryUsage,

                // Data
                EnableLogging = EnableLogging,
                AutoSaveLogs = AutoSaveLogs,
                LogRetentionDays = LogRetentionDays
            };

            // Ensure directory exists
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize and save
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(SettingsFilePath, json);
            Debug.WriteLine($"Settings saved to: {SettingsFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load settings from file.
    /// </summary>
    private void LoadSettingsFromFile()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Debug.WriteLine("Settings file not found, using defaults");
                return;
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings != null)
            {
                // Connection
                ConnectionTypeIndex = settings.ConnectionTypeIndex;
                BaudRateIndex = settings.BaudRateIndex;
                AutoReconnect = settings.AutoReconnect;
                ConnectionTimeout = settings.ConnectionTimeout;

                // Map
                MapProviderIndex = settings.MapProviderIndex;
                DefaultZoomLevel = settings.DefaultZoomLevel;
                ShowMapGrid = settings.ShowMapGrid;
                ShowFlightTrack = settings.ShowFlightTrack;
                CenterMapOnDrone = settings.CenterMapOnDrone;
                TrackColorIndex = settings.TrackColorIndex;

                // Alerts
                MasterAlertEnabled = settings.MasterAlertEnabled;
                AlertVolume = settings.AlertVolume;
                UseTTS = settings.UseTTS;
                PlayAlertSound = settings.PlayAlertSound;
                BatteryAlertEnabled = settings.BatteryAlertEnabled;
                GpsAlertEnabled = settings.GpsAlertEnabled;
                ConnectionAlertEnabled = settings.ConnectionAlertEnabled;
                GeofenceAlertEnabled = settings.GeofenceAlertEnabled;
                FlightModeAlertEnabled = settings.FlightModeAlertEnabled;

                // UI
                ThemeIndex = settings.ThemeIndex;
                LanguageIndex = settings.LanguageIndex;
                FontSizeIndex = settings.FontSizeIndex;
                UnitSystemIndex = settings.UnitSystemIndex;
                ShowTooltips = settings.ShowTooltips;
                HighContrastMode = settings.HighContrastMode;
                EnableAnimations = settings.EnableAnimations;

                // Performance
                TelemetryUpdateRate = settings.TelemetryUpdateRate;
                MapQualityIndex = settings.MapQualityIndex;
                EnableCaching = settings.EnableCaching;
                HardwareAcceleration = settings.HardwareAcceleration;
                LowPowerMode = settings.LowPowerMode;
                MaxMemoryUsage = settings.MaxMemoryUsage;

                // Data
                EnableLogging = settings.EnableLogging;
                AutoSaveLogs = settings.AutoSaveLogs;
                LogRetentionDays = settings.LogRetentionDays;

                Debug.WriteLine("Settings loaded successfully");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Reset all settings to default values.
    /// </summary>
    public void ResetToDefaults()
    {
        // Connection
        ConnectionTypeIndex = 0;
        BaudRateIndex = 3;
        AutoReconnect = true;
        ConnectionTimeout = 10;

        // Map
        MapProviderIndex = 0;
        DefaultZoomLevel = 15;
        ShowMapGrid = false;
        ShowFlightTrack = true;
        CenterMapOnDrone = true;
        TrackColorIndex = 0;

        // Alerts
        MasterAlertEnabled = true;
        AlertVolume = 80;
        UseTTS = true;
        PlayAlertSound = true;
        BatteryAlertEnabled = true;
        GpsAlertEnabled = true;
        ConnectionAlertEnabled = true;
        GeofenceAlertEnabled = true;
        FlightModeAlertEnabled = true;

        // UI
        ThemeIndex = 0;
        LanguageIndex = 0;
        FontSizeIndex = 1;
        UnitSystemIndex = 0;
        ShowTooltips = true;
        HighContrastMode = false;
        EnableAnimations = true;

        // Performance
        TelemetryUpdateRate = 10;
        MapQualityIndex = 2;
        EnableCaching = true;
        HardwareAcceleration = true;
        LowPowerMode = false;
        MaxMemoryUsage = 512;

        // Data
        EnableLogging = true;
        AutoSaveLogs = true;
        LogRetentionDays = 30;

        Debug.WriteLine("Settings reset to defaults");
    }

    /// <summary>
    /// Export settings to a file.
    /// </summary>
    public async Task<bool> ExportSettings()
    {
        try
        {
            // For now, just save to the default location
            // In a full implementation, this would open a file picker
            return await SaveSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error exporting settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Import settings from a file.
    /// </summary>
    public async Task<bool> ImportSettings()
    {
        try
        {
            // For now, just load from the default location
            // In a full implementation, this would open a file picker
            await Task.Run(() => LoadSettingsFromFile());
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error importing settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clear cache.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            // Clear application cache directory
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Pigeon_Uno",
                "Cache");
            
            if (Directory.Exists(cacheDir))
            {
                var files = Directory.GetFiles(cacheDir);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.WriteLine($"Deleted cache file: {file}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting cache file {file}: {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"Cache cleared: {files.Length} files deleted");
            }
            else
            {
                Debug.WriteLine("Cache directory does not exist");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear old logs.
    /// </summary>
    public void ClearOldLogs()
    {
        try
        {
            // Clear old log files based on retention days
            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Pigeon_Uno",
                "Logs");
            
            if (Directory.Exists(logsDir))
            {
                var cutoffDate = DateTime.Now.AddDays(-LogRetentionDays);
                var files = Directory.GetFiles(logsDir);
                int deletedCount = 0;
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(file);
                            deletedCount++;
                            Debug.WriteLine($"Deleted old log file: {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting log file {file}: {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"Old logs cleared: {deletedCount} files deleted (older than {LogRetentionDays} days)");
            }
            else
            {
                Debug.WriteLine("Logs directory does not exist");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing logs: {ex.Message}");
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// Application settings data model for serialization.
/// </summary>
public class AppSettings
{
    // Connection
    public int ConnectionTypeIndex { get; set; }
    public int BaudRateIndex { get; set; }
    public bool AutoReconnect { get; set; }
    public int ConnectionTimeout { get; set; }

    // Map
    public int MapProviderIndex { get; set; }
    public int DefaultZoomLevel { get; set; }
    public bool ShowMapGrid { get; set; }
    public bool ShowFlightTrack { get; set; }
    public bool CenterMapOnDrone { get; set; }
    public int TrackColorIndex { get; set; }

    // Alerts
    public bool MasterAlertEnabled { get; set; }
    public int AlertVolume { get; set; }
    public bool UseTTS { get; set; }
    public bool PlayAlertSound { get; set; }
    public bool BatteryAlertEnabled { get; set; }
    public bool GpsAlertEnabled { get; set; }
    public bool ConnectionAlertEnabled { get; set; }
    public bool GeofenceAlertEnabled { get; set; }
    public bool FlightModeAlertEnabled { get; set; }

    // UI
    public int ThemeIndex { get; set; }
    public int LanguageIndex { get; set; }
    public int FontSizeIndex { get; set; }
    public int UnitSystemIndex { get; set; }
    public bool ShowTooltips { get; set; }
    public bool HighContrastMode { get; set; }
    public bool EnableAnimations { get; set; }

    // Performance
    public int TelemetryUpdateRate { get; set; }
    public int MapQualityIndex { get; set; }
    public bool EnableCaching { get; set; }
    public bool HardwareAcceleration { get; set; }
    public bool LowPowerMode { get; set; }
    public int MaxMemoryUsage { get; set; }

    // Data
    public bool EnableLogging { get; set; }
    public bool AutoSaveLogs { get; set; }
    public int LogRetentionDays { get; set; }
}
