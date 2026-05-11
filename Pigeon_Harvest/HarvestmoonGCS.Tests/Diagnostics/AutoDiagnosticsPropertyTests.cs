using System;
using System.Threading;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Diagnostics;
using Xunit;

namespace HarvestmoonGCS.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for automatic diagnostics.
    /// </summary>
    public class AutoDiagnosticsPropertyTests
    {
        /// <summary>
        /// Property 38: Automatic Diagnostics
        /// For any situation where telemetry stops flowing for more than 5 seconds, 
        /// the system should automatically run diagnostics and log a report of likely causes.
        /// Validates: Requirements 10.5
        /// </summary>
        [Fact]
        public void AutomaticDiagnosticsOnTelemetryStop()
        {
            var logger = new DiagnosticLogger();
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();
            var failureDetector = new FailureModeDetector(logger);
            var autoDiag = new AutoDiagnostics(healthMonitor, perfMonitor, failureDetector, logger);

            bool reportGenerated = false;
            DiagnosticReport? generatedReport = null;

            autoDiag.DiagnosticReportGenerated += (sender, report) =>
            {
                reportGenerated = true;
                generatedReport = report;
            };

            // Start monitoring
            autoDiag.Start();

            // Record initial telemetry
            autoDiag.RecordTelemetryUpdate();

            // Manually trigger timeout by setting last update time to 6 seconds ago
            var autoType = autoDiag.GetType();
            var field = autoType.GetField("_lastTelemetryUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(autoDiag, DateTime.Now.AddSeconds(-6));

            var checkMethod = autoType.GetMethod("CheckTelemetryFlow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkMethod?.Invoke(autoDiag, new object?[] { null });

            // Wait for check interval
            Thread.Sleep(200);

            autoDiag.Stop();
            autoDiag.Dispose();

            Assert.True(reportGenerated);
            Assert.NotNull(generatedReport);
            Assert.NotEmpty(generatedReport.DiagnosticMessage);
        }

        [Fact]
        public void NoDiagnosticsWhenTelemetryFlowing()
        {
            var logger = new DiagnosticLogger();
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();
            var failureDetector = new FailureModeDetector(logger);
            var autoDiag = new AutoDiagnostics(healthMonitor, perfMonitor, failureDetector, logger);

            bool reportGenerated = false;

            autoDiag.DiagnosticReportGenerated += (sender, report) =>
            {
                reportGenerated = true;
            };

            autoDiag.Start();

            // Continuously record telemetry updates
            for (int i = 0; i < 10; i++)
            {
                autoDiag.RecordTelemetryUpdate();
                Thread.Sleep(100);
            }

            autoDiag.Stop();
            autoDiag.Dispose();

            // No report should be generated when telemetry is flowing
            Assert.False(reportGenerated);
        }

        [Fact]
        public void DiagnosticReportContainsAllComponents()
        {
            var logger = new DiagnosticLogger();
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();
            var failureDetector = new FailureModeDetector(logger);
            var autoDiag = new AutoDiagnostics(healthMonitor, perfMonitor, failureDetector, logger);

            DiagnosticReport? report = null;

            autoDiag.DiagnosticReportGenerated += (sender, r) =>
            {
                report = r;
            };

            autoDiag.Start();
            autoDiag.RecordTelemetryUpdate();

            // Trigger timeout
            var autoType = autoDiag.GetType();
            var field = autoType.GetField("_lastTelemetryUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(autoDiag, DateTime.Now.AddSeconds(-6));

            var checkMethod = autoType.GetMethod("CheckTelemetryFlow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkMethod?.Invoke(autoDiag, new object?[] { null });

            Thread.Sleep(200);

            autoDiag.Stop();
            autoDiag.Dispose();

            Assert.NotNull(report);
            Assert.NotNull(report.OverallHealth);
            Assert.NotNull(report.PerformanceReport);
            Assert.NotEmpty(report.DiagnosticMessage);
        }
    }
}
