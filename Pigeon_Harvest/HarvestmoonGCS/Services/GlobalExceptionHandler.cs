using System;
using HarvestmoonGCS.Core.Services;
using Microsoft.UI.Xaml;

namespace HarvestmoonGCS.Services;

public class GlobalExceptionHandler
{
    private readonly ILoggingService _logger;

    public GlobalExceptionHandler(ILoggingService logger)
    {
        _logger = logger;
        Application.Current.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger.LogError("Unhandled exception occurred", e.Exception);
        
        // Show user-friendly error dialog
        ShowErrorDialog("An unexpected error occurred. Please check the logs for details.");
        
        e.Handled = true;
    }

    private void ShowErrorDialog(string message)
    {
        try
        {
            // Simplified for Uno Platform compatibility
            System.Diagnostics.Debug.WriteLine($"Error: {message}");
        }
        catch
        {
            // Fallback - ignore dialog errors
        }
    }
}
