#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Speech.Tts;
using HarvestmoonGCS.Services;
using HarvestmoonGCS.Core.Models;
using Java.Util;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Android implementation of ISpeechService using Android.Speech.Tts.
/// </summary>
public class AndroidSpeechService : Java.Lang.Object, ISpeechService, TextToSpeech.IOnInitListener
{
    private readonly Context _context;
    private TextToSpeech _textToSpeech;
    private bool _isInitialized;
    private bool _isSpeaking;
    private readonly Queue<string> _speechQueue;
    private float _speechRate = 1.0f;
    private float _volume = 1.0f;
    private float _pitch = 1.0f;

    public bool IsSpeaking => _isSpeaking;
    public bool IsAvailable => _isInitialized && _textToSpeech != null;

    public float SpeechRate
    {
        get => _speechRate;
        set
        {
            _speechRate = Math.Clamp(value, 0.5f, 2.0f);
            _textToSpeech?.SetSpeechRate(_speechRate);
        }
    }

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = Math.Clamp(value, 0.5f, 2.0f);
            _textToSpeech?.SetPitch(_pitch);
        }
    }

    public event EventHandler SpeechStarted;
    public event EventHandler SpeechCompleted;
    public event EventHandler<string> SpeechError;

    public AndroidSpeechService(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _speechQueue = new Queue<string>();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                _textToSpeech = new TextToSpeech(_context, this);
            }
            catch (Exception ex)
            {
                OnSpeechError($"Failed to initialize TTS: {ex.Message}");
            }
        });

        // Wait for initialization
        var timeout = DateTime.Now.AddSeconds(5);
        while (!_isInitialized && DateTime.Now < timeout)
        {
            await Task.Delay(100);
        }

        if (!_isInitialized)
        {
            OnSpeechError("TTS initialization timeout");
        }
    }

    public void OnInit(OperationResult status)
    {
        if (status == OperationResult.Success)
        {
            _isInitialized = true;
            
            // Set default language to US English
            var result = _textToSpeech.SetLanguage(Locale.Us);
            
            if (result == LanguageAvailableResult.MissingData || 
                result == LanguageAvailableResult.NotSupported)
            {
                OnSpeechError("Language not supported");
            }

            // Set default properties
            _textToSpeech.SetSpeechRate(_speechRate);
            _textToSpeech.SetPitch(_pitch);

            // Set up utterance progress listener
            _textToSpeech.SetOnUtteranceProgressListener(new AndroidUtteranceProgressListener(this));
        }
        else
        {
            OnSpeechError("TTS initialization failed");
        }
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

        await Task.Run(() =>
        {
            try
            {
                var queueMode = interrupt ? QueueMode.Flush : QueueMode.Add;
                var utteranceId = Guid.NewGuid().ToString();
                
                var parameters = new global::Android.OS.Bundle();
                parameters.PutFloat(TextToSpeech.Engine.KeyParamVolume, _volume);
                
                _textToSpeech.Speak(text, queueMode, parameters, utteranceId);
                
                if (interrupt)
                {
                    _speechQueue.Clear();
                }
            }
            catch (Exception ex)
            {
                OnSpeechError($"Speech error: {ex.Message}");
            }
        });
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
        await Task.Run(() =>
        {
            try
            {
                _textToSpeech?.Stop();
                _speechQueue.Clear();
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error stopping speech: {ex.Message}");
            }
        });
    }

    public async Task PauseAsync()
    {
        // Android TTS doesn't support pause/resume directly
        // We'll stop instead
        await StopAsync();
    }

    public async Task ResumeAsync()
    {
        // Android TTS doesn't support pause/resume directly
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<VoiceInfo>> GetAvailableVoicesAsync()
    {
        if (!IsAvailable)
        {
            return Array.Empty<VoiceInfo>();
        }

        return await Task.Run<IReadOnlyList<VoiceInfo>>(() =>
        {
            try
            {
                var voices = new List<VoiceInfo>();
                
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
                {
                    var availableVoices = _textToSpeech.Voices;
                    if (availableVoices != null)
                    {
                        foreach (var voice in availableVoices)
                        {
                            voices.Add(new VoiceInfo
                            {
                                Id = voice.Name,
                                Name = voice.Name,
                                Language = voice.Locale?.DisplayName ?? "Unknown",
                                Gender = "Unknown"
                            });
                        }
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
    }

    public async Task SetVoiceAsync(string voiceId)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(voiceId))
        {
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Lollipop)
                {
                    var voices = _textToSpeech.Voices;
                    var voice = voices?.FirstOrDefault(v => v.Name == voiceId);
                    
                    if (voice != null)
                    {
                        var result = _textToSpeech.SetVoice(voice);
                        if (result != OperationResult.Success)
                        {
                            OnSpeechError($"Failed to set voice: {voiceId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnSpeechError($"Error setting voice: {ex.Message}");
            }
        });
    }

    protected virtual void OnSpeechStarted()
    {
        _isSpeaking = true;
        SpeechStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSpeechCompleted()
    {
        _isSpeaking = false;
        SpeechCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSpeechError(string error)
    {
        _isSpeaking = false;
        SpeechError?.Invoke(this, error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textToSpeech?.Stop();
            _textToSpeech?.Shutdown();
            _textToSpeech?.Dispose();
            _textToSpeech = null;
        }
        base.Dispose(disposing);
    }

    // Utterance progress listener
    private class AndroidUtteranceProgressListener : UtteranceProgressListener
    {
        private readonly AndroidSpeechService _service;

        public AndroidUtteranceProgressListener(AndroidSpeechService service)
        {
            _service = service;
        }

        public override void OnStart(string utteranceId)
        {
            _service.OnSpeechStarted();
        }

        public override void OnDone(string utteranceId)
        {
            _service.OnSpeechCompleted();
        }

        public override void OnError(string utteranceId)
        {
            _service.OnSpeechError("Speech synthesis error");
        }
    }
}
#endif