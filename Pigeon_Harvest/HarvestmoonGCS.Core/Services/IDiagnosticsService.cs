using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for system diagnostics and health monitoring
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Gets current system health status
    /// </summary>
    SystemHealthStatus GetSystemHealth();

    /// <summary>
    /// Gets diagnostic information
    /// </summary>
    DiagnosticInfo GetDiagnosticInfo();

    /// <summary>
    /// Gets performance metrics
    /// </summary>
    PerformanceMetrics GetPerformanceMetrics();

    /// <summary>
    /// Gets recent error log entries
    /// </summary>
    List<ErrorLogEntry> GetRecentErrors(int count);

    /// <summary>
    /// Exports diagnostic logs to file
    /// </summary>
    Task<string> ExportLogsAsync();

    /// <summary>
    /// Clears old log files
    /// </summary>
    Task ClearOldLogsAsync(int daysToKeep);
}
