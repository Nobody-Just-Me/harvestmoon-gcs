using System;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI;

public class TelemetrySamplerTests
{
    [Fact]
    public void ShouldSample_FirstSnapshot_ReturnsTrue()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var result = sampler.ShouldSample(CreateSnapshot(new DateTime(2026, 4, 21, 10, 0, 0)));

        Assert.True(result);
    }

    [Fact]
    public void ShouldSample_UnderMinIntervalWithoutChanges_ReturnsFalse()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0));

        var result = sampler.ShouldSample(CreateSnapshot(t0.AddMilliseconds(200)));

        Assert.False(result);
    }

    [Fact]
    public void ShouldSample_OverForceInterval_ReturnsTrue()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0));

        var result = sampler.ShouldSample(CreateSnapshot(t0.AddMilliseconds(2100)));

        Assert.True(result);
    }

    [Fact]
    public void ShouldSample_SignificantChangeAfterMinInterval_ReturnsTrue()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0, batteryPercent: 80));

        var result = sampler.ShouldSample(CreateSnapshot(
            t0.AddMilliseconds(600),
            batteryPercent: 78.8)); // delta 1.2%

        Assert.True(result);
    }

    [Fact]
    public void ShouldSample_FlightModeChange_ReturnsTrueEvenUnderMinInterval()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0, flightMode: FlightMode.STABILIZER));

        var result = sampler.ShouldSample(CreateSnapshot(
            t0.AddMilliseconds(100),
            flightMode: FlightMode.AUTO));

        Assert.True(result);
    }

    [Fact]
    public void Reset_ClearsState_NextSnapshotSampled()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0));
        sampler.Reset();

        var result = sampler.ShouldSample(CreateSnapshot(t0.AddMilliseconds(50)));

        Assert.True(result);
    }

    [Fact]
    public void ShouldSample_LinkQualitySignificantChange_ReturnsTrue()
    {
        var settings = new AISettings();
        var sampler = new TelemetrySampler(settings);

        var t0 = new DateTime(2026, 4, 21, 10, 0, 0);
        sampler.ShouldSample(CreateSnapshot(t0, linkQuality: 95));

        var result = sampler.ShouldSample(CreateSnapshot(
            t0.AddMilliseconds(700),
            linkQuality: 80));

        Assert.True(result);
    }

    private static TelemetrySnapshot CreateSnapshot(
        DateTime timestamp,
        double batteryPercent = 80,
        double altitude = 100,
        double speed = 10,
        double linkQuality = 90,
        FlightMode flightMode = FlightMode.AUTO)
    {
        return new TelemetrySnapshot
        {
            Timestamp = timestamp,
            BatteryPercent = batteryPercent,
            Altitude = altitude,
            Speed = speed,
            VerticalSpeed = 0,
            Heading = 90,
            GpsLatitude = -6.2,
            GpsLongitude = 106.8,
            LinkQualityPercent = linkQuality,
            FlightMode = flightMode,
            Armed = true
        };
    }
}
