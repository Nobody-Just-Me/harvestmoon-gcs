using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Tests.Services.AI;

public class TelemetryAnalysisServiceTests
{
    private class FakeTelemetryBuffer : ITelemetryBufferProvider
    {
        private readonly List<TelemetrySnapshot> _snapshots;
        public FakeTelemetryBuffer(IEnumerable<TelemetrySnapshot> snapshots)
        {
            _snapshots = new List<TelemetrySnapshot>(snapshots);
        }
        public IEnumerable<TelemetrySnapshot> GetSnapshots(int lastSeconds) => _snapshots;
    }

    private class FakeLLMService : ILLMService
    {
        private readonly TelemetryAnalysis? _analysisToReturn;
        public FakeLLMService(TelemetryAnalysis? analysisToReturn)
        {
            _analysisToReturn = analysisToReturn;
        }
        public bool IsAvailable => true;
        public string ProviderName => "FakeLLM";
        public Task<LLMResult> GenerateAsync(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, System.Threading.CancellationToken ct = default)
        {
            // Return a static string as JSON in this test scenario
            var json = _analysisToReturn?.ToString() ?? string.Empty;
            return Task.FromResult(LLMResult.Ok(json, "FakeLLM"));
        }
        public Task<T?> GenerateStructuredAsync<T>(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, System.Threading.CancellationToken ct = default) where T : class
        {
            if (_analysisToReturn is T t)
                return Task.FromResult<T?>(t);
            // If requested type is TelemetryAnalysis, return the provided one
            if (typeof(T) == typeof(TelemetryAnalysis))
                return Task.FromResult<T?>( _analysisToReturn as T);
            return Task.FromResult<T?>(null);
        }
        public Task<bool> TestConnectionAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(true);
        public LLMHealthStatus GetHealthStatus() => new LLMHealthStatus { IsConnected = true };
    }

    private class DummyOutputHandler
    {
        public TelemetryAnalysis Analysis { get; set; } = new TelemetryAnalysis { OverallStatus = "GOOD", Confidence = 0.99 };
    }

    [Fact]
    public async Task Analyze_Reports_AnalysisCompleted_On_Success()
    {
        // Arrange
        var snapshots = new List<TelemetrySnapshot> { new TelemetrySnapshot { Timestamp = DateTime.UtcNow } };
        var buffer = new FakeTelemetryBuffer(snapshots);
        var expected = new TelemetryAnalysis { OverallStatus = "GOOD", Confidence = 0.95 };
        var aiSettings = new AISettings { Enabled = true };
        aiSettings.Analysis = new HarvestmoonGCS.Core.Models.AI.AIAnalysisConfig { IntervalSeconds = 1, BufferSeconds = 0 };
        var service = new TelemetryAnalysisService(aiSettings, buffer, () => new FakeLLMService(expected));

        TelemetryAnalysis? completed = null;
        service.AnalysisCompleted += (s, a) => completed = a;

        // Act
        service.Start();
        await Task.Delay(500);
        service.Stop();

        // Assert
        completed.Should().NotBeNull();
        completed!.OverallStatus.Should().Be("GOOD");
    }

    [Fact]
    public async Task Analyze_Does_NotEmit_When_LLM_Fails()
    {
        // Arrange: LLM returns null analysis
        var snapshots = new List<TelemetrySnapshot> { new TelemetrySnapshot { Timestamp = DateTime.UtcNow } };
        var buffer = new FakeTelemetryBuffer(snapshots);
        var service = new TelemetryAnalysisService(new AISettings { Enabled = true }, buffer, () => (ILLMService)null!);

        bool fired = false;
        service.AnalysisCompleted += (s, a) => fired = true;

        // Act
        service.Start();
        await Task.Delay(500);
        service.Stop();

        // Assert
        fired.Should().BeFalse();
    }
}
