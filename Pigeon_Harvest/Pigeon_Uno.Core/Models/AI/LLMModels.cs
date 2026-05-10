namespace Pigeon_Uno.Core.Models.AI;

// ── LLM Result ──────────────────────────────────────────────────────────────

/// <summary>Hasil dari panggilan LLM, termasuk metadata untuk debugging</summary>
public class LLMResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public string ModelUsed { get; init; } = string.Empty;
    public bool WasFallback { get; init; }
    public bool WasCached { get; init; }
    public int LatencyMs { get; init; }
    public string? ErrorMessage { get; init; }

    public static LLMResult Ok(string text, string model, bool cached = false,
        bool fallback = false, int latencyMs = 0)
        => new() { Success = true, Text = text, ModelUsed = model,
                   WasCached = cached, WasFallback = fallback, LatencyMs = latencyMs };

    public static LLMResult Fail(string error, string model = "")
        => new() { Success = false, ErrorMessage = error, ModelUsed = model };
}

// ── Health Status ────────────────────────────────────────────────────────────

public class LLMHealthStatus
{
    public bool IsConnected { get; set; }
    public string PrimaryProvider { get; set; } = string.Empty;
    public string FallbackProvider { get; set; } = string.Empty;
    public string ActiveProvider { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;
    public string FallbackModel { get; set; } = string.Empty;
    public bool CircuitOpen { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime? LastRequestAt { get; set; }
    public DateTime? CircuitOpenUntil { get; set; }
    public double CacheHitRate { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public long FallbackUsageCount { get; set; }
    public bool PrimaryApiKeyConfigured { get; set; }
    public bool FallbackApiKeyConfigured { get; set; }
    public bool FallbackAvailable { get; set; }
    public int LastLatencyMs { get; set; }
    public string LastError { get; set; } = string.Empty;
}

// ── Telemetry Analysis ───────────────────────────────────────────────────────

public class TelemetryAnalysis
{
    public string OverallStatus { get; set; } = "UNKNOWN"; // GOOD / WARNING / CRITICAL
    public string KeyInsights { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> PredictedIssues { get; set; } = new();
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ── Anomaly ──────────────────────────────────────────────────────────────────

public enum AnomalyType
{
    BatteryCritical, BatteryWarning,
    GpsLost, GpsDegraded,
    MotorFailure, VibrationHigh,
    AttitudeUnstable, TemperatureWarning,
    WindExcessive, LinkQualityDegraded,
    GeofenceViolation, PerformanceSuboptimal,
    SensorDrift, StatisticalOutlier
}

public enum AnomalySeverity { Info, Warning, Critical }

public class Anomaly
{
    public AnomalyType Type { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Priority { get; set; }
}

// ── Chat ─────────────────────────────────────────────────────────────────────

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public string? PendingCommand { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ── Maintenance ──────────────────────────────────────────────────────────────

public class MaintenancePrediction
{
    public string Component { get; set; } = string.Empty;
    public double CurrentCondition { get; set; }
    public string PredictedFailureMode { get; set; } = string.Empty;
    public int EstimatedDaysUntilMaintenance { get; set; }
    public string Severity { get; set; } = "low";
    public string RecommendedAction { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

// ── Battery Prediction ───────────────────────────────────────────────────────

public class BatteryPrediction
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CurrentBatteryPercent { get; set; }
    public double EstimatedDrainRatePerMinute { get; set; }
    public double EstimatedRemainingMinutes { get; set; }
    public DateTime EstimatedDepletionAt { get; set; } = DateTime.UtcNow;
    public double HealthScore { get; set; }
    public string Condition { get; set; } = "UNKNOWN"; // GOOD / WARNING / CRITICAL
    public string Recommendation { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class BatteryPredictionMetrics
{
    public int SampleCount { get; set; }
    public double MeanAbsolutePercentageError { get; set; } // MAPE (%)
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

// ── Performance ──────────────────────────────────────────────────────────────

public class PerformanceScore
{
    public double EfficiencyScore { get; set; }
    public double StabilityScore { get; set; }
    public double SafetyScore { get; set; }
    public double SkillScore { get; set; }
    public double TotalScore { get; set; }
    public string Grade { get; set; } = "C";
    public string Feedback { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ── Voice Command ────────────────────────────────────────────────────────────

public enum VoiceCommand
{
    Unknown,
    Arm,
    Disarm,
    Takeoff,
    Land,
    ReturnToLaunch,
    PauseMission,
    ResumeMission,
    EmergencyStop,
    SetAltitude,
    SetSpeed,
    ModeStabilize,
    ModeLoiter,
    ModeAuto,
    ModeGuided,
    ModeCircle,
    ModeFollow,
    ModePoshold,
    ModeAcro,
    StartMission,
    GoToWaypoint,
    NextWaypoint,
    ClearMission,
    HoldPosition,
    SetHome,
    RequestLogs,
    TakePhoto,
    StartRecording,
    StopRecording,
    ZoomIn,
    ZoomOut,
    CenterCamera,
    Status,
    BatteryCheck,
    GpsCheck,
    AltitudeCheck,
    SpeedCheck,
    MissionStatus,
    WaypointStatus,
    CalibrationStatus,
    GimbalDown,
    GimbalUp,
    GimbalForward,
    EnableGeofence,
    DisableGeofence
}

public class VoiceCommandResult
{
    public VoiceCommand Command { get; set; } = VoiceCommand.Unknown;
    public string RawText { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public bool IsValid { get; set; }
    public bool IsExecuted { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ── Chat Context ─────────────────────────────────────────────────────────────

public enum ChatIntent
{
    Unknown,
    StatusQuery,
    AnalysisRequest,
    Command,
    Troubleshooting,
    Learning,
    Confirmation,
    Greeting
}

public class ChatContext
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Language { get; set; } = "id-ID";
    public List<ChatMessage> History { get; set; } = new();
}

// ── Maintenance ──────────────────────────────────────────────────────────────

public enum MaintenanceType
{
    Clean,
    Inspect,
    Repair,
    Replace
}

public class MaintenanceFeatures
{
    public double AvgBatteryVoltage { get; set; }
    public double AvgBatteryDrainRate { get; set; }
    public double AvgVibration { get; set; }
    public double AvgWindSpeed { get; set; }
    public double MaxTemperature { get; set; }
    public int FlightCount { get; set; }
    public double TotalFlightMinutes { get; set; }
}

public class MaintenanceTask
{
    public string Component { get; set; } = string.Empty;
    public MaintenanceType Type { get; set; } = MaintenanceType.Inspect;
    public string Priority { get; set; } = "normal";
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);
    public string RecommendedAction { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class MaintenanceSchedule
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<MaintenanceTask> Tasks { get; set; } = new();
}

// ── Performance ──────────────────────────────────────────────────────────────

public class PerformanceFeedback
{
    public string OverallAssessment { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PerformanceTrend
{
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public double Score { get; set; }
    public string Grade { get; set; } = "C";
}

// ── Research Validation ──────────────────────────────────────────────────────

public class AnomalyEvaluationMetrics
{
    public long TruePositive { get; set; }
    public long FalsePositive { get; set; }
    public long TrueNegative { get; set; }
    public long FalseNegative { get; set; }
    public long SampleCount => TruePositive + FalsePositive + TrueNegative + FalseNegative;
    public double Precision => TruePositive + FalsePositive == 0 ? 0 : (double)TruePositive / (TruePositive + FalsePositive);
    public double Recall => TruePositive + FalseNegative == 0 ? 0 : (double)TruePositive / (TruePositive + FalseNegative);
    public double F1Score
    {
        get
        {
            var precision = Precision;
            var recall = Recall;
            return precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        }
    }

    public double Accuracy => SampleCount == 0 ? 0 : (double)(TruePositive + TrueNegative) / SampleCount;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FlightSessionSummary
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime SessionStart { get; set; } = DateTime.UtcNow;
    public DateTime SessionEnd { get; set; } = DateTime.UtcNow;
    public int SnapshotCount { get; set; }
    public double DurationMinutes { get; set; }
    public double StartBatteryPercent { get; set; }
    public double EndBatteryPercent { get; set; }
    public double BatteryUsedPercent { get; set; }
    public double AvgSpeed { get; set; }
    public double MaxAltitude { get; set; }
    public int AnomalyCount { get; set; }
    public string OverallStatus { get; set; } = "UNKNOWN";
    public string SummaryText { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class ResearchValidationSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long AnomalySampleCount { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double BatteryMape { get; set; }
    public int BatteryMapeSamples { get; set; }
    public double ChatLatencyMs { get; set; }
    public double VoiceLatencyMs { get; set; }
    public double LlmLatencyMs { get; set; }
    public double AnomalyLatencyMs { get; set; }
    public double TelemetryRateHz { get; set; }
}

public class ResearchExportMetadata
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string PrimaryProvider { get; set; } = string.Empty;
    public string FallbackProvider { get; set; } = string.Empty;
    public string ActiveProvider { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;
    public string FallbackModel { get; set; } = string.Empty;
    public bool FallbackAvailable { get; set; }
    public bool CircuitOpen { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double CacheHitRate { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public long FallbackUsageCount { get; set; }
    public int LastLatencyMs { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public class DatasetEvaluationReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Pipeline { get; set; } = "telemetry_log -> pseudo_label -> predicted_from_anomaly_history -> confusion_matrix";
    public string LabelingStrategy { get; set; } = "threshold-based pseudo label";
    public string PredictionSource { get; set; } = "PIA_AnomalyHistory time-window match";
    public int EvaluationWindowSeconds { get; set; } = 5;
    public int TelemetrySampleCount { get; set; }
    public int PredictedPositiveCount { get; set; }
    public int PseudoLabelPositiveCount { get; set; }
    public long TruePositive { get; set; }
    public long FalsePositive { get; set; }
    public long TrueNegative { get; set; }
    public long FalseNegative { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double Accuracy { get; set; }
}

public class CommandAuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string InputText { get; set; } = string.Empty;
    public VoiceCommand Command { get; set; } = VoiceCommand.Unknown;
    public float Confidence { get; set; }
    public bool IsValid { get; set; }
    public bool IsExecuted { get; set; }
    public string ResultMessage { get; set; } = string.Empty;
    public string Source { get; set; } = "voice";
}
