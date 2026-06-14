using System;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Android-safe video player bridge.
/// Native rendering is handled by UI controls; this service keeps command state
/// and avoids desktop OpenCV dependencies in Android builds.
/// </summary>
public sealed class AndroidVideoPlayerService : IVideoPlayerService
{
    private string? _source;
    private VideoPlayerState _state = VideoPlayerState.Idle;
    private double _volume = 1.0;

    public bool IsPlaying => _state == VideoPlayerState.Playing;
    public bool HasSource => !string.IsNullOrWhiteSpace(_source);

    public double Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0, 1.0);
    }

    public bool IsMuted { get; set; }

    public event EventHandler<VideoFrameEventArgs>? FrameReceived;
    public event EventHandler<VideoPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<VideoPlayerErrorEventArgs>? ErrorOccurred;

    public Task SetSourceAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            SetError("Video source cannot be empty.");
            return Task.CompletedTask;
        }

        _source = source.Trim();
        ChangeState(VideoPlayerState.Idle);
        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        if (!HasSource)
        {
            SetError("No video source has been selected.");
            return Task.CompletedTask;
        }

        ChangeState(VideoPlayerState.Playing);
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        if (_state == VideoPlayerState.Playing)
        {
            ChangeState(VideoPlayerState.Paused);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        ChangeState(VideoPlayerState.Stopped);
        _source = null;
        return Task.CompletedTask;
    }

    public Task<byte[]?> TakeSnapshotAsync()
    {
        ErrorOccurred?.Invoke(
            this,
            new VideoPlayerErrorEventArgs("Android video snapshot is not available from this service yet."));
        return Task.FromResult<byte[]?>(null);
    }

    private void SetError(string message)
    {
        ChangeState(VideoPlayerState.Error);
        ErrorOccurred?.Invoke(this, new VideoPlayerErrorEventArgs(message));
    }

    private void ChangeState(VideoPlayerState newState)
    {
        if (_state == newState)
        {
            return;
        }

        var oldState = _state;
        _state = newState;
        StateChanged?.Invoke(this, new VideoPlayerStateChangedEventArgs(oldState, newState));
    }
}
