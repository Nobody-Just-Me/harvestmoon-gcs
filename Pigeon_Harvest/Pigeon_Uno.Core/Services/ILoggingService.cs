using System;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Interface for logging service
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Log an error message
    /// </summary>
    void LogError(string message, Exception? exception = null);

    /// <summary>
    /// Log a warning message
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Log an informational message
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Log a debug message
    /// </summary>
    void LogDebug(string message);

    /// <summary>
    /// Log an error with context
    /// </summary>
    void LogError(string message, string component, Exception? exception = null);

    /// <summary>
    /// Log a warning with context
    /// </summary>
    void LogWarning(string message, string component);

    /// <summary>
    /// Log an informational message with context
    /// </summary>
    void LogInfo(string message, string component);

    /// <summary>
    /// Log a debug message with context
    /// </summary>
    void LogDebug(string message, string component);
}
