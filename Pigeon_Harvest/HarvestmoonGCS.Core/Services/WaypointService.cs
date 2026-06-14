using System.Globalization;
using System.Text;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Shared in-memory waypoint store for map surfaces and mission UI.
/// </summary>
public sealed class WaypointService : IWaypointService
{
    private readonly object _syncRoot = new();
    private readonly List<WaypointData> _waypoints = new();
    private int? _currentWaypointSequence;

    public event EventHandler? WaypointsChanged;

    public int WaypointCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _waypoints.Count;
            }
        }
    }

    public Task<List<WaypointData>> GetWaypointsAsync()
    {
        lock (_syncRoot)
        {
            return Task.FromResult(_waypoints
                .OrderBy(w => w.Sequence)
                .Select(CloneWaypoint)
                .ToList());
        }
    }

    public Task<WaypointData?> GetWaypointAsync(int sequence)
    {
        lock (_syncRoot)
        {
            var waypoint = _waypoints.FirstOrDefault(w => w.Sequence == sequence);
            return Task.FromResult(waypoint == null ? null : CloneWaypoint(waypoint));
        }
    }

    public Task AddWaypointAsync(WaypointData waypoint)
    {
        lock (_syncRoot)
        {
            var copy = CloneWaypoint(waypoint);
            if (copy.Sequence <= 0)
            {
                copy.Sequence = GetNextSequenceNoLock();
            }

            _waypoints.Add(copy);
            ApplyCurrentWaypointNoLock();
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public Task ReplaceWaypointsAsync(IEnumerable<WaypointData> waypoints)
    {
        lock (_syncRoot)
        {
            _waypoints.Clear();
            _waypoints.AddRange(waypoints
                .Where(IsRenderableWaypoint)
                .OrderBy(w => w.Sequence <= 0 ? int.MaxValue : w.Sequence)
                .Select(CloneWaypoint));

            AssignMissingSequencesNoLock();
            ApplyCurrentWaypointNoLock();
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public Task UpdateWaypointAsync(WaypointData waypoint)
    {
        lock (_syncRoot)
        {
            var index = _waypoints.FindIndex(w => w.Sequence == waypoint.Sequence);
            if (index >= 0)
            {
                _waypoints[index] = CloneWaypoint(waypoint);
            }
            else
            {
                _waypoints.Add(CloneWaypoint(waypoint));
            }

            AssignMissingSequencesNoLock();
            ApplyCurrentWaypointNoLock();
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public Task RemoveWaypointAsync(int sequence)
    {
        lock (_syncRoot)
        {
            _waypoints.RemoveAll(w => w.Sequence == sequence);
            if (_currentWaypointSequence == sequence)
            {
                _currentWaypointSequence = null;
            }

            ApplyCurrentWaypointNoLock();
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public Task ClearWaypointsAsync()
    {
        lock (_syncRoot)
        {
            _waypoints.Clear();
            _currentWaypointSequence = null;
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public Task<WaypointData?> GetCurrentWaypointAsync()
    {
        lock (_syncRoot)
        {
            var waypoint = _currentWaypointSequence.HasValue
                ? _waypoints.FirstOrDefault(w => w.Sequence == _currentWaypointSequence.Value)
                : _waypoints.FirstOrDefault(w => w.IsCurrent);

            return Task.FromResult(waypoint == null ? null : CloneWaypoint(waypoint));
        }
    }

    public Task SetCurrentWaypointAsync(int sequence)
    {
        lock (_syncRoot)
        {
            _currentWaypointSequence = sequence;
            ApplyCurrentWaypointNoLock();
        }

        RaiseWaypointsChanged();
        return Task.CompletedTask;
    }

    public async Task LoadWaypointsFromFileAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        await ReplaceWaypointsAsync(ParseWaypointLines(lines));
    }

    public async Task SaveWaypointsToFileAsync(string filePath)
    {
        List<WaypointData> snapshot;
        lock (_syncRoot)
        {
            snapshot = _waypoints
                .OrderBy(w => w.Sequence)
                .Select(CloneWaypoint)
                .ToList();
        }

        var builder = new StringBuilder();
        builder.AppendLine("QGC WPL 110");

        for (var i = 0; i < snapshot.Count; i++)
        {
            var wp = snapshot[i];
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                i,
                i == 0 ? 1 : 0,
                3,
                (int)wp.Command,
                wp.Param1,
                wp.Param2,
                wp.Param3,
                wp.Param4,
                wp.Latitude,
                wp.Longitude,
                wp.Altitude,
                1));
        }

        await File.WriteAllTextAsync(filePath, builder.ToString());
    }

    private static IEnumerable<WaypointData> ParseWaypointLines(IEnumerable<string> lines)
    {
        var lineList = lines.ToList();
        var isMissionPlannerFormat = lineList.Count > 0 && lineList[0].StartsWith("QGC WPL", StringComparison.OrdinalIgnoreCase);
        var waypoints = new List<WaypointData>();

        foreach (var line in lineList)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("QGC", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(new[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (isMissionPlannerFormat && parts.Length >= 11)
            {
                if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) &&
                    int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var command) &&
                    double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) &&
                    double.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) &&
                    double.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude))
                {
                    waypoints.Add(new WaypointData
                    {
                        Sequence = sequence,
                        Latitude = latitude,
                        Longitude = longitude,
                        Altitude = altitude,
                        Command = Enum.IsDefined(typeof(WaypointCommand), command)
                            ? (WaypointCommand)command
                            : WaypointCommand.Waypoint,
                        Param1 = TryParseDouble(parts, 4),
                        Param2 = TryParseDouble(parts, 5),
                        Param3 = TryParseDouble(parts, 6),
                        Param4 = TryParseDouble(parts, 7)
                    });
                }
            }
            else if (!isMissionPlannerFormat && parts.Length >= 3)
            {
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude))
                {
                    waypoints.Add(new WaypointData
                    {
                        Sequence = waypoints.Count + 1,
                        Latitude = latitude,
                        Longitude = longitude,
                        Altitude = altitude,
                        Command = WaypointCommand.Waypoint
                    });
                }
            }
        }

        return waypoints;
    }

    private static double TryParseDouble(string[] parts, int index)
    {
        return index < parts.Length &&
               double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private int GetNextSequenceNoLock()
    {
        return _waypoints.Count == 0 ? 1 : _waypoints.Max(w => w.Sequence) + 1;
    }

    private void AssignMissingSequencesNoLock()
    {
        var nextSequence = 1;
        var usedSequences = new HashSet<int>();
        foreach (var waypoint in _waypoints.OrderBy(w => w.Sequence <= 0 ? int.MaxValue : w.Sequence))
        {
            if (waypoint.Sequence > 0 && usedSequences.Add(waypoint.Sequence))
            {
                nextSequence = Math.Max(nextSequence, waypoint.Sequence + 1);
                continue;
            }

            while (usedSequences.Contains(nextSequence))
            {
                nextSequence++;
            }

            waypoint.Sequence = nextSequence;
            usedSequences.Add(nextSequence);
            nextSequence++;
        }
    }

    private void ApplyCurrentWaypointNoLock()
    {
        foreach (var waypoint in _waypoints)
        {
            waypoint.IsCurrent = _currentWaypointSequence.HasValue &&
                                 waypoint.Sequence == _currentWaypointSequence.Value;
        }
    }

    private static bool IsRenderableWaypoint(WaypointData waypoint)
    {
        return waypoint.Latitude >= -90 &&
               waypoint.Latitude <= 90 &&
               waypoint.Longitude >= -180 &&
               waypoint.Longitude <= 180 &&
               (Math.Abs(waypoint.Latitude) > 1e-9 || Math.Abs(waypoint.Longitude) > 1e-9);
    }

    private static WaypointData CloneWaypoint(WaypointData waypoint)
    {
        return new WaypointData
        {
            Sequence = waypoint.Sequence,
            Latitude = waypoint.Latitude,
            Longitude = waypoint.Longitude,
            Altitude = waypoint.Altitude,
            Command = waypoint.Command,
            Param1 = waypoint.Param1,
            Param2 = waypoint.Param2,
            Param3 = waypoint.Param3,
            Param4 = waypoint.Param4,
            IsCurrent = waypoint.IsCurrent
        };
    }

    private void RaiseWaypointsChanged()
    {
        WaypointsChanged?.Invoke(this, EventArgs.Empty);
    }
}
