using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Interface utama untuk semua LLM provider di PIA.
/// Implementasi: OpenRouterService
/// </summary>
public interface ILLMService
{
    /// <summary>Apakah service siap digunakan (API key valid, tidak circuit-open)</summary>
    bool IsAvailable { get; }

    /// <summary>Nama provider aktif saat ini</summary>
    string ProviderName { get; }

    /// <summary>
    /// Generate teks dari prompt. Otomatis fallback ke model gratis jika model utama gagal.
    /// </summary>
    Task<LLMResult> GenerateAsync(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default);

    /// <summary>
    /// Generate dan parse JSON langsung ke object T.
    /// </summary>
    Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default) where T : class;

    /// <summary>Test koneksi ke OpenRouter</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>Status health semua model</summary>
    LLMHealthStatus GetHealthStatus();
}

/// <summary>Peran/fitur PIA yang memanggil LLM — menentukan model yang dipakai</summary>
public enum LLMRole
{
    TelemetryAnalysis,
    AnomalyDetection,
    NaturalLanguageChat,
    MaintenancePrediction,
    PerformanceScoring,
    BatteryPrediction,
    FlightSessionSummary,
    VoiceIntent,
    Fallback
}
