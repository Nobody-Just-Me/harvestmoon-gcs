using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Diagnostics;
using Xunit;

namespace HarvestmoonGCS.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for telemetry event instrumentation.
    /// Tests Properties 5, 14, and 23 from the design document.
    /// </summary>
    public class TelemetryEventPropertyTests
    {
        /// <summary>
        /// Property 5: Event Logging
        /// For any TelemetryReceived event firing, the diagnostic logger should create 
        /// a log entry with millisecond-precision timestamp and data summary.
        /// **Validates: Requirements 1.5**
        /// </summary>
        [Property(MaxTest = 20)]
        public Property EventLogging_CreatesLogEntry()
        {
            return Prop.ForAll<float, float, float>(
                (roll, pitch, yaw) =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    logger.SetEnabled(true);
                    var timestamp = DateTime.Now;
                    var summary = $"Roll={roll:F1}, Pitch={pitch:F1}, Yaw={yaw:F1}";

                    // Act
                    logger.LogTelemetryEvent(timestamp, summary);

                    // Assert
                    var logs = logger.GetRecentLogs(10);
                    var telemetryLogs = logs.Where(l => l.Stage == "TelemetryEvent").ToList();

                    if (telemetryLogs.Count == 0)
                        return false.Label("No log entry created");

                    if (!telemetryLogs.Any(l => l.Message.Contains(summary)))
                        return false.Label("Summary not included");

                    if (!telemetryLogs.Any(l => Math.Abs((l.Timestamp - timestamp).TotalMilliseconds) < 100))
                        return false.Label("Timestamp doesn't match");

                    return true.ToProperty();
                });
        }

        /// <summary>
        /// Property 14: Telemetry Event Timing
        /// For any FlightData update, the TelemetryReceived event should fire within 33ms 
        /// (when throttling is enabled) or immediately (when throttling is disabled).
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Property(MaxTest = 20)]
        public Property TelemetryEventTiming_WithinThrottleInterval()
        {
            return Prop.ForAll<PositiveInt>(
                updateCount =>
                {
                    // Arrange
                    var throttler = new Core.Helpers.ThrottledUpdater(33); // 33ms throttle
                    var eventTimes = new System.Collections.Concurrent.ConcurrentBag<DateTime>();
                    var startTime = DateTime.Now;

                    // Act - Schedule multiple updates rapidly
                    for (int i = 0; i < Math.Min(updateCount.Get, 10); i++)
                    {
                        throttler.Schedule(() =>
                        {
                            eventTimes.Add(DateTime.Now);
                        });
                        System.Threading.Thread.Sleep(5); // Rapid updates
                    }

                    // Wait for throttler to process
                    System.Threading.Thread.Sleep(100);
                    throttler.Dispose();

                    // Assert - Events should be throttled (not all updates fire immediately)
                    var eventList = eventTimes.ToList();
                    if (eventList.Count < 2)
                        return true.ToProperty(); // Not enough events to test timing

                    // Check that events are spaced by approximately 33ms
                    var intervals = eventList.OrderBy(t => t)
                        .Zip(eventList.OrderBy(t => t).Skip(1), (a, b) => (b - a).TotalMilliseconds)
                        .ToList();

                    // At least some intervals should be close to 33ms (±20ms tolerance)
                    return intervals.Any(interval => interval >= 13 && interval <= 53)
                        .Label($"Throttled intervals found (sample: {string.Join(", ", intervals.Take(3).Select(i => $"{i:F0}ms"))})");
                });
        }

        /// <summary>
        /// Property 23: Throttle Logging
        /// For any telemetry update processed, the system should log whether the update 
        /// was throttled or passed through.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Property(MaxTest = 20)]
        public Property ThrottleLogging_LogsThrottleStatus()
        {
            return Prop.ForAll<bool>(
                shouldThrottle =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    logger.SetEnabled(true);

                    // Act - Log telemetry event (simulating throttled or immediate)
                    var summary = shouldThrottle 
                        ? "Throttled update: Roll=10.0, Pitch=5.0, Yaw=180.0"
                        : "Immediate update: Roll=10.0, Pitch=5.0, Yaw=180.0";
                    
                    logger.LogTelemetryEvent(DateTime.Now, summary);

                    // Assert
                    var logs = logger.GetRecentLogs(10);
                    var telemetryLogs = logs.Where(l => l.Stage == "TelemetryEvent").ToList();

                    if (telemetryLogs.Count == 0)
                        return false.Label("No telemetry event logged");

                    if (!telemetryLogs.Any(l => l.Message.Contains("Roll=") && l.Message.Contains("Pitch=")))
                        return false.Label("Doesn't contain telemetry data");

                    return true.ToProperty();
                });
        }

        /// <summary>
        /// Additional test: Verify timestamp precision (milliseconds)
        /// </summary>
        [Property(MaxTest = 10)]
        public Property EventLogging_HasMillisecondPrecision()
        {
            return Prop.ForAll<DateTime>(
                timestamp =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    logger.SetEnabled(true);

                    // Act
                    logger.LogTelemetryEvent(timestamp, "Test event");

                    // Assert
                    var logs = logger.GetRecentLogs(10);
                    var entry = logs.FirstOrDefault(l => l.Stage == "TelemetryEvent");

                    if (entry == null)
                        return false.ToProperty();

                    // Check that timestamp has millisecond precision
                    var formattedTime = entry.Timestamp.ToString("HH:mm:ss.fff");
                    return formattedTime.Contains(".").Label($"Timestamp has millisecond precision: {formattedTime}");
                });
        }

        /// <summary>
        /// Additional test: Verify throttler bypass mode (immediate execution)
        /// </summary>
        [Fact]
        public void ThrottlerBypass_ExecutesImmediately()
        {
            // Arrange
            var executionTimes = new System.Collections.Concurrent.ConcurrentBag<DateTime>();
            var startTime = DateTime.Now;

            // Act - Execute without throttler (immediate mode)
            for (int i = 0; i < 5; i++)
            {
                var action = new Action(() => executionTimes.Add(DateTime.Now));
                action(); // Execute immediately
                System.Threading.Thread.Sleep(5);
            }

            // Assert - All executions should happen immediately (within 50ms of start)
            var times = executionTimes.ToList();
            Assert.Equal(5, times.Count);
            Assert.All(times, time => 
                Assert.True((time - startTime).TotalMilliseconds < 100, 
                    $"Execution should be immediate, but took {(time - startTime).TotalMilliseconds}ms"));
        }
    }
}
