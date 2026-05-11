using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// Hybrid DialogService implementation for Uno Platform
    /// Uses ContentDialog for Desktop, platform-specific implementations for mobile
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Shows an alert dialog with OK button
        /// </summary>
        public async Task ShowAlertAsync(string message, string title = "Alert")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = GetXamlRoot()
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        public async Task<bool> ShowConfirmAsync(string message, string title = "Confirm")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = GetXamlRoot()
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Shows a custom dialog and returns the result
        /// </summary>
        public async Task<T?> ShowDialogAsync<T>() where T : class
        {
            // Create instance of the dialog
            var dialog = Activator.CreateInstance<T>();
            
            if (dialog is ContentDialog contentDialog)
            {
                contentDialog.XamlRoot = GetXamlRoot();
                var result = await contentDialog.ShowAsync();
                
                // If dialog has a Result property, return it
                var resultProperty = typeof(T).GetProperty("Result");
                if (resultProperty != null && result == ContentDialogResult.Primary)
                {
                    return resultProperty.GetValue(dialog) as T;
                }
                
                return result == ContentDialogResult.Primary ? dialog : null;
            }
            
            throw new InvalidOperationException($"Type {typeof(T).Name} must be a ContentDialog");
        }

        /// <summary>
        /// Shows a custom dialog with a parameter and returns the result
        /// </summary>
        public async Task<TResult?> ShowDialogAsync<T, TParam, TResult>(TParam parameter) 
            where T : class 
            where TResult : class
        {
            // Create instance with parameter
            var dialog = Activator.CreateInstance(typeof(T), parameter);
            
            if (dialog is ContentDialog contentDialog)
            {
                contentDialog.XamlRoot = GetXamlRoot();
                var result = await contentDialog.ShowAsync();
                
                // If dialog has a Result property, return it
                var resultProperty = typeof(T).GetProperty("Result");
                if (resultProperty != null && result == ContentDialogResult.Primary)
                {
                    return resultProperty.GetValue(dialog) as TResult;
                }
                
                return null;
            }
            
            throw new InvalidOperationException($"Type {typeof(T).Name} must be a ContentDialog");
        }

        /// <summary>
        /// Gets the XamlRoot from the main window
        /// </summary>
        private Microsoft.UI.Xaml.XamlRoot? GetXamlRoot()
        {
            return App.MainWindow?.Content?.XamlRoot;
        }
    }
}
