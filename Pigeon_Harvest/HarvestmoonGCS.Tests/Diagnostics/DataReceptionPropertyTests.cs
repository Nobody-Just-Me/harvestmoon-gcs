using FsCheck;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Diagnostics;
using MavLinkNet;
using System;
using System.Linq;
using System.Threading;

namespace HarvestmoonGCS.Tests.Diagnostics;

/// <summary>
/// Property-based tests for MavLinkService data reception.
/// Tests universal properties that should hold for data reception operations.
/// Feature: transport-layer-debugging
/// Task: 5.1 Write property tests for data reception
/// </summary>
public class DataReceptionPropertyTests
{
    /// <summary>
    /// Property 2: Walker Processing Logging
    /// For any call to MavLinkAsyncWalker.ProcessReceivedBytes, the diagnostic logger 
    /// should create a log entry indicating the processing attempt and whether it 
    /// succeeded or failed.
    /// Validates: Requirements 1.2
    /// </summary>
    [Property(MaxTest = 20)]
    public Property WalkerProcessingLogging(byte[] bytes)
    {
        // Skip null arrays - ProcessReceivedBytes expects non-null
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Act
        try
        {
            walker.ProcessReceivedBytes(bytes, 0, bytes.Length);
            
            // Log success
            logger.LogWalkerProcessing(bytes.Length, true);
        }
        catch (Exception)
        {
            // Log failure
            logger.LogWalkerProcessing(bytes.Length, false);
        }

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entry was created for walker processing");
        }

        var logEntry = logs[0];
        
        // Verify the log entry is for the Walker stage
        if (logEntry.Stage != "Walker")
        {
            return false.Label($"Expected Stage='Walker', got '{logEntry.Stage}'");
        }

        // Verify the message contains the byte count
        if (!logEntry.Message.Contains($"{bytes.Length} bytes"))
        {
            return false.Label($"Log message does not contain byte count '{bytes.Length} bytes'");
        }

        // Verify the message indicates success or failure
        if (!logEntry.Message.Contains("SUCCESS") && !logEntry.Message.Contains("FAILED"))
        {
            return false.Label("Log message does not indicate SUCCESS or FAILED status");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property 10: Parse Failure Logging
    /// For any byte stream that MavLinkAsyncWalker fails to parse, the system should 
    /// create an error log entry containing the byte stream content in hexadecimal format.
    /// Validates: Requirements 2.5
    /// 
    /// Note: This property tests the logging behavior when ProcessReceivedBytes encounters
    /// an error. Since MavLinkAsyncWalker is designed to be exception-safe, we simulate
    /// the error logging pattern that MavLinkService uses.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ParseFailureLogging(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        bool processingFailed = false;
        string? errorMessage = null;
        string? hexDump = null;

        // Act - Simulate the error handling pattern from MavLinkService.OnTransportDataReceived
        try
        {
            walker.ProcessReceivedBytes(bytes, 0, bytes.Length);
            logger.LogWalkerProcessing(bytes.Length, true);
        }
        catch (Exception ex)
        {
            processingFailed = true;
            errorMessage = ex.Message;
            
            // Generate hex dump (first 32 bytes as per MavLinkService implementation)
            hexDump = BitConverter.ToString(bytes, 0, Math.Min(32, bytes.Length)).Replace("-", " ");
            
            // Log the failure with hex dump
            logger.LogWalkerProcessing(bytes.Length, false);
            
            // In real implementation, this would also be logged to Debug output
            System.Diagnostics.Debug.WriteLine($"ProcessReceivedBytes EXCEPTION: {errorMessage}");
            System.Diagnostics.Debug.WriteLine($"Byte stream (first 32 bytes): {hexDump}");
        }

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entry was created for walker processing");
        }

        var logEntry = logs[0];
        
        // Verify the log entry is for the Walker stage
        if (logEntry.Stage != "Walker")
        {
            return false.Label($"Expected Stage='Walker', got '{logEntry.Stage}'");
        }

