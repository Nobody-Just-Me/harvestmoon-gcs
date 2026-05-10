using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Services.AI;
using Xunit;

namespace Pigeon_Uno.Tests.Services.AI;

public class NaturalLanguageServiceTests
{
    [Fact]
    public async Task ProcessMessageAsync_CommandQuery_ReturnsAdvisoryOnlyResponse()
    {
        var llm = new FakeLLMService();
        var buffer = CreateBufferWithSingleSnapshot();
        var service = new NaturalLanguageService(llm, buffer);

        var response = await service.ProcessMessageAsync("takeoff");

        response.Should().NotBeNull();
        response.Role.Should().Be(ChatRole.Assistant);
        response.RequireConfirmation.Should().BeFalse();
        response.PendingCommand.Should().BeNull();
        response.Content.Should().Contain("PIA hanya memberikan informasi");
        llm.GenerateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessMessageAsync_StatusQuery_UsesRuleBasedResponseWithoutLLM()
    {
        var llm = new FakeLLMService();
        var buffer = CreateBufferWithSingleSnapshot();
        var service = new NaturalLanguageService(llm, buffer);

        var response = await service.ProcessMessageAsync("status lengkap drone");

        response.Content.Should().Contain("Status Drone");
        response.RequireConfirmation.Should().BeFalse();
        response.PendingCommand.Should().BeNull();
        llm.GenerateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessMessageAsync_UnknownQuery_UsesLLMButStillNoPendingCommand()
    {
        var llm = new FakeLLMService();
        var buffer = CreateBufferWithSingleSnapshot();
        var service = new NaturalLanguageService(llm, buffer);

        var response = await service.ProcessMessageAsync("jelaskan kondisi angin hari ini");

        response.Should().NotBeNull();
        response.RequireConfirmation.Should().BeFalse();
        response.PendingCommand.Should().BeNull();
        llm.GenerateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessVoiceAsync_DelegatesToMessagePipeline()
    {
        var llm = new FakeLLMService();
        var buffer = CreateBufferWithSingleSnapshot();
        var service = new NaturalLanguageService(llm, buffer);

        var response = await service.ProcessVoiceAsync("status lengkap drone");

        response.Should().NotBeNull();
        response.Content.Should().Contain("Status Drone");
        service.MessageHistory.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearHistory_RemovesAllMessages()
    {
        var llm = new FakeLLMService();
        var buffer = CreateBufferWithSingleSnapshot();
        var service = new NaturalLanguageService(llm, buffer);
        await service.ProcessMessageAsync("status lengkap drone");
        service.MessageHistory.Should().NotBeEmpty();

        service.ClearHistory();

        service.MessageHistory.Should().BeEmpty();
    }

    private static TelemetryBuffer CreateBufferWithSingleSnapshot()
    {
        var buffer = new TelemetryBuffer(windowMinutes: 5, maxSnapshots: 360);
        buffer.Add(new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            BatteryPercent = 85,
            BatteryVoltage = 12.2,
            GpsLatitude = -6.2,
            GpsLongitude = 106.8,
            GpsAltitude = 100,
            GpsSatellites = 10,
            GpsHdop = 0.9,
            Altitude = 100,
            Speed = 12,
            VerticalSpeed = 0.3,
            Heading = 182,
            Roll = 1.1,
            Pitch = 0.8,
            Yaw = 182,
            FlightMode = FlightMode.AUTO,
            Armed = true,
            VibrationX = 3,
            VibrationY = 3.2,
            VibrationZ = 4.1,
            WindSpeed = 2.2,
            WindDirection = 150
        });
        return buffer;
    }

    private sealed class FakeLLMService : ILLMService
    {
        public bool IsAvailable => true;
        public string ProviderName => "FakeLLM";
        public int GenerateCallCount { get; private set; }

        public Task<LLMResult> GenerateAsync(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default)
        {
            GenerateCallCount++;
            return Task.FromResult(LLMResult.Ok("Respons sintetis.", "fake-model"));
        }

        public Task<T?> GenerateStructuredAsync<T>(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default) where T : class
        {
            return Task.FromResult<T?>(null);
        }

        public Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public LLMHealthStatus GetHealthStatus()
        {
            return new LLMHealthStatus { IsConnected = true };
        }
    }
}
