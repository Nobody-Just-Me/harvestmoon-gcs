using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HarvestmoonGCS.Services;

public sealed class IncidentTimelineService
{
    private readonly object _lock = new();
    private readonly List<IncidentTimelineEntry> _entries = new();

    public event EventHandler? TimelineChanged;

    public IReadOnlyList<IncidentTimelineEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public void Add(string type, string message, string severity = "info")
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_lock)
        {
            _entries.Insert(0, new IncidentTimelineEntry
            {
                Timestamp = DateTime.Now,
                Type = string.IsNullOrWhiteSpace(type) ? "event" : type,
                Message = message,
                Severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity
            });

            if (_entries.Count > 200)
            {
                _entries.RemoveRange(200, _entries.Count - 200);
            }
        }

        TimelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ToJson(int maxItems = 80)
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_entries.Take(maxItems).ToList(), new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public string ToPlainText(int maxItems = 12)
    {
        lock (_lock)
        {
            return _entries.Count == 0
                ? "No incident timeline events."
                : string.Join(Environment.NewLine, _entries.Take(maxItems).Select(e => $"{e.Timestamp:HH:mm:ss} [{e.Type}] {e.Message}"));
        }
    }
}

public sealed class IncidentTimelineEntry
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}
