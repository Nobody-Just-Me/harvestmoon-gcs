using System;
using System.Threading;
using FsCheck;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Diagnostics;
using Xunit;

namespace HarvestmoonGCS.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for failure mode detection.
    /// </summary>
    public class FailureModePropertyTests
    {
        /// <summary>
        /// Property 37: Failure Mode Detection
        /// For any common failure mode, the system should detect the failure and 
        /// log a specific diagnostic message identifying the likely cause.
        /// Validates: Requirements 10.4
        /// </summary>
        [Fact]
        public void DisconnectedHardwareDetection()
        {
            var logger = new DiagnosticLogger();
            var detector = new FailureModeDetector(logger);
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();

            // Record byte reception, then wait
            detector.RecordByteReception();
            Thread.Sleep(100); // Simulate time passing

            // Manually set last byte time to simulate 5+ seconds ago
            var detectorType = detector.GetType();
            var field = detectorType.GetField("_lastByteReceived", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(detector, DateTime.Now.AddSeconds(-6));

            var mode = detector.DetectFailureMode(healthMonitor, perfMonitor);

            Assert.Equal(FailureMode.DisconnectedHardware, mode);
            Assert.Contains("No data received", detector.GetDiagnosticMessage(mode));
        }

        [Fact]
        public void WrongBaudRateDetection()
        {
            var logger = new DiagnosticLogger();
            var detector = new FailureModeDetector(logger);
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();

            // Record bytes but no packets
            detector.RecordByteReception();

            var mode = detector.DetectFailureMode(healthMonitor, perfMonitor);

            Assert.Equal(FailureMode.WrongBaudRate, mode);
            Assert.Contains("Baud rate", detector.GetDiagnosticMessage(mode));
        }

        [Fact]
        public void CorruptedPacketsDetection()
        {
            var logger = new DiagnosticLogger();
            var detector = new FailureModeDetector(logger);
            var healthMonitor = new HealthMonitor(logger);
            var perfMonitor = new PerformanceMonitor();

            // Record many CRC failures
            for (int i = 0; i < 15; i++)
            {
                detector.RecordCrcFailure();
            }

            var mode = detector.DetectFailureMode(healthMonitor, perfMonitor);

            Assert.Equal(FailureMode.CorruptedPackets, mode);
            Assert.Contains("CRC failures", detector.GetDiagnosticMessage(mode));
        }

        /// <summary>
        /// Property: Diagnostic Message Completeness
        /// For any failure mode, the diagnostic message should be non-empty 
        /// and contain actionable information.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property DiagnosticMessageCompleteness()
        {
            return Prop.ForAll(
                Arb.Default.Derive<FailureMode>(),
                mode =>
                {
                    var logger = new DiagnosticLogger();
                    var detector = new FailureModeDetector(logger);

                    var message = detector.GetDiagnosticMessage(mode);

                    return !string.IsNullOrWhiteSpace(message);
                });
        }
    }
}
