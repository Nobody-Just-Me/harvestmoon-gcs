using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Centralized diagnostic logger for MAVLink telemetry pipeline.
    /// Logs every stage of data flow from transport to UI.
    /// </summary>
    public interface IDiagnosticLogger
    {
        void LogTransportData(byte[] data, int length);
        void LogWalkerProcessing(int bytesProcessed, bool success);
        void LogPacketParsed(int messageId, int sequenceNumber, byte systemId);
        void LogFlightDataUpdate(string fieldName, object oldValue, object newValue);
        void LogTelemetryEvent(DateTime timestamp, string summary);
        void LogUIUpdate(string controlName, string propertyName, object value);
        void SetEnabled(bool enabled);
        string GetLogSummary();
        List<LogEntry> GetRecentLogs(int count = 100);
    }

    public class DiagnosticLogger : IDiagnosticLogger
    {
        private readonly List<LogEntry> _logs = new();
        private readonly object _lock = new();
        private bool _enabled = true;
        private const int MAX_LOG_ENTRIES = 10000;

        public void LogTransportData(byte[] data, int length)
        {
            if (!_enabled) return;

            var hex = BitConverter.ToString(data, 0, Math.Min(16, length)).Replace("-", " ");
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Transport",
                Message = $"Received {length} bytes: {hex}..."
            };

            AddLog(entry);
        }

        public void LogWalkerProcessing(int bytesProcessed, bool success)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Walker",
                Message = $"Processed {bytesProcessed} bytes: {(success ? "SUCCESS" : "FAILED")}"
            };

            AddLog(entry);
        }

        public void LogPacketParsed(int messageId, int sequenceNumber, byte systemId)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Parser",
                Message = $"Packet parsed - ID={messageId}, Seq={sequenceNumber}, SysID={systemId}"
            };

            AddLog(entry);
        }

        public void LogFlightDataUpdate(string fieldName, object oldValue, object newValue)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "FlightData",
                Message = $"{fieldName}: {oldValue} → {newValue}"
            };

            AddLog(entry);
        }

        public void LogTelemetryEvent(DateTime timestamp, string summary)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = timestamp,
                Stage = "TelemetryEvent",
                Message = summary
            };

            AddLog(entry);
        }

        public void LogUIUpdate(string controlName, string propertyName, object value)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "UI",
                Message = $"{controlName}.{propertyName} = {value}"
            };

            AddLog(entry);
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (enabled)
            {
                System.Diagnostics.Debug.WriteLine("[DiagnosticLogger] Logging ENABLED");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DiagnosticLogger] Logging DISABLED");
            }
        }

        public string GetLogSummary()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== Diagnostic Log Summary ===");
                sb.AppendLine($"Total Entries: {_logs.Count}");
                sb.AppendLine($"Logging Enabled: {_enabled}");
                sb.AppendLine();

                var byStage = _logs.GroupBy(l => l.Stage)
                    .Select(g => new { Stage = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count);

                sb.AppendLine("Entries by Stage:");
                foreach (var stage in byStage)
                {
                    sb.AppendLine($"  {stage.Stage}: {stage.Count}");
                }

                return sb.ToString();
            }
        }

        public List<LogEntry> GetRecentLogs(int count = 100)
        {
            lock (_lock)
            {
                return _logs.TakeLast(count).ToList();
            }
        }

        private void AddLog(LogEntry entry)
        {
            lock (_lock)
            {
                _logs.Add(entry);
                if (_logs.Count > MAX_LOG_ENTRIES)
                {
                    _logs.RemoveAt(0);
                }
            }

            // Also write to debug output
            System.Diagnostics.Debug.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Stage}] {entry.Message}");
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Stage { get; set; } = "";
        public string Message { get; set; } = "";

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Stage}] {Message}";
        }
    }
}
