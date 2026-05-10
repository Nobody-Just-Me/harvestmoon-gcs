using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Desktop/Linux voice-recognition adapter using external STT command.
/// Configure command via PIA_STT_COMMAND env var.
/// </summary>
public sealed class DesktopVoiceRecognitionService : IVoiceRecognitionService, IDisposable
{
    private const int DefaultCommandTimeoutMs = 20000;
    private const int DefaultLoopDelayMs = 250;
    private const string DefaultLanguage = "id-ID";
    private const int DefaultDurationSeconds = 4;

    private string _commandTemplate = string.Empty;
    private readonly int _commandTimeoutMs;
    private CancellationTokenSource? _listeningCts;
    private Task? _listeningTask;
    private bool _isAvailable;
    private bool _usingCustomCommand;
    private string _availabilityReason = "Voice STT belum diinisialisasi.";
    private string? _lastError;
    private string _language = DefaultLanguage;

    public event VoiceCommandEventHandler? CommandRecognized;
    public event VoiceRecognitionErrorEventHandler? RecognitionError;

    public bool IsAvailable => _isAvailable;
    public string AvailabilityReason => _availabilityReason;
    public string? LastError => _lastError;

    public string Language
    {
        get => _language;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim();
            if (string.Equals(_language, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _language = normalized;
            _commandTemplate = ResolveCommandTemplate(_language, out _usingCustomCommand);
            RefreshAvailability();
        }
    }

    public bool IsListening { get; private set; }

    public DesktopVoiceRecognitionService()
    {
        _language = (Environment.GetEnvironmentVariable("PIA_STT_LANGUAGE") ?? DefaultLanguage).Trim();
        if (string.IsNullOrWhiteSpace(_language))
        {
            _language = DefaultLanguage;
        }

        _commandTemplate = ResolveCommandTemplate(_language, out _usingCustomCommand);
        _commandTimeoutMs = ParsePositiveInt(
            Environment.GetEnvironmentVariable("PIA_STT_TIMEOUT_MS"),
            DefaultCommandTimeoutMs);
        RefreshAvailability();
    }

    public Task StartListeningAsync()
    {
        if (!IsAvailable)
        {
            _commandTemplate = ResolveCommandTemplate(_language, out _usingCustomCommand);
            RefreshAvailability();
        }

        if (!IsAvailable)
        {
            ReportError(_availabilityReason);
            throw new InvalidOperationException(_availabilityReason);
        }

        if (IsListening)
        {
            return Task.CompletedTask;
        }

        _listeningCts = new CancellationTokenSource();
        IsListening = true;
        _listeningTask = Task.Run(() => ListenLoopAsync(_listeningCts.Token));
        return Task.CompletedTask;
    }

    public void StopListening()
    {
        IsListening = false;
        try
        {
            _listeningCts?.Cancel();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        StopListening();
        _listeningCts?.Dispose();
        _listeningCts = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var (text, confidence, error) = await RunRecognizerCommandAsync(ct);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    ReportError(error);
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    _lastError = null;
                    CommandRecognized?.Invoke(this, new VoiceCommandEventArgs
                    {
                        Command = text,
                        RawText = text,
                        Confidence = confidence
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ReportError($"STT desktop error: {ex.Message}");
            }

            try
            {
                await Task.Delay(DefaultLoopDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        IsListening = false;
    }

    private async Task<(string Text, float Confidence, string Error)> RunRecognizerCommandAsync(CancellationToken ct)
    {
        var psi = BuildShellProcessStartInfo(_commandTemplate);
        using var process = new Process { StartInfo = psi };

        if (!process.Start())
        {
            return (string.Empty, 0f, "Gagal menjalankan proses STT.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_commandTimeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return string.IsNullOrWhiteSpace(stderr)
                ? (string.Empty, 0f, string.Empty)
                : (string.Empty, 0f, $"STT stderr: {stderr.Trim()}");
        }

        return ParseRecognizerOutput(stdout);
    }

    private static ProcessStartInfo BuildShellProcessStartInfo(string commandTemplate)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {commandTemplate}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-lc \"{EscapeForDoubleQuotedShell(commandTemplate)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static (string Text, float Confidence, string Error) ParseRecognizerOutput(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorNode))
                {
                    var parsedError = errorNode.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(parsedError))
                    {
                        return (string.Empty, 0f, parsedError.Trim());
                    }
                }

                var text = root.TryGetProperty("text", out var textNode)
                    ? textNode.GetString() ?? string.Empty
                    : string.Empty;

                var confidence = 0.65f;
                if (root.TryGetProperty("confidence", out var confNode) &&
                    confNode.TryGetSingle(out var parsedConf))
                {
                    confidence = Math.Clamp(parsedConf, 0f, 1f);
                }

                return (text.Trim(), confidence, string.Empty);
            }
            catch
            {
            }
        }

        var firstLine = trimmed
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim();

        if (firstLine.Contains('|', StringComparison.Ordinal))
        {
            var parts = firstLine.Split('|', 2);
            var text = parts[0].Trim();
            if (parts.Length == 2 &&
                float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var conf))
            {
                return (text, Math.Clamp(conf, 0f, 1f), string.Empty);
            }

            return (text, 0.65f, string.Empty);
        }

        return (firstLine, 0.65f, string.Empty);
    }

    private void RefreshAvailability()
    {
        if (string.IsNullOrWhiteSpace(_commandTemplate))
        {
            _isAvailable = false;
            _availabilityReason =
                "Voice STT desktop belum siap: script `pia_stt_listener.py` atau `PIA_STT_COMMAND` belum ditemukan.";
            return;
        }

        if (_usingCustomCommand)
        {
            _isAvailable = true;
            _availabilityReason = "Voice STT siap (custom command: PIA_STT_COMMAND).";
            return;
        }

        var pythonExe = ResolvePythonExecutable();
        var hasRecorder = !string.IsNullOrWhiteSpace(FindExecutable("arecord")) ||
                          !string.IsNullOrWhiteSpace(FindExecutable("ffmpeg"));
        var hasModule = HasSpeechRecognitionModule(pythonExe);

        if (string.IsNullOrWhiteSpace(pythonExe) || !hasRecorder || !hasModule)
        {
            _isAvailable = false;
            var missing = string.Join(", ", new[]
            {
                string.IsNullOrWhiteSpace(pythonExe) ? "python/python3" : null,
                !hasRecorder ? "arecord/ffmpeg" : null,
                !hasModule ? "python module speech_recognition" : null
            }.Where(static x => !string.IsNullOrWhiteSpace(x)));
            _availabilityReason = $"Voice STT desktop belum lengkap. Dependency hilang: {missing}.";
            return;
        }

        _isAvailable = true;
        _availabilityReason = "Voice STT desktop siap (python helper).";
    }

    private static int ParsePositiveInt(string? raw, int fallback)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }

