using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services
{
    /// <summary>
    /// Interface for showing dialogs across all platforms
    /// Supports hybrid approach: native dialogs for simple alerts, custom dialogs for complex forms
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an alert dialog with OK button
        /// </summary>
        Task ShowAlertAsync(string message, string title = "Alert");

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        /// <returns>True if user clicked Yes, false otherwise</returns>
        Task<bool> ShowConfirmAsync(string message, string title = "Confirm");

        /// <summary>
        /// Shows a custom dialog and returns the result
        /// </summary>
        /// <typeparam name="T">Type of dialog to show (must be a class)</typeparam>
        /// <returns>Result from the dialog, or null if cancelled</returns>
        Task<T?> ShowDialogAsync<T>() where T : class;

        /// <summary>
        /// Shows a custom dialog with a parameter and returns the result
        /// </summary>
        /// <typeparam name="T">Type of dialog to show</typeparam>
        /// <typeparam name="TParam">Type of parameter to pass to dialog</typeparam>
        /// <typeparam name="TResult">Type of result returned from dialog</typeparam>
        /// <param name="parameter">Parameter to pass to dialog</param>
        /// <returns>Result from the dialog, or default if cancelled</returns>
        Task<TResult?> ShowDialogAsync<T, TParam, TResult>(TParam parameter) 
            where T : class 
            where TResult : class;
    }
}
