using System;
using System.Linq;
using FluentAssertions;
using HarvestmoonGCS.Core.Services.AI;
using Xunit;

namespace HarvestmoonGCS.Tests.Services.AI
{
    public class RunningStatsTests
    {
        [Fact]
        public void Empty_stats_has_zero_counts_and_variance()
        {
            var rs = new RunningStats();
            rs.Count.Should().Be(0);
            rs.VarianceSample.Should().Be(0);
            rs.VariancePopulation.Should().Be(0);
            rs.Mean.Should().BeApproximately(0, 1e-9);
            rs.Min.Should().Be(double.MaxValue);
            rs.Max.Should().Be(double.MinValue);
        }

        [Fact]
        public void Adding_values_updates_statistics_correctly()
        {
            var rs = new RunningStats();
            rs.Add(1);
            rs.Count.Should().Be(1);
            rs.Mean.Should().Be(1);
            rs.VarianceSample.Should().Be(0);
            rs.Min.Should().Be(1);
            rs.Max.Should().Be(1);

            rs.Add(3);
            rs.Count.Should().Be(2);
            rs.Mean.Should().Be(2);
            rs.VarianceSample.Should().BeApproximately(2.0, 1e-9);
            rs.Min.Should().Be(1);
            rs.Max.Should().Be(3);
        }

        [Fact]
        public void Merges_stats_produce_correct_combined_values()
        {
            var a = new RunningStats();
            a.Add(1); a.Add(2);
            var b = new RunningStats();
            b.Add(3); b.Add(4);

            a.Merge(b);

            a.Count.Should().Be(4);
            a.Mean.Should().BeApproximately(2.5, 1e-9);
            a.VarianceSample.Should().BeApproximately(1.6666666667, 1e-7);
            a.Min.Should().Be(1);
            a.Max.Should().Be(4);
        }
    }
}
