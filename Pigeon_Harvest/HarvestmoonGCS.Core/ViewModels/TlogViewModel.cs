using HarvestmoonGCS.Core.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.Core.Services;
using System;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Models;
using System.Threading;

namespace HarvestmoonGCS.ViewModels;

public partial class TlogViewModel : ViewModelBase
{
    private readonly IDispatcherService _dispatcherService;
    private readonly IMavLinkService _mavLinkService;
    private readonly IFileService _fileService;
    private readonly TlogPlayer _tlogPlayer;
    
    private CancellationTokenSource? _playbackCancellation;
    private bool _isSliderBeingDragged;

    [ObservableProperty] private string _filePath = "No File Selected";
    [ObservableProperty] private string _totalDuration = "00:00:00";
    [ObservableProperty] private string _currentTime = "00:00:00";
    [ObservableProperty] private string _startTime = "-";
    [ObservableProperty] private int _totalPackets = 0;
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isFileLoaded;
    [ObservableProperty] private int _speedIndex = 1; // Default to 1x speed
    [ObservableProperty] private string _playbackStatus = "Silakan buka file TLOG untuk memulai playback.\n\nFile TLOG dapat diperoleh dari Mission Planner, QGroundControl, atau aplikasi GCS lainnya setelah melakukan koneksi dengan autopilot.";
    [ObservableProperty] private TelemetryData _telemetryData = new();

    // Command enable properties
    [ObservableProperty] private bool _canPlay;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isLoading;

    public TlogViewModel(IDispatcherService dispatcherService, IMavLinkService mavLinkService, IFileService fileService)
    {
        _dispatcherService = dispatcherService;
        _mavLinkService = mavLinkService;
        _fileService = fileService;
        _tlogPlayer = new TlogPlayer();
        
        UpdateCommandStates();
    }

    partial void OnSpeedIndexChanged(int value)
    {
        // Speed changed, no action needed here as it's used in playback loop
    }

