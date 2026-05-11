using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Fallback voice recognition service for platforms without native speech-to-text.
/// Keeps API surface available while reporting unsupported capability explicitly.
/// </summary>
public sealed class NoOpVoiceRecognitionService : IVoiceRecognitionService
{
    public bool IsAvailable => false;
    public string AvailabilityReason => "Voice recognition engine tidak tersedia di platform ini.";
    public string? LastError { get; private set; }
    public string Language { get; set; } = "id-ID";
    public bool IsListening => false;

    public event VoiceCommandEventHandler? CommandRecognized;
    public event VoiceRecognitionErrorEventHandler? RecognitionError;

    public Task StartListeningAsync()
    {
        LastError = AvailabilityReason;
        RecognitionError?.Invoke(this, LastError);
        throw new InvalidOperationException(LastError);
    }

    public void StopListening()
    {
        // Intentionally no-op.
    }

    /// <summary>
    /// Test helper hook for manual command injection when needed.
    /// </summary>
    public void InjectRecognizedText(string text, float confidence = 0.6f)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CommandRecognized?.Invoke(this, new VoiceCommandEventArgs
        {
            Command = text,
            RawText = text,
            Confidence = Math.Clamp(confidence, 0f, 1f)
        });
    }
}
