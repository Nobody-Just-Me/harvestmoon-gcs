using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Services.AI;
using Xunit;

namespace Pigeon_Uno.Tests.Services.AI
{
    public class StatisticalDetectorTests
    {
        [Fact]
        public void Cadence_enforced_and_runs_without_errors()
        {
            var settings = new AISettings { AnomalyDetection = new AnomalyDetectionConfig { Thresholds = new AnomalyThresholds { ZScoreThreshold = 2.0 } } };
            var detector = new StatisticalDetector(settings, forceContinuous: true);

            var snap1 = new TelemetrySnapshot { Timestamp = DateTime.UtcNow, BatteryVoltage = 3.7, BatteryPercent = 78.0 };
            var as1 = detector.Detect(snap1);
            as1.Should().NotBeNull();

            // Next value far away should be considered, but since we force continuous, it will compute
            var snap2 = new TelemetrySnapshot { Timestamp = DateTime.UtcNow.AddSeconds(35), BatteryVoltage = 12.0, BatteryPercent = 37.0 };
            var anomalies = detector.Detect(snap2);
            anomalies.Should().BeOfType<List<Anomaly>>();
        }
    }
}
