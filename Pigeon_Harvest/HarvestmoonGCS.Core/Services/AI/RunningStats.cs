using System;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// numerically stable running statistics using Welford's algorithm.
/// Keeps track of count, mean, M2 (for variance), min and max.
/// </summary>
public class RunningStats
{
    public long Count { get; private set; }
    public double Mean { get; private set; }
    public double M2 { get; private set; }
    public double Min { get; private set; }
    public double Max { get; private set; }

    public RunningStats()
    {
        Count = 0;
        Mean = 0.0;
        M2 = 0.0;
        Min = double.MaxValue;
        Max = double.MinValue;
    }

    /// <summary>
    /// Add a new value to the running statistics.
    /// </summary>
    public void Add(double x)
    {
        Count++;
        // Welford's method
        double delta = x - Mean;
        Mean += delta / Count;
        M2 += delta * (x - Mean);
        Min = Math.Min(Min, x);
        Max = Math.Max(Max, x);
    }

    /// <summary>
    /// Merge another RunningStats into this one.
    /// </summary>
    public void Merge(RunningStats other)
    {
        if (other == null || other.Count == 0)
            return;

        if (Count == 0)
        {
            Count = other.Count;
            Mean = other.Mean;
            M2 = other.M2;
            Min = other.Min;
            Max = other.Max;
            return;
        }

        // Combined statistics
        long total = Count + other.Count;
        double delta = other.Mean - Mean;
        Mean = (Mean * Count + other.Mean * other.Count) / total;
        M2 = M2 + other.M2 + delta * delta * Count * other.Count / (double)total;
        Count = total;
        Min = Math.Min(Min, other.Min);
        Max = Math.Max(Max, other.Max);
    }

    /// <summary>
    /// Population variance. If less than 2 values, returns 0.
    /// </summary>
    public double VariancePopulation => Count > 0 ? M2 / Count : 0.0;

    /// <summary>
    /// Sample variance. If less than 2 values, returns 0.
    /// </summary>
    public double VarianceSample => Count > 1 ? M2 / (Count - 1) : 0.0;

    /// <summary>
    /// Standard deviation (sample).
    /// </summary>
    public double StdDev => Math.Sqrt(VarianceSample);
}
