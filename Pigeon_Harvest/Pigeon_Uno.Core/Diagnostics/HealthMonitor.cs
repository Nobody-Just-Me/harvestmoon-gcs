using System;

namespace Pigeon_Uno.Core.Diagnostics
{
    /// <summary>
    /// Monitors the health of the MAVLink telemetry pipeline components.
    /// Provides real-time health status for transport, walker, parser, and event chain.
    /// </summary>
    public interface IHealthMonitor
    {
        HealthStatus GetTransportHealth();
        HealthStatus GetWalkerHealth();
        HealthStatus GetParserHealth();
        HealthStatus GetEventChainHealth();
        OverallHealth GetOverallHealth();
        void UpdateTransportActivity();
        void UpdateWalkerActivity();
        void UpdateParserActivity();
        void UpdateEventChainActivity();
    }

    /// <summary>
    /// Implementation of health monitoring for the telemetry pipeline.
    /// Tracks last activity timestamp for each component and determines health status.
    /// </summary>
    public class HealthMonitor : IHealthMonitor
    {
        private readonly IDiagnosticLogger _logger;
        private DateTime _lastTransportData = DateTime.MinValue;
        private DateTime _lastWalkerActivity = DateTime.MinValue;
        private DateTime _lastPacketParsed = DateTime.MinValue;
        private DateTime _lastTelemetryEvent = DateTime.MinValue;

        public HealthMonitor(IDiagnosticLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void UpdateTransportActivity()
        {
            _lastTransportData = DateTime.Now;
        }

        public void UpdateWalkerActivity()
        {
            _lastWalkerActivity = DateTime.Now;
        }

        public void UpdateParserActivity()
        {
            _lastPacketParsed = DateTime.Now;
        }

        public void UpdateEventChainActivity()
        {
            _lastTelemetryEvent = DateTime.Now;
        }

        public HealthStatus GetTransportHealth()
        {
            var timeSinceLastData = DateTime.Now - _lastTransportData;

            if (_lastTransportData == DateTime.MinValue)
                return HealthStatus.Unknown("No data received yet");

            if (timeSinceLastData.TotalSeconds < 2)
                return HealthStatus.Healthy("Receiving data");

            if (timeSinceLastData.TotalSeconds < 10)
                return HealthStatus.Warning($"No data for {timeSinceLastData.TotalSeconds:F1}s");

            return HealthStatus.Critical($"No data for {timeSinceLastData.TotalSeconds:F1}s");
        }

        public HealthStatus GetWalkerHealth()
        {
            var timeSinceLastActivity = DateTime.Now - _lastWalkerActivity;

            if (_lastWalkerActivity == DateTime.MinValue)
                return HealthStatus.Unknown("Walker not active yet");

            if (timeSinceLastActivity.TotalSeconds < 2)
                return HealthStatus.Healthy("Processing data");

            if (timeSinceLastActivity.TotalSeconds < 10)
                return HealthStatus.Warning($"No activity for {timeSinceLastActivity.TotalSeconds:F1}s");

            return HealthStatus.Critical($"No activity for {timeSinceLastActivity.TotalSeconds:F1}s");
        }

        public HealthStatus GetParserHealth()
        {
            var timeSinceLastParsed = DateTime.Now - _lastPacketParsed;

            if (_lastPacketParsed == DateTime.MinValue)
                return HealthStatus.Unknown("No packets parsed yet");

            if (timeSinceLastParsed.TotalSeconds < 2)
                return HealthStatus.Healthy("Parsing packets");

            if (timeSinceLastParsed.TotalSeconds < 10)
                return HealthStatus.Warning($"No packets for {timeSinceLastParsed.TotalSeconds:F1}s");

            return HealthStatus.Critical($"No packets for {timeSinceLastParsed.TotalSeconds:F1}s");
        }

        public HealthStatus GetEventChainHealth()
        {
            var timeSinceLastEvent = DateTime.Now - _lastTelemetryEvent;

            if (_lastTelemetryEvent == DateTime.MinValue)
                return HealthStatus.Unknown("No telemetry events yet");

            if (timeSinceLastEvent.TotalSeconds < 2)
                return HealthStatus.Healthy("Events firing");

            if (timeSinceLastEvent.TotalSeconds < 10)
                return HealthStatus.Warning($"No events for {timeSinceLastEvent.TotalSeconds:F1}s");

            return HealthStatus.Critical($"No events for {timeSinceLastEvent.TotalSeconds:F1}s");
        }

        public OverallHealth GetOverallHealth()
        {
            var transport = GetTransportHealth();
            var walker = GetWalkerHealth();
            var parser = GetParserHealth();
            var events = GetEventChainHealth();

            return new OverallHealth
            {
                Transport = transport,
                Walker = walker,
                Parser = parser,
                EventChain = events,
                Status = DetermineOverallStatus(transport, walker, parser, events)
            };
        }

        private HealthStatusLevel DetermineOverallStatus(params HealthStatus[] statuses)
        {
            bool hasCritical = false;
            bool hasWarning = false;
            bool hasHealthy = false;

            foreach (var status in statuses)
            {
                switch (status.Level)
                {
                    case HealthStatusLevel.Critical:
                        hasCritical = true;
                        break;
                    case HealthStatusLevel.Warning:
                        hasWarning = true;
                        break;
                    case HealthStatusLevel.Healthy:
                        hasHealthy = true;
                        break;
                }
            }

            if (hasCritical)
                return HealthStatusLevel.Critical;
            if (hasWarning)
                return HealthStatusLevel.Warning;
            if (hasHealthy && statuses.Length > 0)
            {
                // All statuses are healthy
                bool allHealthy = true;
                foreach (var status in statuses)
                {
                    if (status.Level != HealthStatusLevel.Healthy)
                    {
                        allHealthy = false;
                        break;
                    }
                }
                if (allHealthy)
                    return HealthStatusLevel.Healthy;
            }

            return HealthStatusLevel.Unknown;
        }
    }

    /// <summary>
    /// Represents the health status of a single component.
    /// </summary>
    public class HealthStatus
    {
        public HealthStatusLevel Level { get; set; }
        public string Message { get; set; } = "";

        public static HealthStatus Healthy(string msg) =>
            new HealthStatus { Level = HealthStatusLevel.Healthy, Message = msg };

        public static HealthStatus Warning(string msg) =>
            new HealthStatus { Level = HealthStatusLevel.Warning, Message = msg };

        public static HealthStatus Critical(string msg) =>
            new HealthStatus { Level = HealthStatusLevel.Critical, Message = msg };

        public static HealthStatus Unknown(string msg) =>
            new HealthStatus { Level = HealthStatusLevel.Unknown, Message = msg };
    }

    /// <summary>
    /// Health status levels for components.
    /// </summary>
    public enum HealthStatusLevel
    {
        Unknown,
        Healthy,
        Warning,
        Critical
    }

    /// <summary>
    /// Overall health status of the entire telemetry pipeline.
    /// </summary>
    public class OverallHealth
    {
        public HealthStatus Transport { get; set; } = HealthStatus.Unknown("");
        public HealthStatus Walker { get; set; } = HealthStatus.Unknown("");
        public HealthStatus Parser { get; set; } = HealthStatus.Unknown("");
        public HealthStatus EventChain { get; set; } = HealthStatus.Unknown("");
        public HealthStatusLevel Status { get; set; }
    }
}
