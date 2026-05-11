using System;

namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// System diagnostic information
/// </summary>
public class DiagnosticInfo
{
    public string AppVersion { get; set; } = "";
    public string Platform { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public long MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ThreadCount { get; set; }
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealthStatus
{
    public HealthLevel OverallHealth { get; set; }
    public DateTime LastCheck { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Health level enumeration
/// </summary>
public enum HealthLevel
{
    Unknown,
    Good,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Performance metrics
/// </summary>
public class PerformanceMetrics
{
    public double AverageFps { get; set; }
    public int ActiveConnections { get; set; }
    public int TelemetryMessagesPerSecond { get; set; }
    public long NetworkBytesReceived { get; set; }
    public long NetworkBytesSent { get; set; }
}

/// <summary>
/// Error log entry
/// </summary>
public class ErrorLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
}
