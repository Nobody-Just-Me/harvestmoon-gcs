using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// SQLite-backed persistent history for PIA chat/anomaly/performance trend.
/// </summary>
public sealed class SqlitePIAHistoryStore : IPIAHistoryStore
{
    private const int MaxChatRows = 2000;
    private const int MaxAnomalyRows = 2000;
    private const int MaxPerformanceRows = 5000;
    private const int MaxBatteryPredictionRows = 5000;
    private const int MaxSessionSummaryRows = 1000;
    private const int MaxResearchRows = 2000;
    private const int MaxCommandAuditRows = 5000;
    private const int MaxTelemetryRawRows = 10_000;

    private readonly string _dbPath;
    private readonly string _telemetryDbPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqlitePIAHistoryStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pigeon_Uno");
        Directory.CreateDirectory(root);
        _dbPath = Path.Combine(root, "pia_history.db");
        _telemetryDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pigeon_telemetry.db");
        InitializeDatabase();
    }

    public async Task SaveChatMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        if (message == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT OR REPLACE INTO PIA_ChatHistory
                    (Id, Role, Content, Urgency, Timestamp, PendingCommand, RequireConfirmation, Confirmed)
                    VALUES ($id, $role, $content, $urgency, $timestamp, $pending, $requireConfirmation, $confirmed)
                    """;
                command.Parameters.AddWithValue("$id", message.Id);
                command.Parameters.AddWithValue("$role", (int)message.Role);
                command.Parameters.AddWithValue("$content", message.Content ?? string.Empty);
                command.Parameters.AddWithValue("$urgency", (int)message.Urgency);
                command.Parameters.AddWithValue("$timestamp", message.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$pending", (object?)message.PendingCommand ?? DBNull.Value);
                command.Parameters.AddWithValue("$requireConfirmation", message.RequireConfirmation ? 1 : 0);
                command.Parameters.AddWithValue("$confirmed", message.Confirmed.HasValue ? (message.Confirmed.Value ? 1 : 0) : DBNull.Value);
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_ChatHistory", MaxChatRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> LoadRecentChatMessagesAsync(int limit = 100, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>();
        var take = Math.Clamp(limit, 1, 1000);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, Role, Content, Urgency, Timestamp, PendingCommand, RequireConfirmation, Confirmed
                FROM PIA_ChatHistory
                ORDER BY Timestamp DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var message = new ChatMessage
                {
                    Id = reader.IsDBNull(0) ? $"msg-{DateTime.UtcNow.Ticks}" : reader.GetString(0),
                    Role = ReadEnum(reader, 1, ChatRole.User),
                    Content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Urgency = ReadEnum(reader, 3, ChatUrgency.Normal),
                    Timestamp = ParseTimestamp(reader, 4),
                    PendingCommand = reader.IsDBNull(5) ? null : reader.GetString(5),
                    RequireConfirmation = !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
                    Confirmed = ReadNullableBool(reader, 7)
                };
                messages.Add(message);
            }
        }
        catch
        {
            return Array.Empty<ChatMessage>();
        }
        finally
        {
            _gate.Release();
        }

        messages.Reverse();
        return messages;
    }

    public async Task ClearChatHistoryAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PIA_ChatHistory";
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAnomalyAsync(Anomaly anomaly, string sourceLayer, CancellationToken ct = default)
    {
        if (anomaly == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_AnomalyHistory
                    (Type, Severity, Message, Recommendation, Timestamp, Priority, SourceLayer)
                    VALUES ($type, $severity, $message, $recommendation, $timestamp, $priority, $sourceLayer)
                    """;
                command.Parameters.AddWithValue("$type", (int)anomaly.Type);
                command.Parameters.AddWithValue("$severity", (int)anomaly.Severity);
                command.Parameters.AddWithValue("$message", anomaly.Message ?? string.Empty);
                command.Parameters.AddWithValue("$recommendation", (object?)anomaly.Recommendation ?? DBNull.Value);
                command.Parameters.AddWithValue("$timestamp", anomaly.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$priority", anomaly.Priority);
                command.Parameters.AddWithValue("$sourceLayer", sourceLayer ?? "Unknown");
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_AnomalyHistory", MaxAnomalyRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Anomaly>> LoadRecentAnomaliesAsync(int limit = 50, CancellationToken ct = default)
    {
        var anomalies = new List<Anomaly>();
        var take = Math.Clamp(limit, 1, 1000);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Type, Severity, Message, Recommendation, Timestamp, Priority
                FROM PIA_AnomalyHistory
                ORDER BY Timestamp DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                anomalies.Add(new Anomaly
                {
                    Type = ReadEnum(reader, 0, AnomalyType.StatisticalOutlier),
                    Severity = ReadEnum(reader, 1, AnomalySeverity.Warning),
                    Message = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Recommendation = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Timestamp = ParseTimestamp(reader, 4),
                    Priority = reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
                });
            }
        }
        catch
        {
            return Array.Empty<Anomaly>();
        }
        finally
        {
            _gate.Release();
        }

        return anomalies;
    }

    public async Task SavePerformanceScoreAsync(PerformanceScore score, CancellationToken ct = default)
    {
        if (score == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_PerformanceHistory
                    (Timestamp, Score, Grade)
                    VALUES ($timestamp, $score, $grade)
                    """;
                command.Parameters.AddWithValue("$timestamp", score.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$score", score.TotalScore);
                command.Parameters.AddWithValue("$grade", score.Grade ?? "C");
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_PerformanceHistory", MaxPerformanceRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PerformanceTrend>> LoadRecentPerformanceTrendsAsync(int limit = 20, CancellationToken ct = default)
    {
        var trends = new List<PerformanceTrend>();
        var take = Math.Clamp(limit, 1, 1000);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Timestamp, Score, Grade
                FROM PIA_PerformanceHistory
                ORDER BY Timestamp DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                trends.Add(new PerformanceTrend
                {
                    Date = ParseTimestamp(reader, 0),
                    Score = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                    Grade = reader.IsDBNull(2) ? "C" : reader.GetString(2)
                });
            }
        }
        catch
        {
            return Array.Empty<PerformanceTrend>();
        }
        finally
        {
            _gate.Release();
        }

        trends.Reverse();
        return trends;
    }

    public async Task SaveBatteryPredictionAsync(BatteryPrediction prediction, CancellationToken ct = default)
    {
        if (prediction == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_BatteryPredictionHistory
                    (Timestamp, CurrentBatteryPercent, EstimatedDrainRatePerMinute, EstimatedRemainingMinutes,
                     EstimatedDepletionAt, HealthScore, Condition, Recommendation, Confidence)
                    VALUES ($timestamp, $currentBattery, $drainRate, $remainingMinutes,
                            $depletionAt, $healthScore, $condition, $recommendation, $confidence)
                    """;
                command.Parameters.AddWithValue("$timestamp", prediction.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$currentBattery", prediction.CurrentBatteryPercent);
                command.Parameters.AddWithValue("$drainRate", prediction.EstimatedDrainRatePerMinute);
                command.Parameters.AddWithValue("$remainingMinutes", prediction.EstimatedRemainingMinutes);
                command.Parameters.AddWithValue("$depletionAt", prediction.EstimatedDepletionAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$healthScore", prediction.HealthScore);
                command.Parameters.AddWithValue("$condition", prediction.Condition ?? "UNKNOWN");
                command.Parameters.AddWithValue("$recommendation", prediction.Recommendation ?? string.Empty);
                command.Parameters.AddWithValue("$confidence", prediction.Confidence);
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_BatteryPredictionHistory", MaxBatteryPredictionRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BatteryPrediction>> LoadRecentBatteryPredictionsAsync(int limit = 20, CancellationToken ct = default)
    {
        var predictions = new List<BatteryPrediction>();
        var take = Math.Clamp(limit, 1, 1000);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Timestamp, CurrentBatteryPercent, EstimatedDrainRatePerMinute, EstimatedRemainingMinutes,
                       EstimatedDepletionAt, HealthScore, Condition, Recommendation, Confidence
                FROM PIA_BatteryPredictionHistory
                ORDER BY Timestamp DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                predictions.Add(new BatteryPrediction
                {
                    Timestamp = ParseTimestamp(reader, 0),
                    CurrentBatteryPercent = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                    EstimatedDrainRatePerMinute = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    EstimatedRemainingMinutes = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    EstimatedDepletionAt = ParseTimestamp(reader, 4),
                    HealthScore = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    Condition = reader.IsDBNull(6) ? "UNKNOWN" : reader.GetString(6),
                    Recommendation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Confidence = reader.IsDBNull(8) ? 0 : reader.GetDouble(8)
                });
            }
        }
        catch
        {
            return Array.Empty<BatteryPrediction>();
        }
        finally
        {
            _gate.Release();
        }

        predictions.Reverse();
        return predictions;
    }

    public async Task SaveFlightSessionSummaryAsync(FlightSessionSummary summary, CancellationToken ct = default)
    {
        if (summary == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_FlightSessionHistory
                    (GeneratedAt, SessionStart, SessionEnd, SnapshotCount, DurationMinutes,
                     StartBatteryPercent, EndBatteryPercent, BatteryUsedPercent, AvgSpeed, MaxAltitude,
                     AnomalyCount, OverallStatus, SummaryText, RecommendedAction, Confidence)
                    VALUES ($generatedAt, $sessionStart, $sessionEnd, $snapshotCount, $durationMinutes,
                            $startBattery, $endBattery, $batteryUsed, $avgSpeed, $maxAltitude,
                            $anomalyCount, $overallStatus, $summaryText, $recommendedAction, $confidence)
                    """;
                command.Parameters.AddWithValue("$generatedAt", summary.GeneratedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$sessionStart", summary.SessionStart.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$sessionEnd", summary.SessionEnd.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$snapshotCount", summary.SnapshotCount);
                command.Parameters.AddWithValue("$durationMinutes", summary.DurationMinutes);
                command.Parameters.AddWithValue("$startBattery", summary.StartBatteryPercent);
                command.Parameters.AddWithValue("$endBattery", summary.EndBatteryPercent);
                command.Parameters.AddWithValue("$batteryUsed", summary.BatteryUsedPercent);
                command.Parameters.AddWithValue("$avgSpeed", summary.AvgSpeed);
                command.Parameters.AddWithValue("$maxAltitude", summary.MaxAltitude);
                command.Parameters.AddWithValue("$anomalyCount", summary.AnomalyCount);
                command.Parameters.AddWithValue("$overallStatus", summary.OverallStatus ?? "UNKNOWN");
                command.Parameters.AddWithValue("$summaryText", summary.SummaryText ?? string.Empty);
                command.Parameters.AddWithValue("$recommendedAction", summary.RecommendedAction ?? string.Empty);
                command.Parameters.AddWithValue("$confidence", summary.Confidence);
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_FlightSessionHistory", MaxSessionSummaryRows, ct, "GeneratedAt");
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<FlightSessionSummary>> LoadRecentFlightSessionSummariesAsync(int limit = 10, CancellationToken ct = default)
    {
        var summaries = new List<FlightSessionSummary>();
        var take = Math.Clamp(limit, 1, 500);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT GeneratedAt, SessionStart, SessionEnd, SnapshotCount, DurationMinutes,
                       StartBatteryPercent, EndBatteryPercent, BatteryUsedPercent, AvgSpeed, MaxAltitude,
                       AnomalyCount, OverallStatus, SummaryText, RecommendedAction, Confidence
                FROM PIA_FlightSessionHistory
                ORDER BY GeneratedAt DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                summaries.Add(new FlightSessionSummary
                {
                    GeneratedAt = ParseTimestamp(reader, 0),
                    SessionStart = ParseTimestamp(reader, 1),
                    SessionEnd = ParseTimestamp(reader, 2),
                    SnapshotCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    DurationMinutes = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    StartBatteryPercent = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    EndBatteryPercent = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                    BatteryUsedPercent = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                    AvgSpeed = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                    MaxAltitude = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
                    AnomalyCount = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                    OverallStatus = reader.IsDBNull(11) ? "UNKNOWN" : reader.GetString(11),
                    SummaryText = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    RecommendedAction = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    Confidence = reader.IsDBNull(14) ? 0 : reader.GetDouble(14)
                });
            }
        }
        catch
        {
            return Array.Empty<FlightSessionSummary>();
        }
        finally
        {
            _gate.Release();
        }

        summaries.Reverse();
        return summaries;
    }

    public async Task SaveResearchValidationSnapshotAsync(ResearchValidationSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_ResearchValidationHistory
                    (Timestamp, AnomalySampleCount, Precision, Recall, F1Score, BatteryMape, BatteryMapeSamples,
                     ChatLatencyMs, VoiceLatencyMs, LlmLatencyMs, AnomalyLatencyMs, TelemetryRateHz)
                    VALUES ($timestamp, $anomalySampleCount, $precision, $recall, $f1Score, $batteryMape, $batteryMapeSamples,
                            $chatLatencyMs, $voiceLatencyMs, $llmLatencyMs, $anomalyLatencyMs, $telemetryRateHz)
                    """;
                command.Parameters.AddWithValue("$timestamp", snapshot.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$anomalySampleCount", snapshot.AnomalySampleCount);
                command.Parameters.AddWithValue("$precision", snapshot.Precision);
                command.Parameters.AddWithValue("$recall", snapshot.Recall);
                command.Parameters.AddWithValue("$f1Score", snapshot.F1Score);
                command.Parameters.AddWithValue("$batteryMape", snapshot.BatteryMape);
                command.Parameters.AddWithValue("$batteryMapeSamples", snapshot.BatteryMapeSamples);
                command.Parameters.AddWithValue("$chatLatencyMs", snapshot.ChatLatencyMs);
                command.Parameters.AddWithValue("$voiceLatencyMs", snapshot.VoiceLatencyMs);
                command.Parameters.AddWithValue("$llmLatencyMs", snapshot.LlmLatencyMs);
                command.Parameters.AddWithValue("$anomalyLatencyMs", snapshot.AnomalyLatencyMs);
                command.Parameters.AddWithValue("$telemetryRateHz", snapshot.TelemetryRateHz);
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_ResearchValidationHistory", MaxResearchRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ResearchValidationSnapshot?> LoadLatestResearchValidationSnapshotAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Timestamp, AnomalySampleCount, Precision, Recall, F1Score, BatteryMape, BatteryMapeSamples,
                       ChatLatencyMs, VoiceLatencyMs, LlmLatencyMs, AnomalyLatencyMs, TelemetryRateHz
                FROM PIA_ResearchValidationHistory
                ORDER BY Timestamp DESC
                LIMIT 1
                """;

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new ResearchValidationSnapshot
                {
                    Timestamp = ParseTimestamp(reader, 0),
                    AnomalySampleCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    Precision = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    Recall = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    F1Score = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    BatteryMape = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    BatteryMapeSamples = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    ChatLatencyMs = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                    VoiceLatencyMs = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                    LlmLatencyMs = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
                    AnomalyLatencyMs = reader.IsDBNull(10) ? 0 : reader.GetDouble(10),
                    TelemetryRateHz = reader.IsDBNull(11) ? 0 : reader.GetDouble(11)
                };
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }

        return null;
    }

    public async Task SaveCommandAuditEntryAsync(CommandAuditEntry entry, CancellationToken ct = default)
    {
        if (entry == null)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO PIA_CommandAuditHistory
                    (Timestamp, InputText, Command, Confidence, IsValid, IsExecuted, ResultMessage, Source)
                    VALUES ($timestamp, $inputText, $command, $confidence, $isValid, $isExecuted, $resultMessage, $source)
                    """;
                command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$inputText", entry.InputText ?? string.Empty);
                command.Parameters.AddWithValue("$command", (int)entry.Command);
                command.Parameters.AddWithValue("$confidence", entry.Confidence);
                command.Parameters.AddWithValue("$isValid", entry.IsValid ? 1 : 0);
                command.Parameters.AddWithValue("$isExecuted", entry.IsExecuted ? 1 : 0);
                command.Parameters.AddWithValue("$resultMessage", entry.ResultMessage ?? string.Empty);
                command.Parameters.AddWithValue("$source", entry.Source ?? "voice");
                await command.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(connection, "PIA_CommandAuditHistory", MaxCommandAuditRows, ct);
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<CommandAuditEntry>> LoadRecentCommandAuditEntriesAsync(int limit = 50, CancellationToken ct = default)
    {
        var result = new List<CommandAuditEntry>();
        var take = Math.Clamp(limit, 1, 500);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Timestamp, InputText, Command, Confidence, IsValid, IsExecuted, ResultMessage, Source
                FROM PIA_CommandAuditHistory
                ORDER BY Timestamp DESC
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", take);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new CommandAuditEntry
                {
                    Timestamp = ParseTimestamp(reader, 0),
                    InputText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Command = ReadEnum(reader, 2, VoiceCommand.Unknown),
                    Confidence = reader.IsDBNull(3) ? 0 : (float)reader.GetDouble(3),
                    IsValid = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                    IsExecuted = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                    ResultMessage = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Source = reader.IsDBNull(7) ? "voice" : reader.GetString(7)
                });
            }
        }
        catch
        {
            return Array.Empty<CommandAuditEntry>();
        }
        finally
        {
            _gate.Release();
        }

        result.Reverse();
        return result;
    }

    public async Task ApplyRetentionPolicyAsync(int retentionDays = 30, CancellationToken ct = default)
    {
        var safeRetentionDays = Math.Clamp(retentionDays, 1, 3650);
        var cutoff = DateTime.UtcNow.AddDays(-safeRetentionDays).ToString("o", CultureInfo.InvariantCulture);

        await _gate.WaitAsync(ct);
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(ct);

            var retentionTargets = new (string Table, string Column)[]
            {
                ("PIA_ChatHistory", "Timestamp"),
                ("PIA_AnomalyHistory", "Timestamp"),
                ("PIA_PerformanceHistory", "Timestamp"),
                ("PIA_BatteryPredictionHistory", "Timestamp"),
                ("PIA_FlightSessionHistory", "GeneratedAt"),
                ("PIA_ResearchValidationHistory", "Timestamp"),
                ("PIA_CommandAuditHistory", "Timestamp")
            };

            foreach (var (table, column) in retentionTargets)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {table} WHERE {column} < $cutoff";
                command.Parameters.AddWithValue("$cutoff", cutoff);
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        catch
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> ExportResearchReportJsonAsync(
        int limitPerSection = 200,
        LLMHealthStatus? providerHealth = null,
        AISettings? aiSettings = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limitPerSection, 1, 2000);
        var chat = await LoadRecentChatMessagesAsync(take, ct);
        var anomalies = await LoadRecentAnomaliesAsync(take, ct);
        var performance = await LoadRecentPerformanceTrendsAsync(take, ct);
        var battery = await LoadRecentBatteryPredictionsAsync(take, ct);
        var sessions = await LoadRecentFlightSessionSummariesAsync(Math.Min(take, 500), ct);
        var commandAudit = await LoadRecentCommandAuditEntriesAsync(take, ct);
        var validation = await LoadLatestResearchValidationSnapshotAsync(ct);
        var telemetryRaw = await LoadTelemetryRawAsync(Math.Min(MaxTelemetryRawRows, take * 10), ct);
        var telemetrySnapshots = BuildSnapshots(telemetryRaw);
        var telemetrySampled = BuildSampledSnapshots(telemetrySnapshots, aiSettings);
        var telemetrySummary = TelemetryAggregator.Summarize(telemetrySampled);
        var metadata = BuildResearchMetadata(providerHealth, aiSettings);
        var datasetEvaluation = BuildDatasetEvaluation(telemetrySampled, anomalies, aiSettings);

        var payload = new
        {
            GeneratedAt = DateTime.UtcNow,
            Metadata = metadata,
            Validation = validation,
            Telemetry = new
            {
                Raw = telemetryRaw,
                Sampled = telemetrySampled,
                Summary = telemetrySummary
            },
            DatasetEvaluation = datasetEvaluation,
            Chat = chat,
            Anomalies = anomalies,
            PerformanceTrends = performance,
            BatteryPredictions = battery,
            FlightSessions = sessions,
            CommandAudit = commandAudit
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task<string> ExportResearchReportCsvAsync(
        int limitPerSection = 200,
        LLMHealthStatus? providerHealth = null,
        AISettings? aiSettings = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limitPerSection, 1, 2000);
        var anomalies = await LoadRecentAnomaliesAsync(take, ct);
        var performance = await LoadRecentPerformanceTrendsAsync(take, ct);
        var battery = await LoadRecentBatteryPredictionsAsync(take, ct);
        var sessions = await LoadRecentFlightSessionSummariesAsync(Math.Min(take, 500), ct);
        var commandAudit = await LoadRecentCommandAuditEntriesAsync(take, ct);
        var validation = await LoadLatestResearchValidationSnapshotAsync(ct);
        var telemetryRaw = await LoadTelemetryRawAsync(Math.Min(MaxTelemetryRawRows, take * 10), ct);
        var telemetrySnapshots = BuildSnapshots(telemetryRaw);
        var telemetrySampled = BuildSampledSnapshots(telemetrySnapshots, aiSettings);
        var telemetrySummary = TelemetryAggregator.Summarize(telemetrySampled);
        var metadata = BuildResearchMetadata(providerHealth, aiSettings);
        var datasetEvaluation = BuildDatasetEvaluation(telemetrySampled, anomalies, aiSettings);

        var sb = new StringBuilder();
        sb.AppendLine("# PIA Research Report");
        sb.AppendLine($"# GeneratedAt,{DateTime.UtcNow:O}");

        sb.AppendLine();
        sb.AppendLine("[ProviderMetadata]");
        sb.AppendLine("generated_at,primary_provider,fallback_provider,active_provider,active_model,primary_model,fallback_model,fallback_available,circuit_open,consecutive_failures,cache_hit_rate,total_requests,failed_requests,fallback_usage,last_latency_ms,last_error");
        sb.AppendLine($"{metadata.GeneratedAt:O},{CsvEscape(metadata.PrimaryProvider)},{CsvEscape(metadata.FallbackProvider)},{CsvEscape(metadata.ActiveProvider)},{CsvEscape(metadata.ActiveModel)},{CsvEscape(metadata.PrimaryModel)},{CsvEscape(metadata.FallbackModel)},{metadata.FallbackAvailable},{metadata.CircuitOpen},{metadata.ConsecutiveFailures},{metadata.CacheHitRate.ToString(CultureInfo.InvariantCulture)},{metadata.TotalRequests},{metadata.FailedRequests},{metadata.FallbackUsageCount},{metadata.LastLatencyMs},{CsvEscape(metadata.LastError)}");

        sb.AppendLine();
        sb.AppendLine("[Validation]");
        if (validation == null)
        {
            sb.AppendLine("timestamp,anomaly_samples,precision,recall,f1,battery_mape,battery_mape_samples,chat_latency_ms,voice_latency_ms,llm_latency_ms,anomaly_latency_ms,telemetry_rate_hz");
            sb.AppendLine(",,,,,,,,,,,,");
        }
        else
        {
            sb.AppendLine("timestamp,anomaly_samples,precision,recall,f1,battery_mape,battery_mape_samples,chat_latency_ms,voice_latency_ms,llm_latency_ms,anomaly_latency_ms,telemetry_rate_hz");
            sb.AppendLine($"{validation.Timestamp:O},{validation.AnomalySampleCount},{validation.Precision.ToString(CultureInfo.InvariantCulture)},{validation.Recall.ToString(CultureInfo.InvariantCulture)},{validation.F1Score.ToString(CultureInfo.InvariantCulture)},{validation.BatteryMape.ToString(CultureInfo.InvariantCulture)},{validation.BatteryMapeSamples},{validation.ChatLatencyMs.ToString(CultureInfo.InvariantCulture)},{validation.VoiceLatencyMs.ToString(CultureInfo.InvariantCulture)},{validation.LlmLatencyMs.ToString(CultureInfo.InvariantCulture)},{validation.AnomalyLatencyMs.ToString(CultureInfo.InvariantCulture)},{validation.TelemetryRateHz.ToString(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine();
        sb.AppendLine("[DatasetEvaluation]");
        sb.AppendLine("generated_at,pipeline,labeling_strategy,prediction_source,evaluation_window_seconds,telemetry_samples,predicted_positive,pseudo_positive,tp,fp,tn,fn,precision,recall,f1,accuracy");
        sb.AppendLine($"{datasetEvaluation.GeneratedAt:O},{CsvEscape(datasetEvaluation.Pipeline)},{CsvEscape(datasetEvaluation.LabelingStrategy)},{CsvEscape(datasetEvaluation.PredictionSource)},{datasetEvaluation.EvaluationWindowSeconds},{datasetEvaluation.TelemetrySampleCount},{datasetEvaluation.PredictedPositiveCount},{datasetEvaluation.PseudoLabelPositiveCount},{datasetEvaluation.TruePositive},{datasetEvaluation.FalsePositive},{datasetEvaluation.TrueNegative},{datasetEvaluation.FalseNegative},{datasetEvaluation.Precision.ToString(CultureInfo.InvariantCulture)},{datasetEvaluation.Recall.ToString(CultureInfo.InvariantCulture)},{datasetEvaluation.F1Score.ToString(CultureInfo.InvariantCulture)},{datasetEvaluation.Accuracy.ToString(CultureInfo.InvariantCulture)}");

        sb.AppendLine();
        sb.AppendLine("[TelemetrySummary]");
        sb.AppendLine("snapshot_count,window_start,window_end,battery_min,battery_max,battery_avg,battery_drain_rate,altitude_min,altitude_max,altitude_avg,speed_min,speed_max,speed_avg,gps_hdop_avg,gps_hdop_max,link_quality_avg,packet_loss_avg,distance_travelled_m,dropout_count,mode_changes,link_degraded_count,high_vibration_count,high_wind_count,stability_score");
        sb.AppendLine($"{telemetrySummary.SnapshotCount},{telemetrySummary.WindowStart:O},{telemetrySummary.WindowEnd:O},{telemetrySummary.BatteryMin.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.BatteryMax.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.BatteryAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.BatteryDrainRate.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.AltitudeMin.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.AltitudeMax.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.AltitudeAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.SpeedMin.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.SpeedMax.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.SpeedAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.GpsHdopAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.GpsHdopMax.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.LinkQualityAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.PacketLossAvg.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.DistanceTravelledMeters.ToString(CultureInfo.InvariantCulture)},{telemetrySummary.DropoutCount},{telemetrySummary.ModeChanges},{telemetrySummary.LinkDegradedCount},{telemetrySummary.HighVibrationCount},{telemetrySummary.HighWindCount},{telemetrySummary.StabilityScore.ToString(CultureInfo.InvariantCulture)}");

        sb.AppendLine();
        sb.AppendLine("[TelemetryRaw]");
        sb.AppendLine("timestamp,latitude,longitude,altitude,heading,pitch,roll,speed,ground_speed,battery_voltage,battery_current,battery_percent,satellites,hdop,flight_mode,is_armed");
        foreach (var item in telemetryRaw)
        {
            sb.AppendLine($"{item.Timestamp:O},{item.Latitude.ToString(CultureInfo.InvariantCulture)},{item.Longitude.ToString(CultureInfo.InvariantCulture)},{item.Altitude.ToString(CultureInfo.InvariantCulture)},{item.Heading.ToString(CultureInfo.InvariantCulture)},{item.Pitch.ToString(CultureInfo.InvariantCulture)},{item.Roll.ToString(CultureInfo.InvariantCulture)},{item.Speed.ToString(CultureInfo.InvariantCulture)},{item.GroundSpeed.ToString(CultureInfo.InvariantCulture)},{item.BatteryVoltage.ToString(CultureInfo.InvariantCulture)},{item.BatteryCurrent.ToString(CultureInfo.InvariantCulture)},{item.BatteryPercent.ToString(CultureInfo.InvariantCulture)},{item.SatelliteCount},{item.HDOP.ToString(CultureInfo.InvariantCulture)},{item.FlightMode},{item.IsArmed}");
        }

        sb.AppendLine();
        sb.AppendLine("[TelemetrySampled]");
        sb.AppendLine("timestamp,battery_percent,battery_voltage,battery_current,gps_lat,gps_lon,gps_hdop,gps_satellites,altitude,ground_speed,air_speed,vertical_speed,heading,roll,pitch,yaw,link_quality,packet_loss,vibration_mag,wind_speed,flight_mode,armed");
        foreach (var item in telemetrySampled)
        {
            sb.AppendLine($"{item.Timestamp:O},{item.BatteryPercent.ToString(CultureInfo.InvariantCulture)},{item.BatteryVoltage.ToString(CultureInfo.InvariantCulture)},{item.BatteryCurrent.ToString(CultureInfo.InvariantCulture)},{item.GpsLatitude.ToString(CultureInfo.InvariantCulture)},{item.GpsLongitude.ToString(CultureInfo.InvariantCulture)},{item.GpsHdop.ToString(CultureInfo.InvariantCulture)},{item.GpsSatellites},{item.Altitude.ToString(CultureInfo.InvariantCulture)},{item.GroundSpeed.ToString(CultureInfo.InvariantCulture)},{item.AirSpeed.ToString(CultureInfo.InvariantCulture)},{item.VerticalSpeed.ToString(CultureInfo.InvariantCulture)},{item.Heading.ToString(CultureInfo.InvariantCulture)},{item.Roll.ToString(CultureInfo.InvariantCulture)},{item.Pitch.ToString(CultureInfo.InvariantCulture)},{item.Yaw.ToString(CultureInfo.InvariantCulture)},{item.LinkQualityPercent.ToString(CultureInfo.InvariantCulture)},{item.PacketLossPercent.ToString(CultureInfo.InvariantCulture)},{item.VibrationMagnitude.ToString(CultureInfo.InvariantCulture)},{item.WindSpeed.ToString(CultureInfo.InvariantCulture)},{item.FlightMode},{item.Armed}");
        }

        sb.AppendLine();
        sb.AppendLine("[Anomalies]");
        sb.AppendLine("timestamp,type,severity,priority,message,recommendation");
        foreach (var item in anomalies)
        {
            sb.AppendLine($"{item.Timestamp:O},{item.Type},{item.Severity},{item.Priority.ToString(CultureInfo.InvariantCulture)},{CsvEscape(item.Message)},{CsvEscape(item.Recommendation)}");
        }

        sb.AppendLine();
        sb.AppendLine("[PerformanceTrend]");
        sb.AppendLine("date,score,grade");
        foreach (var item in performance)
        {
            sb.AppendLine($"{item.Date:O},{item.Score.ToString(CultureInfo.InvariantCulture)},{CsvEscape(item.Grade)}");
        }

        sb.AppendLine();
        sb.AppendLine("[BatteryPrediction]");
        sb.AppendLine("timestamp,current_battery,drain_rate,remaining_minutes,depletion_at,health_score,condition,confidence,recommendation");
        foreach (var item in battery)
        {
            sb.AppendLine($"{item.Timestamp:O},{item.CurrentBatteryPercent.ToString(CultureInfo.InvariantCulture)},{item.EstimatedDrainRatePerMinute.ToString(CultureInfo.InvariantCulture)},{item.EstimatedRemainingMinutes.ToString(CultureInfo.InvariantCulture)},{item.EstimatedDepletionAt:O},{item.HealthScore.ToString(CultureInfo.InvariantCulture)},{CsvEscape(item.Condition)},{item.Confidence.ToString(CultureInfo.InvariantCulture)},{CsvEscape(item.Recommendation)}");
        }

        sb.AppendLine();
        sb.AppendLine("[FlightSessions]");
        sb.AppendLine("generated_at,session_start,session_end,duration_minutes,battery_used,anomaly_count,status,avg_speed,max_altitude,confidence,summary");
        foreach (var item in sessions)
        {
            sb.AppendLine($"{item.GeneratedAt:O},{item.SessionStart:O},{item.SessionEnd:O},{item.DurationMinutes.ToString(CultureInfo.InvariantCulture)},{item.BatteryUsedPercent.ToString(CultureInfo.InvariantCulture)},{item.AnomalyCount},{CsvEscape(item.OverallStatus)},{item.AvgSpeed.ToString(CultureInfo.InvariantCulture)},{item.MaxAltitude.ToString(CultureInfo.InvariantCulture)},{item.Confidence.ToString(CultureInfo.InvariantCulture)},{CsvEscape(item.SummaryText)}");
        }

        sb.AppendLine();
        sb.AppendLine("[CommandAudit]");
        sb.AppendLine("timestamp,source,input,command,confidence,is_valid,is_executed,result");
        foreach (var item in commandAudit)
        {
            sb.AppendLine($"{item.Timestamp:O},{CsvEscape(item.Source)},{CsvEscape(item.InputText)},{item.Command},{item.Confidence.ToString(CultureInfo.InvariantCulture)},{item.IsValid},{item.IsExecuted},{CsvEscape(item.ResultMessage)}");
        }

        return sb.ToString();
    }

    private async Task<IReadOnlyList<TelemetryData>> LoadTelemetryRawAsync(int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, MaxTelemetryRawRows);
        var result = new List<TelemetryData>(take);

        if (!File.Exists(_telemetryDbPath))
        {
            return result;
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_telemetryDbPath}");
            await connection.OpenAsync(ct);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT Latitude, Longitude, Altitude, Heading, Pitch, Roll, Speed,
                           GroundSpeed, AirSpeed, VerticalSpeed,
                           BatteryVoltage, BatteryCurrent, BatteryPercentage,
                           SatelliteCount, HDOP, FlightMode, IsArmed, Timestamp
                    FROM Telemetry
                    ORDER BY Timestamp DESC
                    LIMIT $limit
                    """;
                command.Parameters.AddWithValue("$limit", take);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    result.Add(ReadTelemetryRawExtended(reader));
                }
            }
            catch (SqliteException)
            {
                // Backward compatibility for telemetry DB schema that only has base columns.
                await using var legacyCommand = connection.CreateCommand();
                legacyCommand.CommandText = """
                    SELECT Latitude, Longitude, Altitude, Heading, Pitch, Roll, Speed, Timestamp
                    FROM Telemetry
                    ORDER BY Timestamp DESC
                    LIMIT $limit
                    """;
                legacyCommand.Parameters.AddWithValue("$limit", take);

                await using var legacyReader = await legacyCommand.ExecuteReaderAsync(ct);
                while (await legacyReader.ReadAsync(ct))
                {
                    result.Add(ReadTelemetryRawLegacy(legacyReader));
                }
            }
        }
        catch
        {
            return Array.Empty<TelemetryData>();
        }

        result.Reverse();
        return result;
    }

    private static TelemetryData ReadTelemetryRawExtended(SqliteDataReader reader)
    {
        var speed = ReadDoubleValue(reader, 6);
        var groundSpeed = ReadDoubleValue(reader, 7, speed);

        return new TelemetryData
        {
            Latitude = ReadDoubleValue(reader, 0),
            Longitude = ReadDoubleValue(reader, 1),
            Altitude = ReadDoubleValue(reader, 2),
            Heading = ReadDoubleValue(reader, 3),
            Pitch = ReadDoubleValue(reader, 4),
            Roll = ReadDoubleValue(reader, 5),
            Speed = speed,
            GroundSpeed = groundSpeed,
            AirSpeed = ReadDoubleValue(reader, 8, groundSpeed),
            VerticalSpeed = ReadDoubleValue(reader, 9),
            BatteryVoltage = ReadDoubleValue(reader, 10),
            BatteryCurrent = ReadDoubleValue(reader, 11),
            BatteryPercentage = ReadDoubleValue(reader, 12),
            SatelliteCount = ReadIntValue(reader, 13),
            HDOP = ReadDoubleValue(reader, 14),
            FlightMode = ReadEnumValue(reader, 15, FlightMode.MANUAL),
            IsArmed = ReadBoolValue(reader, 16),
            Timestamp = ParseTimestamp(reader, 17)
        };
    }

    private static TelemetryData ReadTelemetryRawLegacy(SqliteDataReader reader)
    {
        var speed = ReadDoubleValue(reader, 6);
        return new TelemetryData
        {
            Latitude = ReadDoubleValue(reader, 0),
            Longitude = ReadDoubleValue(reader, 1),
            Altitude = ReadDoubleValue(reader, 2),
            Heading = ReadDoubleValue(reader, 3),
            Pitch = ReadDoubleValue(reader, 4),
            Roll = ReadDoubleValue(reader, 5),
            Speed = speed,
            GroundSpeed = speed,
            AirSpeed = speed,
            VerticalSpeed = 0,
            BatteryVoltage = 0,
            BatteryCurrent = 0,
            BatteryPercentage = 0,
            SatelliteCount = 0,
            HDOP = 0,
            FlightMode = FlightMode.MANUAL,
            IsArmed = false,
            Timestamp = ParseTimestamp(reader, 7)
        };
    }

    private static double ReadDoubleValue(SqliteDataReader reader, int ordinal, double fallback = 0)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static int ReadIntValue(SqliteDataReader reader, int ordinal, int fallback = 0)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool ReadBoolValue(SqliteDataReader reader, int ordinal, bool fallback = false)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) == 1;
        }
        catch
        {
            return fallback;
        }
    }

    private static TEnum ReadEnumValue<TEnum>(SqliteDataReader reader, int ordinal, TEnum fallback) where TEnum : struct, Enum
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            var raw = Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            return Enum.IsDefined(typeof(TEnum), raw)
                ? (TEnum)Enum.ToObject(typeof(TEnum), raw)
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static List<TelemetrySnapshot> BuildSnapshots(IReadOnlyList<TelemetryData> telemetryRaw)
    {
        var snapshots = new List<TelemetrySnapshot>(telemetryRaw.Count);
        TelemetrySnapshot? previous = null;
        foreach (var sample in telemetryRaw)
        {
            var snapshot = sample.ToSnapshot(previous);
            snapshots.Add(snapshot);
            previous = snapshot;
        }

        return snapshots;
    }

    private static List<TelemetrySnapshot> BuildSampledSnapshots(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        AISettings? aiSettings)
    {
        if (snapshots.Count == 0)
        {
            return new List<TelemetrySnapshot>();
        }

        var settings = aiSettings ?? new AISettings();
        var sampler = new TelemetrySampler(settings);
        var sampled = new List<TelemetrySnapshot>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            if (sampler.ShouldSample(snapshot))
            {
                sampled.Add(snapshot);
            }
        }

        return sampled;
    }

    private static ResearchExportMetadata BuildResearchMetadata(
        LLMHealthStatus? providerHealth,
        AISettings? aiSettings)
    {
        return new ResearchExportMetadata
        {
            GeneratedAt = DateTime.UtcNow,
            PrimaryProvider = providerHealth?.PrimaryProvider ?? aiSettings?.Provider ?? "unknown",
            FallbackProvider = providerHealth?.FallbackProvider ?? aiSettings?.FallbackProvider ?? "unknown",
            ActiveProvider = providerHealth?.ActiveProvider ?? "unknown",
            ActiveModel = providerHealth?.ActiveModel ?? "unknown",
            PrimaryModel = providerHealth?.PrimaryModel ?? aiSettings?.Models?.TelemetryAnalysis ?? "unknown",
            FallbackModel = providerHealth?.FallbackModel ?? aiSettings?.Models?.Fallback ?? "unknown",
            FallbackAvailable = providerHealth?.FallbackAvailable ?? false,
            CircuitOpen = providerHealth?.CircuitOpen ?? false,
            ConsecutiveFailures = providerHealth?.ConsecutiveFailures ?? 0,
            CacheHitRate = providerHealth?.CacheHitRate ?? 0,
            TotalRequests = providerHealth?.TotalRequests ?? 0,
            FailedRequests = providerHealth?.FailedRequests ?? 0,
            FallbackUsageCount = providerHealth?.FallbackUsageCount ?? 0,
            LastLatencyMs = providerHealth?.LastLatencyMs ?? 0,
            LastError = providerHealth?.LastError ?? string.Empty
        };
    }

    private static DatasetEvaluationReport BuildDatasetEvaluation(
        IReadOnlyList<TelemetrySnapshot> telemetrySampled,
        IReadOnlyList<Anomaly> anomalies,
        AISettings? aiSettings)
    {
        const int evaluationWindowSeconds = 5;
        var report = new DatasetEvaluationReport
        {
            GeneratedAt = DateTime.UtcNow,
            EvaluationWindowSeconds = evaluationWindowSeconds,
            TelemetrySampleCount = telemetrySampled.Count
        };

        if (telemetrySampled.Count == 0)
        {
            return report;
        }

        var thresholds = aiSettings?.AnomalyDetection?.Thresholds ?? new AnomalyThresholds();
        var predictedTimes = anomalies
            .Where(a => a != null && a.Severity != AnomalySeverity.Info)
            .Select(a => a.Timestamp.ToUniversalTime())
            .OrderBy(t => t)
            .ToList();
        var window = TimeSpan.FromSeconds(evaluationWindowSeconds);

        long tp = 0;
        long fp = 0;
        long tn = 0;
        long fn = 0;
        var predictedPositiveCount = 0;
        var pseudoPositiveCount = 0;

        foreach (var snapshot in telemetrySampled)
        {
            var pseudoPositive = IsPseudoLabelPositive(snapshot, thresholds);
            if (pseudoPositive)
            {
                pseudoPositiveCount++;
            }

            var predictedPositive = predictedTimes.Any(t =>
                t >= snapshot.Timestamp.ToUniversalTime() - window &&
                t <= snapshot.Timestamp.ToUniversalTime() + window);
            if (predictedPositive)
            {
                predictedPositiveCount++;
            }

            if (predictedPositive && pseudoPositive)
            {
                tp++;
            }
            else if (predictedPositive && !pseudoPositive)
            {
                fp++;
            }
            else if (!predictedPositive && pseudoPositive)
            {
                fn++;
            }
            else
            {
                tn++;
            }
        }

        var precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
        var recall = tp + fn == 0 ? 0 : (double)tp / (tp + fn);
        var f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        var total = tp + fp + tn + fn;
        var accuracy = total == 0 ? 0 : (double)(tp + tn) / total;

        report.PredictedPositiveCount = predictedPositiveCount;
        report.PseudoLabelPositiveCount = pseudoPositiveCount;
        report.TruePositive = tp;
        report.FalsePositive = fp;
        report.TrueNegative = tn;
        report.FalseNegative = fn;
        report.Precision = precision;
        report.Recall = recall;
        report.F1Score = f1;
        report.Accuracy = accuracy;
        return report;
    }

    private static bool IsPseudoLabelPositive(TelemetrySnapshot snapshot, AnomalyThresholds thresholds)
    {
        if (snapshot.BatteryPercent > 0 && snapshot.BatteryPercent <= thresholds.BatteryCritical)
        {
            return true;
        }

        if (snapshot.GpsSatellites > 0 && snapshot.GpsSatellites <= thresholds.GpsLostThreshold)
        {
            return true;
        }

        if (snapshot.GpsHdop >= 2.5)
        {
            return true;
        }

        if (snapshot.Altitude >= thresholds.AltitudeCritical)
        {
            return true;
        }

        if (snapshot.Speed >= thresholds.HighSpeedThreshold)
        {
            return true;
        }

        if (snapshot.VerticalSpeed <= thresholds.RapidDescentThreshold)
        {
            return true;
        }

        var vibrationMagnitude = Math.Abs(snapshot.VibrationX) + Math.Abs(snapshot.VibrationY) + Math.Abs(snapshot.VibrationZ);
        if (vibrationMagnitude >= thresholds.HighVibrationThreshold)
        {
            return true;
        }

        return snapshot.WindSpeed >= thresholds.WindMaxSpeed;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        if (normalized.Contains(',', StringComparison.Ordinal) ||
            normalized.Contains('"', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{normalized}\"";
        }

        return normalized;
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = OpenConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS PIA_ChatHistory (
                    Id TEXT PRIMARY KEY,
                    Role INTEGER NOT NULL,
                    Content TEXT NOT NULL,
                    Urgency INTEGER NOT NULL,
                    Timestamp TEXT NOT NULL,
                    PendingCommand TEXT NULL,
                    RequireConfirmation INTEGER NOT NULL,
                    Confirmed INTEGER NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_ChatHistory_Timestamp
                ON PIA_ChatHistory (Timestamp DESC);

                CREATE TABLE IF NOT EXISTS PIA_AnomalyHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type INTEGER NOT NULL,
                    Severity INTEGER NOT NULL,
                    Message TEXT NOT NULL,
                    Recommendation TEXT NULL,
                    Timestamp TEXT NOT NULL,
                    Priority REAL NOT NULL,
                    SourceLayer TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_AnomalyHistory_Timestamp
                ON PIA_AnomalyHistory (Timestamp DESC);

                CREATE TABLE IF NOT EXISTS PIA_PerformanceHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Score REAL NOT NULL,
                    Grade TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_PerformanceHistory_Timestamp
                ON PIA_PerformanceHistory (Timestamp DESC);

                CREATE TABLE IF NOT EXISTS PIA_BatteryPredictionHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    CurrentBatteryPercent REAL NOT NULL,
                    EstimatedDrainRatePerMinute REAL NOT NULL,
                    EstimatedRemainingMinutes REAL NOT NULL,
                    EstimatedDepletionAt TEXT NOT NULL,
                    HealthScore REAL NOT NULL,
                    Condition TEXT NOT NULL,
                    Recommendation TEXT NOT NULL,
                    Confidence REAL NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_BatteryPredictionHistory_Timestamp
                ON PIA_BatteryPredictionHistory (Timestamp DESC);

                CREATE TABLE IF NOT EXISTS PIA_FlightSessionHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GeneratedAt TEXT NOT NULL,
                    SessionStart TEXT NOT NULL,
                    SessionEnd TEXT NOT NULL,
                    SnapshotCount INTEGER NOT NULL,
                    DurationMinutes REAL NOT NULL,
                    StartBatteryPercent REAL NOT NULL,
                    EndBatteryPercent REAL NOT NULL,
                    BatteryUsedPercent REAL NOT NULL,
                    AvgSpeed REAL NOT NULL,
                    MaxAltitude REAL NOT NULL,
                    AnomalyCount INTEGER NOT NULL,
                    OverallStatus TEXT NOT NULL,
                    SummaryText TEXT NOT NULL,
                    RecommendedAction TEXT NOT NULL,
                    Confidence REAL NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_FlightSessionHistory_GeneratedAt
                ON PIA_FlightSessionHistory (GeneratedAt DESC);

                CREATE TABLE IF NOT EXISTS PIA_ResearchValidationHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    AnomalySampleCount INTEGER NOT NULL,
                    Precision REAL NOT NULL,
                    Recall REAL NOT NULL,
                    F1Score REAL NOT NULL,
                    BatteryMape REAL NOT NULL,
                    BatteryMapeSamples INTEGER NOT NULL,
                    ChatLatencyMs REAL NOT NULL DEFAULT 0,
                    VoiceLatencyMs REAL NOT NULL DEFAULT 0,
                    LlmLatencyMs REAL NOT NULL DEFAULT 0,
                    AnomalyLatencyMs REAL NOT NULL DEFAULT 0,
                    TelemetryRateHz REAL NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_ResearchValidationHistory_Timestamp
                ON PIA_ResearchValidationHistory (Timestamp DESC);

                CREATE TABLE IF NOT EXISTS PIA_CommandAuditHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    InputText TEXT NOT NULL,
                    Command INTEGER NOT NULL,
                    Confidence REAL NOT NULL,
                    IsValid INTEGER NOT NULL,
                    IsExecuted INTEGER NOT NULL,
                    ResultMessage TEXT NOT NULL,
                    Source TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PIA_CommandAuditHistory_Timestamp
                ON PIA_CommandAuditHistory (Timestamp DESC);
                """;
            command.ExecuteNonQuery();

            EnsureColumnExists(connection, "PIA_ResearchValidationHistory", "ChatLatencyMs", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(connection, "PIA_ResearchValidationHistory", "VoiceLatencyMs", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(connection, "PIA_ResearchValidationHistory", "LlmLatencyMs", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(connection, "PIA_ResearchValidationHistory", "AnomalyLatencyMs", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(connection, "PIA_ResearchValidationHistory", "TelemetryRateHz", "REAL NOT NULL DEFAULT 0");
        }
        catch
        {
        }
    }

    private static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnTypeSql)
    {
        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = checkCmd.ExecuteReader();
            while (reader.Read())
            {
                var existingColumn = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (string.Equals(existingColumn, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnTypeSql}";
            alterCmd.ExecuteNonQuery();
        }
        catch
        {
        }
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    private static async Task PruneAsync(
        SqliteConnection connection,
        string tableName,
        int maxRows,
        CancellationToken ct,
        string orderByColumn = "Timestamp")
    {
        await using var prune = connection.CreateCommand();
        prune.CommandText = $"""
            DELETE FROM {tableName}
            WHERE rowid NOT IN (
                SELECT rowid FROM {tableName}
                ORDER BY {orderByColumn} DESC
                LIMIT $keep
            )
            """;
        prune.Parameters.AddWithValue("$keep", maxRows);
        await prune.ExecuteNonQueryAsync(ct);
    }

    private static TEnum ReadEnum<TEnum>(SqliteDataReader reader, int ordinal, TEnum fallback) where TEnum : struct, Enum
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var raw = reader.GetInt32(ordinal);
        return Enum.IsDefined(typeof(TEnum), raw)
            ? (TEnum)Enum.ToObject(typeof(TEnum), raw)
            : fallback;
    }

    private static bool? ReadNullableBool(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetInt32(ordinal) == 1;
    }

    private static DateTime ParseTimestamp(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTime.UtcNow;
        }

        var value = reader.GetString(ordinal);
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;
    }
}
