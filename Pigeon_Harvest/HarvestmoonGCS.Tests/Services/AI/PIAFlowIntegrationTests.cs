using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI;

public class PIAFlowIntegrationTests : IDisposable
{
    private readonly AISettings _aiSettings;

    public PIAFlowIntegrationTests()
    {
        _aiSettings = new AISettings
        {
            Enabled = true,
            ApiKey = "test-api-key-for-integration",
            BaseUrl = "https://openrouter.ai/api/v1",
            Cache = new AICacheConfig { Enabled = false }
        };
        _aiSettings.Models.TelemetryAnalysis = "google/gemini-2.5-flash-lite";
        _aiSettings.Models.Fallback = "openrouter/free";
        _aiSettings.Analysis.IntervalSeconds = 1;
        _aiSettings.Analysis.BufferSeconds = 30;
    }

    private static void InjectHttpClient(OpenRouterService service, HttpClient httpClient)
    {
        var field = typeof(OpenRouterService).GetField("_httpClient", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(service, httpClient);
    }

    private static HttpResponseMessage CreateMockOpenRouterResponse()
    {
        var analysisResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(new TelemetryAnalysis
                        {
                            OverallStatus = "GOOD",
                            KeyInsights = "Flight parameters normal.",
                            Warnings = new List<string>(),
                            Recommendations = new List<string> { "Continue mission" },
                            PredictedIssues = new List<string>(),
                            Confidence = 0.95,
                            Timestamp = DateTime.UtcNow
                        })
                    }
                }
            }
        };

        return new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(analysisResponse), Encoding.UTF8, "application/json")
        };
    }

    private static List<TelemetrySnapshot> CreateTestSnapshots(int count, DateTime startTime)
    {
        var snapshots = new List<TelemetrySnapshot>();
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            snapshots.Add(new TelemetrySnapshot
            {
                Timestamp = startTime.AddSeconds(i * 2),
                BatteryPercent = 85 - (i * 0.5),
                BatteryVoltage = 12.6 - (i * 0.01),
                GpsLatitude = -6.2 + (random.NextDouble() * 0.001),
                GpsLongitude = 106.8 + (random.NextDouble() * 0.001),
                GpsAltitude = 50 + (random.NextDouble() * 5),
                GpsSatellites = 10,
                GpsHdop = 1.0,
                Altitude = 50 + (i * 0.5),
                Speed = 10 + (random.NextDouble() * 2),
                VerticalSpeed = random.NextDouble() * 2,
                Heading = 180 + (random.NextDouble() * 10),
                Roll = random.NextDouble() * 5,
                Pitch = random.NextDouble() * 3,
                Yaw = 180 + (random.NextDouble() * 10),
                FlightMode = FlightMode.AUTO,
                Armed = true,
                VibrationX = random.NextDouble() * 10,
                VibrationY = random.NextDouble() * 10,
                VibrationZ = random.NextDouble() * 15,
                WindSpeed = random.NextDouble() * 5,
                WindDirection = random.NextDouble() * 360
            });
        }
        
        return snapshots;
    }

    [Fact]
    public void TelemetryAggregator_Summarize_CalculatesCorrectStatistics()
    {
        var startTime = DateTime.UtcNow.AddMinutes(-1);
        var snapshots = CreateTestSnapshots(10, startTime);

        var summary = TelemetryAggregator.Summarize(snapshots);

        summary.SnapshotCount.Should().Be(10);
        summary.BatteryMin.Should().BeLessThan(summary.BatteryMax);
        summary.AltitudeMin.Should().BeLessThanOrEqualTo(summary.AltitudeMax);
        summary.SpeedMin.Should().BeLessThanOrEqualTo(summary.SpeedMax);
        summary.WindowStart.Should().BeBefore(summary.WindowEnd);
    }

    [Fact]
    public void TelemetryAggregator_ComputesBatteryDrainRate()
    {
        var snapshots = new List<TelemetrySnapshot>
        {
            new TelemetrySnapshot 
            { 
                Timestamp = DateTime.UtcNow.AddMinutes(-10), 
                BatteryPercent = 100,
                Altitude = 50,
                Speed = 10,
                Heading = 180,
                GpsSatellites = 8,
                GpsHdop = 1.0,
                FlightMode = FlightMode.AUTO,
                VibrationX = 5, VibrationY = 5, VibrationZ = 10, WindSpeed = 3
            },
            new TelemetrySnapshot 
            { 
                Timestamp = DateTime.UtcNow.AddMinutes(-5), 
                BatteryPercent = 90,
                Altitude = 55,
                Speed = 10,
                Heading = 180,
                GpsSatellites = 8,
                GpsHdop = 1.0,
                FlightMode = FlightMode.AUTO,
                VibrationX = 5, VibrationY = 5, VibrationZ = 10, WindSpeed = 3
            }
        };

        var summary = TelemetryAggregator.Summarize(snapshots);

        summary.BatteryDrainRate.Should().BeApproximately(2.0, 0.5);
    }

    [Fact]
    public void TelemetryAggregator_HandlesEmptySnapshotList()
    {
        var emptySnapshots = new List<TelemetrySnapshot>();

        var summary = TelemetryAggregator.Summarize(emptySnapshots);

        summary.SnapshotCount.Should().Be(0);
        summary.WindowStart.Should().Be(default);
        summary.WindowEnd.Should().Be(default);
    }

    [Fact]
    public async Task FullFlow_BufferProvider_Aggregator_LLM_Completes()
    {
        var startTime = DateTime.UtcNow.AddMinutes(-1);
        var snapshots = CreateTestSnapshots(10, startTime);
        
        var bufferProvider = new FakeTelemetryBufferProvider(snapshots);
        
        var fakeLLM = new FakeLLMService(new TelemetryAnalysis 
        { 
            OverallStatus = "GOOD", 
            Confidence = 0.95 
        });
        
        var analysisService = new TelemetryAnalysisService(
            _aiSettings,
            bufferProvider,
            () => fakeLLM
        );

        TelemetryAnalysis? capturedAnalysis = null;
        analysisService.AnalysisCompleted += (s, analysis) => capturedAnalysis = analysis;

        analysisService.Start();
        await Task.Delay(1500);
        analysisService.Stop();

        capturedAnalysis.Should().NotBeNull();
        capturedAnalysis!.OverallStatus.Should().Be("GOOD");
        capturedAnalysis.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task TelemetryAnalysisService_SkipsAnalysis_WhenBufferIsEmpty()
    {
        var emptySnapshots = new List<TelemetrySnapshot>();
        var bufferProvider = new FakeTelemetryBufferProvider(emptySnapshots);
        
        var fakeLLM = new FakeLLMService(new TelemetryAnalysis 
        { 
            OverallStatus = "GOOD", 
            Confidence = 0.95 
        });
        
        var analysisService = new TelemetryAnalysisService(
            _aiSettings,
            bufferProvider,
            () => fakeLLM
        );

        bool fired = false;
        analysisService.AnalysisCompleted += (s, a) => fired = true;

        analysisService.Start();
        await Task.Delay(500);
        analysisService.Stop();

        fired.Should().BeFalse();
    }

    [Fact]
    public void OpenRouterService_Properties_ReturnCorrectValues()
    {
        var mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateMockOpenRouterResponse);

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1")
        };

        var service = new OpenRouterService(_aiSettings);
        InjectHttpClient(service, httpClient);

        service.IsAvailable.Should().BeTrue();
        service.ProviderName.Should().Be("OpenRouter");
    }

    [Fact]
    public void TelemetryAggregator_ComputesTrend_Increasing()
    {
        var snapshots = new List<TelemetrySnapshot>();
        var now = DateTime.UtcNow;
        
        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(new TelemetrySnapshot
            {
                Timestamp = now.AddSeconds(-20 + i * 2),
                BatteryPercent = 80 + i,
                Altitude = 50 + i,
                Speed = 10,
                Heading = 180,
                GpsSatellites = 8,
                GpsHdop = 1.0,
                FlightMode = FlightMode.AUTO,
                VibrationX = 5, VibrationY = 5, VibrationZ = 10, WindSpeed = 3
            });
        }

        var summary = TelemetryAggregator.Summarize(snapshots);

        summary.AltitudeTrend.Should().Be(Trend.Increasing);
    }

    [Fact]
    public void TelemetryAggregator_ComputesTrend_Decreasing()
    {
        var snapshots = new List<TelemetrySnapshot>();
        var now = DateTime.UtcNow;
        
        for (int i = 0; i < 10; i++)
        {
            snapshots.Add(new TelemetrySnapshot
            {
                Timestamp = now.AddSeconds(-20 + i * 2),
                BatteryPercent = 90 - i,
                Altitude = 60 - i,
                Speed = 10,
                Heading = 180,
                GpsSatellites = 8,
                GpsHdop = 1.0,
                FlightMode = FlightMode.AUTO,
                VibrationX = 5, VibrationY = 5, VibrationZ = 10, WindSpeed = 3
            });
        }

        var summary = TelemetryAggregator.Summarize(snapshots);

        summary.AltitudeTrend.Should().Be(Trend.Decreasing);
    }

    public void Dispose()
    {
    }

    private class FakeTelemetryBufferProvider : ITelemetryBufferProvider
    {
        private readonly List<TelemetrySnapshot> _snapshots;

        public FakeTelemetryBufferProvider(IEnumerable<TelemetrySnapshot> snapshots)
        {
            _snapshots = new List<TelemetrySnapshot>(snapshots);
        }

        public IEnumerable<TelemetrySnapshot> GetSnapshots(int lastSeconds)
        {
            return _snapshots;
        }
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

        public Task<LLMResult> GenerateAsync(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default)
        {
            var json = _analysisToReturn?.ToString() ?? string.Empty;
            return Task.FromResult(LLMResult.Ok(json, "FakeLLM"));
        }

        public Task<T?> GenerateStructuredAsync<T>(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default) where T : class
        {
            if (_analysisToReturn is T t)
                return Task.FromResult<T?>(t);
            if (typeof(T) == typeof(TelemetryAnalysis))
                return Task.FromResult<T?>(_analysisToReturn as T);
            return Task.FromResult<T?>(null);
        }

        public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public LLMHealthStatus GetHealthStatus() => new LLMHealthStatus { IsConnected = true };
    }
}
