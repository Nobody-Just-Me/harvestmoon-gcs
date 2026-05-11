using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Core.Services.AI;

public class VoiceCommandService : IVoiceCommandService
{
    private static readonly TimeSpan CriticalCommandConfirmationWindow = TimeSpan.FromSeconds(15);
    private readonly IMavLinkService? _mavLinkService;
    private readonly IVoiceRecognitionService? _voiceRecognitionService;
    private readonly ICameraService? _cameraService;
    private readonly IPIAHistoryStore? _historyStore;
    private readonly ISpeechService? _speechService;
    private readonly AISettings _settings;
    private readonly Dictionary<VoiceCommand, string[]> _commandAliases;
    private TelemetrySnapshot? _latestSnapshot;
    private PendingCriticalCommand? _pendingCriticalCommand;
    private string? _lastError;

    public bool IsAvailable => IsVoiceCommandEnabled && (_voiceRecognitionService?.IsAvailable ?? false);
    public bool IsListening { get; private set; }
    public float ConfidenceThreshold { get; set; }
    public string AvailabilityReason =>
        !IsVoiceCommandEnabled
            ? "Voice command dinonaktifkan di settings."
            : _voiceRecognitionService?.AvailabilityReason ?? "Voice recognition engine belum terdaftar.";
    public string? LastError => _lastError ?? _voiceRecognitionService?.LastError;

    public event EventHandler<VoiceCommandResult>? CommandRecognized;
    public event EventHandler<string>? RecognitionError;

    public VoiceCommandService(
        IMavLinkService? mavLinkService,
        AISettings settings,
        IVoiceRecognitionService? voiceRecognitionService = null,
        ICameraService? cameraService = null,
        IPIAHistoryStore? historyStore = null,
        ISpeechService? speechService = null)
    {
        _mavLinkService = mavLinkService;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _voiceRecognitionService = voiceRecognitionService;
        _cameraService = cameraService;
        _historyStore = historyStore;
        _speechService = speechService;
        ConfidenceThreshold = (float)Math.Clamp(_settings.VoiceCommand.ConfidenceThreshold, 0.3, 1.0);
        _commandAliases = BuildCommandAliases();

        if (_voiceRecognitionService != null)
        {
            _voiceRecognitionService.Language = string.IsNullOrWhiteSpace(_settings.VoiceCommand.Language)
                ? "id-ID"
                : _settings.VoiceCommand.Language;
            _voiceRecognitionService.CommandRecognized += OnRawVoiceCommandRecognized;
            _voiceRecognitionService.RecognitionError += OnVoiceEngineError;
        }
    }

    public async Task StartListeningAsync()
    {
        if (!IsVoiceCommandEnabled)
        {
            _lastError = "Voice command dinonaktifkan di settings.";
            RecognitionError?.Invoke(this, "Voice command dinonaktifkan di settings.");
            return;
        }

        if (_voiceRecognitionService != null)
        {
            if (!_voiceRecognitionService.IsAvailable)
            {
                IsListening = false;
                _lastError = _voiceRecognitionService.AvailabilityReason;
                RecognitionError?.Invoke(this, _voiceRecognitionService.AvailabilityReason);
                return;
            }

            try
            {
                await _voiceRecognitionService.StartListeningAsync();
                IsListening = _voiceRecognitionService.IsListening;
                _lastError = null;
            }
            catch (Exception ex)
            {
                IsListening = false;
                _lastError = ex.Message;
                RecognitionError?.Invoke(this, $"Voice recognition gagal dijalankan: {ex.Message}");
            }
            return;
        }

        IsListening = false;
        _lastError = "Voice recognition engine belum terdaftar.";
        RecognitionError?.Invoke(this, "Voice recognition engine belum terdaftar.");
    }

    public Task StopListeningAsync()
    {
        _voiceRecognitionService?.StopListening();
        IsListening = false;
        return Task.CompletedTask;
    }

