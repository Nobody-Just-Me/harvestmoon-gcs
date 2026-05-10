using System;
using Pigeon_Uno.Core.Diagnostics;

namespace Pigeon_Uno.Core.Diagnostics;

/// <summary>
/// Extension methods for IDiagnosticLogger to provide additional logging capabilities
/// </summary>
public static class DiagnosticLoggerExtensions
{
    /// <summary>
    /// Log an error message
    /// </summary>
    public static void LogError(this IDiagnosticLogger logger, string message)
    {
        logger.LogTelemetryEvent(DateTime.Now, $"ERROR: {message}");
    }

    /// <summary>
    /// Log a MAVLink packet
    /// </summary>
    public static void LogPacket(this IDiagnosticLogger logger, object packet, string direction)
    {
        logger.LogTelemetryEvent(DateTime.Now, $"PACKET [{direction}]: {packet?.GetType().Name ?? "null"}");
    }

    /// <summary>
    /// Log a MAVLink command
    /// </summary>
    public static void LogCommand(this IDiagnosticLogger logger, string command, object parameters)
    {
        logger.LogTelemetryEvent(DateTime.Now, $"COMMAND: {command} - {parameters}");
    }

    /// <summary>
    /// Get diagnostic summary
    /// </summary>
    public static string GetSummary(this IDiagnosticLogger logger)
    {
        return logger.GetLogSummary();
    }
}
