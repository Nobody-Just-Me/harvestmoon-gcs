using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Automatically runs diagnostics when telemetry flow stops.
    /// </summary>
    public interface IAutoDiagnostics
    {
        void Start();
        void Stop();
        void RecordTelemetryUpdate();
        event EventHandler<DiagnosticReport>? DiagnosticReportGenerated;
    }

    public class AutoDiagnostics : IAutoDiagnostics, IDisposable
    {
        private readonly HealthMonitor _healthMonitor;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly FailureModeDetector _failureDetector;
        private readonly IDiagnosticLogger _logger;
        private Timer? _monitorTimer;
        private DateTime _lastTelemetryUpdate = DateTime.MinValue;
        private bool _isRunning = false;
        private const int CHECK_INTERVAL_MS = 1000;
        private const int TELEMETRY_TIMEOUT_SECONDS = 5;

        public event EventHandler<DiagnosticReport>? DiagnosticReportGenerated;

        public AutoDiagnostics(
            HealthMonitor healthMonitor,
            PerformanceMonitor performanceMonitor,
            FailureModeDetector failureDetector,
            IDiagnosticLogger logger)
        {
            _healthMonitor = healthMonitor;
            _performanceMonitor = performanceMonitor;
            _failureDetector = failureDetector;
            _logger = logger;
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _lastTelemetryUpdate = DateTime.Now;
            _monitorTimer = new Timer(CheckTelemetryFlow, null, CHECK_INTERVAL_MS, CHECK_INTERVAL_MS);

            _logger.LogFlightDataUpdate("AutoDiagnostics", "Stopped", "Started");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            _logger.LogFlightDataUpdate("AutoDiagnostics", "Started", "Stopped");
        }

        public void RecordTelemetryUpdate()
        {
            _lastTelemetryUpdate = DateTime.Now;
        }

        private void CheckTelemetryFlow(object? state)
        {
            if (!_isRunning) return;

            var now = DateTime.Now;
            var timeSinceLastUpdate = now - _lastTelemetryUpdate;

            // If telemetry has stopped for 5+ seconds, run diagnostics
            if (_lastTelemetryUpdate != DateTime.MinValue &&
                timeSinceLastUpdate.TotalSeconds > TELEMETRY_TIMEOUT_SECONDS)
            {
                var report = GenerateDiagnosticReport();
                DiagnosticReportGenerated?.Invoke(this, report);

                // Reset timer to avoid repeated reports
                _lastTelemetryUpdate = now;
            }
        }

        private DiagnosticReport GenerateDiagnosticReport()
        {
            var report = new DiagnosticReport
            {
                Timestamp = DateTime.Now,
                OverallHealth = _healthMonitor.GetOverallHealth(),
                PerformanceReport = _performanceMonitor.GetReport(),
                FailureMode = _failureDetector.DetectFailureMode(_healthMonitor, _performanceMonitor)
            };

            report.DiagnosticMessage = _failureDetector.GetDiagnosticMessage(report.FailureMode);

            _logger.LogFlightDataUpdate("DiagnosticReport", "Generated", report.FailureMode.ToString());

            return report;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class DiagnosticReport
    {
        public DateTime Timestamp { get; set; }
        public OverallHealth OverallHealth { get; set; } = new OverallHealth();
        public PerformanceReport PerformanceReport { get; set; } = new PerformanceReport();
        public FailureMode FailureMode { get; set; }
        public string DiagnosticMessage { get; set; } = "";

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Diagnostic Report - {Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();
            sb.AppendLine($"Overall Status: {OverallHealth.Status}");
            sb.AppendLine($"Failure Mode: {FailureMode}");
            sb.AppendLine();
            sb.AppendLine("Component Health:");
            sb.AppendLine($"  Transport: {OverallHealth.Transport.Level} - {OverallHealth.Transport.Message}");
            sb.AppendLine($"  Walker: {OverallHealth.Walker.Level} - {OverallHealth.Walker.Message}");
            sb.AppendLine($"  Parser: {OverallHealth.Parser.Level} - {OverallHealth.Parser.Message}");
            sb.AppendLine($"  Event Chain: {OverallHealth.EventChain.Level} - {OverallHealth.EventChain.Message}");
            sb.AppendLine();
            sb.AppendLine("Diagnostic Message:");
            sb.AppendLine(DiagnosticMessage);

            return sb.ToString();
        }
    }
}
