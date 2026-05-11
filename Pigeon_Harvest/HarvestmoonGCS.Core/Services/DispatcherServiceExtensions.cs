using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Extension methods for IDispatcherService
/// </summary>
public static class DispatcherServiceExtensions
{
    /// <summary>
    /// Execute an action on the UI thread
    /// </summary>
    public static async Task ExecuteOnUIThread(this IDispatcherService dispatcher, Action action)
    {
        await dispatcher.RunOnUIThreadAsync(action);
    }
}
