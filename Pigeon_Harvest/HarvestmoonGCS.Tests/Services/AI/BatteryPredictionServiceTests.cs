using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI;

public class BatteryPredictionServiceTests
{
    [Fact]
    public async Task PredictAsync_WithTelemetry_ReturnsPrediction()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 30, maxSnapshots: 3600);
        var now = DateTime.UtcNow;

        buffer.Add(CreateSnapshot(now.AddMinutes(-4), 88));
        buffer.Add(CreateSnapshot(now.AddMinutes(-3), 84));
        buffer.Add(CreateSnapshot(now.AddMinutes(-2), 80));

        var service = new BatteryPredictionService(() => new UnavailableTestLLM(), buffer);

        var prediction = await service.PredictAsync();

        prediction.Should().NotBeNull();
        prediction!.EstimatedRemainingMinutes.Should().BeGreaterThan(0);
        prediction.CurrentBatteryPercent.Should().BeGreaterThan(0);
        prediction.Condition.Should().BeOneOf("GOOD", "WARNING", "CRITICAL");
    }

    [Fact]
    public async Task PredictAsync_MultiplePredictions_UpdatesMapeMetrics()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 30, maxSnapshots: 3600);
        var now = DateTime.UtcNow;

        buffer.Add(CreateSnapshot(now.AddMinutes(-5), 92));
        buffer.Add(CreateSnapshot(now.AddMinutes(-4), 88));
        buffer.Add(CreateSnapshot(now.AddMinutes(-3), 84));

        var service = new BatteryPredictionService(() => new UnavailableTestLLM(), buffer);
        var firstPrediction = await service.PredictAsync();
        firstPrediction.Should().NotBeNull();

        buffer.Add(CreateSnapshot(now, 72));
        var secondPrediction = await service.PredictAsync();
        secondPrediction.Should().NotBeNull();

        var metrics = service.GetMetrics();
        metrics.SampleCount.Should().BeGreaterThan(0);
        metrics.MeanAbsolutePercentageError.Should().BeGreaterThanOrEqualTo(0);
    }

    private static TelemetrySnapshot CreateSnapshot(DateTime timestamp, double batteryPercent)
    {
        return new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryPercent = batteryPercent,
            BatteryVoltage = 15.2,
            Altitude = 110,
            Speed = 12,
            FlightMode = FlightMode.AUTO,
            Armed = true,
            GpsSatellites = 10,
            GpsHdop = 1.0
        };
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