    public async Task<VoiceCommandResult> ProcessTextAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return EmitResult(new VoiceCommandResult
            {
                RawText = text ?? string.Empty,
                Message = "Perintah kosong.",
                IsValid = false,
                IsExecuted = false,
                Confidence = 0
            });
        }

        if (!IsVoiceCommandEnabled)
        {
            return EmitResult(new VoiceCommandResult
            {
                RawText = text,
                Message = "Voice command sedang nonaktif.",
                IsValid = false,
                IsExecuted = false,
                Confidence = 0
            });
        }

        var normalized = Normalize(text);

        if (TryCancelPendingCommand(normalized, out var cancelMessage))
        {
            return EmitResult(new VoiceCommandResult
            {
                Command = VoiceCommand.Unknown,
                RawText = text,
                Confidence = 1f,
                IsValid = true,
                IsExecuted = false,
                Message = cancelMessage
            });
        }

        var (command, confidence) = MatchCommand(normalized);

        if (command == VoiceCommand.Unknown || confidence < ConfidenceThreshold)
        {
            return EmitResult(new VoiceCommandResult
            {
                Command = VoiceCommand.Unknown,
                RawText = text,
                Confidence = confidence,
                IsValid = false,
                IsExecuted = false,
                Message = "Perintah tidak dikenali. Coba ulangi dengan kata kunci yang lebih jelas."
            });
        }

        var validation = CommandValidator.Validate(command, _latestSnapshot, _mavLinkService?.IsConnected == true);
        if (!validation.IsValid)
        {
            return EmitResult(new VoiceCommandResult
            {
                Command = command,
                RawText = text,
                Confidence = confidence,
                IsValid = false,
                IsExecuted = false,
                Message = validation.Message
            });
        }

        if (RequiresCriticalConfirmation(command))
        {
            if (!IsConfirmationText(normalized))
            {
                _pendingCriticalCommand = new PendingCriticalCommand(command, DateTime.UtcNow + CriticalCommandConfirmationWindow);
                return EmitResult(new VoiceCommandResult
                {
                    Command = command,
                    RawText = text,
                    Confidence = confidence,
                    IsValid = true,
                    IsExecuted = false,
                    Message = BuildConfirmationPrompt(command)
                });
            }

            if (!TryConfirmPendingCommand(command, out var confirmationError))
            {
                return EmitResult(new VoiceCommandResult
                {
                    Command = command,
                    RawText = text,
                    Confidence = confidence,
                    IsValid = false,
                    IsExecuted = false,
                    Message = confirmationError
                });
            }
        }

        var (isExecuted, executionMessage) = await ExecuteCommandAsync(command, normalized, ct);

        return EmitResult(new VoiceCommandResult
        {
            Command = command,
            RawText = text,
            Confidence = confidence,
            IsValid = true,
            IsExecuted = isExecuted,
            Message = executionMessage
        });
    }

    public void UpdateTelemetrySnapshot(TelemetrySnapshot snapshot)
    {
        _latestSnapshot = snapshot;
    }

    private bool IsVoiceCommandEnabled => _settings.VoiceCommand.Enabled;

    private void OnRawVoiceCommandRecognized(object sender, VoiceCommandEventArgs e)
    {
        _ = HandleRawVoiceInputAsync(e);
    }

    private void OnVoiceEngineError(object sender, string error)
    {
        IsListening = _voiceRecognitionService?.IsListening ?? false;
        _lastError = error;
        RecognitionError?.Invoke(this, error);
    }

    private async Task HandleRawVoiceInputAsync(VoiceCommandEventArgs e)
    {
        try
        {
            IsListening = _voiceRecognitionService?.IsListening ?? IsListening;
            await ProcessTextAsync(e.RawText);
        }
        catch (Exception ex)
        {
            RecognitionError?.Invoke(this, $"Gagal memproses voice input: {ex.Message}");
        }
    }

    private VoiceCommandResult EmitResult(VoiceCommandResult result)
    {
        result.Timestamp = DateTime.UtcNow;
        CommandRecognized?.Invoke(this, result);
        _ = PersistCommandAuditAsync(result);
        _ = AnnounceResultAsync(result);
        return result;
    }

    private async Task AnnounceResultAsync(VoiceCommandResult result)
    {
        if (_speechService == null || !_settings.VoiceCommand.Enabled || !IsListening)
        {
            return;
        }

        var feedbackText = BuildFeedbackText(result);
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            return;
        }

        try
        {
            await _speechService.SpeakAsync(feedbackText, interrupt: !result.IsExecuted);
        }
        catch
        {
        }
    }

    private static string BuildFeedbackText(VoiceCommandResult result)
    {
        if (result.IsExecuted)
        {
            return result.Message;
        }

        if (result.Command == VoiceCommand.Unknown)
        {
            return "Perintah tidak dikenali.";
        }

        return result.Message;
    }

    private async Task<(bool IsExecuted, string Message)> ExecuteCommandAsync(
        VoiceCommand command,
        string normalizedText,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (command is VoiceCommand.Status or VoiceCommand.BatteryCheck or VoiceCommand.GpsCheck or VoiceCommand.AltitudeCheck or VoiceCommand.SpeedCheck
                or VoiceCommand.MissionStatus or VoiceCommand.WaypointStatus or VoiceCommand.CalibrationStatus)
            {
                return BuildTelemetryMessage(command);
            }

            if (command is VoiceCommand.TakePhoto or VoiceCommand.StartRecording or VoiceCommand.StopRecording or VoiceCommand.ZoomIn or VoiceCommand.ZoomOut)
            {
                return await ExecuteCameraCommandAsync(command);
            }

            if (_mavLinkService == null)
            {
                return (false, $"Perintah dikenali ({command}) tapi MAVLink service belum tersedia.");
            }

            switch (command)
            {
                case VoiceCommand.Arm:
                    return (await _mavLinkService.ArmDisarmAsync(true), "ARM command dikirim.");

                case VoiceCommand.Disarm:
                    return (await _mavLinkService.ArmDisarmAsync(false), "DISARM command dikirim.");

                case VoiceCommand.Takeoff:
                    await _mavLinkService.SendCommandAsync(Command.TAKE_OFF);
                    return (true, "TAKEOFF command dikirim.");

                case VoiceCommand.Land:
                    await _mavLinkService.SendCommandAsync(Command.LAND);
                    return (true, "LAND command dikirim.");

                case VoiceCommand.ReturnToLaunch:
                    await _mavLinkService.SendCommandAsync(Command.RTL);
                    return (true, "RTL command dikirim.");

                case VoiceCommand.PauseMission:
                    return (await _mavLinkService.PauseMissionAsync(), "Pause mission command dikirim.");

                case VoiceCommand.ResumeMission:
                    return (await _mavLinkService.ResumeMissionAsync(), "Resume mission command dikirim.");

                case VoiceCommand.StartMission:
                    return (await _mavLinkService.StartMissionAsync(), "Start mission command dikirim.");

                case VoiceCommand.EmergencyStop:
                {
                    var paused = await _mavLinkService.PauseMissionAsync();
                    await _mavLinkService.SendCommandAsync(Command.LAND);
                    return (true, paused
                        ? "Emergency stop: misi dipause dan LAND command dikirim."
                        : "Emergency stop: LAND command dikirim.");
                }

                case VoiceCommand.ModeStabilize:
                    return (await _mavLinkService.SetFlightModeAsync("Stabilize"), "Mode Stabilize dikirim.");

                case VoiceCommand.ModeLoiter:
                    return (await _mavLinkService.SetFlightModeAsync("Loiter"), "Mode Loiter dikirim.");

                case VoiceCommand.ModeAuto:
                    return (await _mavLinkService.SetFlightModeAsync("Auto"), "Mode Auto dikirim.");

                case VoiceCommand.ModeGuided:
                    return (await _mavLinkService.SetFlightModeAsync("Guided"), "Mode Guided dikirim.");

                case VoiceCommand.ModeCircle:
                    return (await _mavLinkService.SetFlightModeAsync("Circle"), "Mode Circle dikirim.");

                case VoiceCommand.ModeFollow:
                    return (await _mavLinkService.SetFlightModeAsync("Follow"), "Mode Follow dikirim.");

                case VoiceCommand.ModePoshold:
                    return (await _mavLinkService.SetFlightModeAsync("Poshold"), "Mode Poshold dikirim.");

                case VoiceCommand.ModeAcro:
                    return (await _mavLinkService.SetFlightModeAsync("Acro"), "Mode Acro dikirim.");

                case VoiceCommand.GoToWaypoint:
                {
                    var wp = (int)Math.Round(ExtractNumber(normalizedText), MidpointRounding.AwayFromZero);
                    if (wp <= 0)
                    {
                        return (false, "Waypoint belum disebutkan. Contoh: go to waypoint 3.");
                    }

                    return (await _mavLinkService.SetCurrentWaypointAsync(wp - 1), $"Perintah ke waypoint {wp} dikirim.");
                }

                case VoiceCommand.NextWaypoint:
                {
                    var wp = (int)Math.Round(ExtractNumber(normalizedText), MidpointRounding.AwayFromZero);
                    if (wp <= 1)
                    {
                        return (false, "Sebutkan waypoint target. Contoh: next waypoint 4.");
                    }

                    return (await _mavLinkService.SetCurrentWaypointAsync(wp - 1), $"Perintah lompat ke waypoint {wp} dikirim.");
                }

                case VoiceCommand.ClearMission:
                {
                    var cleared = await _mavLinkService.UploadMissionAsync(Array.Empty<WaypointData>());
                    return (cleared, cleared ? "Semua waypoint mission dihapus." : "Gagal menghapus mission.");
                }

                case VoiceCommand.HoldPosition:
                    return (await _mavLinkService.SetFlightModeAsync("Loiter"), "Hold position (Loiter) command dikirim.");

                case VoiceCommand.SetHome:
                {
                    await _mavLinkService.SendCommandLongAsync((int)WaypointCommand.DoSetHome, 1, 0, 0, 0, 0, 0, 0);
                    return (true, "Perintah set home ke posisi saat ini dikirim.");
                }

                case VoiceCommand.RequestLogs:
                {
                    await _mavLinkService.RequestParametersAsync();
                    return (true, "Permintaan data diagnostik dikirim (parameter refresh).");
                }

                case VoiceCommand.SetSpeed:
                {
                    var speed = ExtractNumber(normalizedText);
                    if (speed <= 0)
                    {
                        return (false, "Nilai kecepatan tidak valid. Contoh: speed 10.");
                    }

                    return (await _mavLinkService.ChangeSpeedAsync(0, speed), $"Perintah set speed {speed:0.##} m/s dikirim.");
                }

                case VoiceCommand.SetAltitude:
                {
                    var altitude = ExtractNumber(normalizedText);
                    if (altitude <= 0)
                    {
                        return (false, "Nilai altitude tidak valid. Contoh: altitude 50.");
                    }

                    await _mavLinkService.SendCommandAsync(Command.TAKE_OFF, altitude);
                    return (true, $"Perintah altitude target {altitude:0.##} m dikirim.");
                }

                case VoiceCommand.GimbalDown:
                    return await ExecuteGimbalServoAsync(1100, "Gimbal diarahkan ke bawah.");

                case VoiceCommand.GimbalForward:
                case VoiceCommand.CenterCamera:
                    return await ExecuteGimbalServoAsync(1500, "Gimbal diarahkan ke depan.");

                case VoiceCommand.GimbalUp:
                    return await ExecuteGimbalServoAsync(1900, "Gimbal diarahkan ke atas.");

                case VoiceCommand.EnableGeofence:
                {
                    var enabled = await _mavLinkService.SetParameterAsync("FENCE_ENABLE", 1);
                    return (enabled, enabled ? "Geofence diaktifkan." : "Gagal mengaktifkan geofence.");
                }

                case VoiceCommand.DisableGeofence:
                {
                    var disabled = await _mavLinkService.SetParameterAsync("FENCE_ENABLE", 0);
                    return (disabled, disabled ? "Geofence dinonaktifkan." : "Gagal menonaktifkan geofence.");
                }

                default:
                    return (false, $"Perintah {command} dikenali, tetapi eksekusi otomatis belum tersedia di versi ini.");
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            RecognitionError?.Invoke(this, ex.Message);
            return (false, $"Eksekusi gagal: {ex.Message}");
        }
    }

    private async Task<(bool IsExecuted, string Message)> ExecuteCameraCommandAsync(VoiceCommand command)
    {
        if (_cameraService == null)
        {
            return (false, "Camera service belum tersedia.");
        }

        return command switch
        {
            VoiceCommand.TakePhoto => await HandleTakePhotoAsync(),
            VoiceCommand.StartRecording => await HandleStartRecordingAsync(),
            VoiceCommand.StopRecording => await HandleStopRecordingAsync(),
            VoiceCommand.ZoomIn => await HandleZoomAsync(1.15f, "Zoom in dikirim."),
            VoiceCommand.ZoomOut => await HandleZoomAsync(0.85f, "Zoom out dikirim."),
            _ => (false, $"Command kamera {command} belum didukung.")
        };
    }

    private async Task<(bool IsExecuted, string Message)> HandleTakePhotoAsync()
    {
        var success = await _cameraService!.TakePictureAsync();
        return (success, success ? "Perintah ambil foto dikirim." : "Gagal mengambil foto.");
    }

    private async Task<(bool IsExecuted, string Message)> HandleStartRecordingAsync()
    {
        var started = await _cameraService!.StartRecordingAsync();
        return (started, started ? "Perekaman video dimulai." : "Gagal memulai perekaman.");
    }

    private async Task<(bool IsExecuted, string Message)> HandleStopRecordingAsync()
    {
        var stopped = await _cameraService!.StopRecordingAsync();
        return (stopped, stopped ? "Perekaman video dihentikan." : "Gagal menghentikan perekaman.");
    }

    private async Task<(bool IsExecuted, string Message)> HandleZoomAsync(float value, string successMessage)
    {
        await _cameraService!.SendCameraControlAsync(CameraControlCommand.Zoom, value);
        return (true, successMessage);
    }

    private async Task<(bool IsExecuted, string Message)> ExecuteGimbalServoAsync(int pwm, string successMessage)
    {
        if (_mavLinkService == null)
        {
            return (false, "MAVLink service belum tersedia untuk gimbal control.");
        }

        var success = await _mavLinkService.SetServoAsync(9, pwm);
        return (success, success ? successMessage : "Gagal mengirim perintah gimbal.");
    }

    private (bool IsExecuted, string Message) BuildTelemetryMessage(VoiceCommand command)
    {
        if (_latestSnapshot == null)
        {
            return (false, "Data telemetry belum tersedia.");
        }

        return command switch
        {
            VoiceCommand.Status => (true,
                $"Status: mode {_latestSnapshot.FlightMode}, armed={_latestSnapshot.Armed}, " +
                $"battery {_latestSnapshot.BatteryPercent:0.#}%, GPS {_latestSnapshot.GpsSatellites} sat, " +
                $"alt {_latestSnapshot.Altitude:0.#} m, speed {_latestSnapshot.Speed:0.#} m/s."),
            VoiceCommand.BatteryCheck => (true,
                $"Battery {_latestSnapshot.BatteryPercent:0.#}% ({_latestSnapshot.BatteryVoltage:0.##}V)."),
            VoiceCommand.GpsCheck => (true,
                $"GPS: {_latestSnapshot.GpsSatellites} satelit, HDOP {_latestSnapshot.GpsHdop:0.##}."),
            VoiceCommand.AltitudeCheck => (true,
                $"Altitude saat ini {_latestSnapshot.Altitude:0.#} meter."),
            VoiceCommand.SpeedCheck => (true,
                $"Kecepatan saat ini {_latestSnapshot.Speed:0.#} m/s."),
            VoiceCommand.MissionStatus => (true,
                $"Misi: mode {_latestSnapshot.FlightMode}, waypoint saat ini {_latestSnapshot.MissionCurrentWaypoint}, " +
                $"total waypoint {_latestSnapshot.MissionTotalWaypoints}, armed={_latestSnapshot.Armed}."),
            VoiceCommand.WaypointStatus => (true,
                _latestSnapshot.MissionTotalWaypoints > 0
                    ? $"Waypoint aktif {_latestSnapshot.MissionCurrentWaypoint} dari {_latestSnapshot.MissionTotalWaypoints}."
                    : "Data waypoint belum tersedia. Pastikan mission sudah dimuat."),
            VoiceCommand.CalibrationStatus => (true,
                $"Status kalibrasi: compass1 {_latestSnapshot.CompassCalibrationProgress1}%, " +
                $"compass2 {_latestSnapshot.CompassCalibrationProgress2}%, " +
                $"GPS {_latestSnapshot.GpsSatellites} satelit. " +
                "Eksekusi kalibrasi hardware tetap harus lewat halaman Calibration."),
            _ => (false, "Perintah telemetry tidak dikenali.")
        };
    }

    private (VoiceCommand Command, float Confidence) MatchCommand(string normalizedText)
    {
        var bestCommand = VoiceCommand.Unknown;
        var bestScore = 0f;
        var bestAliasLength = 0;

        foreach (var (command, aliases) in _commandAliases)
        {
            foreach (var alias in aliases)
            {
                var normalizedAlias = Normalize(alias);
                var score = ComputeConfidence(normalizedText, normalizedAlias);
                var isTie = Math.Abs(score - bestScore) < 0.0001f;
                if (score > bestScore || (isTie && normalizedAlias.Length > bestAliasLength))
                {
                    bestScore = score;
                    bestCommand = command;
                    bestAliasLength = normalizedAlias.Length;
                }
            }
        }

        return (bestCommand, bestScore);
    }

    private static float ComputeConfidence(string input, string alias)
    {
        if (string.Equals(input, alias, StringComparison.Ordinal))
        {
            return 1.0f;
        }

        if (input.Contains(alias, StringComparison.Ordinal))
        {
            return 0.9f;
        }

        var inputWords = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var aliasWords = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (aliasWords.Length > 0)
        {
            var matches = aliasWords.Count(aw => inputWords.Any(iw => iw == aw));
            var ratio = (float)matches / aliasWords.Length;
            var wordScore = 0.7f * ratio;

            if (wordScore >= 0.5f)
            {
                return wordScore;
            }
        }

        var distance = LevenshteinDistance(input, alias);
        var maxLen = Math.Max(input.Length, alias.Length);
        if (maxLen == 0)
        {
            return 0;
        }

        var similarity = 1f - ((float)distance / maxLen);
        return 0.8f * Math.Max(0, similarity);
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var matrix = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= target.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }

    private static float ExtractNumber(string text)
    {
        var match = Regex.Match(text, "(?<!\\d)(\\d+(?:[\\.,]\\d+)?)(?!\\d)");
        if (!match.Success)
        {
            return 0;
        }

        var valueText = match.Groups[1].Value.Replace(',', '.');
        return float.TryParse(valueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string Normalize(string text)
    {
        return (text ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace("  ", " ");
    }

    private bool TryCancelPendingCommand(string normalizedText, out string message)
    {
        message = string.Empty;
        if (!IsCancellationText(normalizedText))
        {
            return false;
        }

        if (_pendingCriticalCommand == null || _pendingCriticalCommand.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _pendingCriticalCommand = null;
            message = "Tidak ada perintah kritis yang menunggu konfirmasi.";
            return true;
        }

        var pending = _pendingCriticalCommand.Command;
        _pendingCriticalCommand = null;
        message = $"Perintah kritis {pending} dibatalkan.";
        return true;
    }

    private bool TryConfirmPendingCommand(VoiceCommand command, out string message)
    {
        message = string.Empty;
        var pending = _pendingCriticalCommand;
        if (pending == null)
        {
            message = "Tidak ada perintah kritis yang menunggu konfirmasi.";
            return false;
        }

        if (pending.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _pendingCriticalCommand = null;
            message = "Konfirmasi kedaluwarsa. Ulangi perintah kritis lalu konfirmasi lagi.";
            return false;
        }

        if (pending.Command != command)
        {
            message = $"Konfirmasi tidak cocok. Perintah yang menunggu konfirmasi: {pending.Command}.";
            return false;
        }

        _pendingCriticalCommand = null;
        return true;
    }

    private static bool RequiresCriticalConfirmation(VoiceCommand command)
    {
        return command is
            VoiceCommand.Arm or
            VoiceCommand.Disarm or
            VoiceCommand.Takeoff or
            VoiceCommand.Land or
            VoiceCommand.ReturnToLaunch or
            VoiceCommand.EmergencyStop or
            VoiceCommand.StartMission or
            VoiceCommand.GoToWaypoint or
            VoiceCommand.NextWaypoint or
            VoiceCommand.ClearMission or
            VoiceCommand.SetHome;
    }

    private static bool IsConfirmationText(string normalizedText)
    {
        return normalizedText.Contains("konfirmasi", StringComparison.Ordinal) ||
               normalizedText.Contains("confirm", StringComparison.Ordinal);
    }

    private static bool IsCancellationText(string normalizedText)
    {
        return normalizedText.Contains("batal", StringComparison.Ordinal) ||
               normalizedText.Contains("cancel", StringComparison.Ordinal);
    }

    private static string BuildConfirmationPrompt(VoiceCommand command)
    {
        return $"Perintah kritis {command} dikenali. Ucapkan 'konfirmasi {command}' dalam 15 detik untuk eksekusi, atau ucapkan 'batal'.";
    }

    private static Dictionary<VoiceCommand, string[]> BuildCommandAliases()
    {
        return new Dictionary<VoiceCommand, string[]>
        {
            [VoiceCommand.Arm] = new[] { "arm", "arm drone", "aktifkan motor" },
            [VoiceCommand.Disarm] = new[] { "disarm", "disarm drone", "matikan motor" },
            [VoiceCommand.Takeoff] = new[] { "takeoff", "lepas landas", "terbang" },
            [VoiceCommand.Land] = new[] { "land", "landing", "mendarat" },
            [VoiceCommand.ReturnToLaunch] = new[] { "rtl", "return to launch", "pulang", "kembali home" },
            [VoiceCommand.PauseMission] = new[] { "pause", "pause mission", "hentikan misi sementara" },
            [VoiceCommand.ResumeMission] = new[] { "resume", "resume mission", "lanjutkan misi" },
            [VoiceCommand.EmergencyStop] = new[] { "emergency stop", "berhenti darurat" },
            [VoiceCommand.SetAltitude] = new[] { "set altitude", "altitude", "ketinggian" },
            [VoiceCommand.SetSpeed] = new[] { "set speed", "speed", "kecepatan" },
            [VoiceCommand.ModeStabilize] = new[] { "stabilize", "mode stabilize" },
            [VoiceCommand.ModeLoiter] = new[] { "loiter", "mode loiter" },
            [VoiceCommand.ModeAuto] = new[] { "auto", "mode auto" },
            [VoiceCommand.ModeGuided] = new[] { "guided", "mode guided" },
            [VoiceCommand.ModeCircle] = new[] { "circle", "mode circle" },
            [VoiceCommand.ModeFollow] = new[] { "follow", "mode follow" },
            [VoiceCommand.ModePoshold] = new[] { "poshold", "position hold" },
            [VoiceCommand.ModeAcro] = new[] { "acro", "mode acro" },
            [VoiceCommand.StartMission] = new[] { "start mission", "mulai misi" },
            [VoiceCommand.GoToWaypoint] = new[] { "go to waypoint", "ke waypoint", "waypoint" },
            [VoiceCommand.NextWaypoint] = new[] { "next waypoint", "waypoint berikutnya" },
            [VoiceCommand.ClearMission] = new[] { "clear mission", "hapus misi" },
            [VoiceCommand.HoldPosition] = new[] { "hold position", "tahan posisi", "hover disini" },
            [VoiceCommand.SetHome] = new[] { "set home", "set home here", "jadikan home disini" },
            [VoiceCommand.RequestLogs] = new[] { "request logs", "ambil log", "minta log", "diagnostic logs" },
            [VoiceCommand.TakePhoto] = new[] { "take photo", "ambil foto" },
            [VoiceCommand.StartRecording] = new[] { "start recording", "mulai rekam" },
            [VoiceCommand.StopRecording] = new[] { "stop recording", "stop rekam" },
            [VoiceCommand.ZoomIn] = new[] { "zoom in", "perbesar" },
            [VoiceCommand.ZoomOut] = new[] { "zoom out", "perkecil" },
            [VoiceCommand.CenterCamera] = new[] { "center camera", "tengah kamera" },
            [VoiceCommand.Status] = new[] { "status", "status drone" },
            [VoiceCommand.BatteryCheck] = new[] { "battery", "cek baterai", "status baterai" },
            [VoiceCommand.GpsCheck] = new[] { "gps", "cek gps", "status gps" },
            [VoiceCommand.AltitudeCheck] = new[] { "cek altitude", "cek ketinggian" },
            [VoiceCommand.SpeedCheck] = new[] { "cek speed", "cek kecepatan" },
            [VoiceCommand.MissionStatus] = new[] { "status misi", "mission status", "misi aktif" },
            [VoiceCommand.WaypointStatus] = new[] { "status waypoint", "berapa waypoint", "waypoint status" },
            [VoiceCommand.CalibrationStatus] = new[] { "status kalibrasi", "calibration status", "status prearm" },
            [VoiceCommand.GimbalDown] = new[] { "gimbal down", "kamera bawah" },
            [VoiceCommand.GimbalUp] = new[] { "gimbal up", "kamera atas" },
            [VoiceCommand.GimbalForward] = new[] { "gimbal forward", "kamera depan" },
            [VoiceCommand.EnableGeofence] = new[] { "enable geofence", "aktifkan geofence" },
            [VoiceCommand.DisableGeofence] = new[] { "disable geofence", "matikan geofence" }
        };
    }

    private async Task PersistCommandAuditAsync(VoiceCommandResult result)
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            await _historyStore.SaveCommandAuditEntryAsync(new CommandAuditEntry
            {
                Timestamp = result.Timestamp,
                InputText = result.RawText,
                Command = result.Command,
                Confidence = result.Confidence,
                IsValid = result.IsValid,
                IsExecuted = result.IsExecuted,
                ResultMessage = result.Message,
                Source = IsListening ? "voice" : "text"
            });
        }
        catch
        {
        }
    }
}

