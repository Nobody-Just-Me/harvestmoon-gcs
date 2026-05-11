using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Service that coordinates multi-layer anomaly detection
/// </summary>
public interface IAnomalyDetectionService
{
    /// <summary>
    /// Event raised when anomalies are detected
    /// </summary>
    event EventHandler<AnomaliesDetectedEventArgs>? AnomaliesDetected;

    /// <summary>
    /// Starts the anomaly detection service with timer-based detection
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the anomaly detection service
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Processes a real-time telemetry snapshot (Layer 1 - RuleBased)
    /// Called on every telemetry update
    /// </summary>
    Task ProcessSnapshotAsync(TelemetrySnapshot snapshot);

    /// <summary>
    /// Gets whether the service is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the last detected anomalies
    /// </summary>
    IReadOnlyList<Anomaly> LastDetectedAnomalies { get; }
}

/// <summary>
/// Event args for the AnomaliesDetected event
/// </summary>
public class AnomaliesDetectedEventArgs : EventArgs
{
    public IReadOnlyList<Anomaly> Anomalies { get; }
    public string SourceLayer { get; }
    public DateTime Timestamp { get; }

    public AnomaliesDetectedEventArgs(IReadOnlyList<Anomaly> anomalies, string sourceLayer)
    {
        Anomalies = anomalies ?? new List<Anomaly>();
        SourceLayer = sourceLayer ?? "Unknown";
        Timestamp = DateTime.UtcNow;
    }
}
