using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

public class TlogPlayerService : ITlogPlayerService
{
    private readonly IMavLinkService _mavLinkService;
    private CancellationTokenSource? _cts;
    private string? _currentFilePath;
    private double _playbackSpeed = 1.0;
    private double _duration;
    private double _currentPosition;

    public event Action<bool>? PlaybackStateChanged;
    public event EventHandler<byte[]>? TelemetryEmitted;
    
    public bool IsPlaying => _cts != null;
    public double Duration => _duration;
    public double CurrentPosition => _currentPosition;

    public TlogPlayerService(IMavLinkService mavLinkService)
    {
        _mavLinkService = mavLinkService;
    }

    public async Task LoadFileAsync(string filePath)
    {
        _currentFilePath = filePath;
        _currentPosition = 0;
        
        // Calculate duration by reading file
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            
            long firstTimestamp = -1;
            long lastTimestamp = -1;
            
            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < 8) break;
                
                byte[] timeBytes = reader.ReadBytes(8);
                if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);
                long timestamp = BitConverter.ToInt64(timeBytes, 0);
                
                if (firstTimestamp == -1) firstTimestamp = timestamp;
                lastTimestamp = timestamp;
                
                // Skip packet data
                if (stream.Position >= stream.Length) break;
                byte magic = reader.ReadByte();
                
                if (magic == 0xFE)
                {
                    if (stream.Length - stream.Position < 5) break;
                    byte len = reader.ReadByte();
                    stream.Position += len + 4; // skip rest of packet
                }
                else if (magic == 0xFD)
                {
                    if (stream.Length - stream.Position < 9) break;
                    byte len = reader.ReadByte();
                    stream.Position += len + 11; // skip rest of packet
                }
            }
            
            if (firstTimestamp != -1 && lastTimestamp != -1)
            {
                _duration = (lastTimestamp - firstTimestamp) / 1000000.0; // Convert to seconds
            }
        }
        catch (Exception)
        {
            _duration = 0;
        }
        
        await Task.CompletedTask;
    }

    public async Task PlayAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        if (IsPlaying) await StopAsync();
        
        _cts = new CancellationTokenSource();
        PlaybackStateChanged?.Invoke(true);

        try
        {
            await Task.Run(() => PlayLoop(_currentFilePath, _cts.Token));
        }
        catch (Exception) { }
        finally
        {
            await StopAsync();
        }
    }

    public Task PauseAsync()
    {
        _cts?.Cancel();
        _cts = null;
        PlaybackStateChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _cts = null;
        _currentPosition = 0;
        PlaybackStateChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    public Task SeekAsync(double position)
    {
        _currentPosition = Math.Max(0, Math.Min(position, _duration));
        return Task.CompletedTask;
    }

    public void SetPlaybackSpeed(double speed)
    {
        _playbackSpeed = Math.Max(0.1, Math.Min(speed, 10.0));
    }

    public async Task PlayAsync(string filePath)
    {
        await LoadFileAsync(filePath);
        await PlayAsync();
    }

    public void Stop()
    {
        _ = StopAsync();
    }

    private void PlayLoop(string filePath, CancellationToken token)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        long firstTimestamp = -1;
        DateTime startTime = DateTime.Now;

        while (stream.Position < stream.Length && !token.IsCancellationRequested)
        {
            // Read 8 bytes timestamp (Big Endian)
            if (stream.Length - stream.Position < 8) break;
            
            byte[] timeBytes = reader.ReadBytes(8);
            if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);
            long timestamp = BitConverter.ToInt64(timeBytes, 0);

            if (firstTimestamp == -1) firstTimestamp = timestamp;

            // Update current position
            long offsetUs = timestamp - firstTimestamp;
            _currentPosition = offsetUs / 1000000.0; // Convert to seconds

            // Wait for time with playback speed
            TimeSpan targetDelay = TimeSpan.FromMilliseconds((offsetUs / 1000.0) / _playbackSpeed);
            
            TimeSpan elapsed = DateTime.Now - startTime;
            if (targetDelay > elapsed)
            {
                Thread.Sleep(targetDelay - elapsed);
            }

            // Check cancellation
            if (token.IsCancellationRequested) break;

            // Read packet
            if (stream.Position >= stream.Length) break;
            
            byte magic = reader.ReadByte();
            stream.Position--; 

            MavLinkPacketBase? packet = null;

            if (magic == 0xFE)
            {
                packet = MavLinkPacketV10.Deserialize(reader, 0);
            }
            else if (magic == 0xFD)
            {
                packet = MavLinkPacketV20.Deserialize(reader, 0);
            }
            else
            {
                stream.ReadByte(); // consume garbage
                continue;
            }

            if (packet != null && packet.IsValid)
            {
                _mavLinkService.InjectPacket(packet);
                
                // Emit telemetry event with empty byte array (packet already injected)
                TelemetryEmitted?.Invoke(this, Array.Empty<byte>());
            }
        }
    }
}
