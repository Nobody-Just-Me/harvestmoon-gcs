using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Services.AI;
using Xunit;

namespace Pigeon_Uno.Tests.Services.AI;

public class MaintenancePredictionServiceTests
{
    [Fact]
    public async Task PredictAsync_WithEmptyBuffer_ReturnsEmptyList()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 5, maxSnapshots: 360);
        var settings = new AISettings();
        var service = new MaintenancePredictionService(() => new UnavailableTestLLM(), buffer, settings);

        var result = await service.PredictAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateScheduleAsync_WithTelemetry_ReturnsTasks()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 5, maxSnapshots: 360);
        AddSnapshot(buffer, DateTime.UtcNow.AddSeconds(-3), 95, 2.1, 2.4, 2.9, 4.0);
        AddSnapshot(buffer, DateTime.UtcNow.AddSeconds(-1), 91, 4.4, 4.7, 5.2, 7.5);

        var settings = new AISettings();
        var service = new MaintenancePredictionService(() => new UnavailableTestLLM(), buffer, settings);

        var schedule = await service.GenerateScheduleAsync();

        schedule.Should().NotBeNull();
        schedule.Tasks.Should().NotBeEmpty();
        schedule.Tasks[0].Component.Should().NotBeNullOrWhiteSpace();
    }

    private static void AddSnapshot(TelemetryBuffer buffer, DateTime timestamp, double batteryPercent, double vx, double vy, double vz, double wind)
    {
        buffer.Add(new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryPercent = batteryPercent,
            BatteryVoltage = 12.1,
            Altitude = 102,
            Speed = 11,
            FlightMode = FlightMode.AUTO,
            Armed = true,
            GpsSatellites = 10,
            GpsHdop = 0.9,
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
