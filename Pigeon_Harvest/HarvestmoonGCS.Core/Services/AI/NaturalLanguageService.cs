#if !__WASM__
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// PIA Natural Language Service — handles chat interactions with telemetry context.
/// Classifies intent via keyword matching and falls back to LLM for unknown queries.
/// Advisory only: never executes drone commands.
/// </summary>
public class NaturalLanguageService
{
    private readonly ILLMService? _llmService;
    private readonly Func<ILLMService>? _llmServiceFactory;
    private readonly TelemetryBuffer _telemetryBuffer;
    private readonly List<ChatMessage> _messageHistory = new();
    private const int MaxMessages = 50;

    /// <summary>
    /// Creates a new NaturalLanguageService.
    /// </summary>
    /// <param name="llmService">LLM service obtained from LLMServiceFactory.GetService()</param>
    /// <param name="telemetryBuffer">Telemetry buffer for context-aware responses</param>
    public NaturalLanguageService(ILLMService llmService, TelemetryBuffer telemetryBuffer)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
    }

    /// <summary>
    /// Creates a new NaturalLanguageService with deferred LLM resolution.
    /// Useful when API credentials can change at runtime (no app restart required).
    /// </summary>
    /// <param name="llmServiceFactory">Factory returning the current active LLM service</param>
    /// <param name="telemetryBuffer">Telemetry buffer for context-aware responses</param>
    public NaturalLanguageService(Func<ILLMService> llmServiceFactory, TelemetryBuffer telemetryBuffer)
    {
        _llmServiceFactory = llmServiceFactory ?? throw new ArgumentNullException(nameof(llmServiceFactory));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
    }

    /// <summary>Read-only view of conversation history (capped at 50 messages)</summary>
    public IReadOnlyList<ChatMessage> MessageHistory => _messageHistory.AsReadOnly();

    /// <summary>
    /// Returns the 8 quick command shortcuts for the PIA chat panel.
    /// Matches PIGEON React UI quick commands in piaEngine.ts.
    /// </summary>
    public List<QuickCommand> GetQuickCommands() => new()
    {
        new QuickCommand { Label = "Status",   Query = "status lengkap drone" },
        new QuickCommand { Label = "Baterai",  Query = "cek baterai" },
        new QuickCommand { Label = "GPS",      Query = "status GPS" },
        new QuickCommand { Label = "Analisis", Query = "analisis keamanan misi" },
        new QuickCommand { Label = "Takeoff",  Query = "takeoff" },
        new QuickCommand { Label = "Land",     Query = "landing sekarang" },
        new QuickCommand { Label = "RTL",      Query = "return to launch" },
        new QuickCommand { Label = "ARM",      Query = "arm drone" },
    };

    /// <summary>
    /// Main entry point. Processes a user message, adds it to history,
    /// classifies intent and generates an assistant response.
    /// </summary>
    /// <param name="userMessage">Raw message text from the user/quick command</param>
    /// <param name="ct">Cancellation token for async LLM calls</param>
    /// <returns>The assistant's <see cref="ChatMessage"/> response</returns>
    public async Task<ChatMessage> ProcessMessageAsync(
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return BuildSimpleResponse("Pesan tidak boleh kosong.");
        }

        var userMsg = new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.User,
            Content = userMessage.Trim(),
            Urgency = ChatUrgency.Normal,
            Timestamp = DateTime.UtcNow
        };
        AddToHistory(userMsg);

        var response = await ClassifyAndRespondAsync(userMessage, ct);
        AddToHistory(response);

        return response;
    }

    /// <summary>
    /// Processes speech-to-text result from voice input.
    /// Keeps behavior consistent with text chat pipeline.
    /// </summary>
    public Task<ChatMessage> ProcessVoiceAsync(string voiceText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(voiceText))
        {
            return Task.FromResult(BuildSimpleResponse("Input suara kosong atau tidak dikenali."));
        }

        return ProcessMessageAsync(voiceText, ct);
    }

    /// <summary>
    /// Clears in-memory chat history for the current session.
    /// </summary>
    public void ClearHistory()
    {
        _messageHistory.Clear();
    }

    private async Task<ChatMessage> ClassifyAndRespondAsync(
        string userMessage,
        CancellationToken ct)
    {
        var lower = userMessage.Trim().ToLowerInvariant();

        if (lower == "status" ||
            lower.Contains("status lengkap") ||
            (lower.Contains("status") && lower.Contains("drone")))
        {
            return BuildStatusResponse();
        }

        if (lower.Contains("baterai") ||
            lower.Contains("battery") ||
            lower.Contains("cek baterai"))
        {
            return BuildBatteryResponse();
        }

        if (lower == "gps" ||
            lower.Contains("status gps") ||
            lower.Contains("status satelit"))
        {
            return BuildGpsResponse();
        }

        if (lower.Contains("takeoff") ||
            lower.Contains("tinggal land") ||
            lower.Contains("landing sekarang") ||
            lower == "land")
        {
            return BuildCommandInfoResponse(
                "Takeoff / Landing",
                "Untuk **takeoff**, pastikan drone dalam kondisi ARMED dan mode GUIDED/AUTO.\n" +
                "Gunakan panel kontrol Flight Page untuk mengirim perintah takeoff melalui MAVLink.\n\n" +
                "Untuk **landing**, aktifkan mode LAND di Flight Panel atau kirim perintah Land Command.");
        }

        if (lower == "arm" ||
            lower == "disarm" ||
            lower.Contains("arm drone") ||
            lower.Contains("disarm drone") ||
            (lower.Contains("arm") && !lower.Contains("alarm")))
        {
            return BuildCommandInfoResponse(
                "ARM / DISARM",
                "Untuk **ARM** drone:\n" +
                "1. Pastikan semua pre-flight check lulus (GPS, baterai, kalibrasi)\n" +
                "2. Gunakan tombol ARM di Flight Panel\n" +
                "3. Konfirmasi di dialog safety\n\n" +
                "Untuk **DISARM**, gunakan tombol DISARM setelah landing selesai.");
        }

        if (lower == "rtl" ||
            lower.Contains("return to launch") ||
            lower.Contains("balik home") ||
            lower.Contains("kembali home"))
        {
            return BuildCommandInfoResponse(
                "Return to Launch (RTL)",
                "**RTL** akan mengembalikan drone ke titik Home secara otomatis:\n" +
                "• Drone naik ke ketinggian RTL minimal, lalu terbang ke Home\n" +
                "• Default RTL altitude: parameter RTL_ALT\n\n" +
                "Gunakan tombol RTL di Flight Panel atau set mode ke RTL melalui MAVLink.\n" +
                "Pastikan titik Home sudah ter-set sebelum takeoff.");
        }

        if (lower.Contains("analisis") ||
            lower.Contains("keamanan misi") ||
            lower.Contains("misi aman"))
        {
            return await BuildMissionAnalysisResponseAsync(ct);
        }

        return await BuildLLMResponseAsync(userMessage, ct);
    }

    // ── Response Builders ──────────────────────────────────────────────────────

    private ChatMessage BuildStatusResponse()
    {
        var summary = GetTelemetrySummary();
        var content = BuildStatusText(summary);
        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = content,
            Urgency = DetermineUrgency(summary),
            Timestamp = DateTime.UtcNow
        };
    }

    private ChatMessage BuildBatteryResponse()
    {
        var snapshot = _telemetryBuffer.GetLatest();
        if (snapshot == null)
        {
            return BuildSimpleResponse(
                "Tidak ada data telemetri baterai saat ini.\n" +
                "Pastikan drone terhubung ke GCS.");
        }

        var urgency = snapshot.BatteryPercent < 20
            ? ChatUrgency.Critical
            : snapshot.BatteryPercent < 40
                ? ChatUrgency.Warning
                : ChatUrgency.Normal;

        var sb = new StringBuilder();
        sb.AppendLine("**Status Baterai:**");
        sb.AppendLine($"• Level: **{snapshot.BatteryPercent:F1}%**");
        sb.AppendLine($"• Tegangan: {snapshot.BatteryVoltage:F2} V");

        if (snapshot.BatteryDrainRate.HasValue)
            sb.AppendLine($"• Drain Rate: {snapshot.BatteryDrainRate:F2}%/min");

        if (urgency == ChatUrgency.Critical)
            sb.AppendLine("\n[KRITIS] Baterai sangat rendah! Segera lakukan landing!");
        else if (urgency == ChatUrgency.Warning)
            sb.AppendLine("\n[Peringatan] Baterai mulai rendah. Pertimbangkan RTL.");

        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = sb.ToString().TrimEnd(),
            Urgency = urgency,
            Timestamp = DateTime.UtcNow
        };
    }

    private ChatMessage BuildGpsResponse()
    {
        var snapshot = _telemetryBuffer.GetLatest();
        if (snapshot == null)
        {
            return BuildSimpleResponse(
                "Tidak ada data telemetri GPS saat ini.\n" +
                "Pastikan drone terhubung ke GCS.");
        }

        var urgency = snapshot.GpsSatellites < 6 || snapshot.GpsHdop > 2.0
            ? ChatUrgency.Warning
            : ChatUrgency.Normal;

        var sb = new StringBuilder();
        sb.AppendLine("**Status GPS:**");
        sb.AppendLine($"• Satelit: **{snapshot.GpsSatellites}**");
        sb.AppendLine($"• HDOP: {snapshot.GpsHdop:F2}");
        sb.AppendLine($"• Latitude: {snapshot.GpsLatitude:F6}°");
        sb.AppendLine($"• Longitude: {snapshot.GpsLongitude:F6}°");
        sb.AppendLine($"• Altitude GPS: {snapshot.GpsAltitude:F1} m");

        if (snapshot.GpsSatellites < 6)
            sb.AppendLine("\n[Peringatan] Satelit GPS kurang dari 6. Tunggu GPS lock yang lebih baik.");
        else if (snapshot.GpsHdop > 2.0)
            sb.AppendLine("\n[Peringatan] HDOP tinggi (>2.0). Akurasi posisi berkurang.");

        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = sb.ToString().TrimEnd(),
            Urgency = urgency,
            Timestamp = DateTime.UtcNow
        };
    }

    private ChatMessage BuildCommandInfoResponse(string commandName, string info)
    {
        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = $"**{commandName}**\n\n{info}\n\n" +
                      "*PIA hanya memberikan informasi dan saran. " +
                      "Eksekusi perintah dilakukan melalui panel kontrol GCS.*",
            Urgency = ChatUrgency.Normal,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<ChatMessage> BuildMissionAnalysisResponseAsync(CancellationToken ct)
    {
        var summary = GetTelemetrySummary();
        if (summary == null)
        {
            return BuildSimpleResponse(
                "Tidak ada data telemetri untuk dianalisis.\n" +
                "Hubungkan drone dan mulai penerbangan terlebih dahulu.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Analisis keamanan misi berdasarkan telemetri terkini:");
        sb.AppendLine();
        sb.AppendLine($"Data: {summary.SnapshotCount} snapshot, " +
                      $"{summary.WindowStart:HH:mm:ss} - {summary.WindowEnd:HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Berikan penilaian keamanan misi secara ringkas, identifikasi risiko utama, " +
                      "dan rekomendasi tindakan. Gunakan **bold** untuk poin penting.");

        var prompt = BuildLLMPromptWithContext(sb.ToString(), summary);
        var llmService = ResolveLLMService();
        var result = await llmService.GenerateAsync(prompt, LLMRole.NaturalLanguageChat, ct);

        var content = result.Success
            ? result.Text
            : BuildStatusText(summary) +
              $"\n\nAI tidak tersedia ({result.ErrorMessage}). Tampilan data mentah di atas.";

        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = content,
            Urgency = DetermineUrgency(summary),
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<ChatMessage> BuildLLMResponseAsync(
        string userMessage,
        CancellationToken ct)
    {
        var summary = GetTelemetrySummary();
        var prompt = BuildLLMPrompt(userMessage, summary);

        var llmService = ResolveLLMService();
        var result = await llmService.GenerateAsync(prompt, LLMRole.NaturalLanguageChat, ct);

        var content = result.Success
            ? result.Text
            : BuildActionableLlmFailureText(result.ErrorMessage);

        var urgency = result.Success ? ChatUrgency.Normal : ChatUrgency.Warning;

        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = content,
            Urgency = urgency,
            Timestamp = DateTime.UtcNow
        };
    }

    // ── Prompt Builders ────────────────────────────────────────────────────────

    private string BuildLLMPrompt(string userMessage, TelemetrySummary? summary)
    {
        var sb = new StringBuilder();
        var language = DetectLanguage(userMessage);
        AppendSystemContext(sb);
        AppendTelemetryContext(sb, summary);
        sb.AppendLine($"Pertanyaan pilot: {userMessage}");
        sb.AppendLine();
        if (language == "en")
        {
            sb.AppendLine("Answer in English. Keep it concise and practical. Use **bold** for critical facts. Max 5 sentences.");
        }
        else
        {
            sb.AppendLine("Berikan jawaban singkat, informatif, dan dalam bahasa Indonesia. " +
                          "Gunakan **bold** untuk informasi penting. Maksimal 5 kalimat.");
        }

        return sb.ToString();
    }

    private string BuildLLMPromptWithContext(string instruction, TelemetrySummary? summary)
    {
        var sb = new StringBuilder();
        AppendSystemContext(sb);
        AppendTelemetryContext(sb, summary);
        sb.AppendLine(instruction);
        return sb.ToString();
    }

    private static void AppendSystemContext(StringBuilder sb)
    {
        sb.AppendLine("Kamu adalah PIA (Pigeon Intelligent Assistant), asisten AI untuk Ground Control Station (GCS) drone.");
        sb.AppendLine("Kamu membantu pilot dengan analisis telemetri, diagnostik, dan informasi penerbangan.");
        sb.AppendLine("PENTING: Kamu TIDAK mengeksekusi perintah drone. Kamu hanya memberikan informasi dan saran.");
        sb.AppendLine("Selalu prioritaskan keselamatan penerbangan dalam setiap respons.");
        sb.AppendLine();
    }

    private static void AppendTelemetryContext(StringBuilder sb, TelemetrySummary? summary)
    {
        if (summary == null || summary.SnapshotCount == 0)
        {
            sb.AppendLine("=== Telemetri: Tidak ada data tersedia ===");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("=== Data Telemetri Terkini ===");
        sb.AppendLine($"Window  : {summary.WindowStart:HH:mm:ss} - {summary.WindowEnd:HH:mm:ss} ({summary.SnapshotCount} snapshots)");
        sb.AppendLine($"Baterai : avg={summary.BatteryAvg:F1}%, min={summary.BatteryMin:F1}%, max={summary.BatteryMax:F1}%, σ={summary.BatteryStdDev:F2}%");

        if (summary.BatteryDrainRate > 0)
            sb.AppendLine($"Drain   : {summary.BatteryDrainRate:F2}%/min");

        sb.AppendLine($"Altitude: avg={summary.AltitudeAvg:F1}m, min={summary.AltitudeMin:F1}m, max={summary.AltitudeMax:F1}m, trend={summary.AltitudeTrend}");
        sb.AppendLine($"Speed   : avg={summary.SpeedAvg:F1}m/s, max={summary.SpeedMax:F1}m/s");
        sb.AppendLine($"Heading : avg={summary.HeadingAvg:F1}°");
        sb.AppendLine($"Vibr X  : avg={summary.VibrationXAvg:F3}, max={summary.VibrationXMax:F3}");
        sb.AppendLine($"Vibr Y  : avg={summary.VibrationYAvg:F3}, max={summary.VibrationYMax:F3}");
        sb.AppendLine($"Vibr Z  : avg={summary.VibrationZAvg:F3}, max={summary.VibrationZMax:F3}");
        sb.AppendLine($"Wind    : avg={summary.WindSpeedAvg:F1}m/s, max={summary.WindSpeedMax:F1}m/s");
        sb.AppendLine($"GPS Dropout Events : {summary.DropoutCount}");
        sb.AppendLine($"Mode Changes       : {summary.ModeChanges}");
        sb.AppendLine();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private TelemetrySummary? GetTelemetrySummary()
    {
        var snapshots = _telemetryBuffer.GetAll();
        return snapshots.Count == 0 ? null : TelemetryAggregator.Summarize(snapshots);
    }

    private ILLMService ResolveLLMService()
    {
        if (_llmServiceFactory != null)
        {
            var dynamicService = _llmServiceFactory.Invoke();
            if (dynamicService != null)
            {
                return dynamicService;
            }
        }

        return _llmService ?? new UnavailableLLMService();
    }

    private static string BuildStatusText(TelemetrySummary? summary)
    {
        if (summary == null)
            return "Tidak ada data telemetri. Pastikan drone terhubung ke GCS.";

        return "**Status Drone:**\n" +
               $"• Baterai: **{summary.BatteryAvg:F1}%** (min {summary.BatteryMin:F1}%, max {summary.BatteryMax:F1}%)\n" +
               $"• Altitude: {summary.AltitudeAvg:F1} m ({summary.AltitudeTrend})\n" +
               $"• Speed: {summary.SpeedAvg:F1} m/s\n" +
               $"• GPS Dropout: {summary.DropoutCount}x\n" +
               $"• Mode Changes: {summary.ModeChanges}x\n" +
               $"• Data: {summary.SnapshotCount} snapshots " +
               $"({summary.WindowStart:HH:mm} – {summary.WindowEnd:HH:mm})";
    }

    private static ChatUrgency DetermineUrgency(TelemetrySummary? summary)
    {
        if (summary == null) return ChatUrgency.Normal;
        if (summary.BatteryMin < 20 || summary.DropoutCount > 5) return ChatUrgency.Critical;
        if (summary.BatteryMin < 40 || summary.DropoutCount > 2) return ChatUrgency.Warning;
        return ChatUrgency.Normal;
    }

    private static ChatMessage BuildSimpleResponse(
        string content,
        ChatUrgency urgency = ChatUrgency.Normal)
    {
        return new ChatMessage
        {
            Id = $"msg-{DateTime.UtcNow.Ticks}",
            Role = ChatRole.Assistant,
            Content = content,
            Urgency = urgency,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "id";
        }

        var lower = text.ToLowerInvariant();
        var englishHints = new[]
        {
            "what", "why", "how", "status", "battery", "gps", "mission",
            "please", "help", "takeoff", "landing", "return", "drone"
        };
        var indonesianHints = new[]
        {
            "apa", "kenapa", "bagaimana", "status", "baterai", "misi",
            "tolong", "bantu", "lepas landas", "mendarat", "kembali", "drone"
        };

        var englishScore = englishHints.Count(h => lower.Contains(h, StringComparison.Ordinal));
        var indonesianScore = indonesianHints.Count(h => lower.Contains(h, StringComparison.Ordinal));

        if (englishScore > indonesianScore)
        {
            return "en";
        }

        return "id";
    }

    private static string BuildActionableLlmFailureText(string? rawError)
    {
        var error = string.IsNullOrWhiteSpace(rawError) ? "unknown error" : rawError.Trim();
        var lower = error.ToLowerInvariant();

        string action;
        if (lower.Contains("api key", StringComparison.Ordinal) || lower.Contains("401", StringComparison.Ordinal))
        {
            action = "API key belum valid/kosong. Isi API key primary dan fallback di AI Settings, lalu test provider.";
        }
        else if (lower.Contains("quota", StringComparison.Ordinal) || lower.Contains("402", StringComparison.Ordinal))
        {
            action = "Kuota provider habis. Cek billing, ganti model, atau aktifkan fallback provider.";
        }
        else if (lower.Contains("model", StringComparison.Ordinal) || lower.Contains("404", StringComparison.Ordinal))
        {
            action = "Model tidak tersedia. Periksa nama model primary/fallback di AI Settings.";
        }
        else if (lower.Contains("rate limit", StringComparison.Ordinal) || lower.Contains("429", StringComparison.Ordinal))
        {
            action = "Provider sedang rate-limited. Tunggu beberapa detik lalu coba lagi.";
        }
        else if (lower.Contains("circuit", StringComparison.Ordinal))
        {
            action = "Circuit breaker aktif karena error berulang. Tunggu cooldown, lalu test provider lagi.";
        }
        else
        {
            action = "Periksa provider diagnostic di panel PIA dan AI Settings (key, model, fallback).";
        }

        return $"Maaf, AI tidak tersedia saat ini: {error}\n\n{action}\n" +
               "Sementara itu gunakan quick command (Status/Baterai/GPS) untuk melihat data telemetri langsung.";
    }

    private void AddToHistory(ChatMessage message)
    {
        _messageHistory.Add(message);
        while (_messageHistory.Count > MaxMessages)
            _messageHistory.RemoveAt(0);
    }
}
#endif
