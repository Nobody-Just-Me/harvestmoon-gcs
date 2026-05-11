using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Models.AI;

/// <summary>
/// AI settings configuration for Pigeon GCS
/// </summary>
public class AISettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _enabled = true;
    private string _provider = "OpenRouter";
    private string _fallbackProvider = "Gemini";
    private string _apiKey = string.Empty;
    private string _baseUrl = "https://openrouter.ai/api/v1";
    private string _siteUrl = "https://pigeon-gcs.app";
    private string _siteName = "Pigeon GCS - PIA";
    private AIModelsConfig _models = new();
    private AIAnalysisConfig _analysis = new();
    private TelemetrySamplingConfig _telemetrySampling = new();
    private AICacheConfig _cache = new();
    private VoiceCommandConfig _voiceCommand = new();
    private AnomalyDetectionConfig _anomalyDetection = new();
    private int _historyRetentionDays = 30;

    /// <summary>
    /// Enable or disable AI features
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    /// <summary>
    /// AI provider (OpenRouter, Gemini, etc.)
    /// </summary>
    public string Provider
    {
        get => _provider;
        set => SetProperty(ref _provider, value);
    }

    /// <summary>
    /// Preferred fallback AI provider when primary provider is unavailable.
    /// </summary>
    public string FallbackProvider
    {
        get => _fallbackProvider;
        set => SetProperty(ref _fallbackProvider, value);
    }

    /// <summary>
    /// API key for the AI provider
    /// </summary>
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    /// <summary>
    /// Base URL for AI API endpoints
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    /// <summary>
    /// Site URL for OpenRouter HTTP-Referer header
    /// </summary>
    public string SiteUrl
    {
        get => _siteUrl;
        set => SetProperty(ref _siteUrl, value);
    }

    /// <summary>
    /// Site name for OpenRouter X-Title header
    /// </summary>
    public string SiteName
    {
        get => _siteName;
        set => SetProperty(ref _siteName, value);
    }

    /// <summary>
    /// Model configurations for different AI tasks
    /// </summary>
    public AIModelsConfig Models
    {
        get => _models;
        set => SetProperty(ref _models, value);
    }

    /// <summary>
    /// Analysis configuration
    /// </summary>
    public AIAnalysisConfig Analysis
    {
        get => _analysis;
        set => SetProperty(ref _analysis, value);
    }

    /// <summary>
    /// Telemetry sampling configuration
    /// </summary>
    public TelemetrySamplingConfig TelemetrySampling
    {
        get => _telemetrySampling;
        set => SetProperty(ref _telemetrySampling, value);
    }

    /// <summary>
    /// Cache configuration
    /// </summary>
    public AICacheConfig Cache
    {
        get => _cache;
        set => SetProperty(ref _cache, value);
    }

    /// <summary>
    /// Voice command configuration
    /// </summary>
    public VoiceCommandConfig VoiceCommand
    {
        get => _voiceCommand;
        set => SetProperty(ref _voiceCommand, value);
    }

    /// <summary>
    /// Anomaly detection configuration
    /// </summary>
    public AnomalyDetectionConfig AnomalyDetection
    {
        get => _anomalyDetection;
        set => SetProperty(ref _anomalyDetection, value);
    }

    /// <summary>
    /// Retention policy for persisted PIA history (in days).
    /// </summary>
    public int HistoryRetentionDays
    {
        get => _historyRetentionDays;
        set => SetProperty(ref _historyRetentionDays, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Telemetry sampling configuration for PIA pipeline
/// </summary>
public class TelemetrySamplingConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _minIntervalMs = 500;
    private int _forceIntervalMs = 2000;
    private double _batteryDeltaPercent = 0.8;
    private double _altitudeDeltaMeters = 1.5;
    private double _speedDeltaMps = 0.8;
    private double _headingDeltaDeg = 7;
    private double _verticalSpeedDeltaMps = 0.6;
    private double _gpsDistanceDeltaMeters = 2.5;
    private double _gpsHdopDelta = 0.3;
    private double _batteryVoltageDeltaVolts = 0.08;
    private double _batteryCurrentDeltaAmp = 0.4;
    private double _rollDeltaDeg = 2.0;
    private double _pitchDeltaDeg = 2.0;
    private double _vibrationMagnitudeDelta = 8.0;
    private double _linkQualityDeltaPercent = 8.0;
    private bool _sampleOnFlightModeChange = true;
    private bool _sampleOnArmedStateChange = true;

    public int MinIntervalMs
    {
        get => _minIntervalMs;
        set => SetProperty(ref _minIntervalMs, value);
    }

    public int ForceIntervalMs
    {
        get => _forceIntervalMs;
        set => SetProperty(ref _forceIntervalMs, value);
    }

    public double BatteryDeltaPercent
    {
        get => _batteryDeltaPercent;
        set => SetProperty(ref _batteryDeltaPercent, value);
    }

    public double AltitudeDeltaMeters
    {
        get => _altitudeDeltaMeters;
        set => SetProperty(ref _altitudeDeltaMeters, value);
    }

    public double SpeedDeltaMps
    {
        get => _speedDeltaMps;
        set => SetProperty(ref _speedDeltaMps, value);
    }

    public double HeadingDeltaDeg
    {
        get => _headingDeltaDeg;
        set => SetProperty(ref _headingDeltaDeg, value);
    }

    public double VerticalSpeedDeltaMps
    {
        get => _verticalSpeedDeltaMps;
        set => SetProperty(ref _verticalSpeedDeltaMps, value);
    }

    public double GpsDistanceDeltaMeters
    {
        get => _gpsDistanceDeltaMeters;
        set => SetProperty(ref _gpsDistanceDeltaMeters, value);
    }

    public double GpsHdopDelta
    {
        get => _gpsHdopDelta;
        set => SetProperty(ref _gpsHdopDelta, value);
    }

    public double BatteryVoltageDeltaVolts
    {
        get => _batteryVoltageDeltaVolts;
        set => SetProperty(ref _batteryVoltageDeltaVolts, value);
    }

    public double BatteryCurrentDeltaAmp
    {
        get => _batteryCurrentDeltaAmp;
        set => SetProperty(ref _batteryCurrentDeltaAmp, value);
    }

    public double RollDeltaDeg
    {
        get => _rollDeltaDeg;
        set => SetProperty(ref _rollDeltaDeg, value);
    }

    public double PitchDeltaDeg
    {
        get => _pitchDeltaDeg;
        set => SetProperty(ref _pitchDeltaDeg, value);
    }

    public double VibrationMagnitudeDelta
    {
        get => _vibrationMagnitudeDelta;
        set => SetProperty(ref _vibrationMagnitudeDelta, value);
    }

    public double LinkQualityDeltaPercent
    {
        get => _linkQualityDeltaPercent;
        set => SetProperty(ref _linkQualityDeltaPercent, value);
    }

    public bool SampleOnFlightModeChange
    {
        get => _sampleOnFlightModeChange;
        set => SetProperty(ref _sampleOnFlightModeChange, value);
    }

    public bool SampleOnArmedStateChange
    {
        get => _sampleOnArmedStateChange;
        set => SetProperty(ref _sampleOnArmedStateChange, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Voice command configuration
/// </summary>
public class VoiceCommandConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _enabled = true;
    private double _confidenceThreshold = 0.6;
    private string _language = "id-ID";

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public double ConfidenceThreshold
    {
        get => _confidenceThreshold;
        set => SetProperty(ref _confidenceThreshold, value);
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Model configurations for different AI tasks
/// </summary>
public class AIModelsConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _telemetryAnalysis = "google/gemini-2.5-flash-lite";
    private string _anomalyDetection = "google/gemini-2.5-flash-lite";
    private string _naturalLanguageChat = "google/gemini-2.5-flash-lite";
    private string _maintenancePrediction = "deepseek/deepseek-v4";
    private string _performanceScoring = "deepseek/deepseek-v4";
    private string _batteryPrediction = "deepseek/deepseek-v4";
    private string _flightSessionSummary = "google/gemini-2.5-flash-lite";
    private string _voiceIntent = "google/gemini-2.5-flash-lite";
    private string _fallback = "openrouter/free";

    public string TelemetryAnalysis
    {
        get => _telemetryAnalysis;
        set => SetProperty(ref _telemetryAnalysis, value);
    }

    public string AnomalyDetection
    {
        get => _anomalyDetection;
        set => SetProperty(ref _anomalyDetection, value);
    }

    public string NaturalLanguageChat
    {
        get => _naturalLanguageChat;
        set => SetProperty(ref _naturalLanguageChat, value);
    }

    public string MaintenancePrediction
    {
        get => _maintenancePrediction;
        set => SetProperty(ref _maintenancePrediction, value);
    }

    public string PerformanceScoring
    {
        get => _performanceScoring;
        set => SetProperty(ref _performanceScoring, value);
    }

    public string BatteryPrediction
    {
        get => _batteryPrediction;
        set => SetProperty(ref _batteryPrediction, value);
    }

    public string FlightSessionSummary
    {
        get => _flightSessionSummary;
        set => SetProperty(ref _flightSessionSummary, value);
    }

    public string VoiceIntent
    {
        get => _voiceIntent;
        set => SetProperty(ref _voiceIntent, value);
    }

    public string Fallback
    {
        get => _fallback;
        set => SetProperty(ref _fallback, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Analysis configuration for telemetry analysis
/// </summary>
public class AIAnalysisConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _intervalSeconds = 30;
    private int _bufferSeconds = 30;
    private double _minConfidence = 0.7;

    /// <summary>
    /// Interval between analysis runs in seconds
    /// </summary>
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set => SetProperty(ref _intervalSeconds, value);
    }

    /// <summary>
    /// Buffer size for telemetry data in seconds
    /// </summary>
    public int BufferSeconds
    {
        get => _bufferSeconds;
        set => SetProperty(ref _bufferSeconds, value);
    }

    /// <summary>
    /// Minimum confidence threshold for analysis results
    /// </summary>
    public double MinConfidence
    {
        get => _minConfidence;
        set => SetProperty(ref _minConfidence, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Cache configuration for AI responses
/// </summary>
public class AICacheConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _enabled = true;
    private int _ttlSeconds = 300;
    private int _maxSizeMB = 50;

    /// <summary>
    /// Enable or disable caching
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    /// <summary>
    /// Time-to-live for cached responses in seconds
    /// </summary>
    public int TTLSeconds
    {
        get => _ttlSeconds;
        set => SetProperty(ref _ttlSeconds, value);
    }

    /// <summary>
    /// Maximum cache size in megabytes
    /// </summary>
    public int MaxSizeMB
    {
        get => _maxSizeMB;
        set => SetProperty(ref _maxSizeMB, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Anomaly detection configuration
/// </summary>
public class AnomalyDetectionConfig : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _ruleBasedEnabled = true;
    private bool _statisticalEnabled = true;
    private bool _aiEnabled = true;
    private bool _autoResponseEnabled = false;
    private AnomalyThresholds _thresholds = new();

    /// <summary>
    /// Enable rule-based anomaly detection
    /// </summary>
    public bool RuleBasedEnabled
    {
        get => _ruleBasedEnabled;
        set => SetProperty(ref _ruleBasedEnabled, value);
    }

    /// <summary>
    /// Enable statistical anomaly detection
    /// </summary>
    public bool StatisticalEnabled
    {
        get => _statisticalEnabled;
        set => SetProperty(ref _statisticalEnabled, value);
    }

    /// <summary>
    /// Enable AI-based anomaly detection
    /// </summary>
    public bool AIEnabled
    {
        get => _aiEnabled;
        set => SetProperty(ref _aiEnabled, value);
    }

    /// <summary>
    /// Enable automatic response to detected anomalies
    /// </summary>
    public bool AutoResponseEnabled
    {
        get => _autoResponseEnabled;
        set => SetProperty(ref _autoResponseEnabled, value);
    }

    /// <summary>
    /// Threshold values for anomaly detection
    /// </summary>
    public AnomalyThresholds Thresholds
    {
        get => _thresholds;
        set => SetProperty(ref _thresholds, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Threshold values for anomaly detection
/// </summary>
public class AnomalyThresholds : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _batteryCritical = 15;
    private double _batteryWarning = 30;
    private double _vibrationHigh = 60;
    private double _vibrationCritical = 100;
    private int _gpsMinSatellites = 6;
    private double _windMaxSpeed = 10;

    // Rule-based detector thresholds
    private int _gpsLostThreshold = 5;
    private int _gpsWarningThreshold = 7;
    private double _altitudeCritical = 120;
    private double _highSpeedThreshold = 15;
    private double _rapidDescentThreshold = -3;
    private double _highVibrationThreshold = 30;
    private double _lowBatteryDrainMinutes = 5;

    /// <summary>
    /// Battery level below which is considered critical (percentage)
    /// </summary>
    public double BatteryCritical
    {
        get => _batteryCritical;
        set => SetProperty(ref _batteryCritical, value);
    }

    /// <summary>
    /// Battery level below which is considered warning (percentage)
    /// </summary>
    public double BatteryWarning
    {
        get => _batteryWarning;
        set => SetProperty(ref _batteryWarning, value);
    }

    /// <summary>
    /// Vibration level above which is considered high
    /// </summary>
    public double VibrationHigh
    {
        get => _vibrationHigh;
        set => SetProperty(ref _vibrationHigh, value);
    }

    /// <summary>
    /// Vibration level above which is considered critical
    /// </summary>
    public double VibrationCritical
    {
        get => _vibrationCritical;
        set => SetProperty(ref _vibrationCritical, value);
    }

    /// <summary>
    /// Minimum number of GPS satellites for good fix
    /// </summary>
    public int GpsMinSatellites
    {
        get => _gpsMinSatellites;
        set => SetProperty(ref _gpsMinSatellites, value);
    }

    /// <summary>
    /// Wind speed above which is considered excessive (m/s)
    /// </summary>
    public double WindMaxSpeed
    {
        get => _windMaxSpeed;
        set => SetProperty(ref _windMaxSpeed, value);
    }

    /// <summary>
    /// Z-score threshold used by statistical anomaly detector.
    /// When an absolute z-score for any monitored feature exceeds this
    /// value, an anomaly is reported.
    /// </summary>
    public double ZScoreThreshold
    {
        get => _zScoreThreshold;
        set => SetProperty(ref _zScoreThreshold, value);
    }
    private double _zScoreThreshold = 2.0;

    /// <summary>
    /// GPS satellites below which is considered lost
    /// </summary>
    public int GpsLostThreshold
    {
        get => _gpsLostThreshold;
        set => SetProperty(ref _gpsLostThreshold, value);
    }

    /// <summary>
    /// GPS satellites below which triggers a warning
    /// </summary>
    public int GpsWarningThreshold
    {
        get => _gpsWarningThreshold;
        set => SetProperty(ref _gpsWarningThreshold, value);
    }

    /// <summary>
    /// Altitude above which is considered critical (meters)
    /// </summary>
    public double AltitudeCritical
    {
        get => _altitudeCritical;
        set => SetProperty(ref _altitudeCritical, value);
    }

    /// <summary>
    /// Speed above which is considered high (m/s)
    /// </summary>
    public double HighSpeedThreshold
    {
        get => _highSpeedThreshold;
        set => SetProperty(ref _highSpeedThreshold, value);
    }

    /// <summary>
    /// Vertical speed below which is considered rapid descent (m/s, negative value)
    /// </summary>
    public double RapidDescentThreshold
    {
        get => _rapidDescentThreshold;
        set => SetProperty(ref _rapidDescentThreshold, value);
    }

    /// <summary>
    /// Vibration sum above which is considered high
    /// </summary>
    public double HighVibrationThreshold
    {
        get => _highVibrationThreshold;
        set => SetProperty(ref _highVibrationThreshold, value);
    }

    /// <summary>
    /// Minutes until battery depletion that triggers low battery drain warning
    /// </summary>
    public double LowBatteryDrainMinutes
    {
        get => _lowBatteryDrainMinutes;
        set => SetProperty(ref _lowBatteryDrainMinutes, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
