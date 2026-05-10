#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Platforms.Android;

namespace Pigeon_Uno.Platforms.Android.Services;

/// <summary>
/// Android implementation of <see cref="IVoiceRecognitionService"/> using SpeechRecognizer.
/// </summary>
public class AndroidVoiceRecognitionService : Java.Lang.Object, IVoiceRecognitionService, IRecognitionListener
{
    private const float DefaultConfidence = 0.6f;
    private const int MicrophonePermissionRequestCode = 2307;
    private const string DefaultLanguage = "id-ID";
    private const int MinSpeechLengthMs = 1200;
    private const int CompleteSilenceLengthMs = 700;
    private const int PossiblyCompleteSilenceLengthMs = 450;
    private const bool PreferOfflineRecognition = false;

    private readonly Context _context;
    private readonly Handler _mainHandler;
    private Intent _recognizerIntent;
    private SpeechRecognizer? _recognizer;
    private string? _lastPartialText;
    private float _lastPartialConfidence = 0.45f;
    private string? _lastEmittedText;
    private DateTimeOffset _lastEmittedAt = DateTimeOffset.MinValue;
    private string? _lastError;
    private string _language = DefaultLanguage;
    private string _activeLanguage = DefaultLanguage;
    private int _languageFallbackCursor;

    public event VoiceCommandEventHandler? CommandRecognized;
    public event VoiceRecognitionErrorEventHandler? RecognitionError;

    public bool IsAvailable => SpeechRecognizer.IsRecognitionAvailable(_context) && HasRecordAudioPermission();
    public bool IsListening { get; private set; }
    public string? LastError => _lastError;

