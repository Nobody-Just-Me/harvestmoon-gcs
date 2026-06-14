using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Cross-platform video recorder service implementation.
/// Records video frames to file with configurable settings.
/// </summary>
public class VideoRecorderService : IVideoRecorderService
{
    private bool _isRecording;
    private string _currentRecordingPath = string.Empty;
    private string _currentFramesDirectory = string.Empty;
    private string _currentManifestPath = string.Empty;
    private StreamWriter? _manifestWriter;
    private DateTime _recordingStartTime;
    private int _frameCount;
    private int _targetFps = 30;
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
                    _targetFps = Math.Clamp(fps, 1, 120);
                    _currentFramesDirectory = Path.Combine(
                        directory ?? Path.GetTempPath(),
                        $"{Path.GetFileNameWithoutExtension(outputPath)}_frames");
                    Directory.CreateDirectory(_currentFramesDirectory);

                    _currentManifestPath = outputPath + ".frames.txt";
                    _manifestWriter?.Dispose();
                    _manifestWriter = new StreamWriter(new FileStream(_currentManifestPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        AutoFlush = true
                    };
                    _manifestWriter.WriteLine("# HarvestmoonGCS minimal video recording");
                    _manifestWriter.WriteLine("# This manifest lists captured encoded frames in capture order.");
                    _manifestWriter.WriteLine($"started_utc={DateTime.UtcNow:O}");
                    _manifestWriter.WriteLine($"width={width}");
                    _manifestWriter.WriteLine($"height={height}");
                    _manifestWriter.WriteLine($"fps={_targetFps}");
                    _manifestWriter.WriteLine($"frames_directory={_currentFramesDirectory}");

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
            string outputPath;
            string framesDirectory;
            string manifestPath;
            int fps;
            try
            {
                lock (_recordingLock)
                {
                    outputPath = _currentRecordingPath;
                    framesDirectory = _currentFramesDirectory;
                    manifestPath = _currentManifestPath;
                    fps = _targetFps;
                    _isRecording = false;
                    _manifestWriter?.WriteLine($"stopped_utc={DateTime.UtcNow:O}");
                    _manifestWriter?.Flush();
                    _manifestWriter?.Dispose();
                    _manifestWriter = null;
                    
                    OnRecordingStatusChanged(false);
                }

                TryEncodeMp4(outputPath, framesDirectory, manifestPath, fps);
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
    public bool WriteFrame(byte[] encodedFrame)
    {
        if (!_isRecording || encodedFrame == null || encodedFrame.Length == 0)
        {
            return false;
        }

        try
        {
            lock (_recordingLock)
            {
                if (!_isRecording)
                {
                    return false;
                }

                var nextFrame = _frameCount + 1;
                var frameName = $"frame_{nextFrame:D06}.jpg";
                var framePath = Path.Combine(_currentFramesDirectory, frameName);
                File.WriteAllBytes(framePath, encodedFrame);
                _frameCount = nextFrame;
                _manifestWriter?.WriteLine($"{_frameCount},{DateTime.UtcNow:O},{frameName},{encodedFrame.Length}");

                if (_frameCount % 30 == 0)
                {
                    OnRecordingDurationUpdated(RecordingDuration);
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            OnRecordingError($"Error writing encoded frame: {ex.Message}");
            return false;
        }
    }

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

    private void TryEncodeMp4(string outputPath, string framesDirectory, string manifestPath, int fps)
    {
        if (string.IsNullOrWhiteSpace(outputPath) ||
            string.IsNullOrWhiteSpace(framesDirectory) ||
            !Directory.Exists(framesDirectory) ||
            Directory.GetFiles(framesDirectory, "frame_*.jpg").Length == 0)
        {
            return;
        }

        if (!IsCommandAvailable("ffmpeg"))
        {
            File.WriteAllText(outputPath, $"ffmpeg not available. Frames manifest: {manifestPath}{Environment.NewLine}");
            return;
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -hide_banner -loglevel error -framerate {fps} -i \"{Path.Combine(framesDirectory, "frame_%06d.jpg")}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var finished = process.WaitForExit(30_000);
            if (!finished)
            {
                try { process.Kill(); } catch { }
                File.WriteAllText(outputPath, $"ffmpeg timeout. Frames manifest: {manifestPath}{Environment.NewLine}");
                return;
            }

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                var error = process.StandardError.ReadToEnd();
                File.WriteAllText(outputPath, $"ffmpeg failed. Frames manifest: {manifestPath}{Environment.NewLine}{error}");
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(outputPath, $"ffmpeg error. Frames manifest: {manifestPath}{Environment.NewLine}{ex.Message}");
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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
