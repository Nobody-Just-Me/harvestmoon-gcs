using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

public class PerformanceScoringService
{
    private readonly Func<ILLMService> _llmFactory;
    private readonly TelemetryBuffer _telemetryBuffer;
    private readonly List<PerformanceTrend> _trendHistory = new();

    public PerformanceScore? LastScore { get; private set; }
    public event EventHandler<PerformanceScore>? ScoreUpdated;

    public PerformanceScoringService(
        Func<ILLMService> llmFactory,
        TelemetryBuffer telemetryBuffer)
    {
        _llmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
        _telemetryBuffer = telemetryBuffer ?? throw new ArgumentNullException(nameof(telemetryBuffer));
    }

    public async Task<PerformanceScore> CalculateScoreAsync(CancellationToken ct = default)
    {
        var snapshots = _telemetryBuffer.GetAll();
        if (snapshots.Count == 0)
        {
            var empty = new PerformanceScore
            {
                EfficiencyScore = 0,
                StabilityScore = 0,
                SafetyScore = 0,
                SkillScore = 0,
                TotalScore = 0,
                Grade = "D",
                Feedback = "Belum ada data telemetri untuk dihitung."
            };
            LastScore = empty;
            return empty;
        }

        var summary = TelemetryAggregator.Summarize(snapshots);

        var efficiencyScore = CalculateEfficiency(summary);
        var stabilityScore = CalculateStability(summary);
        var safetyScore = CalculateSafety(summary);
        var skillScore = CalculateSkill(summary);
        var total = Math.Clamp(efficiencyScore + stabilityScore + safetyScore + skillScore, 0, 100);

        var score = new PerformanceScore
        {
            EfficiencyScore = efficiencyScore,
            StabilityScore = stabilityScore,
            SafetyScore = safetyScore,
            SkillScore = skillScore,
            TotalScore = total,
            Grade = GetGrade(total),
            Timestamp = DateTime.UtcNow
        };

        var feedback = await GenerateFeedbackAsync(score, summary, ct);
        score.Feedback = feedback.OverallAssessment;

        LastScore = score;
        _trendHistory.Add(new PerformanceTrend
        {
            Date = score.Timestamp,
            Score = score.TotalScore,
            Grade = score.Grade
        });

        if (_trendHistory.Count > 1000)
        {
            _trendHistory.RemoveRange(0, _trendHistory.Count - 1000);
        }

        ScoreUpdated?.Invoke(this, score);
        return score;
    }

    public async Task<PerformanceFeedback> GenerateFeedbackAsync(
        PerformanceScore score,
        TelemetrySummary summary,
        CancellationToken ct = default)
    {
        try
        {
            var llm = _llmFactory();
            if (llm.IsAvailable)
            {
                var prompt = BuildFeedbackPrompt(score, summary);
                var feedback = await llm.GenerateStructuredAsync<PerformanceFeedback>(
                    prompt,
                    LLMRole.PerformanceScoring,
                    ct);

                if (feedback != null && !string.IsNullOrWhiteSpace(feedback.OverallAssessment))
                {
                    return feedback;
                }
            }
        }
        catch
        {
            // fallback below
        }

        return BuildHeuristicFeedback(score, summary);
    }

    public Task<List<PerformanceTrend>> GetTrendAsync(int lastFlights = 20)
    {
        var take = Math.Max(1, lastFlights);
        return Task.FromResult(_trendHistory.TakeLast(take).ToList());
    }

    private static double CalculateEfficiency(TelemetrySummary summary)
    {
        var drainPenalty = Math.Clamp(summary.BatteryDrainRate * 2.0, 0, 20);
        var speedScore = Math.Clamp(10 - Math.Abs(summary.SpeedAvg - 8.0), 0, 10);
        var result = 20 + speedScore - drainPenalty;
        return Math.Clamp(result, 0, 30);
    }

    private static double CalculateStability(TelemetrySummary summary)
    {
        var vibrationPenalty = Math.Clamp((summary.VibrationXAvg + summary.VibrationYAvg + summary.VibrationZAvg) / 3.0, 0, 15);
        var altitudePenalty = Math.Clamp(summary.AltitudeStdDev / 5.0, 0, 10);
        return Math.Clamp(25 - vibrationPenalty - altitudePenalty, 0, 25);
    }