internal sealed record PendingCriticalCommand(VoiceCommand Command, DateTime ExpiresAtUtc);

internal static class CommandValidator
{
    public static CommandValidationResult Validate(VoiceCommand command, TelemetrySnapshot? snapshot, bool isConnected)
    {
        if (RequiresConnection(command) && !isConnected)
        {
            return new CommandValidationResult(false, "Koneksi MAVLink tidak aktif.");
        }

        snapshot ??= new TelemetrySnapshot
        {
            BatteryPercent = 100,
            GpsSatellites = 10,
            Altitude = 0,
            Armed = false
        };

        return command switch
        {
            VoiceCommand.Arm when snapshot.GpsSatellites < 6 => new CommandValidationResult(false, "ARM ditolak: GPS < 6 satelit."),
            VoiceCommand.Arm when snapshot.BatteryPercent <= 20 => new CommandValidationResult(false, "ARM ditolak: baterai <= 20%."),
            VoiceCommand.Disarm when snapshot.Altitude > 2 => new CommandValidationResult(false, "DISARM ditolak: ketinggian > 2m."),
            VoiceCommand.Takeoff when !snapshot.Armed => new CommandValidationResult(false, "Takeoff ditolak: drone belum ARM."),
            VoiceCommand.Takeoff when snapshot.BatteryPercent <= 20 => new CommandValidationResult(false, "Takeoff ditolak: baterai <= 20%."),
            _ => new CommandValidationResult(true, "Valid")
        };
    }

    private static bool RequiresConnection(VoiceCommand command)
    {
        return command is not VoiceCommand.Status
            and not VoiceCommand.BatteryCheck
            and not VoiceCommand.GpsCheck
            and not VoiceCommand.AltitudeCheck
            and not VoiceCommand.SpeedCheck
            and not VoiceCommand.MissionStatus
            and not VoiceCommand.WaypointStatus
            and not VoiceCommand.CalibrationStatus
            and not VoiceCommand.TakePhoto
            and not VoiceCommand.StartRecording
            and not VoiceCommand.StopRecording
            and not VoiceCommand.ZoomIn
            and not VoiceCommand.ZoomOut
            and not VoiceCommand.Unknown;
    }
}

internal readonly record struct CommandValidationResult(bool IsValid, string Message);
