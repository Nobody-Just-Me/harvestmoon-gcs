using System;
using Serilog;
using Serilog.Events;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Serilog implementation of ILoggingService
/// </summary>
public class SerilogLoggingService : ILoggingService
{
    private readonly ILogger _logger;

    public SerilogLoggingService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogError(string message, Exception? exception = null)
    {
        if (exception != null)
        {
            _logger.Error(exception, message);
        }
        else
        {
            _logger.Error(message);
        }
    }

    public void LogWarning(string message)
    {
        _logger.Warning(message);
    }

    public void LogInfo(string message)
    {
        _logger.Information(message);
    }

    public void LogDebug(string message)
    {
        _logger.Debug(message);
    }

    public void LogError(string message, string component, Exception? exception = null)
    {
        if (exception != null)
        {
            _logger.ForContext("Component", component).Error(exception, message);
        }
        else
        {
            _logger.ForContext("Component", component).Error(message);
        }
    }

    public void LogWarning(string message, string component)
    {
        _logger.ForContext("Component", component).Warning(message);
    }

    public void LogInfo(string message, string component)
    {
        _logger.ForContext("Component", component).Information(message);
    }

    public void LogDebug(string message, string component)
    {
        _logger.ForContext("Component", component).Debug(message);
    }
}
