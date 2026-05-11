using System;
using System.Collections.Generic;
using System.Linq;
using HarvestmoonGCS.Core.Diagnostics;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Simple diagnostic logger implementation
/// </summary>
internal class DiagnosticLogger : IDiagnosticLogger
{
    private readonly List<LogEntry> _logs = new List<LogEntry>();
    private readonly object _logLock = new object();
    private const int MaxLogEntries = 1000;
    private bool _enabled = true;
    
    public void LogTransportData(byte[] data, int length)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            var hex = BitConverter.ToString(data, 0, Math.Min(16, length)).Replace("-", " ");
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Transport",
                Message = $"Received {length} bytes: {hex}..."
            });
            TrimLogs();
        }
    }
    
    public void LogWalkerProcessing(int bytesProcessed, bool success)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Walker",
                Message = $"Processed {bytesProcessed} bytes: {(success ? "SUCCESS" : "FAILED")}"
            });
            TrimLogs();
        }
    }
    
    public void LogPacketParsed(int messageId, int sequenceNumber, byte systemId)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "Parser",
                Message = $"Packet parsed - ID={messageId}, Seq={sequenceNumber}, SysID={systemId}"
            });
            TrimLogs();
        }
    }
    
    public void LogFlightDataUpdate(string fieldName, object oldValue, object newValue)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "FlightData",
                Message = $"{fieldName}: {oldValue} → {newValue}"
            });
            TrimLogs();
        }
    }
    
    public void LogTelemetryEvent(DateTime timestamp, string summary)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = timestamp,
                Stage = "TelemetryEvent",
                Message = summary
            });
            TrimLogs();
        }
    }
    
    public void LogUIUpdate(string controlName, string propertyName, object value)
    {
        if (!_enabled) return;
        
        lock (_logLock)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Stage = "UI",
                Message = $"{controlName}.{propertyName} = {value}"
            });
            TrimLogs();
        }
    }
    
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }
    
    public string GetLogSummary()
    {
        lock (_logLock)
        {
            return string.Join(Environment.NewLine, _logs.Select(l => l.ToString()));
        }
    }
    
    public List<LogEntry> GetRecentLogs(int count = 100)
    {
        lock (_logLock)
        {
            return _logs.TakeLast(count).ToList();
        }
    }
    
    private void TrimLogs()
    {
        if (_logs.Count > MaxLogEntries)
        {
            _logs.RemoveRange(0, _logs.Count - MaxLogEntries);
        }
    }
}
