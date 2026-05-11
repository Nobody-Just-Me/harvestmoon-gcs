using System;
using System.Collections.Generic;
using FluentAssertions;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI;

public class AnomalyEvaluationServiceTests
{
    [Fact]
    public void ObserveSnapshot_ComputesPrecisionRecallFromConfusionMatrix()
    {
        var settings = new AISettings();
        var service = new AnomalyEvaluationService(settings);
        var now = DateTime.UtcNow;

        // TP: predicted anomaly, actual anomaly (battery critical)
        service.ObserveSnapshot(
            CreateSnapshot(now, batteryPercent: 10, gpsSat: 10),
            new List<Anomaly> { CreateAnomaly(now, AnomalySeverity.Critical) });

        // FN: no predicted anomaly, actual anomaly (GPS lost)
        service.ObserveSnapshot(
            CreateSnapshot(now.AddSeconds(1), batteryPercent: 70, gpsSat: 3),
            Array.Empty<Anomaly>());

        // FP: predicted anomaly, no actual anomaly
        service.ObserveSnapshot(
            CreateSnapshot(now.AddSeconds(2), batteryPercent: 80, gpsSat: 10),
            new List<Anomaly> { CreateAnomaly(now.AddSeconds(2), AnomalySeverity.Warning) });

        var metrics = service.GetMetrics();

        metrics.TruePositive.Should().Be(1);
        metrics.FalseNegative.Should().Be(1);
        metrics.FalsePositive.Should().Be(1);
        metrics.SampleCount.Should().Be(3);
        metrics.Precision.Should().BeApproximately(0.5, 0.001);
        metrics.Recall.Should().BeApproximately(0.5, 0.001);
    }

    private static TelemetrySnapshot CreateSnapshot(DateTime timestamp, double batteryPercent, int gpsSat)
    {
        return new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryPercent = batteryPercent,
            BatteryVoltage = 15.0,
            GpsSatellites = gpsSat,
            GpsHdop = 0.9,
            Altitude = 50,
            Speed = 7,
            VerticalSpeed = -0.5,
            FlightMode = FlightMode.AUTO,
            Armed = true,
            VibrationX = 1,
            VibrationY = 1,
            VibrationZ = 1,
            WindSpeed = 2
        };
    }

    private static Anomaly CreateAnomaly(DateTime timestamp, AnomalySeverity severity)
    {
        return new Anomaly
        {
            Type = AnomalyType.BatteryWarning,
            Severity = severity,
            Message = "test",
            Timestamp = timestamp.ToUniversalTime()
        };
    }
}

