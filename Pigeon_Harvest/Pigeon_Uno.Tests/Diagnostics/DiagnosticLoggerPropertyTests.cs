using FsCheck;
using FsCheck.Xunit;
using Pigeon_Uno.Core.Diagnostics;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pigeon_Uno.Tests.Diagnostics;

/// <summary>
/// Property-based tests for DiagnosticLogger.
/// Tests universal properties that should hold across all inputs.
/// Feature: transport-layer-debugging
/// </summary>
public class DiagnosticLoggerPropertyTests
{
    /// <summary>
    /// Property 1: Byte Logging Completeness
    /// For any byte array received from the transport layer, the diagnostic logger 
    /// should create a log entry containing the byte count and hexadecimal 
    /// representation of the first 16 bytes (or all bytes if fewer than 16).
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ByteLoggingCompleteness(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Act
        logger.LogTransportData(bytes, bytes.Length);

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entry was created");
        }

        var logEntry = logs[0];
        
        // Check that the log entry is for the Transport stage
        if (logEntry.Stage != "Transport")
        {
            return false.Label($"Expected Stage='Transport', got '{logEntry.Stage}'");
        }

        // Check that the message contains the byte count
        if (!logEntry.Message.Contains($"{bytes.Length} bytes"))
        {
            return false.Label($"Log message does not contain byte count '{bytes.Length} bytes'");
        }

        // Check that the message contains hex representation
        // For arrays with 16 or more bytes, should show first 16
        // For arrays with fewer than 16 bytes, should show all
        var expectedHexByteCount = Math.Min(16, bytes.Length);
        var expectedHex = BitConverter.ToString(bytes, 0, expectedHexByteCount).Replace("-", " ");
        
        if (!logEntry.Message.Contains(expectedHex))
        {
            return false.Label($"Log message does not contain expected hex: {expectedHex}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property 6: Timestamp Precision
    /// For any log entry created by the diagnostic logger, the timestamp should 
    /// have millisecond precision (format: yyyy-MM-dd HH:mm:ss.fff).
    /// Validates: Requirements 1.7
    /// </summary>
    [Property(MaxTest = 20)]
    public Property TimestampPrecision(byte[] bytes, int messageId, bool success, NonEmptyString fieldNameGen)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0 || messageId <= 0)
            return true.ToProperty();

        var fieldName = fieldNameGen?.Get ?? "TestField";

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Act - Test multiple logging methods
        var beforeLog = DateTime.Now;
        logger.LogTransportData(bytes, bytes.Length);
        logger.LogWalkerProcessing(bytes.Length, success);
        logger.LogPacketParsed(messageId, 0, 1);
        logger.LogFlightDataUpdate(fieldName, 0, 1);
        logger.LogTelemetryEvent(DateTime.Now, "Test summary");
        logger.LogUIUpdate("TestControl", "TestProperty", "TestValue");
        var afterLog = DateTime.Now;

        // Assert
        var logs = logger.GetRecentLogs(10);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entries were created");
        }

        foreach (var logEntry in logs)
        {
            // Check that timestamp is within reasonable range
            if (logEntry.Timestamp < beforeLog.AddSeconds(-1) || 
                logEntry.Timestamp > afterLog.AddSeconds(1))
            {
                return false.Label($"Timestamp {logEntry.Timestamp} is outside expected range");
            }

            // Check that timestamp has millisecond precision by formatting it
            var formattedTimestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            // Verify the format matches the expected pattern
            var timestampPattern = @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}$";
            if (!Regex.IsMatch(formattedTimestamp, timestampPattern))
            {
                return false.Label($"Timestamp format '{formattedTimestamp}' does not match expected pattern");
            }

