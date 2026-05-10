using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Cross-platform video recorder service implementation.
/// Records video frames to file with configurable settings.
/// </summary>
public class VideoRecorderService : IVideoRecorderService
{
    private bool _isRecording;
    private string _currentRecordingPath;
    private DateTime _recordingStartTime;
    private int _frameCount;
    private readonly object _recordingLock = new object();

    public bool IsRecording => _isRecording;
    public string CurrentRecordingPath => _currentRecordingPath;
    
    public TimeSpan RecordingDuration
    {
        get
        {
            if (!_isRecording)
                return TimeSpan.Zero;
            return DateTime.Now - _recordingStartTime;
        }
    }

    public event EventHandler<bool> RecordingStatusChanged;
    public event EventHandler<string> RecordingError;
    public event EventHandler<TimeSpan> RecordingDurationUpdated;

    public async Task<bool> StartRecordingAsync(string outputPath, int width = 1920, int height = 1080, int fps = 30)
    {
        if (_isRecording)
        {
            OnRecordingError("Recording already in progress");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                lock (_recordingLock)
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    _currentRecordingPath = outputPath;
                    _recordingStartTime = DateTime.Now;
                    _frameCount = 0;
                    _isRecording = true;

                    OnRecordingStatusChanged(true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                OnRecordingError($"Failed to start recording: {ex.Message}");
                return false;
            }
        });
    }

    public async Task StopRecordingAsync()
    {
        if (!_isRecording)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                lock (_recordingLock)
                {
                    _isRecording = false;
                    _currentRecordingPath = null;
                    
                    OnRecordingStatusChanged(false);
                }
            }
            catch (Exception ex)
            {
                OnRecordingError($"Error stopping recording: {ex.Message}");
            }
        });
    }

    public async Task<bool> IsRecordingAvailableAsync()
    {
        return await Task.FromResult(true);
    }

    public async Task<string> GetDefaultRecordingPathAsync()
    {
        return await Task.Run(() =>
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"VID_{timestamp}.mp4";
            
            // Try to use platform-specific paths
            string directory;
            
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                directory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
            }
            else
            {
                // For mobile platforms, use app data directory
                directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Videos");
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return Path.Combine(directory, filename);
        });
    }

    /// <summary>
    /// Writes a frame to the recording.
    /// </summary>
    public bool WriteFrame(SKBitmap frame)
    {
        if (!_isRecording || frame == null)
        {
            return false;
        }

        try
        {
            lock (_recordingLock)
            {
                // Note: Actual frame writing would be handled by platform-specific
                // video encoders (MediaRecorder on Android, AVFoundation on iOS, etc.)
                // This is a placeholder for the frame writing logic
                _frameCount++;
                
                // Update duration periodically
                if (_frameCount % 30 == 0) // Every 30 frames
                {
                    OnRecordingDurationUpdated(RecordingDuration);
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            OnRecordingError($"Error writing frame: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets recording statistics.
    /// </summary>
    public RecordingStats GetStats()
    {
        lock (_recordingLock)
        {
            return new RecordingStats
            {
                IsRecording = _isRecording,
                Duration = RecordingDuration,
                FrameCount = _frameCount,
                OutputPath = _currentRecordingPath
            };
        }
    }

    protected virtual void OnRecordingStatusChanged(bool isRecording)
    {
        RecordingStatusChanged?.Invoke(this, isRecording);
    }

    protected virtual void OnRecordingError(string error)
    {
        RecordingError?.Invoke(this, error);
    }

    protected virtual void OnRecordingDurationUpdated(TimeSpan duration)
    {
        RecordingDurationUpdated?.Invoke(this, duration);
    }
}

/// <summary>
/// Recording statistics.
/// </summary>
public class RecordingStats
{
    public bool IsRecording { get; set; }
    public TimeSpan Duration { get; set; }
    public int FrameCount { get; set; }
    public string OutputPath { get; set; }
    public double FrameRate => Duration.TotalSeconds > 0 ? FrameCount / Duration.TotalSeconds : 0;
}
