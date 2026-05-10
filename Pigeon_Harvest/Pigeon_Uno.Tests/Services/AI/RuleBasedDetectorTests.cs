using Xunit;
using Pigeon_Uno.Core.Services.AI;
using Pigeon_Uno.Core.Models.AI;
using Pigeon_Uno.Core.Models;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Pigeon_Uno.Tests.Services.AI;

public class RuleBasedDetectorTests
{
    private readonly AnomalyThresholds _defaultThresholds;
    private readonly RuleBasedDetector _detector;

    public RuleBasedDetectorTests()
    {
        _defaultThresholds = new AnomalyThresholds
        {
            BatteryCritical = 20,
            BatteryWarning = 30,
            GpsMinSatellites = 6,
            GpsLostThreshold = 5,
            GpsWarningThreshold = 7,
            AltitudeCritical = 120,
            HighSpeedThreshold = 15,
            RapidDescentThreshold = -3,
            HighVibrationThreshold = 30,
            LowBatteryDrainMinutes = 5,
            VibrationHigh = 60,
            VibrationCritical = 100,
            WindMaxSpeed = 10
        };
        _detector = new RuleBasedDetector(_defaultThresholds);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullThresholds_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleBasedDetector(null!));
    }

    [Fact]
    public void Constructor_WithValidThresholds_SetsThresholds()
    {
        var detector = new RuleBasedDetector(_defaultThresholds);
        Assert.NotNull(detector);
    }

    #endregion

    #region Battery Tests

    [Fact]
    public void Evaluate_BatteryPercentBelowCritical_ReturnsBatteryCriticalAnomaly()
    {
        var snapshot = CreateSnapshot(batteryPercent: 15);

        var anomalies = _detector.Evaluate(snapshot);

        var batteryCritical = anomalies.FirstOrDefault(a => a.Type == AnomalyType.BatteryCritical);
        Assert.NotNull(batteryCritical);
        Assert.Equal(AnomalySeverity.Critical, batteryCritical.Severity);
        Assert.Contains("15", batteryCritical.Message);
    }

    [Fact]
    public void Evaluate_BatteryPercentAtCriticalThreshold_DoesNotReturnCritical()
    {
        var snapshot = CreateSnapshot(batteryPercent: 20);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.BatteryCritical);
    }

    [Fact]
    public void Evaluate_BatteryPercentBelowWarning_ReturnsBatteryWarningAnomaly()
    {
        var snapshot = CreateSnapshot(batteryPercent: 25);

        var anomalies = _detector.Evaluate(snapshot);

        var batteryWarning = anomalies.FirstOrDefault(a => a.Type == AnomalyType.BatteryWarning);
        Assert.NotNull(batteryWarning);
        Assert.Equal(AnomalySeverity.Warning, batteryWarning.Severity);
    }

    [Fact]
    public void Evaluate_BatteryPercentAtWarningThreshold_DoesNotReturnWarning()
    {
        var snapshot = CreateSnapshot(batteryPercent: 30);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.BatteryWarning);
    }

    [Fact]
    public void Evaluate_BatteryCriticalTakesPrecedenceOverWarning()
    {
        var snapshot = CreateSnapshot(batteryPercent: 15);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.Contains(anomalies, a => a.Type == AnomalyType.BatteryCritical);
        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.BatteryWarning);
    }

    #endregion

    #region GPS Tests

    [Fact]
    public void Evaluate_GpsSatellitesBelowLostThreshold_ReturnsGpsLostAnomaly()
    {
        var snapshot = CreateSnapshot(gpsSatellites: 4);

        var anomalies = _detector.Evaluate(snapshot);

        var gpsLost = anomalies.FirstOrDefault(a => a.Type == AnomalyType.GpsLost);
        Assert.NotNull(gpsLost);
        Assert.Equal(AnomalySeverity.Critical, gpsLost.Severity);
        Assert.Contains("4", gpsLost.Message);
    }

    [Fact]
    public void Evaluate_GpsSatellitesAtLostThreshold_DoesNotReturnGpsLost()
    {
        var snapshot = CreateSnapshot(gpsSatellites: 5);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.GpsLost);
    }

    [Fact]
    public void Evaluate_GpsSatellitesBelowWarningThreshold_ReturnsGpsDegradedAnomaly()
    {
        var snapshot = CreateSnapshot(gpsSatellites: 6);

        var anomalies = _detector.Evaluate(snapshot);

        var gpsDegraded = anomalies.FirstOrDefault(a => a.Type == AnomalyType.GpsDegraded);
        Assert.NotNull(gpsDegraded);
        Assert.Equal(AnomalySeverity.Warning, gpsDegraded.Severity);
    }

    [Fact]
    public void Evaluate_GpsSatellitesAtWarningThreshold_DoesNotReturnGpsDegraded()
    {
        var snapshot = CreateSnapshot(gpsSatellites: 7);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.GpsDegraded);
    }

    [Fact]
    public void Evaluate_GpsLostTakesPrecedenceOverDegraded()
    {
        var snapshot = CreateSnapshot(gpsSatellites: 4);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.Contains(anomalies, a => a.Type == AnomalyType.GpsLost);
        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.GpsDegraded);
    }

    #endregion

    #region Altitude Tests

    [Fact]
    public void Evaluate_AltitudeAboveCritical_ReturnsAltitudeCriticalAnomaly()
    {
        var snapshot = CreateSnapshot(altitude: 130);

        var anomalies = _detector.Evaluate(snapshot);

        var altitudeAnomaly = anomalies.FirstOrDefault(a => a.Message.Contains("altitude", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(altitudeAnomaly);
        Assert.Equal(AnomalySeverity.Critical, altitudeAnomaly.Severity);
    }

    [Fact]
    public void Evaluate_AltitudeAtCriticalThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(altitude: 120);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("altitude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_AltitudeBelowCritical_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(altitude: 100);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("altitude", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Vibration Tests

    [Fact]
    public void Evaluate_VibrationSumAboveThreshold_ReturnsVibrationHighAnomaly()
    {
        var snapshot = CreateSnapshot(vibrationX: 15, vibrationY: 10, vibrationZ: 10);

        var anomalies = _detector.Evaluate(snapshot);

        var vibrationAnomaly = anomalies.FirstOrDefault(a => a.Type == AnomalyType.VibrationHigh);
        Assert.NotNull(vibrationAnomaly);
        Assert.Equal(AnomalySeverity.Warning, vibrationAnomaly.Severity);
    }

    [Fact]
    public void Evaluate_VibrationSumAtThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(vibrationX: 10, vibrationY: 10, vibrationZ: 10);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.VibrationHigh);
    }

    [Fact]
    public void Evaluate_VibrationSumBelowThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(vibrationX: 5, vibrationY: 5, vibrationZ: 5);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Type == AnomalyType.VibrationHigh);
    }

    #endregion

    #region Speed Tests

    [Fact]
    public void Evaluate_SpeedAboveThreshold_ReturnsHighSpeedAnomaly()
    {
        var snapshot = CreateSnapshot(speed: 20);

        var anomalies = _detector.Evaluate(snapshot);

        var speedAnomaly = anomalies.FirstOrDefault(a => a.Message.Contains("speed", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(speedAnomaly);
        Assert.Equal(AnomalySeverity.Warning, speedAnomaly.Severity);
    }

    [Fact]
    public void Evaluate_SpeedAtThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(speed: 15);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("speed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_SpeedBelowThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(speed: 10);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("speed", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Rapid Descent Tests

    [Fact]
    public void Evaluate_VerticalSpeedBelowThreshold_ReturnsRapidDescentAnomaly()
    {
        var snapshot = CreateSnapshot(verticalSpeed: -5);

        var anomalies = _detector.Evaluate(snapshot);

        var descentAnomaly = anomalies.FirstOrDefault(a => a.Message.Contains("descent", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(descentAnomaly);
        Assert.Equal(AnomalySeverity.Warning, descentAnomaly.Severity);
    }

    [Fact]
    public void Evaluate_VerticalSpeedAtThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(verticalSpeed: -3);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("descent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_VerticalSpeedAboveThreshold_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(verticalSpeed: -1);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("descent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_PositiveVerticalSpeed_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(verticalSpeed: 2);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("descent", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Battery Drain Tests

    [Fact]
    public void Evaluate_BatteryDrainRateIndicatingDepletionIn5Min_ReturnsLowBatteryDrainAnomaly()
    {
        // 20% battery, draining at 4% per minute = 5 minutes until empty
        var snapshot = CreateSnapshot(batteryPercent: 20, batteryDrainRate: 4);

        var anomalies = _detector.Evaluate(snapshot);

        var drainAnomaly = anomalies.FirstOrDefault(a => a.Message.Contains("drain", StringComparison.OrdinalIgnoreCase) || 
                                                          a.Message.Contains("depletion", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(drainAnomaly);
    }

    [Fact]
    public void Evaluate_BatteryDrainRateIndicatingDepletionInLessThan5Min_ReturnsLowBatteryDrainAnomaly()
    {
        // 20% battery, draining at 5% per minute = 4 minutes until empty
        var snapshot = CreateSnapshot(batteryPercent: 20, batteryDrainRate: 5);

        var anomalies = _detector.Evaluate(snapshot);

        var drainAnomaly = anomalies.FirstOrDefault(a => a.Message.Contains("drain", StringComparison.OrdinalIgnoreCase) || 
                                                          a.Message.Contains("depletion", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(drainAnomaly);
    }

    [Fact]
    public void Evaluate_BatteryDrainRateIndicatingDepletionInMoreThan5Min_DoesNotReturnAnomaly()
    {
        // 20% battery, draining at 3% per minute = ~6.7 minutes until empty
        var snapshot = CreateSnapshot(batteryPercent: 20, batteryDrainRate: 3);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("drain", StringComparison.OrdinalIgnoreCase) || 
                                               a.Message.Contains("depletion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_NullBatteryDrainRate_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(batteryPercent: 20, batteryDrainRate: null);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("drain", StringComparison.OrdinalIgnoreCase) || 
                                               a.Message.Contains("depletion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_ZeroBatteryDrainRate_DoesNotReturnAnomaly()
    {
        var snapshot = CreateSnapshot(batteryPercent: 20, batteryDrainRate: 0);

        var anomalies = _detector.Evaluate(snapshot);

        Assert.DoesNotContain(anomalies, a => a.Message.Contains("drain", StringComparison.OrdinalIgnoreCase) || 
                                               a.Message.Contains("depletion", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Evaluate_WhenAnomalyDetected_EventIsFired()
    {
        var detectedAnomalies = new List<Anomaly>();
        _detector.AnomalyDetected += (sender, anomaly) => detectedAnomalies.Add(anomaly);

        var snapshot = CreateSnapshot(batteryPercent: 15);
        _detector.Evaluate(snapshot);

        Assert.Single(detectedAnomalies);
        Assert.Equal(AnomalyType.BatteryCritical, detectedAnomalies[0].Type);
    }

    [Fact]
    public void Evaluate_WhenMultipleAnomaliesDetected_EventFiredForEach()
    {
        var detectedAnomalies = new List<Anomaly>();
        _detector.AnomalyDetected += (sender, anomaly) => detectedAnomalies.Add(anomaly);

        var snapshot = CreateSnapshot(batteryPercent: 15, gpsSatellites: 4, altitude: 130);
        _detector.Evaluate(snapshot);

        Assert.Equal(3, detectedAnomalies.Count);
    }

    [Fact]
    public void Evaluate_WhenNoAnomaliesDetected_EventIsNotFired()
    {
        var eventFired = false;
        _detector.AnomalyDetected += (sender, anomaly) => eventFired = true;

        var snapshot = CreateSnapshot(batteryPercent: 50, gpsSatellites: 10);
        _detector.Evaluate(snapshot);

        Assert.False(eventFired);
    }

    #endregion

    #region Multiple Anomalies Tests

    [Fact]
    public void Evaluate_MultipleConditions_ReturnsAllRelevantAnomalies()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 15,
            gpsSatellites: 4,
            altitude: 130,
            speed: 20,
            verticalSpeed: -5,
            vibrationX: 15, vibrationY: 10, vibrationZ: 10
        );

        var anomalies = _detector.Evaluate(snapshot);

        Assert.Contains(anomalies, a => a.Type == AnomalyType.BatteryCritical);
        Assert.Contains(anomalies, a => a.Type == AnomalyType.GpsLost);
        Assert.Contains(anomalies, a => a.Type == AnomalyType.VibrationHigh);
    }

    [Fact]
    public void Evaluate_NormalConditions_ReturnsEmptyList()
    {
        var snapshot = CreateSnapshot(
            batteryPercent: 50,
            gpsSatellites: 10,
            altitude: 50,
            speed: 5,
            verticalSpeed: 0,
            vibrationX: 5, vibrationY: 5, vibrationZ: 5
        );

        var anomalies = _detector.Evaluate(snapshot);

        Assert.Empty(anomalies);
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public void Evaluate_AnomalyHasCorrectTimestamp()
    {
        var beforeEval = DateTime.UtcNow;
        var snapshot = CreateSnapshot(batteryPercent: 15);
        
        var anomalies = _detector.Evaluate(snapshot);
        
        var afterEval = DateTime.UtcNow;
        var anomaly = anomalies.First();

        Assert.True(anomaly.Timestamp >= beforeEval);
        Assert.True(anomaly.Timestamp <= afterEval);
    }

    #endregion

    #region Priority Tests

    [Fact]
    public void Evaluate_CriticalAnomalyHasHigherPriorityThanWarning()
    {
        var criticalSnapshot = CreateSnapshot(batteryPercent: 15);
        var warningSnapshot = CreateSnapshot(batteryPercent: 25);

        var criticalAnomalies = _detector.Evaluate(criticalSnapshot);
        var warningAnomalies = _detector.Evaluate(warningSnapshot);

        var critical = criticalAnomalies.First(a => a.Type == AnomalyType.BatteryCritical);
        var warning = warningAnomalies.First(a => a.Type == AnomalyType.BatteryWarning);

        Assert.True(critical.Priority > warning.Priority);
    }

    #endregion

    #region Helper Methods

    private TelemetrySnapshot CreateSnapshot(
        double batteryPercent = 50,
        int gpsSatellites = 10,
        double altitude = 50,
        double speed = 5,
        double verticalSpeed = 0,
        double vibrationX = 5,
        double vibrationY = 5,
        double vibrationZ = 5,
        double? batteryDrainRate = null)
    {
        return new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            BatteryVoltage = 12.6,
            BatteryPercent = batteryPercent,
            GpsLatitude = -6.2088,
            GpsLongitude = 106.8456,
            GpsAltitude = 50,
            GpsSatellites = gpsSatellites,
            GpsHdop = 1.2,
            Altitude = altitude,
            Speed = speed,
            VerticalSpeed = verticalSpeed,
            Heading = 0,
            Roll = 0,
            Pitch = 0,
            Yaw = 0,
            FlightMode = FlightMode.STABILIZER,
            Armed = true,
            VibrationX = vibrationX,
            VibrationY = vibrationY,
            VibrationZ = vibrationZ,
            WindSpeed = 0,
            WindDirection = 0,
            BatteryDrainRate = batteryDrainRate
        };
    }

    #endregion
}
