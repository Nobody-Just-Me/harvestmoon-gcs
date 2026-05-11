using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Traces events through the entire telemetry pipeline with correlation IDs.
    /// Allows tracking individual packets from transport reception through UI update.
    /// </summary>
    public interface IEventChainTracer
    {
        string StartTrace();
        void TraceTransportReceived(string correlationId, int byteCount);
        void TraceWalkerProcessed(string correlationId, int messageId);
        void TracePacketParsed(string correlationId, string messageType);
        void TraceFlightDataUpdated(string correlationId);
        void TraceTelemetryEvent(string correlationId);
        void TraceViewModelUpdated(string correlationId);
        void TraceUIUpdated(string correlationId);
        EventChainReport GetReport(string correlationId);
        List<string> GetActiveTraces();
    }

    public class EventChainTracer : IEventChainTracer
    {
        private readonly ConcurrentDictionary<string, EventChain> _chains = new();
        private readonly IDiagnosticLogger _logger;
        private const int MAX_CHAINS = 1000;

        public EventChainTracer(IDiagnosticLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string StartTrace()
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _chains[correlationId] = new EventChain { CorrelationId = correlationId };
            
            // Cleanup old traces if we have too many
            if (_chains.Count > MAX_CHAINS)
            {
                var oldestKey = _chains.OrderBy(kvp => kvp.Value.TransportReceived).First().Key;
                _chains.TryRemove(oldestKey, out _);
            }
            
            return correlationId;
        }

        public void TraceTransportReceived(string correlationId, int byteCount)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.TransportReceived = DateTime.Now;
                chain.ByteCount = byteCount;
                // Don't log to DiagnosticLogger here - just track the trace
            }
        }

        public void TraceWalkerProcessed(string correlationId, int messageId)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.WalkerProcessed = DateTime.Now;
                chain.MessageId = messageId;
                // Don't log to DiagnosticLogger here - just track the trace
            }
        }

        public void TracePacketParsed(string correlationId, string messageType)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.PacketParsed = DateTime.Now;
                chain.MessageType = messageType;
            }
        }

        public void TraceFlightDataUpdated(string correlationId)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.FlightDataUpdated = DateTime.Now;
            }
        }

        public void TraceTelemetryEvent(string correlationId)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.TelemetryEvent = DateTime.Now;
            }
        }

        public void TraceViewModelUpdated(string correlationId)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.ViewModelUpdated = DateTime.Now;
            }
        }

        public void TraceUIUpdated(string correlationId)
        {
            if (_chains.TryGetValue(correlationId, out var chain))
            {
                chain.UIUpdated = DateTime.Now;
            }
        }

        public EventChainReport GetReport(string correlationId)
        {
            if (!_chains.TryGetValue(correlationId, out var chain))
                return EventChainReport.NotFound(correlationId);

            var stages = new List<StageReport>();

            if (chain.TransportReceived != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "Transport",
                    Timestamp = chain.TransportReceived,
                    Success = true,
                    Details = $"{chain.ByteCount} bytes"
                });
            }

            if (chain.WalkerProcessed != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "Walker",
                    Timestamp = chain.WalkerProcessed,
                    Success = true,
                    Details = $"Message ID: {chain.MessageId}"
                });
            }

            if (chain.PacketParsed != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "Parser",
                    Timestamp = chain.PacketParsed,
                    Success = true,
                    Details = chain.MessageType
                });
            }

            if (chain.FlightDataUpdated != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "FlightData",
                    Timestamp = chain.FlightDataUpdated,
                    Success = true
                });
            }

            if (chain.TelemetryEvent != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "TelemetryEvent",
                    Timestamp = chain.TelemetryEvent,
                    Success = true
                });
            }

            if (chain.ViewModelUpdated != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "ViewModel",
                    Timestamp = chain.ViewModelUpdated,
                    Success = true
                });
            }

            if (chain.UIUpdated != DateTime.MinValue)
            {
                stages.Add(new StageReport
                {
                    Name = "UI",
                    Timestamp = chain.UIUpdated,
                    Success = true
                });
            }

            var totalLatency = TimeSpan.Zero;
            if (chain.TransportReceived != DateTime.MinValue && chain.UIUpdated != DateTime.MinValue)
            {
                totalLatency = chain.UIUpdated - chain.TransportReceived;
            }

            return new EventChainReport
            {
                CorrelationId = correlationId,
                Stages = stages,
                TotalLatency = totalLatency,
                IsComplete = chain.UIUpdated != DateTime.MinValue
            };
        }

        public List<string> GetActiveTraces()
        {
            return _chains.Keys.ToList();
        }
    }

    public class EventChain
    {
        public string CorrelationId { get; set; } = "";
        public DateTime TransportReceived { get; set; }
        public DateTime WalkerProcessed { get; set; }
        public DateTime PacketParsed { get; set; }
        public DateTime FlightDataUpdated { get; set; }
        public DateTime TelemetryEvent { get; set; }
        public DateTime ViewModelUpdated { get; set; }
        public DateTime UIUpdated { get; set; }
        public int ByteCount { get; set; }
        public int MessageId { get; set; }
        public string MessageType { get; set; } = "";
    }

    public class EventChainReport
    {
        public string CorrelationId { get; set; } = "";
        public List<StageReport> Stages { get; set; } = new();
        public TimeSpan TotalLatency { get; set; }
        public bool IsComplete { get; set; }

        public static EventChainReport NotFound(string id) =>
            new EventChainReport
            {
                CorrelationId = id,
                Stages = new List<StageReport>
                {
                    new StageReport
                    {
                        Name = "Error",
                        Timestamp = DateTime.Now,
                        Success = false,
                        Details = "Correlation ID not found"
                    }
                }
            };

        public string GetSummary()
        {
            if (Stages.Count == 0)
                return $"No stages recorded for {CorrelationId}";

            var completedStages = Stages.Count(s => s.Success);
            var latencyMs = TotalLatency.TotalMilliseconds;

            return $"Correlation {CorrelationId}: {completedStages} stages completed, " +
                   $"Total latency: {latencyMs:F2}ms, Complete: {IsComplete}";
        }
    }

    public class StageReport
    {
        public string Name { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string Details { get; set; } = "";

        public override string ToString()
        {
            var status = Success ? "✓" : "✗";
            var time = Timestamp.ToString("HH:mm:ss.fff");
            return $"{status} {Name} at {time}" + (string.IsNullOrEmpty(Details) ? "" : $" - {Details}");
        }
    }
}