            // Verify that the ToString() method uses millisecond precision
            var logString = logEntry.ToString();
            var timestampInLog = logEntry.Timestamp.ToString("HH:mm:ss.fff");
            if (!logString.Contains(timestampInLog))
            {
                return false.Label($"Log string does not contain millisecond-precision timestamp");
            }
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property 7: Logging Toggle
    /// For any logging operation, when logging is disabled via configuration, 
    /// no log entries should be created; when enabled, log entries should be 
    /// created normally.
    /// Validates: Requirements 1.8
    /// </summary>
    [Property(MaxTest = 20)]
    public Property LoggingToggle(byte[] bytes, int messageId, bool walkerSuccess, NonEmptyString fieldNameGen)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0 || messageId <= 0)
            return true.ToProperty();

        var fieldName = fieldNameGen?.Get ?? "TestField";

        // Test 1: Logging disabled - no entries should be created
        var loggerDisabled = new DiagnosticLogger();
        loggerDisabled.SetEnabled(false);

        loggerDisabled.LogTransportData(bytes, bytes.Length);
        loggerDisabled.LogWalkerProcessing(bytes.Length, walkerSuccess);
        loggerDisabled.LogPacketParsed(messageId, 0, 1);
        loggerDisabled.LogFlightDataUpdate(fieldName, 0, 1);
        loggerDisabled.LogTelemetryEvent(DateTime.Now, "Test summary");
        loggerDisabled.LogUIUpdate("TestControl", "TestProperty", "TestValue");

        var logsDisabled = loggerDisabled.GetRecentLogs(100);
        if (logsDisabled.Count != 0)
        {
            return false.Label($"Expected 0 log entries when disabled, got {logsDisabled.Count}");
        }

        // Test 2: Logging enabled - entries should be created
        var loggerEnabled = new DiagnosticLogger();
        loggerEnabled.SetEnabled(true);

        loggerEnabled.LogTransportData(bytes, bytes.Length);
        loggerEnabled.LogWalkerProcessing(bytes.Length, walkerSuccess);
        loggerEnabled.LogPacketParsed(messageId, 0, 1);
        loggerEnabled.LogFlightDataUpdate(fieldName, 0, 1);
        loggerEnabled.LogTelemetryEvent(DateTime.Now, "Test summary");
        loggerEnabled.LogUIUpdate("TestControl", "TestProperty", "TestValue");

        var logsEnabled = loggerEnabled.GetRecentLogs(100);
        if (logsEnabled.Count != 6)
        {
            return false.Label($"Expected 6 log entries when enabled, got {logsEnabled.Count}");
        }

        // Test 3: Toggle from enabled to disabled
        var loggerToggle = new DiagnosticLogger();
        loggerToggle.SetEnabled(true);
        loggerToggle.LogTransportData(bytes, bytes.Length);
        
        var logsAfterFirstLog = loggerToggle.GetRecentLogs(100);
        if (logsAfterFirstLog.Count != 1)
        {
            return false.Label($"Expected 1 log entry after first log, got {logsAfterFirstLog.Count}");
        }

        loggerToggle.SetEnabled(false);
        loggerToggle.LogTransportData(bytes, bytes.Length);
        
        var logsAfterDisable = loggerToggle.GetRecentLogs(100);
        if (logsAfterDisable.Count != 1)
        {
            return false.Label($"Expected 1 log entry after disabling (no new entries), got {logsAfterDisable.Count}");
        }

        // Test 4: Toggle from disabled to enabled
        loggerToggle.SetEnabled(true);
        loggerToggle.LogTransportData(bytes, bytes.Length);
        
        var logsAfterReEnable = loggerToggle.GetRecentLogs(100);
        if (logsAfterReEnable.Count != 2)
        {
            return false.Label($"Expected 2 log entries after re-enabling, got {logsAfterReEnable.Count}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Additional test: Verify that all logging methods respect the enabled flag
    /// </summary>
    [Property(MaxTest = 10)]
    public Property AllLoggingMethodsRespectEnabledFlag(bool enabled)
    {
        var logger = new DiagnosticLogger();
        logger.SetEnabled(enabled);

        // Call all logging methods
        logger.LogTransportData(new byte[] { 0x01, 0x02 }, 2);
        logger.LogWalkerProcessing(10, true);
        logger.LogPacketParsed(30, 1, 1);
        logger.LogFlightDataUpdate("Roll", 0.0, 1.5);
        logger.LogTelemetryEvent(DateTime.Now, "Test");
        logger.LogUIUpdate("Attitude", "Roll", 1.5);

        var logs = logger.GetRecentLogs(100);
        var expectedCount = enabled ? 6 : 0;

        return (logs.Count == expectedCount)
            .Label($"Expected {expectedCount} logs when enabled={enabled}, got {logs.Count}");
    }

    /// <summary>
    /// Additional test: Verify log entry structure for all logging methods
    /// </summary>
    [Property(MaxTest = 10)]
    public Property AllLogEntriesHaveRequiredFields(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Log using all methods
        logger.LogTransportData(bytes, bytes.Length);
        logger.LogWalkerProcessing(bytes.Length, true);
        logger.LogPacketParsed(30, 1, 1);
        logger.LogFlightDataUpdate("Roll", 0.0, 1.5);
        logger.LogTelemetryEvent(DateTime.Now, "Test");
        logger.LogUIUpdate("Attitude", "Roll", 1.5);

        var logs = logger.GetRecentLogs(100);

        foreach (var log in logs)
        {
            // Every log entry must have a timestamp
            if (log.Timestamp == default(DateTime))
            {
                return false.Label("Log entry has default timestamp");
            }

            // Every log entry must have a non-empty stage
            if (string.IsNullOrWhiteSpace(log.Stage))
            {
                return false.Label("Log entry has empty stage");
            }

            // Every log entry must have a non-empty message
            if (string.IsNullOrWhiteSpace(log.Message))
            {
                return false.Label("Log entry has empty message");
            }
        }

        return true.ToProperty();
    }
}
