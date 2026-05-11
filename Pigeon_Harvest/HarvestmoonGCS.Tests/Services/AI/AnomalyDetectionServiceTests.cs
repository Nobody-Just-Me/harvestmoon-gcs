using Xunit;
using Moq;
using HarvestmoonGCS.Core.Services.AI;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests.Services.AI;

public class AnomalyDetectionServiceTests
{
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly Mock<IAnomalyDetector> _mockRuleBasedDetector;
    private readonly Mock<IAnomalyDetector> _mockStatisticalDetector;
    private readonly Mock<IAnomalyDetector> _mockAiDetector;
    private readonly AnomalyDetectionConfig _config;

    public AnomalyDetectionServiceTests()
    {
        _mockAlertManager = new Mock<IAlertManager>();
        _mockRuleBasedDetector = new Mock<IAnomalyDetector>();
        _mockStatisticalDetector = new Mock<IAnomalyDetector>();
        _mockAiDetector = new Mock<IAnomalyDetector>();
        _config = new AnomalyDetectionConfig
        {
            RuleBasedEnabled = true,
            StatisticalEnabled = true,
            AIEnabled = true
        };

        _mockRuleBasedDetector.Setup(d => d.Name).Returns("RuleBased");
        _mockStatisticalDetector.Setup(d => d.Name).Returns("Statistical");
        _mockAiDetector.Setup(d => d.Name).Returns("AI");
    }

