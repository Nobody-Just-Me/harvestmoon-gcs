using System;
using System.Diagnostics;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Helper for error recovery in event handlers and pipeline stages.
    /// Catches exceptions, logs them, and allows processing to continue.
    /// </summary>
    public static class ErrorRecoveryHelper
    {
        /// <summary>
        /// Executes an action with error recovery.
        /// If the action throws an exception, it is caught, logged, and processing continues.
        /// </summary>
        public static void ExecuteWithRecovery(Action action, string context, IDiagnosticLogger? logger = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                var message = $"[ErrorRecovery] Exception in {context}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}";
                Debug.WriteLine(message);
                logger?.LogFlightDataUpdate("Exception", context, ex.Message);
            }
        }

        /// <summary>
        /// Executes a function with error recovery.
        /// If the function throws an exception, it is caught, logged, and a default value is returned.
        /// </summary>
        public static T ExecuteWithRecovery<T>(Func<T> func, T defaultValue, string context, IDiagnosticLogger? logger = null)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                var message = $"[ErrorRecovery] Exception in {context}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}";
                Debug.WriteLine(message);
                logger?.LogFlightDataUpdate("Exception", context, ex.Message);
                return defaultValue;
            }
        }

        /// <summary>
        /// Wraps an event handler with error recovery.
        /// </summary>
        public static EventHandler<T> WrapEventHandler<T>(EventHandler<T> handler, string context, IDiagnosticLogger? logger = null)
        {
            return (sender, args) =>
            {
                ExecuteWithRecovery(() => handler(sender, args), context, logger);
            };
        }
    }
}
