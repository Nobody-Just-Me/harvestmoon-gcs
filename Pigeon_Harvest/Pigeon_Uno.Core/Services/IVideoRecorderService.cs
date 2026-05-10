using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Video recording service interface
/// Platform-specific implementations for video recording
/// </summary>
public interface IVideoRecorderService
{
    // Properties
    bool IsRecording { get; }
    string CurrentRecordingPath { get; }
    TimeSpan RecordingDuration { get; }
    
    // Events
    event EventHandler<bool> RecordingStatusChanged;
    event EventHandler<string> RecordingError;
    event EventHandler<TimeSpan> RecordingDurationUpdated;
    
    // Methods
    Task<bool> StartRecordingAsync(string outputPath, int width = 1920, int height = 1080, int fps = 30);
    Task StopRecordingAsync();
    Task<bool> IsRecordingAvailableAsync();
    Task<string> GetDefaultRecordingPathAsync();
}
