using System;
using FsCheck;
using FsCheck.Xunit;
using Pigeon_Uno.Core.Diagnostics;
using Xunit;

namespace Pigeon_Uno.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for error recovery functionality.
    /// </summary>
    public class ErrorRecoveryPropertyTests
    {
        /// <summary>
        /// Property 36: Exception Handling Safety
        /// For any exception thrown in an event handler, the system should catch it, 
        /// log it, and continue processing subsequent events without crashing.
        /// Validates: Requirements 10.2
        /// </summary>
        [Property(MaxTest = 20)]
        public Property ExceptionHandlingSafety()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                Arb.Default.Bool(),
                (errorMessage, shouldThrow) =>
                {
                    var logger = new DiagnosticLogger();
                    bool actionExecuted = false;
                    bool exceptionThrown = false;

                    ErrorRecoveryHelper.ExecuteWithRecovery(() =>
                    {
                        actionExecuted = true;
                        if (shouldThrow)
                        {
                            exceptionThrown = true;
                            throw new InvalidOperationException(errorMessage);
                        }
                    }, "TestContext", logger);

                    // Action should always execute
                    if (!actionExecuted) return false;

                    // If exception was thrown, we should still be here (not crashed)
                    return true;
                });
        }

        /// <summary>
        /// Property: Default Value Return on Exception
        /// For any function that throws an exception, ExecuteWithRecovery should 
        /// return the specified default value.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property DefaultValueReturnOnException()
        {
            return Prop.ForAll(
                Arb.Default.Int32(),
                Arb.Default.Int32(),
                Arb.Default.Bool(),
                (expectedValue, defaultValue, shouldThrow) =>
                {
                    var logger = new DiagnosticLogger();

                    var result = ErrorRecoveryHelper.ExecuteWithRecovery(() =>
                    {
                        if (shouldThrow)
                            throw new InvalidOperationException("Test exception");
                        return expectedValue;
                    }, defaultValue, "TestContext", logger);

                    // If exception thrown, should return default value
                    // If no exception, should return expected value
                    return shouldThrow ? result == defaultValue : result == expectedValue;
                });
        }

        /// <summary>
        /// Property: Event Handler Wrapping
        /// For any event handler wrapped with error recovery, exceptions should not 
        /// propagate to the caller.
        /// </summary>
        [Fact]
        public void EventHandlerWrappingSafety()
        {
            var logger = new DiagnosticLogger();
            bool handlerExecuted = false;

            EventHandler<string> throwingHandler = (sender, args) =>
            {
                handlerExecuted = true;
                throw new InvalidOperationException("Test exception");
            };

            var wrappedHandler = ErrorRecoveryHelper.WrapEventHandler(throwingHandler, "TestHandler", logger);

            // Should not throw
            var exception = Record.Exception(() => wrappedHandler(this, "test"));

            Assert.Null(exception);
            Assert.True(handlerExecuted);
        }

        /// <summary>
        /// Property: Logging on Exception
        /// For any exception caught by error recovery, a log entry should be created.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property LoggingOnException()
        {
            var nonEmptyStringArb = Arb.From(Arb.Default.String().Generator.Where(s => !string.IsNullOrWhiteSpace(s)));
            
            return Prop.ForAll(
                nonEmptyStringArb,
                errorMessage =>
                {
                    var logger = new DiagnosticLogger();
                    logger.SetEnabled(true);

                    ErrorRecoveryHelper.ExecuteWithRecovery(() =>
                    {
                        throw new InvalidOperationException(errorMessage);
                    }, "TestContext", logger);

                    var logs = logger.GetRecentLogs(10);
                    return logs.Count > 0;
                });
        }
    }
}
