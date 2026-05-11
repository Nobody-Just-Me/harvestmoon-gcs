#if !__WASM__
using System;
using System.Collections.Generic;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Thread-safe rolling buffer for storing telemetry snapshots within a time window.
/// Automatically evicts expired snapshots and enforces minimum time between snapshots.
/// </summary>
public class TelemetryBuffer
{
    private readonly Queue<TelemetrySnapshot> _buffer = new();
    private readonly object _lock = new();
    private readonly TimeSpan _windowDuration;
    private readonly int _maxSnapshots;
    private DateTime _lastAddedTimestamp = DateTime.MinValue;
    // 50Hz compatible minimum spacing (20ms).
    private const double MinTimeBetweenSnapshotsSeconds = 0.02;

    /// <summary>
    /// Creates a new TelemetryBuffer with the specified retention window.
    /// </summary>
    /// <param name="windowMinutes">How long to retain snapshots (default: 5 minutes)</param>
    /// <param name="maxSnapshots">Maximum number of snapshots retained (default: 360)</param>
    public TelemetryBuffer(int windowMinutes = 5, int maxSnapshots = 360)
    {
        _windowDuration = TimeSpan.FromMinutes(windowMinutes);
        _maxSnapshots = Math.Max(1, maxSnapshots);
    }

    /// <summary>
    /// Gets the number of snapshots currently in the buffer (after expired eviction).
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                EvictExpiredLocked();
                return _buffer.Count;
            }
        }
    }

    /// <summary>
    /// Adds a snapshot to the buffer if it meets the minimum time interval requirement.
    /// Expired snapshots are evicted before adding.
    /// </summary>
    /// <param name="snapshot">The telemetry snapshot to add</param>
    public void Add(TelemetrySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        lock (_lock)
        {
            var timeSinceLast = (snapshot.Timestamp - _lastAddedTimestamp).TotalSeconds;
            if (_lastAddedTimestamp != DateTime.MinValue && Math.Abs(timeSinceLast) < MinTimeBetweenSnapshotsSeconds)
            {
                return;
            }

            if (snapshot.Timestamp > _lastAddedTimestamp)
            {
                _lastAddedTimestamp = snapshot.Timestamp;
            }

            EvictExpiredLocked();
            _buffer.Enqueue(snapshot);

            while (_buffer.Count > _maxSnapshots)
            {
                _buffer.Dequeue();
            }
        }
    }

    /// <summary>
    /// Returns all snapshots currently in the buffer.
    /// </summary>
    /// <returns>List of all non-expired snapshots</returns>
    public List<TelemetrySnapshot> GetAll()
    {
        lock (_lock)
        {
            EvictExpiredLocked();
            return new List<TelemetrySnapshot>(_buffer);
        }
    }

    /// <summary>
    /// Returns the most recent snapshot in the buffer, or null if empty.
    /// </summary>
    /// <returns>The latest snapshot or null</returns>
    public TelemetrySnapshot? GetLatest()
    {
        lock (_lock)
        {
            EvictExpiredLocked();
            if (_buffer.Count == 0)
                return null;

            TelemetrySnapshot? last = null;
            foreach (var snapshot in _buffer)
            {
                last = snapshot;
            }
            return last;
        }
    }

    /// <summary>
    /// Returns snapshots within the specified time window from now.
    /// </summary>
    /// <param name="window">Time window to retrieve snapshots for</param>
    /// <returns>List of snapshots within the window</returns>
    public List<TelemetrySnapshot> GetWindow(TimeSpan window)
    {
        lock (_lock)
        {
            EvictExpiredLocked();
            var cutoff = DateTime.UtcNow - window;
            var result = new List<TelemetrySnapshot>();

            foreach (var snapshot in _buffer)
            {
                if (snapshot.Timestamp >= cutoff)
                {
                    result.Add(snapshot);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Clears all snapshots from the buffer and resets the timestamp tracker.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _lastAddedTimestamp = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Removes snapshots older than the configured window duration.
    /// Must be called while holding the lock.
    /// </summary>
    private void EvictExpiredLocked()
    {
        var cutoff = DateTime.UtcNow - _windowDuration;

        while (_buffer.Count > 0)
        {
            var oldest = _buffer.Peek();
            if (oldest.Timestamp < cutoff)
            {
                _buffer.Dequeue();
            }
            else
            {
                break;
            }
        }
    }
}
#endif
