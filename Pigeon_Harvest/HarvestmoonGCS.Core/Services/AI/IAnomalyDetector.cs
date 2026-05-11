using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Interface for anomaly detectors (RuleBased, Statistical, AI)
/// </summary>
public interface IAnomalyDetector
{
    /// <summary>
    /// Name of the detector for logging/debugging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates a single telemetry snapshot for anomalies
    /// </summary>
    /// <param name="snapshot">Current telemetry snapshot</param>
    /// <returns>List of detected anomalies (empty if none)</returns>
    IList<Anomaly> Evaluate(TelemetrySnapshot snapshot);

    /// <summary>
    /// Evaluates a batch of telemetry snapshots for anomalies
    /// Used by statistical and AI detectors that need historical context
    /// </summary>
    /// <param name="snapshots">Collection of telemetry snapshots</param>
    /// <returns>List of detected anomalies (empty if none)</returns>
    Task<IList<Anomaly>> EvaluateBatchAsync(IList<TelemetrySnapshot> snapshots);
}
