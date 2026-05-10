using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// Persistent storage contract for PIA-related runtime history.
/// </summary>
public interface IPIAHistoryStore
{
    Task SaveChatMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> LoadRecentChatMessagesAsync(int limit = 100, CancellationToken ct = default);
    Task ClearChatHistoryAsync(CancellationToken ct = default);

    Task SaveAnomalyAsync(Anomaly anomaly, string sourceLayer, CancellationToken ct = default);
    Task<IReadOnlyList<Anomaly>> LoadRecentAnomaliesAsync(int limit = 50, CancellationToken ct = default);

    Task SavePerformanceScoreAsync(PerformanceScore score, CancellationToken ct = default);
    Task<IReadOnlyList<PerformanceTrend>> LoadRecentPerformanceTrendsAsync(int limit = 20, CancellationToken ct = default);

    Task SaveBatteryPredictionAsync(BatteryPrediction prediction, CancellationToken ct = default);
    Task<IReadOnlyList<BatteryPrediction>> LoadRecentBatteryPredictionsAsync(int limit = 20, CancellationToken ct = default);

    Task SaveFlightSessionSummaryAsync(FlightSessionSummary summary, CancellationToken ct = default);
    Task<IReadOnlyList<FlightSessionSummary>> LoadRecentFlightSessionSummariesAsync(int limit = 10, CancellationToken ct = default);

    Task SaveResearchValidationSnapshotAsync(ResearchValidationSnapshot snapshot, CancellationToken ct = default);
    Task<ResearchValidationSnapshot?> LoadLatestResearchValidationSnapshotAsync(CancellationToken ct = default);

    Task SaveCommandAuditEntryAsync(CommandAuditEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<CommandAuditEntry>> LoadRecentCommandAuditEntriesAsync(int limit = 50, CancellationToken ct = default);

    Task ApplyRetentionPolicyAsync(int retentionDays = 30, CancellationToken ct = default);
    Task<string> ExportResearchReportJsonAsync(
        int limitPerSection = 200,
        LLMHealthStatus? providerHealth = null,
        AISettings? aiSettings = null,
        CancellationToken ct = default);
    Task<string> ExportResearchReportCsvAsync(
        int limitPerSection = 200,
        LLMHealthStatus? providerHealth = null,
        AISettings? aiSettings = null,
        CancellationToken ct = default);
}
