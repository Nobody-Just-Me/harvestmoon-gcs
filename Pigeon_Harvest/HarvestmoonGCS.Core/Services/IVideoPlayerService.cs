using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Platform abstraction for video playback.
/// Provides cross-platform access to video streaming for camera feeds.
/// </summary>
public interface IVideoPlayerService
{
    /// <summary>
    /// Sets the video source URL or stream.
    /// </summary>
    /// <param name="source">The video source (URL, RTSP stream, file path, etc.)</param>
    Task SetSourceAsync(string source);

    /// <summary>
    /// Starts or resumes video playback.
    /// </summary>
    Task PlayAsync();

    /// <summary>
    /// Pauses video playback.
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Stops video playback and releases resources.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Takes a snapshot of the current video frame.
    /// </summary>
    /// <returns>Byte array containing the image data (PNG or JPEG format)</returns>
    Task<byte[]?> TakeSnapshotAsync();

    /// <summary>
    /// Gets whether the video player is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Gets whether the video player has a valid source.
    /// </summary>
    bool HasSource { get; }

    /// <summary>
    /// Gets or sets the volume (0.0 to 1.0).
    /// </summary>
    double Volume { get; set; }

    /// <summary>
    /// Gets or sets whether the video is muted.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Event raised when a new video frame is available.
    /// </summary>
    event EventHandler<VideoFrameEventArgs>? FrameReceived;

    /// <summary>
    /// Event raised when the video player state changes.
    /// </summary>
    event EventHandler<VideoPlayerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when an error occurs during playback.
    /// </summary>
    event EventHandler<VideoPlayerErrorEventArgs>? ErrorOccurred;
}

/// <summary>
/// Event args for video frame received events.
/// </summary>
public class VideoFrameEventArgs : EventArgs
{
    /// <summary>
    /// The video frame data (format depends on platform).
    /// </summary>
    public byte[] FrameData { get; }

    /// <summary>
    /// Width of the frame in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the frame in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Timestamp of the frame.
    /// </summary>
    public DateTime Timestamp { get; }

    public VideoFrameEventArgs(byte[] frameData, int width, int height, DateTime timestamp)
    {
        FrameData = frameData;
        Width = width;
        Height = height;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event args for video player state changed events.
/// </summary>
public class VideoPlayerStateChangedEventArgs : EventArgs
{
    public VideoPlayerState OldState { get; }
    public VideoPlayerState NewState { get; }

    public VideoPlayerStateChangedEventArgs(VideoPlayerState oldState, VideoPlayerState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Video player states.
/// </summary>
public enum VideoPlayerState
{
    Idle,
    Loading,
    Playing,
    Paused,
    Stopped,
    Error
}

/// <summary>
/// Event args for video player error events.
/// </summary>
public class VideoPlayerErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public Exception? Exception { get; }

    public VideoPlayerErrorEventArgs(string errorMessage, Exception? exception = null)
    {
        ErrorMessage = errorMessage;
        Exception = exception;
    }
}