    private static string ResolveCommandTemplate(string language, out bool usingCustomCommand)
    {
        var configured = (Environment.GetEnvironmentVariable("PIA_STT_COMMAND") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            usingCustomCommand = true;
            return configured;
        }

        usingCustomCommand = false;
        var scriptPath = ResolveRecognizerScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return string.Empty;
        }

        var duration = ParsePositiveInt(Environment.GetEnvironmentVariable("PIA_STT_DURATION_SECONDS"), DefaultDurationSeconds);
        var escapedScript = scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var resolvedLanguage = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;
        var escapedLanguage = resolvedLanguage.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        if (OperatingSystem.IsWindows())
        {
            return $"python \"{escapedScript}\" --duration {duration} --language \"{escapedLanguage}\"";
        }

        return $"python3 \"{escapedScript}\" --duration {duration} --language \"{escapedLanguage}\"";
    }

    private static string ResolveRecognizerScriptPath()
    {
        var explicitPath = (Environment.GetEnvironmentVariable("PIA_STT_SCRIPT") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullExplicitPath = SafeFullPath(explicitPath);
            if (!string.IsNullOrWhiteSpace(fullExplicitPath) && File.Exists(fullExplicitPath))
            {
                return fullExplicitPath;
            }
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "pia_stt_listener.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "pia_stt_listener.py"),
            Path.Combine(AppContext.BaseDirectory, "Pigeon_Uno", "pia_stt_listener.py")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = SafeFullPath(candidate);
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var roots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Distinct();

        foreach (var root in roots)
        {
            var current = root;
            for (var depth = 0; depth < 8; depth++)
            {
                var direct = SafeFullPath(Path.Combine(current, "pia_stt_listener.py"));
                if (!string.IsNullOrWhiteSpace(direct) && File.Exists(direct))
                {
                    return direct;
                }

                var nested = SafeFullPath(Path.Combine(current, "Pigeon_Uno", "pia_stt_listener.py"));
                if (!string.IsNullOrWhiteSpace(nested) && File.Exists(nested))
                {
                    return nested;
                }

                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }
        }

        return string.Empty;
    }

    private static string ResolvePythonExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return FindExecutable("python", "python3") ?? string.Empty;
        }

        return FindExecutable("python3", "python") ?? string.Empty;
    }

    private static bool HasSpeechRecognitionModule(string pythonExecutable)
    {
        if (string.IsNullOrWhiteSpace(pythonExecutable))
        {
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    Arguments = "-c \"import speech_recognition\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            if (!process.WaitForExit(3000))
            {
                TryKillProcess(process);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExecutable(params string[] names)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        var separators = OperatingSystem.IsWindows() ? ';' : ':';
        var paths = pathEnv.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                if (OperatingSystem.IsWindows())
                {
                    var exeCandidate = $"{candidate}.exe";
                    if (File.Exists(exeCandidate))
                    {
                        return exeCandidate;
                    }
                }
            }
        }

        return null;
    }

    private void ReportError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return;
        }

        _lastError = error.Trim();
        RecognitionError?.Invoke(this, _lastError);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string EscapeForDoubleQuotedShell(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
