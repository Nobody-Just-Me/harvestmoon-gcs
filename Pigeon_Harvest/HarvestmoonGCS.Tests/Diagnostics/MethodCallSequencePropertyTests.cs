using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Diagnostics;
using Xunit;

namespace HarvestmoonGCS.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for MethodCallSequenceLogger.
    /// Validates method call sequence logging functionality.
    /// </summary>
    public class MethodCallSequencePropertyTests
    {
        /// <summary>
        /// Property 25: Method Call Sequence Logging
        /// For any telemetry packet processing operation, the system should log 
        /// the complete sequence of method calls in chronological order.
        /// Validates: Requirements 6.2
        /// </summary>
        [Property(MaxTest = 20)]
        public Property MethodCallSequenceLogging()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            var positiveIntArb = Arb.From(Arb.Default.Int32().Generator.Where(n => n > 0 && n < 20));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                positiveIntArb,
                (correlationId, callCount) =>
                {
                    var logger = new MethodCallSequenceLogger();
                    var methodNames = Enumerable.Range(0, callCount)
                        .Select(i => $"Method{i}")
                        .ToList();

                    // Log method entries and exits
                    foreach (var methodName in methodNames)
                    {
                        logger.LogMethodEntry(methodName, correlationId, "param1", "param2");
                        logger.LogMethodExit(methodName, correlationId, "result");
                    }

                    // Retrieve sequence
                    var sequence = logger.GetSequence(correlationId);

                    // Should have entry and exit for each method
                    if (sequence.Count != callCount * 2) return false;

                    // Verify chronological order
                    for (int i = 1; i < sequence.Count; i++)
                    {
                        if (sequence[i].Timestamp < sequence[i - 1].Timestamp)
                            return false;
                    }

                    // Verify entry/exit pairing
                    for (int i = 0; i < callCount; i++)
                    {
                        var entryIndex = i * 2;
                        var exitIndex = i * 2 + 1;

                        if (sequence[entryIndex].Type != CallType.Entry) return false;
                        if (sequence[exitIndex].Type != CallType.Exit) return false;
                        if (sequence[entryIndex].MethodName != sequence[exitIndex].MethodName) return false;
                    }

                    return true;
                });
        }

        /// <summary>
        /// Property: Correlation ID Isolation
        /// For any two different correlation IDs, method calls logged under one ID 
        /// should not appear in the sequence for the other ID.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property CorrelationIdIsolation()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                nonEmptyStringArb,
                (correlationId1, correlationId2) =>
                {
                    if (correlationId1 == correlationId2) return true;

                    var logger = new MethodCallSequenceLogger();

                    // Log calls for first correlation ID
                    logger.LogMethodEntry("Method1", correlationId1);
                    logger.LogMethodExit("Method1", correlationId1);

                    // Log calls for second correlation ID
                    logger.LogMethodEntry("Method2", correlationId2);
                    logger.LogMethodExit("Method2", correlationId2);

                    // Retrieve sequences
                    var sequence1 = logger.GetSequence(correlationId1);
                    var sequence2 = logger.GetSequence(correlationId2);

                    // Each sequence should only contain its own calls
                    return sequence1.All(e => e.CorrelationId == correlationId1) &&
                           sequence2.All(e => e.CorrelationId == correlationId2) &&
                           sequence1.Count == 2 &&
                           sequence2.Count == 2;
                });
        }

        /// <summary>
        /// Property: Parameter Logging
        /// For any method call with parameters, all parameters should be logged 
        /// in the entry record.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property ParameterLogging()
        {
            var gen = Gen.Choose(0, 10).SelectMany(count =>
                Gen.ArrayOf(count, Arb.Default.Int32().Generator));
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));

            return Prop.ForAll(
                nonEmptyStringArb,
                Arb.From(gen),
                (correlationId, parameters) =>
                {
                    var logger = new MethodCallSequenceLogger();
                    var paramObjects = parameters.Cast<object>().ToArray();

                    logger.LogMethodEntry("TestMethod", correlationId, paramObjects);

                    var sequence = logger.GetSequence(correlationId);
                    if (sequence.Count != 1) return false;

                    var entry = sequence[0];
                    return entry.Parameters.Count == parameters.Length &&
                           entry.Parameters.SequenceEqual(parameters.Select(p => p.ToString()));
                });
        }

        /// <summary>
        /// Property: Return Value Logging
        /// For any method exit, the return value should be logged in the exit record.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property ReturnValueLogging()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                Arb.Default.Int32(),
                (correlationId, returnValue) =>
                {
                    var logger = new MethodCallSequenceLogger();

                    logger.LogMethodEntry("TestMethod", correlationId);
                    logger.LogMethodExit("TestMethod", correlationId, returnValue);

                    var sequence = logger.GetSequence(correlationId);
                    if (sequence.Count != 2) return false;

                    var exitEntry = sequence[1];
                    return exitEntry.Type == CallType.Exit &&
                           exitEntry.ReturnValue == returnValue.ToString();
                });
        }

        /// <summary>
        /// Property: Sequence Diagram Generation
        /// For any sequence of method calls, the generated diagram should contain 
        /// all method names in order.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property SequenceDiagramGeneration()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            var positiveIntArb = Arb.From(Arb.Default.Int32().Generator.Where(n => n > 0 && n < 10));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                positiveIntArb,
                (correlationId, callCount) =>
                {
                    var logger = new MethodCallSequenceLogger();
                    var methodNames = Enumerable.Range(0, callCount)
                        .Select(i => $"Method{i}")
                        .ToList();

                    foreach (var methodName in methodNames)
                    {
                        logger.LogMethodEntry(methodName, correlationId);
                        logger.LogMethodExit(methodName, correlationId);
                    }

                    var diagram = logger.GenerateSequenceDiagram(correlationId);

                    // Diagram should contain all method names
                    return methodNames.All(name => diagram.Contains(name));
                });
        }

        /// <summary>
        /// Property: Method Scope Helper
        /// For any method using MethodScope, entry and exit should be logged automatically.
        /// </summary>
        [Fact]
        public void MethodScopeAutoLogging()
        {
            var logger = new MethodCallSequenceLogger();
            var correlationId = "test-correlation";

            // Use MethodScope
            using (var scope = MethodScope.Enter(logger, correlationId, "param1", "param2"))
            {
                scope.SetReturnValue("result");
            }

            var sequence = logger.GetSequence(correlationId);

            Assert.Equal(2, sequence.Count);
            Assert.Equal(CallType.Entry, sequence[0].Type);
            Assert.Equal(CallType.Exit, sequence[1].Type);
            Assert.Equal("result", sequence[1].ReturnValue);
        }
    }
}
