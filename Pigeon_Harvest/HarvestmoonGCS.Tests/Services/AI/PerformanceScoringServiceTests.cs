using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI;

public class PerformanceScoringServiceTests
{
    [Fact]
    public async Task CalculateScoreAsync_WithEmptyBuffer_ReturnsZeroScore()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 5, maxSnapshots: 360);
        var service = new PerformanceScoringService(() => new UnavailableTestLLM(), buffer);

        var score = await service.CalculateScoreAsync();

        score.TotalScore.Should().Be(0);
        score.Grade.Should().Be("D");
        score.Feedback.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CalculateScoreAsync_WithTelemetry_ProducesNonZeroScore()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 5, maxSnapshots: 360);
        AddSnapshot(buffer, DateTime.UtcNow.AddSeconds(-4), 96, 7.5, 1.1, 1.3, 1.2, 3.5, 180);
        AddSnapshot(buffer, DateTime.UtcNow.AddSeconds(-2), 94, 8.1, 1.2, 1.1, 1.4, 4.2, 182);
        AddSnapshot(buffer, DateTime.UtcNow, 92, 8.4, 1.0, 1.2, 1.3, 3.9, 178);

        var service = new PerformanceScoringService(() => new UnavailableTestLLM(), buffer);

        var score = await service.CalculateScoreAsync();

        score.TotalScore.Should().BeGreaterThan(0);
        score.Grade.Should().NotBeNullOrWhiteSpace();
        score.Feedback.Should().NotBeNullOrWhiteSpace();
    }

    private static void AddSnapshot(
        TelemetryBuffer buffer,
        DateTime timestamp,
        double batteryPercent,
        double speed,
        double vx,
        double vy,
        double vz,
        double wind,
        double heading)
    {
        buffer.Add(new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryPercent = batteryPercent,
            BatteryVoltage = 12.3,
            Altitude = 100,
            Speed = speed,
            Heading = heading,
            FlightMode = FlightMode.AUTO,
            Armed = true,
            GpsSatellites = 11,
            GpsHdop = 0.8,
            VibrationX = vx,
            VibrationY = vy,
            VibrationZ = vz,
            WindSpeed = wind
        });
    }

    private sealed class UnavailableTestLLM : ILLMService
    {
        public bool IsAvailable => false;
        public string ProviderName => "Unavailable";

        public Task<LLMResult> GenerateAsync(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default)
            => Task.FromResult(LLMResult.Fail("Unavailable"));

        public Task<T?> GenerateStructuredAsync<T>(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(false);

        public LLMHealthStatus GetHealthStatus() => new() { IsConnected = false };
    }
}
