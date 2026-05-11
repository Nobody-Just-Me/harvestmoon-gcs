using FluentAssertions;
using HarvestmoonGCS.Helpers;
using Xunit;
using System;

namespace HarvestmoonGCS.Tests;

public class GeoMathTests
{
    [Fact]
    public void Distance_ShouldBeCorrect()
    {
        // 1 deg lat ~ 111km
        double dist = GeoMath.Distance(0, 0, 1, 0);
        dist.Should().BeApproximately(111194, 200); 
    }

    [Fact]
    public void Bearing_ShouldBeNorth()
    {
        double bearing = GeoMath.Bearing(0, 0, 1, 0);
        bearing.Should().BeApproximately(0, 0.1);
    }

    [Fact]
    public void Bearing_ShouldBeEast()
    {
        double bearing = GeoMath.Bearing(0, 0, 0, 1);
        bearing.Should().BeApproximately(90, 0.1);
    }
    
    [Fact]
    public void Pitch_ShouldBe45Deg()
    {
        // Distance 100m, Height diff 100m -> 45 deg
        double pitch = GeoMath.Pitch(100, 0, 100);
        pitch.Should().BeApproximately(45, 0.1);
    }
}
