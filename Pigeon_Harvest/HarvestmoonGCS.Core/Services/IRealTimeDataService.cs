using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Interface for real-time data service that receives MAVLink telemetry via WebSocket
/// </summary>
public interface IRealTimeDataService : IDisposable
{
    /// <summary>
    /// Event fired when telemetry data is received
    /// </summary>
    event EventHandler<TelemetryData>? TelemetryReceived;

    /// <summary>
    /// Event fired when connection status changes
    /// </summary>
    event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Event fired when an error occurs
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Gets whether the service is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to the WebSocket server
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// Disconnect from the WebSocket server
    /// </summary>
    Task DisconnectAsync();
}
