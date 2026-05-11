using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Exceptions;

namespace HarvestmoonGCS.Core.Helpers;

/// <summary>
/// Helper class for consistent error handling patterns
/// </summary>
public static class ErrorHandler
{
    /// <summary>
    /// Execute an action with error handling and logging
    /// </summary>
    public static void Execute(Action action, ILoggingService logger, string component, string operationName)
    {
        try
        {
            action();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            throw new PigeonException($"Unexpected error in operation '{operationName}'", ex);
        }
    }

    /// <summary>
    /// Execute an async action with error handling and logging
    /// </summary>
    public static async Task ExecuteAsync(Func<Task> action, ILoggingService logger, string component, string operationName)
    {
        try
        {
            await action();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            throw new PigeonException($"Unexpected error in operation '{operationName}'", ex);
        }
    }

    /// <summary>
    /// Execute a function with error handling and logging
    /// </summary>
    public static T Execute<T>(Func<T> func, ILoggingService logger, string component, string operationName)
    {
        try
        {
            return func();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            throw new PigeonException($"Unexpected error in operation '{operationName}'", ex);
        }
    }

    /// <summary>
    /// Execute an async function with error handling and logging
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, ILoggingService logger, string component, string operationName)
    {
        try
        {
            return await func();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            throw new PigeonException($"Unexpected error in operation '{operationName}'", ex);
        }
    }

    /// <summary>
    /// Execute an action with error handling, logging, and a default return value on error
    /// </summary>
    public static T ExecuteWithDefault<T>(Func<T> func, T defaultValue, ILoggingService logger, string component, string operationName)
    {
        try
        {
            return func();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            return defaultValue;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            return defaultValue;
        }
    }

    /// <summary>
    /// Execute an async function with error handling, logging, and a default return value on error
    /// </summary>
    public static async Task<T> ExecuteWithDefaultAsync<T>(Func<Task<T>> func, T defaultValue, ILoggingService logger, string component, string operationName)
    {
        try
        {
            return await func();
        }
        catch (PigeonException ex)
        {
            logger.LogError($"Operation '{operationName}' failed: {ex.Message}", component, ex);
            return defaultValue;
        }
        catch (Exception ex)
        {
            logger.LogError($"Unexpected error in operation '{operationName}': {ex.Message}", component, ex);
            return defaultValue;
        }
    }
}
