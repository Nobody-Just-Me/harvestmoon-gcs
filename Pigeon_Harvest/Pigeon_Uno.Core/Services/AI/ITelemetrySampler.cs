namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// Decides whether an incoming telemetry snapshot should be forwarded
/// to PIA pipeline components.
/// </summary>
public interface ITelemetrySampler
{
    /// <summary>
    /// Returns true when snapshot should be sampled.
    /// </summary>
    bool ShouldSample(TelemetrySnapshot snapshot);

    /// <summary>
    /// Clears sampler state (used on reconnect/disconnect).
    /// </summary>
    void Reset();
}
