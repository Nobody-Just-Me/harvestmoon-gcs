using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Uno Platform implementation of IErrorDisplayService using ContentDialog
/// </summary>
public class UnoErrorDisplayService : IErrorDisplayService
{
    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = CreateStyledDialog(title, message, "Error");
        await dialog.ShowAsync();
    }

    public async Task ShowWarningAsync(string title, string message)
    {
        var dialog = CreateStyledDialog(title, message, "Warning");
        await dialog.ShowAsync();
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = CreateStyledDialog(title, message, "Info");
        await dialog.ShowAsync();
    }

    public async Task ShowErrorAsync(string title, string message, Exception exception)
    {
        var fullMessage = FormatErrorMessage(message, exception);
        var dialog = CreateStyledDialog(title, fullMessage, "Error");
        await dialog.ShowAsync();
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = CreateMessageContent(message),
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private ContentDialog CreateStyledDialog(string title, string message, string type)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = CreateMessageContent(message),
            CloseButtonText = "OK",
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        // Style the dialog based on type
        switch (type)
        {
            case "Error":
                dialog.RequestedTheme = ElementTheme.Dark;
                break;
            case "Warning":
                dialog.RequestedTheme = ElementTheme.Light;
                break;
            case "Info":
                dialog.RequestedTheme = ElementTheme.Default;
                break;
        }

        return dialog;
    }

    private FrameworkElement CreateMessageContent(string message)
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            Margin = new Thickness(0, 10, 0, 0)
        };

        scrollViewer.Content = textBlock;
        return scrollViewer;
    }

    private string FormatErrorMessage(string message, Exception exception)
    {
        var formattedMessage = message;

        if (exception != null)
        {
            formattedMessage += $"\n\n📋 Error Details:\n{exception.Message}";

            // Add inner exception details if available
            if (exception.InnerException != null)
            {
                formattedMessage += $"\n\n🔍 Inner Exception:\n{exception.InnerException.Message}";
            }

#if DEBUG
            // In debug mode, show stack trace
            formattedMessage += $"\n\n🔧 Stack Trace (Debug):\n{exception.StackTrace}";
#endif

            // Add helpful suggestions based on exception type
            formattedMessage += GetHelpfulSuggestions(exception);
        }

        return formattedMessage;
    }

    private string GetHelpfulSuggestions(Exception exception)
    {
        var suggestions = "\n\n💡 Suggestions:";

        switch (exception)
        {
            case System.Net.Sockets.SocketException:
                suggestions += "\n• Check network connection\n• Verify IP address and port\n• Ensure firewall allows connection";
                break;
            case System.IO.FileNotFoundException:
                suggestions += "\n• Verify file path is correct\n• Check file permissions\n• Ensure file exists";
                break;
            case System.UnauthorizedAccessException:
                suggestions += "\n• Run application as administrator\n• Check file/folder permissions\n• Ensure resource is not in use";
                break;
            case System.TimeoutException:
                suggestions += "\n• Check connection stability\n• Increase timeout value\n• Verify device is responding";
                break;
            case System.ArgumentException:
                suggestions += "\n• Check input parameters\n• Verify data format\n• Ensure values are within valid range";
                break;
            case System.InvalidOperationException:
                suggestions += "\n• Check operation sequence\n• Verify system state\n• Ensure prerequisites are met";
                break;
            default:
                suggestions += "\n• Restart the application\n• Check system resources\n• Contact support if problem persists";
                break;
        }

        return suggestions;
    }
}
