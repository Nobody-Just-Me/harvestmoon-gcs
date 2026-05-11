using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace HarvestmoonGCS.Services;

public sealed class ObservabilityService
{
    private sealed class CounterState
    {
        public long Total;
        public long LastSnapshotTotal;
        public long LastSnapshotTicks;
        public double LastRate;
        public readonly object Sync = new();
    }

    public readonly struct CounterSnapshot
    {
        public string Key { get; }
        public long Total { get; }
        public double RatePerSecond { get; }

        public CounterSnapshot(string key, long total, double ratePerSecond)
        {
            Key = key;
            Total = total;
            RatePerSecond = ratePerSecond;
        }
    }

    private readonly ConcurrentDictionary<string, CounterState> _counters = new();

    public void Track(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var state = _counters.GetOrAdd(key, _ => new CounterState());
        Interlocked.Increment(ref state.Total);
    }

    public IReadOnlyDictionary<string, CounterSnapshot> GetSnapshot()
    {
        var result = new Dictionary<string, CounterSnapshot>(StringComparer.Ordinal);
        var nowTicks = Stopwatch.GetTimestamp();
        var frequency = Stopwatch.Frequency;

        foreach (var item in _counters)
        {
            var key = item.Key;
            var state = item.Value;

            lock (state.Sync)
            {
                var total = Interlocked.Read(ref state.Total);

                if (state.LastSnapshotTicks == 0)
                {
                    state.LastSnapshotTicks = nowTicks;
                    state.LastSnapshotTotal = total;
                    state.LastRate = 0;
                }
                else
                {
                    var deltaTicks = nowTicks - state.LastSnapshotTicks;
                    if (deltaTicks > 0)
                    {
                        var deltaSeconds = (double)deltaTicks / frequency;
                        var deltaTotal = total - state.LastSnapshotTotal;
                        state.LastRate = deltaTotal / deltaSeconds;
                        state.LastSnapshotTicks = nowTicks;
                        state.LastSnapshotTotal = total;
                    }
                }

                result[key] = new CounterSnapshot(key, total, state.LastRate);
            }
        }

        return result;
    }
}
