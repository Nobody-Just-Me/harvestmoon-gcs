using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Interface for alert management services
/// </summary>
public interface IAlertManager
{
    /// <summary>
    /// Queues a custom alert with specified priority
    /// </summary>
    Task QueueCustomAlertAsync(string message, AlertPriority priority = AlertPriority.Normal);
}