    private AnomalyDetectionService CreateService(
        AnomalyDetectionConfig? config = null,
        IAnomalyDetector? ruleBased = null,
        IAnomalyDetector? statistical = null,
        IAnomalyDetector? ai = null)
    {
        return new AnomalyDetectionService(
            _mockAlertManager.Object,
            ruleBased ?? _mockRuleBasedDetector.Object,
            statistical,
            ai,
            config ?? _config);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullAlertManager_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnomalyDetectionService(
                null!,
                _mockRuleBasedDetector.Object,
                null,
                null,
                _config));
    }

    [Fact]
    public void Constructor_WithNullRuleBasedDetector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnomalyDetectionService(
                _mockAlertManager.Object,
                null!,
                null,
                null,
                _config));
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        var service = CreateService();
        Assert.NotNull(service);
        Assert.False(service.IsRunning);
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenNotRunning_SetsIsRunningTrue()
    {
        var service = CreateService();

        await service.StartAsync();

        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        var service = CreateService();
        await service.StartAsync();

        await service.StartAsync();

        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WithStatisticalDisabled_DoesNotStartLayer2Timer()
    {
        _config.StatisticalEnabled = false;
        var service = CreateService(config: _config, statistical: _mockStatisticalDetector.Object);

        await service.StartAsync();

        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WithAIDisabled_DoesNotStartLayer3Timer()
    {
        _config.AIEnabled = false;
        var service = CreateService(config: _config, ai: _mockAiDetector.Object);

        await service.StartAsync();

        Assert.True(service.IsRunning);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhenRunning_SetsIsRunningFalse()
    {
        var service = CreateService();
        await service.StartAsync();

        await service.StopAsync();

        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        var service = CreateService();

        await service.StopAsync();

        Assert.False(service.IsRunning);
    }

    #endregion

    #region ProcessSnapshotAsync Tests - Layer 1

    [Fact]
    public async Task ProcessSnapshotAsync_WhenNotRunning_DoesNotEvaluate()
    {
        var service = CreateService();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);

        _mockRuleBasedDetector.Verify(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WithNullSnapshot_DoesNotEvaluate()
    {
        var service = CreateService();
        await service.StartAsync();

        await service.ProcessSnapshotAsync(null!);

        _mockRuleBasedDetector.Verify(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WhenRuleBasedEnabled_EvaluatesSnapshot()
    {
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly>());

        var service = CreateService();
        await service.StartAsync();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);

        _mockRuleBasedDetector.Verify(d => d.Evaluate(snapshot), Times.Once);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WhenRuleBasedDisabled_DoesNotEvaluate()
    {
        _config.RuleBasedEnabled = false;
        var service = CreateService(config: _config);
        await service.StartAsync();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);

        _mockRuleBasedDetector.Verify(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WhenAnomaliesDetected_QueuesAlertsToAlertManager()
    {
        var anomalies = new List<Anomaly>
        {
            new()
            {
                Type = AnomalyType.BatteryCritical,
                Severity = AnomalySeverity.Critical,
                Message = "Battery critical: 10%"
            }
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(anomalies);

        var service = CreateService();
        await service.StartAsync();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);

        _mockAlertManager.Verify(
            a => a.QueueCustomAlertAsync("Battery critical: 10%", AlertPriority.Critical),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WhenAnomaliesDetected_RaisesAnomaliesDetectedEvent()
    {
        var anomalies = new List<Anomaly>
        {
            new()
            {
                Type = AnomalyType.BatteryWarning,
                Severity = AnomalySeverity.Warning,
                Message = "Battery low: 25%"
            }
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(anomalies);

        var service = CreateService();
        var eventRaised = false;
        AnomaliesDetectedEventArgs? eventArgs = null;

        service.AnomaliesDetected += (_, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        await service.StartAsync();
        await service.ProcessSnapshotAsync(CreateSnapshot());

        Assert.True(eventRaised);
        Assert.NotNull(eventArgs);
        Assert.Single(eventArgs!.Anomalies);
        Assert.Equal("Layer1-RuleBased", eventArgs.SourceLayer);
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public async Task ProcessSnapshotAsync_SameCriticalAnomalyWithin1Minute_Deduplicates()
    {
        var anomaly = new Anomaly
        {
            Type = AnomalyType.BatteryCritical,
            Severity = AnomalySeverity.Critical,
            Message = "Battery critical"
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly> { anomaly });

        var service = CreateService();
        await service.StartAsync();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);
        await service.ProcessSnapshotAsync(snapshot);

        _mockAlertManager.Verify(
            a => a.QueueCustomAlertAsync(It.IsAny<string>(), AlertPriority.Critical),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_SameWarningAnomalyWithin5Minutes_Deduplicates()
    {
        var anomaly = new Anomaly
        {
            Type = AnomalyType.BatteryWarning,
            Severity = AnomalySeverity.Warning,
            Message = "Battery warning"
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly> { anomaly });

        var service = CreateService();
        await service.StartAsync();
        var snapshot = CreateSnapshot();

        await service.ProcessSnapshotAsync(snapshot);
        await service.ProcessSnapshotAsync(snapshot);

        _mockAlertManager.Verify(
            a => a.QueueCustomAlertAsync(It.IsAny<string>(), AlertPriority.High),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_DifferentAnomalyTypes_DoesNotDeduplicate()
    {
        var callCount = 0;
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(() =>
            {
                callCount++;
                return new List<Anomaly>
                {
                    new()
                    {
                        Type = callCount == 1 ? AnomalyType.BatteryCritical : AnomalyType.GpsLost,
                        Severity = AnomalySeverity.Critical,
                        Message = $"Anomaly {callCount}"
                    }
                };
            });

        var service = CreateService();
        await service.StartAsync();

        await service.ProcessSnapshotAsync(CreateSnapshot());
        await service.ProcessSnapshotAsync(CreateSnapshot());

        _mockAlertManager.Verify(
            a => a.QueueCustomAlertAsync(It.IsAny<string>(), AlertPriority.Critical),
            Times.Exactly(2));
    }

    #endregion

    #region Severity to Priority Mapping Tests

    [Theory]
    [InlineData(AnomalySeverity.Critical, AlertPriority.Critical)]
    [InlineData(AnomalySeverity.Warning, AlertPriority.High)]
    [InlineData(AnomalySeverity.Info, AlertPriority.Normal)]
    public async Task ProcessSnapshotAsync_MapsSeverityToCorrectPriority(
        AnomalySeverity severity, AlertPriority expectedPriority)
    {
        var anomaly = new Anomaly
        {
            Type = AnomalyType.BatteryWarning,
            Severity = severity,
            Message = "Test message"
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly> { anomaly });

        var service = CreateService();
        await service.StartAsync();

        await service.ProcessSnapshotAsync(CreateSnapshot());

        _mockAlertManager.Verify(
            a => a.QueueCustomAlertAsync("Test message", expectedPriority),
            Times.Once);
    }

    #endregion

    #region LastDetectedAnomalies Tests

    [Fact]
    public async Task ProcessSnapshotAsync_UpdatesLastDetectedAnomalies()
    {
        var anomalies = new List<Anomaly>
        {
            new() { Type = AnomalyType.BatteryCritical, Severity = AnomalySeverity.Critical, Message = "Test" }
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(anomalies);

        var service = CreateService();
        await service.StartAsync();

        Assert.Empty(service.LastDetectedAnomalies);

        await service.ProcessSnapshotAsync(CreateSnapshot());

        Assert.Single(service.LastDetectedAnomalies);
        Assert.Equal(AnomalyType.BatteryCritical, service.LastDetectedAnomalies[0].Type);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_WhenAnomaliesDeduped_DoesNotUpdateLastDetected()
    {
        var anomaly = new Anomaly
        {
            Type = AnomalyType.BatteryCritical,
            Severity = AnomalySeverity.Critical,
            Message = "Test"
        };
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly> { anomaly });

        var service = CreateService();
        await service.StartAsync();

        await service.ProcessSnapshotAsync(CreateSnapshot());
        var firstAnomalies = service.LastDetectedAnomalies.ToList();

        await service.ProcessSnapshotAsync(CreateSnapshot());
        var secondAnomalies = service.LastDetectedAnomalies.ToList();

        Assert.Equal(firstAnomalies.Count, secondAnomalies.Count);
    }

    #endregion

    #region Buffer Management Tests

    [Fact]
    public async Task ProcessSnapshotAsync_AddsSnapshotsToBuffer()
    {
        _mockRuleBasedDetector
            .Setup(d => d.Evaluate(It.IsAny<TelemetrySnapshot>()))
            .Returns(new List<Anomaly>());

        var service = CreateService();
        await service.StartAsync();

        for (int i = 0; i < 10; i++)
        {
            await service.ProcessSnapshotAsync(CreateSnapshot());
        }

        Assert.True(service.IsRunning);
    }

    #endregion

    #region EvaluateBatch Tests (Layer 2 & 3 Interface Contract)

    [Fact]
    public async Task Service_WithStatisticalDetector_CallsEvaluateBatchAsync()
    {
        _mockStatisticalDetector
            .Setup(d => d.EvaluateBatchAsync(It.IsAny<IList<TelemetrySnapshot>>()))
            .ReturnsAsync(new List<Anomaly>());

        var service = CreateService(statistical: _mockStatisticalDetector.Object);
        await service.StartAsync();

        for (int i = 0; i < 5; i++)
        {
            await service.ProcessSnapshotAsync(CreateSnapshot());
        }

        Assert.True(service.IsRunning);
        _mockStatisticalDetector.Verify(d => d.EvaluateBatchAsync(It.IsAny<IList<TelemetrySnapshot>>()), Times.Never);
    }

    [Fact]
    public async Task Service_WithAIDetector_CallsEvaluateBatchAsync()
    {
        _mockAiDetector
            .Setup(d => d.EvaluateBatchAsync(It.IsAny<IList<TelemetrySnapshot>>()))
            .ReturnsAsync(new List<Anomaly>());

        var service = CreateService(ai: _mockAiDetector.Object);
        await service.StartAsync();

        for (int i = 0; i < 5; i++)
        {
            await service.ProcessSnapshotAsync(CreateSnapshot());
        }

        Assert.True(service.IsRunning);
        _mockAiDetector.Verify(d => d.EvaluateBatchAsync(It.IsAny<IList<TelemetrySnapshot>>()), Times.Never);
    }

    #endregion

    #region Helper Methods

    private static TelemetrySnapshot CreateSnapshot(
        double batteryPercent = 50,
        int gpsSatellites = 10,
        double altitude = 100)
    {
        return new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            BatteryPercent = batteryPercent,
            GpsSatellites = gpsSatellites,
            Altitude = altitude,
            Armed = true,
            FlightMode = FlightMode.STABILIZER
        };
    }

    #endregion
}