    public string Language
    {
        get => _language;
        set
        {
            var next = NormalizeLanguageTag(value);
            if (string.Equals(_language, next, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _language = next;
            _activeLanguage = _language;
            _languageFallbackCursor = 0;
            _recognizerIntent = BuildRecognizerIntent(_activeLanguage);
        }
    }

    public string AvailabilityReason
    {
        get
        {
            if (!SpeechRecognizer.IsRecognitionAvailable(_context))
            {
                return "Speech engine Android tidak tersedia. Install/update Google app atau Speech Services by Google.";
            }

            if (!HasRecordAudioPermission())
            {
                return "Izin mikrofon belum aktif (RECORD_AUDIO). Aktifkan permission mikrofon di Settings.";
            }

            if (!string.Equals(_activeLanguage, _language, StringComparison.OrdinalIgnoreCase))
            {
                return $"Voice recognition Android siap (fallback language: {_activeLanguage}, preferensi: {_language}).";
            }

            return $"Voice recognition Android siap ({_activeLanguage}).";
        }
    }

    public AndroidVoiceRecognitionService(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mainHandler = new Handler(Looper.MainLooper
            ?? throw new InvalidOperationException("Android main looper tidak tersedia."));
        _language = NormalizeLanguageTag(_language);
        _activeLanguage = _language;
        _recognizerIntent = BuildRecognizerIntent(_activeLanguage);
    }

    public async Task StartListeningAsync()
    {
        if (!SpeechRecognizer.IsRecognitionAvailable(_context))
        {
            ReportError(AvailabilityReason);
            throw new InvalidOperationException(AvailabilityReason);
        }

        if (!HasRecordAudioPermission())
        {
            RequestRecordAudioPermissionIfPossible();
            ReportError(AvailabilityReason);
            throw new InvalidOperationException(AvailabilityReason);
        }

        try
        {
            await RunOnMainThreadAsync(() =>
            {
                EnsureRecognizer();
                if (_recognizer == null)
                {
                    throw new InvalidOperationException("Speech recognizer gagal diinisialisasi.");
                }

                if (IsListening)
                {
                    return;
                }

                _lastError = null;
                _lastPartialText = null;
                _lastPartialConfidence = 0.45f;
                _activeLanguage = _language;
                _languageFallbackCursor = 0;
                _recognizerIntent = BuildRecognizerIntent(_activeLanguage);
                IsListening = true;
                _recognizer.StartListening(_recognizerIntent);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            IsListening = false;
            ReportError($"Gagal memulai voice recognition ({_activeLanguage}): {ex.Message}");
            throw;
        }
    }

    public void StopListening()
    {
        IsListening = false;
        _ = RunOnMainThreadAsync(() =>
        {
            _recognizer?.StopListening();
        });
    }

    public void OnReadyForSpeech(Bundle? @params)
    {
    }

    public void OnBeginningOfSpeech()
    {
    }

    public void OnRmsChanged(float rmsdB)
    {
    }

    public void OnBufferReceived(byte[]? buffer)
    {
    }

    public void OnEndOfSpeech()
    {
        if (!string.IsNullOrWhiteSpace(_lastPartialText))
        {
            EmitRecognizedText(_lastPartialText!, _lastPartialConfidence);
        }
    }

    public void OnResults(Bundle? results)
    {
        var text = ExtractBestText(results);
        if (string.IsNullOrWhiteSpace(text))
        {
            RestartListening();
            return;
        }

        var confidence = ExtractConfidence(results);
        EmitRecognizedText(text, confidence);
        RestartListening();
    }

    public void OnPartialResults(Bundle? partialResults)
    {
        var text = ExtractBestText(partialResults);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _lastPartialText = text;
        _lastPartialConfidence = Math.Max(0.45f, ExtractConfidence(partialResults));
    }

    public void OnEvent(int eventType, Bundle? @params)
    {
    }

    public void OnError([GeneratedEnum] SpeechRecognizerError error)
    {
        if (!IsListening)
        {
            return;
        }

        if (IsLanguageUnavailableError(error))
        {
            if (TrySwitchToFallbackLanguage())
            {
                RestartListening(delayMs: 200);
                return;
            }
        }

        var message = MapError(error);

        if ((error is SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout)
            && !string.IsNullOrWhiteSpace(_lastPartialText))
        {
            EmitRecognizedText(_lastPartialText!, _lastPartialConfidence);
            RestartListening(delayMs: 250);
            return;
        }

        if (error is SpeechRecognizerError.NoMatch
            or SpeechRecognizerError.SpeechTimeout
            or SpeechRecognizerError.RecognizerBusy
            or SpeechRecognizerError.Network
            or SpeechRecognizerError.NetworkTimeout
            or SpeechRecognizerError.Server)
        {
            RestartListening(delayMs: 150);
            return;
        }

        ReportError(message);
        IsListening = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopListening();
            _ = RunOnMainThreadAsync(() =>
            {
                _recognizer?.Destroy();
                _recognizer = null;
            });
        }

        base.Dispose(disposing);
    }

    private void EnsureRecognizer()
    {
        if (_recognizer != null)
        {
            return;
        }

        _recognizer = SpeechRecognizer.CreateSpeechRecognizer(_context);
        _recognizer?.SetRecognitionListener(this);
    }

    private void RestartListening(int delayMs = 0)
    {
        if (!IsListening)
        {
            return;
        }

        _ = RunOnMainThreadAsync(() =>
        {
            try
            {
                _recognizer?.Cancel();
            }
            catch
            {
            }

            _mainHandler.PostDelayed(new Java.Lang.Runnable(() =>
            {
                if (!IsListening)
                {
                    return;
                }

                try
                {
                    EnsureRecognizer();
                    _recognizer?.StartListening(_recognizerIntent);
                }
                catch (Exception ex)
                {
                    IsListening = false;
                    ReportError($"Gagal melanjutkan voice recognition: {ex.Message}");
                }
            }), Math.Max(delayMs, 80));
        });
    }

    private static Intent BuildRecognizerIntent(string language)
    {
        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 3);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, language);
        intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, language);
        intent.PutExtra(RecognizerIntent.ExtraOnlyReturnLanguagePreference, false);
        intent.PutExtra(RecognizerIntent.ExtraPreferOffline, PreferOfflineRecognition);
        intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, MinSpeechLengthMs);
        intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, CompleteSilenceLengthMs);
        intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, PossiblyCompleteSilenceLengthMs);
        intent.PutExtra(RecognizerIntent.ExtraCallingPackage, global::Android.App.Application.Context.PackageName);
        return intent;
    }

    private static string ExtractBestText(Bundle? results)
    {
        var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
        if (matches == null || matches.Count == 0)
        {
            return string.Empty;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var text = matches[i]?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static float ExtractConfidence(Bundle? results)
    {
        var scores = results?.GetFloatArray(SpeechRecognizer.ConfidenceScores);
        if (scores == null || scores.Length == 0)
        {
            return DefaultConfidence;
        }

        var score = scores[0];
        if (float.IsNaN(score) || score < 0)
        {
            return DefaultConfidence;
        }

        return Math.Clamp(score, 0f, 1f);
    }

    private void EmitRecognizedText(string text, float confidence)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var normalizedText = text.Trim();
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastEmittedText, normalizedText, StringComparison.OrdinalIgnoreCase)
            && now - _lastEmittedAt < TimeSpan.FromMilliseconds(900))
        {
            return;
        }

        _lastError = null;
        _lastPartialText = null;
        _lastPartialConfidence = 0.45f;
        _lastEmittedText = normalizedText;
        _lastEmittedAt = now;

        CommandRecognized?.Invoke(this, new VoiceCommandEventArgs
        {
            Command = normalizedText,
            RawText = normalizedText,
            Confidence = Math.Clamp(confidence, 0f, 1f)
        });
    }

    private bool HasRecordAudioPermission()
    {
        return ContextCompat.CheckSelfPermission(_context, global::Android.Manifest.Permission.RecordAudio)
            == Permission.Granted;
    }

    private void RequestRecordAudioPermissionIfPossible()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
        {
            return;
        }

        var activity = _context as global::Android.App.Activity
            ?? AndroidCompatibility.CurrentActivity;
        if (activity == null)
        {
            return;
        }

        ActivityCompat.RequestPermissions(
            activity,
            new[] { global::Android.Manifest.Permission.RecordAudio },
            MicrophonePermissionRequestCode);
    }

    private bool TrySwitchToFallbackLanguage()
    {
        var candidates = BuildLanguageFallbackCandidates(_language);
        while (_languageFallbackCursor < candidates.Count)
        {
            var candidate = candidates[_languageFallbackCursor++];
            if (string.Equals(candidate, _activeLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _activeLanguage = candidate;
            _recognizerIntent = BuildRecognizerIntent(_activeLanguage);
            _lastError = $"Bahasa {_language} belum tersedia, fallback ke {_activeLanguage}.";
            return true;
        }

        return false;
    }

    private static bool IsLanguageUnavailableError(SpeechRecognizerError error)
    {
        var code = (int)error;
        return code is 12 or 13;
    }

    private static List<string> BuildLanguageFallbackCandidates(string preferredLanguage)
    {
        var candidates = new List<string>(6);
        AddLanguageCandidate(candidates, preferredLanguage);

        var locale = Java.Util.Locale.Default;
        if (locale != null)
        {
            AddLanguageCandidate(candidates, locale.ToLanguageTag());
            AddLanguageCandidate(candidates, $"{locale.Language}-{locale.Country}");
        }

        AddLanguageCandidate(candidates, DefaultLanguage);
        AddLanguageCandidate(candidates, "en-US");
        AddLanguageCandidate(candidates, "en-GB");
        return candidates;
    }

    private static void AddLanguageCandidate(List<string> candidates, string? language)
    {
        var normalized = NormalizeLanguageTag(language, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(normalized);
    }

    private static string NormalizeLanguageTag(string? language, string fallback = DefaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return fallback;
        }

        var normalized = language.Trim().Replace('_', '-');
        if (normalized.Length == 2)
        {
            return $"{normalized.ToLowerInvariant()}-{normalized.ToUpperInvariant()}";
        }

        return normalized;
    }

    private static string MapError(SpeechRecognizerError error)
    {
        return (int)error switch
        {
            (int)SpeechRecognizerError.NetworkTimeout => "Timeout jaringan saat voice recognition.",
            (int)SpeechRecognizerError.Network => "Jaringan bermasalah saat voice recognition.",
            (int)SpeechRecognizerError.Audio => "Audio input bermasalah saat membaca mikrofon.",
            (int)SpeechRecognizerError.Server => "Speech recognition server error.",
            (int)SpeechRecognizerError.Client => "Speech recognizer client error.",
            (int)SpeechRecognizerError.SpeechTimeout => "Tidak ada ucapan terdeteksi (timeout).",
            (int)SpeechRecognizerError.NoMatch => "Ucapan tidak dikenali.",
            (int)SpeechRecognizerError.RecognizerBusy => "Speech recognizer sedang sibuk.",
            (int)SpeechRecognizerError.InsufficientPermissions => "Izin mikrofon ditolak.",
            10 => "Terlalu banyak request voice recognition. Coba lagi sebentar.",
            11 => "Koneksi ke speech service terputus.",
            12 => "Bahasa voice recognition tidak didukung pada engine ini.",
            13 => "Bahasa voice recognition belum tersedia pada engine ini.",
            14 => "Engine tidak bisa mengecek dukungan bahasa saat ini.",
            15 => "Engine tidak bisa mendengarkan progres download bahasa.",
            _ => $"Speech recognition error: {error}"
        };
    }

    private void ReportError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _lastError = message.Trim();
        RecognitionError?.Invoke(this, _lastError);
    }

    private Task RunOnMainThreadAsync(Action action)
    {
        if (action == null)
        {
            return Task.CompletedTask;
        }

        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainHandler.Post(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }
}
#endif