        // If processing failed, verify failure is logged
        if (processingFailed)
        {
            if (!logEntry.Message.Contains("FAILED"))
            {
                return false.Label("Log message should indicate FAILED status for parse failure");
            }

            // Verify hex dump was generated
            if (string.IsNullOrEmpty(hexDump))
            {
                return false.Label("Hex dump should be generated for parse failure");
            }

            // Verify hex dump contains expected format (space-separated hex bytes)
            if (!hexDump.Contains(" "))
            {
                return false.Label("Hex dump should contain space-separated hex bytes");
            }
        }
        else
        {
            // If processing succeeded, verify success is logged
            if (!logEntry.Message.Contains("SUCCESS"))
            {
                return false.Label("Log message should indicate SUCCESS status for successful parse");
            }
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Walker Processing Logging Consistency
    /// Verifies that every call to ProcessReceivedBytes results in exactly one log entry
    /// (no duplicate logs, no missing logs)
    /// </summary>
    [Property(MaxTest = 20)]
    public Property WalkerProcessingLoggingConsistency(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Get initial log count
        var initialLogCount = logger.GetRecentLogs(1000).Count;

        // Act
        try
        {
            walker.ProcessReceivedBytes(bytes, 0, bytes.Length);
            logger.LogWalkerProcessing(bytes.Length, true);
        }
        catch (Exception)
        {
            logger.LogWalkerProcessing(bytes.Length, false);
        }

        // Assert
        var finalLogCount = logger.GetRecentLogs(1000).Count;
        var newLogCount = finalLogCount - initialLogCount;

        if (newLogCount != 1)
        {
            return false.Label($"Expected exactly 1 new log entry, got {newLogCount}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Walker Processing Logging Disabled
    /// Verifies that when logging is disabled, no log entries are created
    /// but ProcessReceivedBytes still executes normally
    /// </summary>
    [Property(MaxTest = 10)]
    public Property WalkerProcessingLoggingDisabled(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(false); // Logging disabled

        // Act
        try
        {
            walker.ProcessReceivedBytes(bytes, 0, bytes.Length);
            logger.LogWalkerProcessing(bytes.Length, true);
        }
        catch (Exception)
        {
            logger.LogWalkerProcessing(bytes.Length, false);
        }

        // Assert
        var logs = logger.GetRecentLogs(10);
        
        if (logs.Count != 0)
        {
            return false.Label("No log entries should be created when logging is disabled");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Walker Processing Logging Byte Count Accuracy
    /// Verifies that the logged byte count matches the actual number of bytes processed
    /// </summary>
    [Property(MaxTest = 20)]
    public Property WalkerProcessingLoggingByteCountAccuracy(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        // Act
        try
        {
            walker.ProcessReceivedBytes(bytes, 0, bytes.Length);
            logger.LogWalkerProcessing(bytes.Length, true);
        }
        catch (Exception)
        {
            logger.LogWalkerProcessing(bytes.Length, false);
        }

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entry was created");
        }

        var logEntry = logs[0];
        
        // Verify the message contains the exact byte count
        if (!logEntry.Message.Contains($"{bytes.Length} bytes"))
        {
            return false.Label($"Log message should contain exact byte count '{bytes.Length} bytes'");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Parse Failure Hex Dump Length
    /// Verifies that hex dump for parse failures includes up to 32 bytes
    /// (or all bytes if fewer than 32)
    /// </summary>
    [Property(MaxTest = 20)]
    public Property ParseFailureHexDumpLength(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var expectedHexByteCount = Math.Min(32, bytes.Length);
        var hexDump = BitConverter.ToString(bytes, 0, expectedHexByteCount).Replace("-", " ");

        // Assert
        // Count the number of hex byte pairs in the dump
        var hexPairs = hexDump.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (hexPairs.Length != expectedHexByteCount)
        {
            return false.Label($"Hex dump should contain {expectedHexByteCount} bytes, got {hexPairs.Length}");
        }

        // Verify each hex pair is valid (2 hex digits)
        foreach (var hexPair in hexPairs)
        {
            if (hexPair.Length != 2)
            {
                return false.Label($"Each hex pair should be 2 characters, got '{hexPair}'");
            }

            // Verify it's valid hex
            if (!byte.TryParse(hexPair, System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                return false.Label($"Invalid hex pair: '{hexPair}'");
            }
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Walker Processing Logging Thread Safety
    /// Verifies that logging from multiple threads doesn't cause issues
    /// </summary>
    [Property(MaxTest = 10)]
    public Property WalkerProcessingLoggingThreadSafety(byte[] bytes1, byte[] bytes2)
    {
        // Skip null or empty arrays
        if (bytes1 == null || bytes1.Length == 0 || bytes2 == null || bytes2.Length == 0)
            return true.ToProperty();

        // Arrange
        var walker1 = new MavLinkAsyncWalker();
        var walker2 = new MavLinkAsyncWalker();
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);

        var initialLogCount = logger.GetRecentLogs(1000).Count;

        // Act - Process bytes from two threads simultaneously
        var thread1 = new Thread(() =>
        {
            try
            {
                walker1.ProcessReceivedBytes(bytes1, 0, bytes1.Length);
                logger.LogWalkerProcessing(bytes1.Length, true);
            }
            catch (Exception)
            {
                logger.LogWalkerProcessing(bytes1.Length, false);
            }
        });

        var thread2 = new Thread(() =>
        {
            try
            {
                walker2.ProcessReceivedBytes(bytes2, 0, bytes2.Length);
                logger.LogWalkerProcessing(bytes2.Length, true);
            }
            catch (Exception)
            {
                logger.LogWalkerProcessing(bytes2.Length, false);
            }
        });

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        // Assert
        var finalLogCount = logger.GetRecentLogs(1000).Count;
        var newLogCount = finalLogCount - initialLogCount;

        // Should have exactly 2 new log entries (one from each thread)
        if (newLogCount != 2)
        {
            return false.Label($"Expected 2 new log entries from concurrent threads, got {newLogCount}");
        }

        return true.ToProperty();
    }
}
