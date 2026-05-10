#if !__ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Services;

#if WINDOWS
using System.Speech.Synthesis;
#endif

namespace Pigeon_Uno.Platforms.Desktop.Services;

/// <summary>
/// Desktop implementation of ISpeechService using System.Speech (Windows only).
/// On non-Windows platforms, this service will be unavailable.
/// </summary>
public class DesktopSpeechService : ISpeechService
{
#if WINDOWS
    private SpeechSynthesizer _synthesizer;
#endif
    private bool _isInitialized;
    private bool _isSpeaking;
    private readonly Queue<string> _speechQueue;
    private float _speechRate = 1.0f;
    private float _volume = 1.0f;
    private float _pitch = 1.0f;

    public bool IsSpeaking => _isSpeaking;
    public bool IsAvailable =>
#if WINDOWS
        _isInitialized && _synthesizer != null;
#else
        false;
#endif

    public float SpeechRate
    {
        get => _speechRate;
        set
        {
            _speechRate = Math.Clamp(value, 0.5f, 2.0f);
#if WINDOWS
            if (_synthesizer != null)
            {
                // System.Speech uses -10 to 10 range
                _synthesizer.Rate = (int)((_speechRate - 1.0f) * 10);
            }
#endif
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0f, 1.0f);
#if WINDOWS
            if (_synthesizer != null)
            {
                _synthesizer.Volume = (int)(_volume * 100);
            }
#endif
        }
    }

    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, 0.5f, 2.0f);
    }

    public event EventHandler SpeechStarted;
    public event EventHandler SpeechCompleted;
    public event EventHandler<string> SpeechError;

    public DesktopSpeechService()
    {
        _speechQueue = new Queue<string>();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();

                // Set up event handlers
                _synthesizer.SpeakStarted += OnSynthesizerSpeakStarted;
                _synthesizer.SpeakCompleted += OnSynthesizerSpeakCompleted;

                // Set default properties
                _synthesizer.Rate = 0; // Normal speed
                _synthesizer.Volume = 100; // Full volume

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                OnSpeechError($"Failed to initialize TTS: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
        OnSpeechError("Speech synthesis is only available on Windows");
#endif
    }

    public async Task SpeakAsync(string text)
    {
        await SpeakAsync(text, false);
    }

    public async Task SpeakAsync(string text, bool interrupt = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!IsAvailable)
        {
            await InitializeAsync();
            if (!IsAvailable)
            {
                OnSpeechError("TTS not available");
                return;
            }
        }

#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                if (interrupt)
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    _speechQueue.Clear();
                }

                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                OnSpeechError($"Speech error: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
#endif
    }

    public async Task QueueSpeechAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _speechQueue.Enqueue(text);

        if (!_isSpeaking)
        {
            await ProcessSpeechQueue();
        }
    }

    private async Task ProcessSpeechQueue()
    {
        while (_speechQueue.Count > 0)
        {
            var text = _speechQueue.Dequeue();
            await SpeakAsync(text, false);

            // Wait for speech to complete
            while (_isSpeaking)
            {
                await Task.Delay(100);
            }
        }
    }

    public async Task StopAsync()
    {
#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                _synthesizer?.SpeakAsyncCancelAll();
                _speechQueue.Clear();
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error stopping speech: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
#endif
    }

    public async Task PauseAsync()
    {
#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                _synthesizer?.Pause();
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error pausing speech: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
#endif
    }

    public async Task ResumeAsync()
    {
#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                _synthesizer?.Resume();
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error resuming speech: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
#endif
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetAvailableVoicesAsync()
    {
        if (!IsAvailable)
        {
            return Array.Empty<VoiceInfo>();
        }

#if WINDOWS
        return await Task.Run(() =>
        {
            try
            {
                var voices = new List<VoiceInfo>();
                var installedVoices = _synthesizer.GetInstalledVoices();

                foreach (var voice in installedVoices)
                {
                    if (voice.Enabled)
                    {
                        var info = voice.VoiceInfo;
                        voices.Add(new VoiceInfo
                        {
                            Id = info.Name,
                            Name = info.Name,
                            Language = info.Culture.DisplayName,
                            Gender = info.Gender.ToString()
                        });
                    }
                }

                return voices.AsReadOnly();
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error getting voices: {ex.Message}");
                return Array.Empty<VoiceInfo>();
            }
        });
#else
        return await Task.FromResult(Array.Empty<VoiceInfo>());
#endif
    }

    public async Task SetVoiceAsync(string voiceId)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(voiceId))
        {
            return;
        }

#if WINDOWS
        await Task.Run(() =>
        {
            try
            {
                _synthesizer.SelectVoice(voiceId);
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error setting voice: {ex.Message}");
            }
        });
#else
        await Task.CompletedTask;
#endif
    }

#if WINDOWS
    private void OnSynthesizerSpeakStarted(object sender, SpeakStartedEventArgs e)
    {
        _isSpeaking = true;
        OnSpeechStarted();
    }

    private void OnSynthesizerSpeakCompleted(object sender, SpeakCompletedEventArgs e)
    {
        _isSpeaking = false;
        
        if (e.Error != null)
        {
            OnSpeechError($"Speech error: {e.Error.Message}");
        }
        else if (e.Cancelled)
        {
            // Speech was cancelled
        }
        else
        {
            OnSpeechCompleted();
        }
    }
#endif

    protected virtual void OnSpeechStarted()
    {
        SpeechStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSpeechCompleted()
    {
        SpeechCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSpeechError(string error)
    {
        SpeechError?.Invoke(this, error);
    }

    public void Dispose()
    {
#if WINDOWS
        if (_synthesizer != null)
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakStarted -= OnSynthesizerSpeakStarted;
            _synthesizer.SpeakCompleted -= OnSynthesizerSpeakCompleted;
            _synthesizer.Dispose();
            _synthesizer = null;
        }
#endif
    }
}
#endif