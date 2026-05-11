#if !__WASM__
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Services.AI;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.ViewModels;

/// <summary>
/// PIA Chat ViewModel — MVVM ViewModel for the PIA chat panel.
/// Manages chat messages, connects to NaturalLanguageService, and exposes health status.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly NaturalLanguageService _naturalLanguageService;
    private readonly IAnomalyDetectionService? _anomalyDetectionService;
    private readonly TelemetryAnalysisService? _telemetryAnalysisService;
    private readonly IVoiceCommandService? _voiceCommandService;
    private readonly MaintenancePredictionService? _maintenancePredictionService;
    private readonly PerformanceScoringService? _performanceScoringService;
    private readonly BatteryPredictionService? _batteryPredictionService;
    private readonly IAnomalyEvaluationService? _anomalyEvaluationService;
    private readonly FlightSessionSummaryService? _flightSessionSummaryService;
    private readonly LLMServiceFactory? _llmServiceFactory;
    private readonly AISettings? _aiSettings;
    private readonly IMavLinkService? _mavLinkService;
    private readonly IPIAHistoryStore? _historyStore;
    private readonly ISpeechService? _speechService;
    private readonly DispatcherQueue? _dispatcherQueue;

    private TelemetryAnalysis? _latestAnalysis;
    private bool _isMuted;
    private DateTime _lastAdvancedRefresh = DateTime.MinValue;
    private int _isRefreshingAdvanced;
    private DateTime _lastAlertSpeechAt = DateTime.MinValue;
    private string _lastAlertSpeechText = string.Empty;
    private DateTime _lastTelemetryAt = DateTime.MinValue;
    private double _rollingChatLatencyMs;
    private double _rollingVoiceLatencyMs;
    private double _rollingLlmLatencyMs;
    private double _rollingAnomalyLatencyMs;
    private double _rollingTelemetryRateHz;
    private static readonly TimeSpan AlertSpeechCooldown = TimeSpan.FromSeconds(8);
    private const double ValidationTargetPrecision = 0.85;
    private const double ValidationTargetRecall = 0.90;
    private const double ValidationTargetBatteryMapePercent = 15.0;
    private const double ValidationTargetMaxLlmLatencyMs = 3000.0;

    /// <summary>
    /// Quick commands list for the chat panel (8 items)
    /// </summary>
    public ObservableCollection<QuickCommand> QuickCommands { get; } = new()
    {
        new QuickCommand { Label = "Status", Query = "status lengkap drone" },
        new QuickCommand { Label = "Battery", Query = "cek baterai" },
        new QuickCommand { Label = "GPS", Query = "status GPS" },
        new QuickCommand { Label = "Analysis", Query = "analisis keamanan misi" },
        new QuickCommand { Label = "Takeoff", Query = "takeoff" },
        new QuickCommand { Label = "Land", Query = "landing sekarang" },
        new QuickCommand { Label = "RTL", Query = "return to launch" },
        new QuickCommand { Label = "ARM", Query = "arm drone" },
    };

    // ── Observable Properties ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private bool _isListening;

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _healthStatus = "Disconnected";

    [ObservableProperty]
    private int _batteryPercent;

    [ObservableProperty]
    private int _gpsCount;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isVoiceAvailable;

    [ObservableProperty]
    private string _lastRecognizedCommand = "-";

    [ObservableProperty]
    private string _voiceExecutionStatus = "voice idle";

    [ObservableProperty]
    private float _lastVoiceConfidence;

    [ObservableProperty]
    private string _insightStatus = "UNKNOWN";

    [ObservableProperty]
    private string _insightSummary = "Belum ada analisis.";

    [ObservableProperty]
    private string _performanceGrade = "-";

    [ObservableProperty]
    private double _performanceScore;

    [ObservableProperty]
    private string _performanceFeedback = "Belum ada skor performa.";

    [ObservableProperty]
    private ObservableCollection<Anomaly> _recentAnomalies = new();

    [ObservableProperty]
    private ObservableCollection<string> _maintenanceSummary = new();

    [ObservableProperty]
    private ObservableCollection<MaintenanceTask> _maintenanceTasks = new();

    [ObservableProperty]
    private ObservableCollection<PerformanceTrend> _performanceTrend = new();

    [ObservableProperty]
    private string _batteryForecast = "Belum ada prediksi baterai.";

    [ObservableProperty]
    private string _validationSummary = "Belum ada metrik validasi.";

    [ObservableProperty]
    private string _researchExportStatus = "Belum ada export report.";

    [ObservableProperty]
    private string _lastSessionSummary = "Belum ada ringkasan sesi.";

    [ObservableProperty]
    private string _voiceAvailabilityStatus = "voice unknown";

    [ObservableProperty]
    private string _providerDiagnostic = "provider diagnostic belum tersedia.";

    [ObservableProperty]
    private string _systemReadiness = "AI:unknown | Voice:unknown | Telemetry:unknown | MAVLink:unknown | Fallback:unknown";

    [ObservableProperty]
    private bool _isTelemetryReady;

    [ObservableProperty]
    private bool _isAiReady;

    [ObservableProperty]
    private bool _isFallbackReady;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SendCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand QuickCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand ToggleVoiceListeningCommand { get; }
    public ICommand TogglePIACommand { get; }
    public ICommand ExportResearchJsonCommand { get; }
    public ICommand ExportResearchCsvCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public ChatViewModel(
        NaturalLanguageService naturalLanguageService,
        IAnomalyDetectionService? anomalyDetectionService = null,
        TelemetryAnalysisService? telemetryAnalysisService = null,
        IVoiceCommandService? voiceCommandService = null,
        MaintenancePredictionService? maintenancePredictionService = null,
        PerformanceScoringService? performanceScoringService = null,
        BatteryPredictionService? batteryPredictionService = null,
        IAnomalyEvaluationService? anomalyEvaluationService = null,
        FlightSessionSummaryService? flightSessionSummaryService = null,
        LLMServiceFactory? llmServiceFactory = null,
        AISettings? aiSettings = null,
        IMavLinkService? mavLinkService = null,
        IPIAHistoryStore? historyStore = null,
        ISpeechService? speechService = null)
    {
        _naturalLanguageService = naturalLanguageService ?? throw new ArgumentNullException(nameof(naturalLanguageService));
        _anomalyDetectionService = anomalyDetectionService;
        _telemetryAnalysisService = telemetryAnalysisService;
        _voiceCommandService = voiceCommandService;
        _maintenancePredictionService = maintenancePredictionService;
        _performanceScoringService = performanceScoringService;
        _batteryPredictionService = batteryPredictionService;
        _anomalyEvaluationService = anomalyEvaluationService;
        _flightSessionSummaryService = flightSessionSummaryService;
        _llmServiceFactory = llmServiceFactory;
        _aiSettings = aiSettings;
        _mavLinkService = mavLinkService;
        _historyStore = historyStore;
        _speechService = speechService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Initialize commands
        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        ClearCommand = new RelayCommand(ClearMessages);
        QuickCommand = new AsyncRelayCommand<string>(ExecuteQuickCommandAsync);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleVoiceListeningCommand = new AsyncRelayCommand(ToggleVoiceListeningAsync);
        TogglePIACommand = new RelayCommand(TogglePIA);
        ExportResearchJsonCommand = new AsyncRelayCommand(() => ExportResearchReportAsync("json"));
        ExportResearchCsvCommand = new AsyncRelayCommand(() => ExportResearchReportAsync("csv"));

        // Subscribe to events
        if (_anomalyDetectionService != null)
        {
            _anomalyDetectionService.AnomaliesDetected += OnAnomaliesDetected;
        }

        if (_telemetryAnalysisService != null)
        {
            _telemetryAnalysisService.AnalysisCompleted += OnAnalysisCompleted;
        }

        if (_voiceCommandService != null)
        {
            _voiceCommandService.CommandRecognized += OnVoiceCommandRecognized;
            _voiceCommandService.RecognitionError += OnVoiceRecognitionError;
            IsVoiceAvailable = _voiceCommandService.IsAvailable;
            IsListening = _voiceCommandService.IsListening;
            VoiceAvailabilityStatus = _voiceCommandService.AvailabilityReason;
        }

        if (_maintenancePredictionService != null)
        {
            _maintenancePredictionService.ScheduleUpdated += OnMaintenanceScheduleUpdated;
        }

        if (_performanceScoringService != null)
        {
            _performanceScoringService.ScoreUpdated += OnPerformanceScoreUpdated;
        }

        if (_batteryPredictionService != null)
        {
            _batteryPredictionService.PredictionUpdated += OnBatteryPredictionUpdated;
            _batteryPredictionService.MetricsUpdated += OnBatteryMetricsUpdated;
        }

        if (_anomalyEvaluationService != null)
        {
            _anomalyEvaluationService.MetricsUpdated += OnAnomalyMetricsUpdated;
        }

        if (_flightSessionSummaryService != null)
        {
            _flightSessionSummaryService.SummaryGenerated += OnSessionSummaryGenerated;
        }

        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived += OnTelemetryReceived;
            _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        // Add welcome message
        AddAssistantMessage(
            "Halo! Saya PIA (Pigeon Intelligent Assistant).\n" +
            "Saya dapat membantu Anda dengan:\n" +
            "• Status drone, baterai, dan GPS\n" +
            "• Analisis keamanan misi\n" +
            "• Informasi perintah (takeoff, land, RTL, ARM)\n\n" +
            "Gunakan quick command di bawah atau ketik pertanyaan Anda.",
            ChatUrgency.Normal,
            persist: false);

        _ = InitializePersistedStateAsync();
        RefreshSystemReadiness();
    }

    // ── Command Implementations ───────────────────────────────────────────────

    private bool CanSend() => !string.IsNullOrWhiteSpace(InputText) && !IsThinking;

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsThinking)
            return;

        var userMessage = InputText.Trim();
        InputText = string.Empty;

        // Add user message
        AddUserMessage(userMessage);

        // Process through NaturalLanguageService
        IsThinking = true;
        var chatLatency = Stopwatch.StartNew();
        try
        {
            var response = await _naturalLanguageService.ProcessMessageAsync(userMessage, CancellationToken.None);
            // MVP safety guardrail: PIA is advisory only and never requests command confirmation.
            response.PendingCommand = null;
            response.RequireConfirmation = false;
            response.Confirmed = null;
            AddMessage(response);
        }
        catch (Exception ex)
        {
            AddAssistantMessage(
                $"Maaf, terjadi kesalahan: {ex.Message}",
                ChatUrgency.Warning);
        }
        finally
        {
            chatLatency.Stop();
            UpdateRollingAverage(ref _rollingChatLatencyMs, chatLatency.Elapsed.TotalMilliseconds);
            IsThinking = false;
        }
    }

    private void ClearMessages()
    {
        Messages.Clear();
        _ = ClearPersistedChatHistoryAsync();

        // Re-add welcome message
        AddAssistantMessage(
            "Percakapan telah dihapus. Ada yang bisa saya bantu?",
            ChatUrgency.Normal,
            persist: false);
    }

    private async Task ExecuteQuickCommandAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        InputText = query;
        await SendAsync();
    }

    private void ToggleMute()
    {
        _isMuted = !_isMuted;
        AddAssistantMessage(
            _isMuted ? "TTS dimatikan." : "TTS diaktifkan.",
            ChatUrgency.Normal);
    }

    private async Task ToggleVoiceListeningAsync()
    {
        if (_voiceCommandService == null)
        {
            AddAssistantMessage("Voice command service belum tersedia.", ChatUrgency.Warning);
            VoiceAvailabilityStatus = "service tidak tersedia";
            RefreshSystemReadiness();
            return;
        }

        try
        {
            if (_voiceCommandService.IsListening)
            {
                await _voiceCommandService.StopListeningAsync();
                IsListening = false;
                VoiceExecutionStatus = "voice stopped";
                VoiceAvailabilityStatus = _voiceCommandService.AvailabilityReason;
            }
            else
            {
                IsVoiceAvailable = _voiceCommandService.IsAvailable;
                IsListening = IsVoiceAvailable;
                VoiceExecutionStatus = IsVoiceAvailable ? "listening..." : "voice unavailable";
                VoiceAvailabilityStatus = IsVoiceAvailable
                    ? "voice starting..."
                    : _voiceCommandService.AvailabilityReason;
                RefreshSystemReadiness();

                await _voiceCommandService.StartListeningAsync();
                IsListening = _voiceCommandService.IsListening;
                VoiceExecutionStatus = IsListening ? "listening..." : "voice unavailable";
                VoiceAvailabilityStatus = _voiceCommandService.AvailabilityReason;

                if (!IsListening && !_voiceCommandService.IsAvailable)
                {
                    AddAssistantMessage($"Voice diagnostic: {_voiceCommandService.AvailabilityReason}", ChatUrgency.Warning);
                }
            }

            IsVoiceAvailable = _voiceCommandService.IsAvailable;
            RefreshSystemReadiness();
        }
        catch (Exception ex)
        {
            AddAssistantMessage($"Gagal mengubah mode listening: {ex.Message}", ChatUrgency.Warning);
            VoiceAvailabilityStatus = _voiceCommandService.LastError ?? ex.Message;
            RefreshSystemReadiness();
        }
    }

    private void TogglePIA()
    {
        IsOpen = !IsOpen;
    }

    // ── Event Handlers ──────────────────────────────────────────────────────────

    private void OnAnomaliesDetected(object? sender, AnomaliesDetectedEventArgs e)
    {
        if (e.Anomalies.Count > 0)
        {
            var now = DateTime.UtcNow;
            var latencySamples = e.Anomalies
                .Select(a => Math.Max(0, (now - a.Timestamp.ToUniversalTime()).TotalMilliseconds))
                .ToList();

            if (latencySamples.Count > 0)
            {
                UpdateRollingAverage(ref _rollingAnomalyLatencyMs, latencySamples.Average());
            }
        }

        RunOnUIThread(() =>
        {
            foreach (var anomaly in e.Anomalies)
            {
                var urgency = anomaly.Severity switch
                {
                    AnomalySeverity.Critical => ChatUrgency.Critical,
                    AnomalySeverity.Warning => ChatUrgency.Warning,
                    _ => ChatUrgency.Normal
                };

                var message = $"[ANOMALY] **Anomaly Detected** ({e.SourceLayer})\n" +
                             $"**Type:** {anomaly.Type}\n" +
                             $"**Message:** {anomaly.Message}";

                if (!string.IsNullOrEmpty(anomaly.Recommendation))
                {
                    message += $"\n**Recommendation:** {anomaly.Recommendation}";
                }

                AddAssistantMessage(message, urgency);
                RecentAnomalies.Insert(0, anomaly);
                _ = PersistAnomalyAsync(anomaly, e.SourceLayer);

                if (anomaly.Severity is AnomalySeverity.Warning or AnomalySeverity.Critical)
                {
                    var alertSpeech = $"Peringatan {anomaly.Type}. {anomaly.Message}";
                    _ = SpeakAlertIfNeededAsync(alertSpeech);
                }
            }

            while (RecentAnomalies.Count > 10)
            {
                RecentAnomalies.RemoveAt(RecentAnomalies.Count - 1);
            }
        });
    }

    private void OnAnalysisCompleted(object? sender, TelemetryAnalysis analysis)
    {
        RunOnUIThread(() =>
        {
            _latestAnalysis = analysis;
            IsTelemetryReady = true;
            InsightStatus = analysis.OverallStatus;
            InsightSummary = analysis.KeyInsights;

            // Only notify if status is warning or critical
            if (analysis.OverallStatus is "WARNING" or "CRITICAL")
            {
                var urgency = analysis.OverallStatus == "CRITICAL"
                    ? ChatUrgency.Critical
                    : ChatUrgency.Warning;

                var message = $"[ANALYSIS] **Telemetry Analysis**\n" +
                             $"**Status:** {analysis.OverallStatus}\n" +
                             $"**Insights:** {analysis.KeyInsights}";

                if (analysis.Warnings.Any())
                {
                    message += $"\n**Warnings:** {string.Join(", ", analysis.Warnings.Take(3))}";
                }

                AddAssistantMessage(message, urgency);
                _ = SpeakAlertIfNeededAsync(
                    $"Status {analysis.OverallStatus}. {analysis.KeyInsights}",
                    interrupt: true);
            }

            RefreshSystemReadiness();
        });
    }

    private void OnVoiceCommandRecognized(object? sender, VoiceCommandResult result)
    {
        _ = HandleVoiceCommandRecognizedAsync(result);
    }

    private async Task HandleVoiceCommandRecognizedAsync(VoiceCommandResult result)
    {
        var voiceLatencyMs = Math.Max(0, (DateTime.UtcNow - result.Timestamp.ToUniversalTime()).TotalMilliseconds);
        UpdateRollingAverage(ref _rollingVoiceLatencyMs, voiceLatencyMs);

        RunOnUIThread(() =>
        {
            IsListening = _voiceCommandService?.IsListening == true;
            LastRecognizedCommand = $"{result.Command}";
            LastVoiceConfidence = result.Confidence;
            VoiceExecutionStatus = result.IsExecuted ? "executed" : result.IsValid ? "recognized" : "rejected";
            RefreshSystemReadiness();
        });

        if (ShouldRouteVoiceToChat(result))
        {
            await ProcessVoiceAsChatAsync(result.RawText);
            return;
        }

        RunOnUIThread(() =>
        {
            var urgency = result.IsExecuted
                ? ChatUrgency.Success
                : result.IsValid
                    ? ChatUrgency.Warning
                    : ChatUrgency.Critical;

            AddAssistantMessage(
                $"[VOICE] **Input:** {result.RawText}\n" +
                $"• Command: {result.Command}\n" +
                $"• Confidence: {result.Confidence:P0}\n" +
                $"• Status: {result.Message}",
                urgency);
        });
    }

    private bool ShouldRouteVoiceToChat(VoiceCommandResult result)
    {
        if (result.Command != VoiceCommand.Unknown)
        {
            return false;
        }

        // Keep internal voice-control flows (e.g., pending critical cancel/confirm feedback)
        // on the command lane and route true unknown utterances to chat.
        if (result.IsValid)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(result.RawText))
        {
            return false;
        }

        return true;
    }

    private async Task ProcessVoiceAsChatAsync(string rawText)
    {
        var chatLatency = Stopwatch.StartNew();
        RunOnUIThread(() =>
        {
            VoiceExecutionStatus = "voice -> chat";
            AddUserMessage(rawText);
            IsThinking = true;
        });

        try
        {
            var response = await _naturalLanguageService.ProcessMessageAsync(rawText, CancellationToken.None);
            // Keep PIA in advisory mode for voice path as well.
            response.PendingCommand = null;
            response.RequireConfirmation = false;
            response.Confirmed = null;

            RunOnUIThread(() => AddMessage(response));
            _ = SpeakIfAvailableAsync(CleanForSpeech(response.Content), interrupt: false);
        }
        catch (Exception ex)
        {
            RunOnUIThread(() =>
            {
                AddAssistantMessage(
                    $"Gagal memproses voice sebagai chat: {ex.Message}",
                    ChatUrgency.Warning);
            });
        }
        finally
        {
            chatLatency.Stop();
            UpdateRollingAverage(ref _rollingChatLatencyMs, chatLatency.Elapsed.TotalMilliseconds);
            RunOnUIThread(() => IsThinking = false);
        }
    }

    private async Task SpeakIfAvailableAsync(string text, bool interrupt)
    {
        if (_isMuted || _speechService == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            await _speechService.SpeakAsync(text, interrupt);
        }
        catch
        {
        }
    }

    private async Task SpeakAlertIfNeededAsync(string text, bool interrupt = true)
    {
        var normalized = CleanForSpeech(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (string.Equals(normalized, _lastAlertSpeechText, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastAlertSpeechAt) < AlertSpeechCooldown)
        {
            return;
        }

        _lastAlertSpeechText = normalized;
        _lastAlertSpeechAt = now;
        await SpeakIfAvailableAsync(normalized, interrupt);
    }

    private static string CleanForSpeech(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = text
            .Replace("**", " ", StringComparison.Ordinal)
            .Replace("`", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        while (clean.Contains("  ", StringComparison.Ordinal))
        {
            clean = clean.Replace("  ", " ", StringComparison.Ordinal);
        }

        return clean.Length > 240
            ? $"{clean[..240]}..."
            : clean;
    }

    private void OnVoiceRecognitionError(object? sender, string error)
    {
        RunOnUIThread(() =>
        {
            IsListening = _voiceCommandService?.IsListening == true;
            VoiceExecutionStatus = "voice error";
            VoiceAvailabilityStatus = string.IsNullOrWhiteSpace(error)
                ? _voiceCommandService?.LastError ?? "voice error"
                : error;
            IsVoiceAvailable = _voiceCommandService?.IsAvailable == true;
            if (!IsListening)
            {
                VoiceExecutionStatus = IsVoiceAvailable ? "voice idle" : "voice unavailable";
            }
            AddAssistantMessage($"Voice error: {error}", ChatUrgency.Warning);
            RefreshSystemReadiness();
        });
    }

    private void OnMaintenanceScheduleUpdated(object? sender, MaintenanceSchedule schedule)
    {
        RunOnUIThread(() =>
        {
            UpdateMaintenanceCollections(schedule);
        });
    }

    private void OnPerformanceScoreUpdated(object? sender, PerformanceScore score)
    {
        RunOnUIThread(() =>
        {
            PerformanceScore = score.TotalScore;
            PerformanceGrade = score.Grade;
            PerformanceFeedback = score.Feedback;
        });

        _ = PersistPerformanceAndRefreshTrendAsync(score);
    }

    private void OnBatteryPredictionUpdated(object? sender, BatteryPrediction prediction)
    {
        RunOnUIThread(() =>
        {
            BatteryForecast =
                $"Sisa {prediction.EstimatedRemainingMinutes:0} menit | " +
                $"Drain {prediction.EstimatedDrainRatePerMinute:0.00}%/min | " +
                $"Kondisi {prediction.Condition} | Confidence {prediction.Confidence:P0}";
        });

        _ = PersistBatteryPredictionAsync(prediction);
    }

    private void OnBatteryMetricsUpdated(object? sender, BatteryPredictionMetrics metrics)
    {
        UpdateValidationSummary();
        _ = PersistValidationSnapshotAsync();
    }

    private void OnAnomalyMetricsUpdated(object? sender, AnomalyEvaluationMetrics metrics)
    {
        UpdateValidationSummary();
        _ = PersistValidationSnapshotAsync();
    }

    private void OnSessionSummaryGenerated(object? sender, FlightSessionSummary summary)
    {
        RunOnUIThread(() =>
        {
            LastSessionSummary = summary.SummaryText;
        });
    }

    private void OnTelemetryReceived(object? sender, FlightData data)
    {
        var now = DateTime.UtcNow;
        if (_lastTelemetryAt != DateTime.MinValue)
        {
            var deltaMs = (now - _lastTelemetryAt).TotalMilliseconds;
            if (deltaMs > 0.1)
            {
                var hz = 1000.0 / deltaMs;
                if (hz is > 0 and < 500)
                {
                    UpdateRollingAverage(ref _rollingTelemetryRateHz, hz, 0.15);
                }
            }
        }
        _lastTelemetryAt = now;

        RunOnUIThread(() =>
        {
            // Update health status properties from telemetry
            // BatteryVolt is in centi-volts (e.g., 1260 = 12.6V), estimate percentage from voltage
            var voltage = data.BatteryVolt / 100.0f; // Convert to volts
            BatteryPercent = EstimateBatteryPercent(voltage);
            GpsCount = data.Sats;
            IsConnected = true;
            IsTelemetryReady = true;

            // Determine health status
            HealthStatus = DetermineHealthStatus(data);
            RefreshSystemReadiness();
        });

        _ = RefreshAdvancedPanelsAsync();
    }

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        RunOnUIThread(() =>
        {
            IsConnected = isConnected;
            if (!isConnected)
            {
                HealthStatus = "Disconnected";
                BatteryPercent = 0;
                GpsCount = 0;
            }

            if (!isConnected && _latestAnalysis == null)
            {
                IsTelemetryReady = false;
            }
            RefreshSystemReadiness();
        });
    }

    private async Task RefreshAdvancedPanelsAsync()
    {
        if (Interlocked.Exchange(ref _isRefreshingAdvanced, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastAdvancedRefresh).TotalSeconds < 30)
            {
                return;
            }

            _lastAdvancedRefresh = now;

            if (_performanceScoringService != null)
            {
                var score = await _performanceScoringService.CalculateScoreAsync();
                RunOnUIThread(() =>
                {
                    PerformanceScore = score.TotalScore;
                    PerformanceGrade = score.Grade;
                    PerformanceFeedback = score.Feedback;
                });
            }

            if (_maintenancePredictionService != null)
            {
                var schedule = await _maintenancePredictionService.GenerateScheduleAsync();
                RunOnUIThread(() =>
                {
                    UpdateMaintenanceCollections(schedule);
                });
            }

            if (_batteryPredictionService != null)
            {
                var prediction = await _batteryPredictionService.PredictAsync();
                if (prediction != null)
                {
                    RunOnUIThread(() =>
                    {
                        BatteryForecast =
                            $"Sisa {prediction.EstimatedRemainingMinutes:0} menit | " +
                            $"Drain {prediction.EstimatedDrainRatePerMinute:0.00}%/min | " +
                            $"Kondisi {prediction.Condition} | Confidence {prediction.Confidence:P0}";
                    });
                }
            }

            UpdateValidationSummary();
        }
        catch (Exception ex)
        {
            RunOnUIThread(() =>
            {
                AddAssistantMessage($"Gagal refresh panel PIA: {ex.Message}", ChatUrgency.Warning);
            });
        }
        finally
        {
            Interlocked.Exchange(ref _isRefreshingAdvanced, 0);
        }
    }

    private void RefreshSystemReadiness()
    {
        var primaryHealth = _llmServiceFactory?.PrimaryService?.GetHealthStatus();
        var fallbackHealth = _llmServiceFactory?.FallbackService?.GetHealthStatus();
        var circuitStatus = _llmServiceFactory?.GetCircuitStatus();

        var voiceReady = _voiceCommandService?.IsAvailable == true;
        var voiceReason = _voiceCommandService?.AvailabilityReason ?? "voice service unavailable";
        var aiReady = primaryHealth?.IsConnected == true || fallbackHealth?.IsConnected == true;
        var fallbackReady = fallbackHealth?.IsConnected == true;
        var telemetryReady = IsTelemetryReady;
        var mavlinkReady = IsConnected;
        var llmLatency = primaryHealth?.LastLatencyMs ?? 0;
        if ((llmLatency <= 0) && (fallbackHealth?.LastLatencyMs > 0))
        {
            llmLatency = fallbackHealth.LastLatencyMs;
        }
        if (llmLatency > 0)
        {
            UpdateRollingAverage(ref _rollingLlmLatencyMs, llmLatency);
        }

        RunOnUIThread(() =>
        {
            IsAiReady = aiReady;
            IsFallbackReady = fallbackReady;
            IsVoiceAvailable = voiceReady;
            VoiceAvailabilityStatus = voiceReason;

            var primaryProvider = _llmServiceFactory?.PrimaryService?.ProviderName
                ?? _aiSettings?.Provider
                ?? "unknown";
            var fallbackProvider = _llmServiceFactory?.FallbackService?.ProviderName
                ?? _aiSettings?.FallbackProvider
                ?? "unknown";

            var primaryKey = primaryHealth?.PrimaryApiKeyConfigured == true ? "OK" : "kosong";
            var fallbackKey = fallbackHealth?.PrimaryApiKeyConfigured == true ? "OK" : "kosong";
            var activeProvider = primaryHealth?.ActiveProvider;
            if (string.IsNullOrWhiteSpace(activeProvider))
            {
                activeProvider = primaryHealth?.IsConnected == true
                    ? primaryProvider
                    : fallbackHealth?.IsConnected == true
                        ? fallbackProvider
                        : "offline";
            }

            var circuitText = circuitStatus == null
                ? "n/a"
                : $"{circuitStatus.CircuitState} fail={circuitStatus.ConsecutiveFailures}";

            var primaryCacheRate = primaryHealth?.CacheHitRate ?? 0;
            var fallbackCacheRate = fallbackHealth?.CacheHitRate ?? 0;
            var primaryCacheReq = primaryHealth?.TotalRequests ?? 0;
            var fallbackCacheReq = fallbackHealth?.TotalRequests ?? 0;
            var cacheText = $"Cache P:{primaryCacheRate * 100:0.0}%/{primaryCacheReq} F:{fallbackCacheRate * 100:0.0}%/{fallbackCacheReq}";

            var lastError = primaryHealth?.LastError;
            if (string.IsNullOrWhiteSpace(lastError))
            {
                lastError = fallbackHealth?.LastError;
            }

            ProviderDiagnostic =
                $"Primary {primaryProvider} (key {primaryKey}) | " +
                $"Fallback {fallbackProvider} (key {fallbackKey}) | " +
                $"Active {activeProvider} | Circuit {circuitText} | {cacheText}" +
                (string.IsNullOrWhiteSpace(lastError) ? string.Empty : $" | LastError: {lastError}");

            SystemReadiness =
                $"AI:{(aiReady ? "ready" : "not-ready")} | " +
                $"Voice:{(voiceReady ? "ready" : "not-ready")} | " +
                $"Telemetry:{(telemetryReady ? "ready" : "not-ready")} | " +
                $"MAVLink:{(mavlinkReady ? "ready" : "not-ready")} | " +
                $"Fallback:{(fallbackReady ? "ready" : "not-ready")}";
        });
    }

    // ── Helper Methods ──────────────────────────────────────────────────────────

    private void AddUserMessage(string content, bool persist = true)
    {
        var message = new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.User,
            Content = content,
            Urgency = ChatUrgency.Normal,
            Timestamp = DateTime.UtcNow
        };
        AddMessage(message, persist);
    }

    private void AddAssistantMessage(string content, ChatUrgency urgency, bool persist = true)
    {
        var message = new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = content,
            Urgency = urgency,
            Timestamp = DateTime.UtcNow
        };
        AddMessage(message, persist);
    }

    private void AddMessage(ChatMessage message, bool persist = true)
    {
        // Cap at 50 messages
        while (Messages.Count >= 50)
        {
            Messages.RemoveAt(0);
        }

        Messages.Add(message);

        if (persist)
        {
            _ = PersistMessageAsync(message);
        }
    }

    private void UpdateMaintenanceCollections(MaintenanceSchedule schedule)
    {
        MaintenanceSummary.Clear();
        MaintenanceTasks.Clear();

        foreach (var task in schedule.Tasks.OrderBy(t => t.DueDate))
        {
            MaintenanceTasks.Add(task);
        }

        foreach (var task in schedule.Tasks.OrderBy(t => t.DueDate).Take(5))
        {
            MaintenanceSummary.Add(
                $"{task.Component} [{task.Priority}] - {task.Type} - due {task.DueDate:dd MMM}");
        }
    }

    private async Task InitializePersistedStateAsync()
    {
        if (_historyStore == null)
        {
            await RefreshPerformanceTrendAsync();
            return;
        }

        try
        {
            var retentionDays = Math.Clamp(_aiSettings?.HistoryRetentionDays ?? 30, 1, 3650);
            await _historyStore.ApplyRetentionPolicyAsync(retentionDays);
            var persistedChat = await _historyStore.LoadRecentChatMessagesAsync(120);
            var persistedAnomalies = await _historyStore.LoadRecentAnomaliesAsync(20);
            var persistedBatteryPredictions = await _historyStore.LoadRecentBatteryPredictionsAsync(1);
            var persistedSessionSummaries = await _historyStore.LoadRecentFlightSessionSummariesAsync(1);
            var persistedValidation = await _historyStore.LoadLatestResearchValidationSnapshotAsync();

            RunOnUIThread(() =>
            {
                if (persistedChat.Count > 0)
                {
                    Messages.Clear();
                    foreach (var message in persistedChat.TakeLast(50))
                    {
                        AddMessage(message, persist: false);
                    }
                }

                if (persistedAnomalies.Count > 0)
                {
                    RecentAnomalies.Clear();
                    foreach (var anomaly in persistedAnomalies.Take(10))
                    {
                        RecentAnomalies.Add(anomaly);
                    }
                }

                if (persistedBatteryPredictions.Count > 0)
                {
                    var latest = persistedBatteryPredictions[^1];
                    BatteryForecast =
                        $"Sisa {latest.EstimatedRemainingMinutes:0} menit | " +
                        $"Drain {latest.EstimatedDrainRatePerMinute:0.00}%/min | " +
                        $"Kondisi {latest.Condition} | Confidence {latest.Confidence:P0}";
                }

                if (persistedSessionSummaries.Count > 0)
                {
                    LastSessionSummary = persistedSessionSummaries[^1].SummaryText;
                }

                if (persistedValidation != null)
                {
                    _rollingChatLatencyMs = persistedValidation.ChatLatencyMs;
                    _rollingVoiceLatencyMs = persistedValidation.VoiceLatencyMs;
                    _rollingLlmLatencyMs = persistedValidation.LlmLatencyMs;
                    _rollingAnomalyLatencyMs = persistedValidation.AnomalyLatencyMs;
                    _rollingTelemetryRateHz = persistedValidation.TelemetryRateHz;
                    var gateSummary = BuildValidationGateSummary(
                        persistedValidation.Precision,
                        persistedValidation.Recall,
                        persistedValidation.BatteryMape,
                        _rollingLlmLatencyMs);
                    ValidationSummary =
                        $"Precision {persistedValidation.Precision:P1} | " +
                        $"Recall {persistedValidation.Recall:P1} | " +
                        $"F1 {persistedValidation.F1Score:P1} | " +
                        $"MAPE {persistedValidation.BatteryMape:0.0}% (n={persistedValidation.BatteryMapeSamples})\n" +
                        $"Latency chat {_rollingChatLatencyMs:0}ms | voice {_rollingVoiceLatencyMs:0}ms | llm {_rollingLlmLatencyMs:0}ms | anomaly {_rollingAnomalyLatencyMs:0}ms | telemetry {_rollingTelemetryRateHz:0.0}Hz\n" +
                        gateSummary;
                }
            });
        }
        catch
        {
        }

        await RefreshPerformanceTrendAsync();
        RefreshSystemReadiness();
    }

    private async Task PersistMessageAsync(ChatMessage message)
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            await _historyStore.SaveChatMessageAsync(message);
        }
        catch
        {
        }
    }

    private async Task PersistAnomalyAsync(Anomaly anomaly, string sourceLayer)
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            await _historyStore.SaveAnomalyAsync(anomaly, sourceLayer);
        }
        catch
        {
        }
    }

    private async Task ClearPersistedChatHistoryAsync()
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            await _historyStore.ClearChatHistoryAsync();
        }
        catch
        {
        }
    }

    private async Task PersistPerformanceAndRefreshTrendAsync(PerformanceScore score)
    {
        if (_historyStore != null)
        {
            try
            {
                await _historyStore.SavePerformanceScoreAsync(score);
            }
            catch
            {
            }
        }

        await RefreshPerformanceTrendAsync();
    }

    private async Task PersistBatteryPredictionAsync(BatteryPrediction prediction)
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            await _historyStore.SaveBatteryPredictionAsync(prediction);
        }
        catch
        {
        }
    }

    private async Task PersistValidationSnapshotAsync()
    {
        if (_historyStore == null)
        {
            return;
        }

        try
        {
            var anomalyMetrics = _anomalyEvaluationService?.GetMetrics() ?? new AnomalyEvaluationMetrics();
            var batteryMetrics = _batteryPredictionService?.GetMetrics() ?? new BatteryPredictionMetrics();
            var snapshot = new ResearchValidationSnapshot
            {
                Timestamp = DateTime.UtcNow,
                AnomalySampleCount = anomalyMetrics.SampleCount,
                Precision = anomalyMetrics.Precision,
                Recall = anomalyMetrics.Recall,
                F1Score = anomalyMetrics.F1Score,
                BatteryMape = batteryMetrics.MeanAbsolutePercentageError,
                BatteryMapeSamples = batteryMetrics.SampleCount,
                ChatLatencyMs = _rollingChatLatencyMs,
                VoiceLatencyMs = _rollingVoiceLatencyMs,
                LlmLatencyMs = _rollingLlmLatencyMs,
                AnomalyLatencyMs = _rollingAnomalyLatencyMs,
                TelemetryRateHz = _rollingTelemetryRateHz
            };

            await _historyStore.SaveResearchValidationSnapshotAsync(snapshot);
        }
        catch
        {
        }
    }

    private void UpdateValidationSummary()
    {
        var anomalyMetrics = _anomalyEvaluationService?.GetMetrics();
        var batteryMetrics = _batteryPredictionService?.GetMetrics();

        if (anomalyMetrics == null && batteryMetrics == null)
        {
            return;
        }

        var precision = anomalyMetrics?.Precision ?? 0;
        var recall = anomalyMetrics?.Recall ?? 0;
        var f1 = anomalyMetrics?.F1Score ?? 0;
        var sampleCount = anomalyMetrics?.SampleCount ?? 0;
        var mape = batteryMetrics?.MeanAbsolutePercentageError ?? 0;
        var mapeSamples = batteryMetrics?.SampleCount ?? 0;
        var gateSummary = BuildValidationGateSummary(
            precision,
            recall,
            mape,
            _rollingLlmLatencyMs);

        RunOnUIThread(() =>
        {
            ValidationSummary =
                $"Precision {precision:P1} | Recall {recall:P1} | F1 {f1:P1} " +
                $"(n={sampleCount}) | MAPE {mape:0.0}% (n={mapeSamples})\n" +
                $"Latency chat {_rollingChatLatencyMs:0}ms | voice {_rollingVoiceLatencyMs:0}ms | llm {_rollingLlmLatencyMs:0}ms | anomaly {_rollingAnomalyLatencyMs:0}ms | telemetry {_rollingTelemetryRateHz:0.0}Hz\n" +
                gateSummary;
        });
    }

    private static string BuildValidationGateSummary(
        double precision,
        double recall,
        double batteryMapePercent,
        double llmLatencyMs)
    {
        var precisionOk = precision >= ValidationTargetPrecision;
        var recallOk = recall >= ValidationTargetRecall;
        var mapeOk = batteryMapePercent < ValidationTargetBatteryMapePercent;
        var latencyMeasured = llmLatencyMs > 0;
        var latencyOk = latencyMeasured && llmLatencyMs <= ValidationTargetMaxLlmLatencyMs;
        var allTargetsMet = precisionOk && recallOk && mapeOk && latencyOk;

        var latencyValue = latencyMeasured
            ? $"{llmLatencyMs:0}ms"
            : "n/a";

        return
            $"Target {(allTargetsMet ? "LULUS" : "BELUM")} | " +
            $"P>85% {(precisionOk ? "OK" : "X")} | " +
            $"R>90% {(recallOk ? "OK" : "X")} | " +
            $"MAPE<15% {(mapeOk ? "OK" : "X")} | " +
            $"LLM<3s {(latencyOk ? "OK" : "X")} ({latencyValue})";
    }

    private async Task ExportResearchReportAsync(string format)
    {
        var normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is not ("json" or "csv"))
        {
            return;
        }

        if (_historyStore == null)
        {
            RunOnUIThread(() =>
            {
                ResearchExportStatus = "Export gagal: history store belum tersedia.";
                AddAssistantMessage("Export report gagal: history store belum tersedia.", ChatUrgency.Warning);
            });
            return;
        }

        try
        {
            LLMHealthStatus? providerHealth = null;
            try
            {
                providerHealth = _llmServiceFactory?.GetService()?.GetHealthStatus();
            }
            catch
            {
            }

            var content = normalized == "json"
                ? await _historyStore.ExportResearchReportJsonAsync(300, providerHealth, _aiSettings)
                : await _historyStore.ExportResearchReportCsvAsync(300, providerHealth, _aiSettings);

            var exportRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HarvestmoonGCS",
                "reports");
            Directory.CreateDirectory(exportRoot);

            var fileName = $"pia_research_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{normalized}";
            var fullPath = Path.Combine(exportRoot, fileName);
            await File.WriteAllTextAsync(fullPath, content);

            RunOnUIThread(() =>
            {
                ResearchExportStatus = $"Report {normalized.ToUpperInvariant()} tersimpan: {fullPath}";
                AddAssistantMessage($"Research report {normalized.ToUpperInvariant()} berhasil diekspor.", ChatUrgency.Success);
            });
        }
        catch (Exception ex)
        {
            RunOnUIThread(() =>
            {
                ResearchExportStatus = $"Export gagal: {ex.Message}";
                AddAssistantMessage($"Export report gagal: {ex.Message}", ChatUrgency.Warning);
            });
        }
    }

    private async Task RefreshPerformanceTrendAsync()
    {
        List<PerformanceTrend> trend = new();

        try
        {
            if (_historyStore != null)
            {
                trend = (await _historyStore.LoadRecentPerformanceTrendsAsync(12)).ToList();
            }
            else if (_performanceScoringService != null)
            {
                trend = await _performanceScoringService.GetTrendAsync(12);
            }
        }
        catch
        {
        }

        RunOnUIThread(() =>
        {
            PerformanceTrend.Clear();
            foreach (var item in trend.TakeLast(12))
            {
                PerformanceTrend.Add(item);
            }
        });
    }

    private static string DetermineHealthStatus(FlightData data)
    {
        var batteryPercent = EstimateBatteryPercent(data.BatteryVolt / 100.0f);

        // Critical conditions
        if (batteryPercent > 0 && batteryPercent < 20)
            return "Critical";
        if (data.Sats < 6)
            return "Critical";

        // Warning conditions
        if (batteryPercent > 0 && batteryPercent < 40)
            return "Warning";
        if (data.Sats < 8)
            return "Warning";
        if (data.Hdop > 200)
            return "Warning";

        return "Good";
    }

    private static int EstimateBatteryPercent(float voltage)
    {
        // Estimate battery percentage from voltage (assuming 3S LiPo: 12.6V full, 9.0V empty)
        // This is a rough estimate - actual percentage depends on battery chemistry and load
        if (voltage <= 0) return 0;
        var percent = (int)((voltage - 9.0f) / (12.6f - 9.0f) * 100);
        return Math.Clamp(percent, 0, 100);
    }

    private static void UpdateRollingAverage(ref double currentValue, double sample, double alpha = 0.2)
    {
        if (double.IsNaN(sample) || double.IsInfinity(sample) || sample < 0)
        {
            return;
        }

        var normalizedAlpha = Math.Clamp(alpha, 0.01, 1.0);
        if (currentValue <= 0)
        {
            currentValue = sample;
            return;
        }

        currentValue = (currentValue * (1.0 - normalizedAlpha)) + (sample * normalizedAlpha);
    }

    private void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _ = _dispatcherQueue.TryEnqueue(() => action());
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_anomalyDetectionService != null)
        {
            _anomalyDetectionService.AnomaliesDetected -= OnAnomaliesDetected;
        }

        if (_telemetryAnalysisService != null)
        {
            _telemetryAnalysisService.AnalysisCompleted -= OnAnalysisCompleted;
        }

        if (_voiceCommandService != null)
        {
            _voiceCommandService.CommandRecognized -= OnVoiceCommandRecognized;
            _voiceCommandService.RecognitionError -= OnVoiceRecognitionError;
        }

        if (_maintenancePredictionService != null)
        {
            _maintenancePredictionService.ScheduleUpdated -= OnMaintenanceScheduleUpdated;
        }

        if (_performanceScoringService != null)
        {
            _performanceScoringService.ScoreUpdated -= OnPerformanceScoreUpdated;
        }

        if (_batteryPredictionService != null)
        {
            _batteryPredictionService.PredictionUpdated -= OnBatteryPredictionUpdated;
            _batteryPredictionService.MetricsUpdated -= OnBatteryMetricsUpdated;
        }

        if (_anomalyEvaluationService != null)
        {
            _anomalyEvaluationService.MetricsUpdated -= OnAnomalyMetricsUpdated;
        }

        if (_flightSessionSummaryService != null)
        {
            _flightSessionSummaryService.SummaryGenerated -= OnSessionSummaryGenerated;
        }

        if (_mavLinkService != null)
        {
            _mavLinkService.TelemetryReceived -= OnTelemetryReceived;
            _mavLinkService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        }
    }
}
#endif
