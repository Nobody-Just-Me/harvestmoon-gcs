using System;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

public interface IVoiceCommandService
{
    bool IsAvailable { get; }
    bool IsListening { get; }
    float ConfidenceThreshold { get; set; }
    string AvailabilityReason { get; }
    string? LastError { get; }

    event EventHandler<VoiceCommandResult>? CommandRecognized;
    event EventHandler<string>? RecognitionError;

    Task StartListeningAsync();
    Task StopListeningAsync();
    Task<VoiceCommandResult> ProcessTextAsync(string text, CancellationToken ct = default);

    void UpdateTelemetrySnapshot(TelemetrySnapshot snapshot);
}
