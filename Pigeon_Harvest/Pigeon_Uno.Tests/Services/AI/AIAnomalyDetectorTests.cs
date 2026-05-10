using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Services.AI;
using Xunit;
using TelemetrySnapshot = Pigeon_Uno.Core.Services.AI.TelemetrySnapshot;

namespace Pigeon_Uno.Tests.Services.AI
{
    public class AIAnomalyDetectorTests
    {
        [Fact]
        public async Task DetectAsync_returns_anomalies_from_llm()
        {
            var llmMock = new Mock<ILLMService>();
            llmMock.Setup(m => m.GenerateStructuredAsync<List<Anomaly>>(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<System.Threading.CancellationToken>()))
                   .ReturnsAsync(new List<Anomaly>
                   {
                       new Anomaly { Type = AnomalyType.StatisticalOutlier, Severity = AnomalySeverity.Warning, Message = "Test" }
                   });

            var settings = new AISettings { Enabled = true };
            var detector = new AIAnomalyDetector(llmMock.Object, settings);

            var snap = new TelemetrySnapshot { Timestamp = System.DateTime.UtcNow };
            var anomalies = await detector.DetectAsync(snap);

            anomalies.Should().NotBeNull();
            anomalies.Should().ContainSingle();
        }

        [Fact]
        public async Task DetectAsync_fallsback_to_empty_on_failure()
        {
            var llmMock = new Mock<ILLMService>();
            llmMock.Setup(m => m.GenerateStructuredAsync<List<Anomaly>>(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<System.Threading.CancellationToken>()))
                   .ThrowsAsync(new System.Exception("LLM failed"));

            var settings = new AISettings { Enabled = true };
            var detector = new AIAnomalyDetector(llmMock.Object, settings);
            var snap = new TelemetrySnapshot { Timestamp = System.DateTime.UtcNow };
            var anomalies = await detector.DetectAsync(snap);
            anomalies.Should().BeEmpty();
        }
    }
}
