using FsCheck;
using FsCheck.Xunit;
using Pigeon_Uno.Core.Diagnostics;
using Xunit;

namespace Pigeon_Uno.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for event chain tracing functionality.
    /// Tests universal properties that should hold across all event traces.
    /// </summary>
    public class EventTracingPropertyTests
    {
        /// <summary>
        /// Property 16: Correlation ID Tracing
        /// For any packet entering the pipeline, a correlation ID should be assigned 
        /// and logged at every stage from transport reception through UI update.
        /// **Validates: Requirements 3.7**
        /// </summary>
        [Property(MaxTest = 20)]
        public Property CorrelationIdTracingProperty()
        {
            return Prop.ForAll<PositiveInt, PositiveInt>(
                (byteCount, messageId) =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);

                    // Act - Start a trace
                    var correlationId = tracer.StartTrace();

                    // Assert - Correlation ID should be generated
                    var isValidCorrelationId = !string.IsNullOrEmpty(correlationId) && correlationId.Length == 8;

                    if (!isValidCorrelationId)
                        return false.ToProperty();

                    // Act - Trace through all stages
                    tracer.TraceTransportReceived(correlationId, byteCount.Get);
                    tracer.TraceWalkerProcessed(correlationId, messageId.Get);
                    tracer.TracePacketParsed(correlationId, "TEST_MESSAGE");
                    tracer.TraceFlightDataUpdated(correlationId);
                    tracer.TraceTelemetryEvent(correlationId);
                    tracer.TraceViewModelUpdated(correlationId);
                    tracer.TraceUIUpdated(correlationId);

                    // Assert - Get report and verify all stages are present
                    var report = tracer.GetReport(correlationId);

                    var hasAllStages = report.Stages.Count == 7 &&
                                      report.Stages.Any(s => s.Name == "Transport") &&
                                      report.Stages.Any(s => s.Name == "Walker") &&
                                      report.Stages.Any(s => s.Name == "Parser") &&
                                      report.Stages.Any(s => s.Name == "FlightData") &&
                                      report.Stages.Any(s => s.Name == "TelemetryEvent") &&
                                      report.Stages.Any(s => s.Name == "ViewModel") &&
                                      report.Stages.Any(s => s.Name == "UI");

                    var allStagesSuccessful = report.Stages.All(s => s.Success);
                    var isComplete = report.IsComplete;
                    var hasPositiveLatency = report.TotalLatency.TotalMilliseconds > 0;

                    return (hasAllStages && allStagesSuccessful && isComplete && hasPositiveLatency)
                        .ToProperty()
                        .Label($"All stages traced for correlation ID {correlationId}");
                });
        }

        /// <summary>
        /// Property: Correlation ID Uniqueness
        /// Each call to StartTrace should generate a unique correlation ID.
        /// </summary>
        [Property(MaxTest = 20)]
        public Property CorrelationIdUniquenessProperty()
        {
            return Prop.ForAll<PositiveInt>(
                count =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var correlationIds = new HashSet<string>();
                    var iterations = Math.Min(count.Get, 100); // Limit to 100 iterations

                    // Act - Generate multiple correlation IDs
                    for (int i = 0; i < iterations; i++)
                    {
                        var id = tracer.StartTrace();
                        correlationIds.Add(id);
                    }

                    // Assert - All IDs should be unique
                    return (correlationIds.Count == iterations)
                        .ToProperty()
                        .Label($"Generated {iterations} unique correlation IDs");
                });
        }

        /// <summary>
        /// Property: Stage Ordering
        /// Stages should be recorded in chronological order.
        /// </summary>
        [Property(MaxTest = 20)]
        public Property StageOrderingProperty()
        {
            return Prop.ForAll<PositiveInt>(
                byteCount =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var correlationId = tracer.StartTrace();

                    // Act - Trace stages in order with small delays
                    tracer.TraceTransportReceived(correlationId, byteCount.Get);
                    System.Threading.Thread.Sleep(1); // Ensure timestamp difference
                    tracer.TraceWalkerProcessed(correlationId, 1);
                    System.Threading.Thread.Sleep(1);
                    tracer.TracePacketParsed(correlationId, "TEST");
                    System.Threading.Thread.Sleep(1);
                    tracer.TraceFlightDataUpdated(correlationId);

                    // Assert - Timestamps should be in order
                    var report = tracer.GetReport(correlationId);
                    var timestamps = report.Stages.Select(s => s.Timestamp).ToList();

                    var isOrdered = true;
                    for (int i = 1; i < timestamps.Count; i++)
                    {
                        if (timestamps[i] < timestamps[i - 1])
                        {
                            isOrdered = false;
                            break;
                        }
                    }

                    return isOrdered
                        .ToProperty()
                        .Label("Stages are in chronological order");
                });
        }

        /// <summary>
        /// Property: Report Retrieval for Non-Existent ID
        /// Requesting a report for a non-existent correlation ID should return a NotFound report.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property NonExistentCorrelationIdProperty()
        {
            return Prop.ForAll<NonEmptyString>(
                idString =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var nonExistentId = idString.Get;

                    // Act
                    var report = tracer.GetReport(nonExistentId);

                    // Assert - Should return a report indicating not found
                    var hasErrorStage = report.Stages.Any(s => s.Name == "Error" && !s.Success);
                    var hasCorrectId = report.CorrelationId == nonExistentId;

                    return (hasErrorStage && hasCorrectId)
                        .ToProperty()
                        .Label($"NotFound report returned for ID {nonExistentId}");
                });
        }

        /// <summary>
        /// Property: Partial Trace Handling
        /// A trace that doesn't complete all stages should still be retrievable.
        /// </summary>
        [Property(MaxTest = 20)]
        public Property PartialTraceProperty()
        {
            return Prop.ForAll<PositiveInt>(
                stageCount =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var correlationId = tracer.StartTrace();
                    var stages = Math.Min(stageCount.Get % 7, 7); // 0-6 stages

                    // Act - Trace only some stages
                    if (stages >= 1) tracer.TraceTransportReceived(correlationId, 100);
                    if (stages >= 2) tracer.TraceWalkerProcessed(correlationId, 1);
                    if (stages >= 3) tracer.TracePacketParsed(correlationId, "TEST");
                    if (stages >= 4) tracer.TraceFlightDataUpdated(correlationId);
                    if (stages >= 5) tracer.TraceTelemetryEvent(correlationId);
                    if (stages >= 6) tracer.TraceViewModelUpdated(correlationId);

                    // Assert
                    var report = tracer.GetReport(correlationId);
                    var recordedStages = report.Stages.Count;
                    var isNotComplete = !report.IsComplete; // Should not be complete without UI update

                    return (recordedStages == stages && isNotComplete)
                        .ToProperty()
                        .Label($"Partial trace with {stages} stages recorded correctly");
                });
        }

        /// <summary>
        /// Property: Latency Calculation
        /// Total latency should be the difference between first and last stage timestamps.
        /// </summary>
        [Property(MaxTest = 20)]
        public Property LatencyCalculationProperty()
        {
            return Prop.ForAll<PositiveInt>(
                byteCount =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var correlationId = tracer.StartTrace();

                    // Act - Trace with measurable delays
                    var startTime = DateTime.Now;
                    tracer.TraceTransportReceived(correlationId, byteCount.Get);
                    System.Threading.Thread.Sleep(10); // 10ms delay
                    tracer.TraceUIUpdated(correlationId);
                    var endTime = DateTime.Now;

                    // Assert
                    var report = tracer.GetReport(correlationId);
                    var reportedLatency = report.TotalLatency.TotalMilliseconds;
                    var expectedLatency = (endTime - startTime).TotalMilliseconds;

                    // Latency should be positive and within reasonable bounds
                    var isReasonable = reportedLatency >= 5 && reportedLatency <= 100;

                    return isReasonable
                        .ToProperty()
                        .Label($"Latency {reportedLatency:F2}ms is reasonable");
                });
        }

        /// <summary>
        /// Property: Active Traces Tracking
        /// GetActiveTraces should return all correlation IDs that have been started.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property ActiveTracesTrackingProperty()
        {
            return Prop.ForAll<PositiveInt>(
                count =>
                {
                    // Arrange
                    var logger = new DiagnosticLogger();
                    var tracer = new EventChainTracer(logger);
                    var expectedIds = new List<string>();
                    var iterations = Math.Min(count.Get, 50); // Limit to 50

                    // Act - Start multiple traces
                    for (int i = 0; i < iterations; i++)
                    {
                        var id = tracer.StartTrace();
                        expectedIds.Add(id);
                    }

                    var activeTraces = tracer.GetActiveTraces();

                    // Assert - All started traces should be in active list
                    var allPresent = expectedIds.All(id => activeTraces.Contains(id));

                    return allPresent
                        .ToProperty()
                        .Label($"All {iterations} traces are tracked as active");
                });
        }
    }
}