    private static double CalculateSafety(TelemetrySummary summary)
    {
        var batteryPenalty = summary.BatteryMin < 20 ? 10 : summary.BatteryMin < 35 ? 5 : 0;
        var dropoutPenalty = Math.Clamp(summary.DropoutCount * 1.5, 0, 10);
        var windPenalty = summary.WindSpeedMax > 10 ? 5 : 0;
        return Math.Clamp(25 - batteryPenalty - dropoutPenalty - windPenalty, 0, 25);
    }

    private static double CalculateSkill(TelemetrySummary summary)
    {
        var modePenalty = Math.Clamp(summary.ModeChanges, 0, 8);
        var headingPenalty = Math.Clamp(summary.HeadingStdDev / 30.0, 0, 12);
        return Math.Clamp(20 - modePenalty - headingPenalty, 0, 20);
    }

    private static string GetGrade(double total)
    {
        if (total >= 90) return "A+";
        if (total >= 85) return "A";
        if (total >= 80) return "B+";
        if (total >= 75) return "B";
        if (total >= 70) return "C+";
        if (total >= 65) return "C";
        return "D";
    }

    private static PerformanceFeedback BuildHeuristicFeedback(PerformanceScore score, TelemetrySummary summary)
    {
        var feedback = new PerformanceFeedback();

        feedback.OverallAssessment =
            $"Skor total {score.TotalScore:0.0} ({score.Grade}). " +
            "Fokuskan perbaikan pada stabilitas dan keselamatan untuk penerbangan berikutnya.";

        if (score.EfficiencyScore >= 22)
        {
            feedback.Strengths.Add("Efisiensi energi cukup baik pada profil kecepatan saat ini.");
        }
        if (score.StabilityScore >= 18)
        {
            feedback.Strengths.Add("Stabilitas attitude dan altitude relatif konsisten.");
        }
        if (score.SafetyScore >= 18)
        {
            feedback.Strengths.Add("Pengelolaan safety secara umum aman.");
        }

        if (summary.BatteryDrainRate > 4)
        {
            feedback.Improvements.Add("Kurangi manuver agresif untuk menekan drain baterai.");
        }
        if (summary.WindSpeedMax > 10)
        {
            feedback.Improvements.Add("Hindari terbang saat angin di atas 10 m/s.");
        }
        if (summary.DropoutCount > 2)
        {
            feedback.Improvements.Add("Stabilkan kualitas link/GPS sebelum lanjut misi.");
        }

        feedback.Recommendations.Add("Lakukan pre-flight check lengkap sebelum takeoff.");
        feedback.Recommendations.Add("Jaga kecepatan jelajah di sekitar 7-9 m/s saat misi normal.");
        feedback.Recommendations.Add("Pantau baterai dan rencanakan RTL saat sisa < 35%.");

        return feedback;
    }

    private static string BuildFeedbackPrompt(PerformanceScore score, TelemetrySummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Anda adalah evaluator performa pilot UAV.");
        sb.AppendLine("Buat feedback personal berdasarkan skor dan telemetri berikut.");
        sb.AppendLine();
        sb.AppendLine($"EfficiencyScore: {score.EfficiencyScore:0.##}/30");
        sb.AppendLine($"StabilityScore: {score.StabilityScore:0.##}/25");
        sb.AppendLine($"SafetyScore: {score.SafetyScore:0.##}/25");
        sb.AppendLine($"SkillScore: {score.SkillScore:0.##}/20");
        sb.AppendLine($"TotalScore: {score.TotalScore:0.##}");
        sb.AppendLine($"BatteryMin: {summary.BatteryMin:0.##}");
        sb.AppendLine($"DropoutCount: {summary.DropoutCount}");
        sb.AppendLine($"WindSpeedMax: {summary.WindSpeedMax:0.##}");
        sb.AppendLine();
        sb.AppendLine("Kembalikan JSON object dengan field:");
        sb.AppendLine("overallAssessment,strengths[],improvements[],recommendations[]");
        return sb.ToString();
    }
}
