using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Logs method call sequences for debugging data flow.
    /// Tracks entry/exit of methods with parameters and return values.
    /// </summary>
    public interface IMethodCallSequenceLogger
    {
        void LogMethodEntry(string methodName, string correlationId, params object[] parameters);
        void LogMethodExit(string methodName, string correlationId, object? returnValue = null);
        List<MethodCallEntry> GetSequence(string correlationId);
        string GenerateSequenceDiagram(string correlationId);
    }

    public class MethodCallSequenceLogger : IMethodCallSequenceLogger
    {
        private readonly Dictionary<string, List<MethodCallEntry>> _sequences = new();
        private readonly object _lock = new();
        private const int MAX_SEQUENCES = 1000;

        public void LogMethodEntry(string methodName, string correlationId, params object[] parameters)
        {
            lock (_lock)
            {
                if (!_sequences.ContainsKey(correlationId))
                {
                    _sequences[correlationId] = new List<MethodCallEntry>();
                }

                var entry = new MethodCallEntry
                {
                    MethodName = methodName,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.Now,
                    Type = CallType.Entry,
                    Parameters = parameters?.Select(p => p?.ToString() ?? "null").ToList() ?? new List<string>()
                };

                _sequences[correlationId].Add(entry);

                Debug.WriteLine($"[MethodCall:{correlationId}] → {methodName}({string.Join(", ", entry.Parameters)})");

                // Limit sequence count
                if (_sequences.Count > MAX_SEQUENCES)
                {
                    var oldestKey = _sequences.Keys.First();
                    _sequences.Remove(oldestKey);
                }
            }
        }

        public void LogMethodExit(string methodName, string correlationId, object? returnValue = null)
        {
            lock (_lock)
            {
                if (!_sequences.ContainsKey(correlationId))
                {
                    _sequences[correlationId] = new List<MethodCallEntry>();
                }

                var entry = new MethodCallEntry
                {
                    MethodName = methodName,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.Now,
                    Type = CallType.Exit,
                    ReturnValue = returnValue?.ToString() ?? "void"
                };

                _sequences[correlationId].Add(entry);

                Debug.WriteLine($"[MethodCall:{correlationId}] ← {methodName} = {entry.ReturnValue}");
            }
        }

        public List<MethodCallEntry> GetSequence(string correlationId)
        {
            lock (_lock)
            {
                return _sequences.TryGetValue(correlationId, out var sequence) 
                    ? new List<MethodCallEntry>(sequence) 
                    : new List<MethodCallEntry>();
            }
        }

        public string GenerateSequenceDiagram(string correlationId)
        {
            var sequence = GetSequence(correlationId);
            if (sequence.Count == 0)
            {
                return $"No sequence found for correlation ID: {correlationId}";
            }

            var diagram = new System.Text.StringBuilder();
            diagram.AppendLine($"Sequence Diagram for {correlationId}");
            diagram.AppendLine("=".PadRight(60, '='));
            diagram.AppendLine();

            int depth = 0;
            foreach (var entry in sequence)
            {
                if (entry.Type == CallType.Exit)
                {
                    depth = Math.Max(0, depth - 1);
                }

                var indent = new string(' ', depth * 2);
                var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");

                if (entry.Type == CallType.Entry)
                {
                    var parameters = entry.Parameters.Count > 0 
                        ? $"({string.Join(", ", entry.Parameters)})" 
                        : "()";
                    diagram.AppendLine($"{timestamp} {indent}→ {entry.MethodName}{parameters}");
                    depth++;
                }
                else
                {
                    diagram.AppendLine($"{timestamp} {indent}← {entry.MethodName} = {entry.ReturnValue}");
                }
            }

            return diagram.ToString();
        }
    }

    public class MethodCallEntry
    {
        public string MethodName { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public CallType Type { get; set; }
        public List<string> Parameters { get; set; } = new();
        public string ReturnValue { get; set; } = "";
    }

    public enum CallType
    {
        Entry,
        Exit
    }

    /// <summary>
    /// Helper class for automatic method entry/exit logging using IDisposable pattern.
    /// Usage: using var _ = MethodScope.Enter(logger, correlationId);
    /// </summary>
    public class MethodScope : IDisposable
    {
        private readonly IMethodCallSequenceLogger _logger;
        private readonly string _methodName;
        private readonly string _correlationId;
        private object? _returnValue;

        private MethodScope(IMethodCallSequenceLogger logger, string methodName, string correlationId, object[] parameters)
        {
            _logger = logger;
            _methodName = methodName;
            _correlationId = correlationId;
            _logger.LogMethodEntry(methodName, correlationId, parameters);
        }

        public static MethodScope Enter(
            IMethodCallSequenceLogger logger, 
            string correlationId,
            [CallerMemberName] string methodName = "",
            params object[] parameters)
        {
            return new MethodScope(logger, methodName, correlationId, parameters);
        }

        public void SetReturnValue(object? value)
        {
            _returnValue = value;
        }

        public void Dispose()
        {
            _logger.LogMethodExit(_methodName, _correlationId, _returnValue);
        }
    }
}
