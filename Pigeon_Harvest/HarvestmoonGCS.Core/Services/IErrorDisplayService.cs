using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for displaying error messages to the user
/// </summary>
public interface IErrorDisplayService
{
    /// <summary>
    /// Display an error message to the user
    /// </summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Display a warning message to the user
    /// </summary>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// Display an informational message to the user
    /// </summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Display an error message with exception details
    /// </summary>
    Task ShowErrorAsync(string title, string message, Exception exception);

    /// <summary>
    /// Ask the user a yes/no question
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
