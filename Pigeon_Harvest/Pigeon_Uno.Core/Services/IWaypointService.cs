using System.Collections.Generic;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Interface for managing waypoints in a mission
/// </summary>
public interface IWaypointService
{
    /// <summary>
    /// Gets all waypoints in the current mission
    /// </summary>
    Task<List<WaypointData>> GetWaypointsAsync();

    /// <summary>
    /// Gets a specific waypoint by sequence number
    /// </summary>
    Task<WaypointData?> GetWaypointAsync(int sequence);

    /// <summary>
    /// Adds a new waypoint to the mission
    /// </summary>
    Task AddWaypointAsync(WaypointData waypoint);

    /// <summary>
    /// Updates an existing waypoint
    /// </summary>
    Task UpdateWaypointAsync(WaypointData waypoint);

    /// <summary>
    /// Removes a waypoint by sequence number
    /// </summary>
    Task RemoveWaypointAsync(int sequence);

    /// <summary>
    /// Clears all waypoints from the mission
    /// </summary>
    Task ClearWaypointsAsync();

    /// <summary>
    /// Gets the current waypoint the vehicle is heading to
    /// </summary>
    Task<WaypointData?> GetCurrentWaypointAsync();

    /// <summary>
    /// Sets the current waypoint the vehicle should go to
    /// </summary>
    Task SetCurrentWaypointAsync(int sequence);

    /// <summary>
    /// Gets the number of waypoints in the mission
    /// </summary>
    int WaypointCount { get; }

    /// <summary>
    /// Event fired when waypoints are added, removed, or modified
    /// </summary>
    event EventHandler? WaypointsChanged;

    /// <summary>
    /// Loads waypoints from a file
    /// </summary>
    Task LoadWaypointsFromFileAsync(string filePath);

    /// <summary>
    /// Saves waypoints to a file
    /// </summary>
    Task SaveWaypointsToFileAsync(string filePath);
}