    private double GetPlaybackSpeed()
    {
        return SpeedIndex switch
        {
            0 => 0.5,
            1 => 1.0,
            2 => 2.0,
            3 => 5.0,
            4 => 10.0,
            _ => 1.0
        };
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        try
        {
            IsLoading = true;
            var file = await _fileService.PickFileAsync(new[] { ".tlog" });
            if (file != null)
            {
                // Stop any current playback
                if (IsPlaying)
                {
                    Stop();
                }

                // Load the tlog file
                bool success = _tlogPlayer.LoadTlogFile(file);
                
                if (success)
                {
                    FilePath = System.IO.Path.GetFileName(file);
                    TotalDuration = _tlogPlayer.TotalDuration.ToString(@"hh\:mm\:ss");
                    StartTime = _tlogPlayer.StartTime.ToString("HH:mm:ss");
                    TotalPackets = _tlogPlayer.TotalPackets;
                    IsFileLoaded = true;
                    CurrentTime = "00:00:00";
                    Progress = 0;
                    
                    PlaybackStatus = $"File TLOG berhasil dimuat!\n\nTotal Paket: {TotalPackets:N0}\nDurasi: {TotalDuration}\nWaktu Mulai: {StartTime}\n\nTekan Play untuk memulai playback.";
                    
                    UpdateCommandStates();
                }
                else
                {
                    PlaybackStatus = "Gagal memuat file TLOG. Pastikan file valid dan tidak rusak.";
                    IsFileLoaded = false;
                    UpdateCommandStates();
                }
            }
        }
        catch (Exception ex)
        {
            PlaybackStatus = $"Error membuka file: {ex.Message}";
            IsFileLoaded = false;
            UpdateCommandStates();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Play()
    {
        if (!IsFileLoaded || IsPlaying) return;

        // Enter playback mode
        if (!_mavLinkService.EnterPlaybackMode())
        {
            PlaybackStatus = "Tidak dapat memulai playback. Putuskan koneksi live terlebih dahulu.";
            return;
        }

        IsPlaying = true;
        UpdateCommandStates();
        
        PlaybackStatus = "Memutar playback...";

        _playbackCancellation = new CancellationTokenSource();
        var token = _playbackCancellation.Token;

        try
        {
            await Task.Run(async () =>
            {
                DateTime? lastPacketTime = null;
                DateTime playbackStartTime = DateTime.Now;
                TimeSpan pausedOffset = TimeSpan.FromSeconds(Progress / 100.0 * _tlogPlayer.TotalDuration.TotalSeconds);

                while (!token.IsCancellationRequested && IsPlaying)
                {
                    var packetData = _tlogPlayer.GetNextPacket();
                    
                    if (packetData == null)
                    {
                        // End of file reached
                        await _dispatcherService.RunOnUIThreadAsync(() =>
                        {
                            Stop();
                            PlaybackStatus = "Playback selesai. Tekan Play untuk memutar ulang.";
                        });
                        break;
                    }

                    DateTime packetTime = packetData.Item1;
                    byte[] packet = packetData.Item2;

                    // Calculate delay based on playback speed
                    if (lastPacketTime.HasValue)
                    {
                        TimeSpan realTimeDiff = packetTime - lastPacketTime.Value;
                        double speedMultiplier = GetPlaybackSpeed();
                        TimeSpan adjustedDelay = TimeSpan.FromMilliseconds(realTimeDiff.TotalMilliseconds / speedMultiplier);
                        
                        if (adjustedDelay.TotalMilliseconds > 0 && adjustedDelay.TotalMilliseconds < 5000)
                        {
                            await Task.Delay(adjustedDelay, token);
                        }
                    }

                    lastPacketTime = packetTime;

                    // Send packet to MAVLink service for processing
                    try
                    {
                        _mavLinkService.ProcessTlogPacket(packet);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TlogViewModel] Error processing packet: {ex.Message}");
                        // Continue with next packet - don't stop playback for single packet errors
                    }

                    // Update UI
                    if (!_isSliderBeingDragged)
                    {
                        TimeSpan currentOffset = _tlogPlayer.GetPacketTimeOffset(_tlogPlayer.CurrentPacketIndex - 1);
                        double progressPercent = _tlogPlayer.TotalDuration.TotalSeconds > 0 
                            ? (currentOffset.TotalSeconds / _tlogPlayer.TotalDuration.TotalSeconds) * 100.0 
                            : 0;

                        await _dispatcherService.RunOnUIThreadAsync(() =>
                        {
                            CurrentTime = currentOffset.ToString(@"hh\:mm\:ss");
                            Progress = progressPercent;
                        });
                    }
                }
            }, token);
        }
        catch (OperationCanceledException)
        {
            // Playback was cancelled, this is expected
            System.Diagnostics.Debug.WriteLine("[TlogViewModel] Playback cancelled by user");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TlogViewModel] Playback error: {ex.Message}");
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                PlaybackStatus = $"Error saat playback: {ex.Message}\n\nTekan Stop untuk reset.";
            });
        }
        finally
        {
            // Ensure playback mode is exited even on errors
            if (!IsPlaying)
            {
                _mavLinkService.ExitPlaybackMode();
            }
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (!IsPlaying) return;

        IsPlaying = false;
        _playbackCancellation?.Cancel();
        
        // Exit playback mode when pausing
        _mavLinkService.ExitPlaybackMode();
        
        UpdateCommandStates();
        
        PlaybackStatus = "Playback dijeda. Tekan Play untuk melanjutkan.";
    }

    [RelayCommand]
    private void Stop()
    {
        IsPlaying = false;
        _playbackCancellation?.Cancel();
        
        // Exit playback mode
        _mavLinkService.ExitPlaybackMode();
        
        if (IsFileLoaded)
        {
            _tlogPlayer.Reset();
            Progress = 0;
            CurrentTime = "00:00:00";
            PlaybackStatus = "Playback dihentikan. Tekan Play untuk memulai dari awal.";
        }
        
        UpdateCommandStates();
    }

    [RelayCommand]
    private void FastForward()
    {
        if (!IsFileLoaded) return;

        TimeSpan currentOffset = TimeSpan.FromSeconds(Progress / 100.0 * _tlogPlayer.TotalDuration.TotalSeconds);
        TimeSpan newOffset = currentOffset + TimeSpan.FromSeconds(10);
        
        if (newOffset > _tlogPlayer.TotalDuration)
        {
            newOffset = _tlogPlayer.TotalDuration;
        }
        
        _tlogPlayer.SeekToTime(newOffset);
        
        Progress = _tlogPlayer.TotalDuration.TotalSeconds > 0 
            ? (newOffset.TotalSeconds / _tlogPlayer.TotalDuration.TotalSeconds) * 100.0 
            : 0;
        CurrentTime = newOffset.ToString(@"hh\:mm\:ss");
        
        PlaybackStatus = $"Fast-forward 10 detik ke: {CurrentTime}";
    }

    public void OnSliderPressed()
    {
        _isSliderBeingDragged = true;
    }

    public void OnSliderReleased()
    {
        _isSliderBeingDragged = false;
        
        if (IsFileLoaded)
        {
            // Seek to the new position
            TimeSpan targetTime = TimeSpan.FromSeconds(Progress / 100.0 * _tlogPlayer.TotalDuration.TotalSeconds);
            _tlogPlayer.SeekToTime(targetTime);
            CurrentTime = targetTime.ToString(@"hh\:mm\:ss");
            
            PlaybackStatus = $"Melompat ke posisi: {CurrentTime}";
        }
    }

    private void UpdateCommandStates()
    {
        CanPlay = IsFileLoaded && !IsPlaying;
        CanPause = IsFileLoaded && IsPlaying;
        CanStop = IsFileLoaded && (IsPlaying || Progress > 0);
    }

    partial void OnIsPlayingChanged(bool value)
    {
        UpdateCommandStates();
    }

    partial void OnIsFileLoadedChanged(bool value)
    {
        UpdateCommandStates();
    }

    partial void OnProgressChanged(double value)
    {
        UpdateCommandStates();
    }
}
