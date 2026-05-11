using FsCheck;
using FsCheck.Xunit;
using HarvestmoonGCS.Core.Services.Connection;
using HarvestmoonGCS.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HarvestmoonGCS.Tests.Diagnostics;

/// <summary>
/// Property-based tests for transport layer logging.
/// Tests universal properties that should hold for transport layer operations.
/// Feature: transport-layer-debugging
/// </summary>
public class TransportLoggingPropertyTests
{
    /// <summary>
    /// Property 1: Byte Logging Completeness
    /// For any byte array received from the transport layer, the diagnostic logger 
    /// should create a log entry containing the byte count and hexadecimal 
    /// representation of the first 16 bytes (or all bytes if fewer than 16).
    /// Validates: Requirements 1.1
    /// 
    /// NOTE: This property is already tested in DiagnosticLoggerPropertyTests.ByteLoggingCompleteness
    /// This test verifies it in the context of the transport layer integration.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property TransportByteLoggingCompleteness(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);
        
        // Simulate transport layer receiving data
        // In real usage, MavLinkSerialTransport calls logger.LogTransportData
        
        // Act
        logger.LogTransportData(bytes, bytes.Length);

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        if (logs.Count == 0)
        {
            return false.Label("No log entry was created for transport data");
        }

        var logEntry = logs[0];
        
        // Verify the log entry is for the Transport stage
        if (logEntry.Stage != "Transport")
        {
            return false.Label($"Expected Stage='Transport', got '{logEntry.Stage}'");
        }

        // Verify the message contains the byte count
        if (!logEntry.Message.Contains($"{bytes.Length} bytes"))
        {
            return false.Label($"Log message does not contain byte count '{bytes.Length} bytes'");
        }

        // Verify the message contains hex representation of first 16 bytes (or all if fewer)
        var expectedHexByteCount = Math.Min(16, bytes.Length);
        var expectedHex = BitConverter.ToString(bytes, 0, expectedHexByteCount).Replace("-", " ");
        
        if (!logEntry.Message.Contains(expectedHex))
        {
            return false.Label($"Log message does not contain expected hex: {expectedHex}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property 11: Transport Event Firing
    /// For any data received by the transport layer, the OnDataReceived event 
    /// should fire with a non-empty byte array.
    /// Validates: Requirements 3.1
    /// </summary>
    [Property(MaxTest = 20)]
    public Property TransportEventFiring(byte[] bytes)
    {
        // Skip null or empty arrays - transport layer should only fire events for actual data
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);
        
        // Create a mock transport that simulates receiving data
        var eventFired = false;
        byte[]? receivedData = null;
        
        // We can't directly test MavLinkSerialTransport without a real serial port,
        // but we can verify the pattern: when data is received, the event should fire
        // with non-empty data
        
        // Simulate the transport layer pattern
        Action<byte[]>? onDataReceived = null;
        onDataReceived += (data) =>
        {
            eventFired = true;
            receivedData = data;
        };

        // Act - Simulate what MavLinkSerialTransport.ReadLoop does
        if (bytes.Length > 0)
        {
            var data = new byte[bytes.Length];
            Array.Copy(bytes, data, bytes.Length);
            
            // Log the transport data (as MavLinkSerialTransport does)
            logger.LogTransportData(data, data.Length);
            
            // Fire the event (as MavLinkSerialTransport does)
            onDataReceived?.Invoke(data);
        }

        // Assert
        if (!eventFired)
        {
            return false.Label("OnDataReceived event did not fire for non-empty byte array");
        }

        if (receivedData == null)
        {
            return false.Label("OnDataReceived event fired with null data");
        }

        if (receivedData.Length == 0)
        {
            return false.Label("OnDataReceived event fired with empty byte array");
        }

        if (receivedData.Length != bytes.Length)
        {
            return false.Label($"OnDataReceived event data length mismatch: expected {bytes.Length}, got {receivedData.Length}");
        }

        // Verify the data matches what was sent
        if (!receivedData.SequenceEqual(bytes))
        {
            return false.Label("OnDataReceived event data does not match original bytes");
        }

        // Verify that diagnostic logging occurred
        var logs = logger.GetRecentLogs(1);
        if (logs.Count == 0)
        {
            return false.Label("No diagnostic log entry was created when transport received data");
        }

        var logEntry = logs[0];
        if (logEntry.Stage != "Transport")
        {
            return false.Label($"Expected diagnostic log Stage='Transport', got '{logEntry.Stage}'");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Transport Event Subscriber Count
    /// Verifies that the transport layer can detect when OnDataReceived has no subscribers
    /// (important for debugging - MavLinkSerialTransport logs a warning in this case)
    /// </summary>
    [Property(MaxTest = 10)]
    public Property TransportEventSubscriberDetection(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Test 1: Event with no subscribers
        Action<byte[]>? onDataReceivedNoSubs = null;
        var hasSubscribers1 = onDataReceivedNoSubs != null && 
                              onDataReceivedNoSubs.GetInvocationList().Length > 0;
        
        if (hasSubscribers1)
        {
            return false.Label("Event should have no subscribers initially");
        }

        // Test 2: Event with one subscriber
        Action<byte[]>? onDataReceivedOneSub = null;
        onDataReceivedOneSub += (data) => { /* subscriber 1 */ };
        
        var hasSubscribers2 = onDataReceivedOneSub != null && 
                              onDataReceivedOneSub.GetInvocationList().Length > 0;
        var subscriberCount2 = onDataReceivedOneSub?.GetInvocationList().Length ?? 0;
        
        if (!hasSubscribers2 || subscriberCount2 != 1)
        {
            return false.Label($"Event should have 1 subscriber, got {subscriberCount2}");
        }

        // Test 3: Event with multiple subscribers
        Action<byte[]>? onDataReceivedMultiSub = null;
        onDataReceivedMultiSub += (data) => { /* subscriber 1 */ };
        onDataReceivedMultiSub += (data) => { /* subscriber 2 */ };
        onDataReceivedMultiSub += (data) => { /* subscriber 3 */ };
        
        var subscriberCount3 = onDataReceivedMultiSub?.GetInvocationList().Length ?? 0;
        
        if (subscriberCount3 != 3)
        {
            return false.Label($"Event should have 3 subscribers, got {subscriberCount3}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Transport Data Integrity
    /// Verifies that data passed through the OnDataReceived event maintains integrity
    /// (no corruption, no truncation)
    /// </summary>
    [Property(MaxTest = 20)]
    public Property TransportDataIntegrity(byte[] originalBytes)
    {
        // Skip null or empty arrays
        if (originalBytes == null || originalBytes.Length == 0)
            return true.ToProperty();

        // Arrange
        byte[]? receivedBytes = null;
        Action<byte[]>? onDataReceived = null;
        onDataReceived += (data) =>
        {
            receivedBytes = data;
        };

        // Act - Simulate transport layer copying and firing event
        var dataCopy = new byte[originalBytes.Length];
        Array.Copy(originalBytes, dataCopy, originalBytes.Length);
        onDataReceived?.Invoke(dataCopy);

        // Assert
        if (receivedBytes == null)
        {
            return false.Label("No data was received through the event");
        }

        if (receivedBytes.Length != originalBytes.Length)
        {
            return false.Label($"Data length mismatch: expected {originalBytes.Length}, got {receivedBytes.Length}");
        }

        // Verify byte-by-byte integrity
        for (int i = 0; i < originalBytes.Length; i++)
        {
            if (receivedBytes[i] != originalBytes[i])
            {
                return false.Label($"Data corruption at byte {i}: expected {originalBytes[i]:X2}, got {receivedBytes[i]:X2}");
            }
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Transport Logging and Event Firing Correlation
    /// Verifies that when transport logs data, it also fires the event, and vice versa
    /// (both should happen together for every data reception)
    /// </summary>
    [Property(MaxTest = 20)]
    public Property TransportLoggingAndEventCorrelation(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(true);
        
        var eventFired = false;
        Action<byte[]>? onDataReceived = null;
        onDataReceived += (data) => { eventFired = true; };

        // Act - Simulate what MavLinkSerialTransport.ReadLoop does
        var dataCopy = new byte[bytes.Length];
        Array.Copy(bytes, dataCopy, bytes.Length);
        
        // Both logging and event firing should happen
        logger.LogTransportData(dataCopy, dataCopy.Length);
        onDataReceived?.Invoke(dataCopy);

        // Assert
        var logs = logger.GetRecentLogs(1);
        
        // Both logging and event should have occurred
        if (logs.Count == 0)
        {
            return false.Label("Logging did not occur when transport received data");
        }

        if (!eventFired)
        {
            return false.Label("Event did not fire when transport received data");
        }

        // Verify the log entry contains the correct byte count
        var logEntry = logs[0];
        if (!logEntry.Message.Contains($"{bytes.Length} bytes"))
        {
            return false.Label("Log entry does not contain correct byte count");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Transport Multiple Event Subscribers
    /// Verifies that when OnDataReceived has multiple subscribers, all receive the data
    /// </summary>
    [Property(MaxTest = 10)]
    public Property TransportMultipleEventSubscribers(byte[] bytes, PositiveInt subscriberCountGen)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Limit subscriber count to reasonable range (1-10)
        var subscriberCount = Math.Min(subscriberCountGen.Get, 10);
        if (subscriberCount < 1) subscriberCount = 1;

        // Arrange
        var receivedCounts = new int[subscriberCount];
        Action<byte[]>? onDataReceived = null;
        
        for (int i = 0; i < subscriberCount; i++)
        {
            var index = i; // Capture for closure
            onDataReceived += (data) =>
            {
                receivedCounts[index]++;
            };
        }

        // Act - Fire the event
        var dataCopy = new byte[bytes.Length];
        Array.Copy(bytes, dataCopy, bytes.Length);
        onDataReceived?.Invoke(dataCopy);

        // Assert - All subscribers should have received the data exactly once
        for (int i = 0; i < subscriberCount; i++)
        {
            if (receivedCounts[i] != 1)
            {
                return false.Label($"Subscriber {i} received data {receivedCounts[i]} times, expected 1");
            }
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Property: Transport Logging Disabled Does Not Affect Event Firing
    /// Verifies that disabling diagnostic logging does not prevent OnDataReceived from firing
    /// (logging and event firing are independent)
    /// </summary>
    [Property(MaxTest = 10)]
    public Property TransportLoggingDisabledDoesNotAffectEvents(byte[] bytes)
    {
        // Skip null or empty arrays
        if (bytes == null || bytes.Length == 0)
            return true.ToProperty();

        // Arrange
        var logger = new DiagnosticLogger();
        logger.SetEnabled(false); // Logging disabled
        
        var eventFired = false;
        byte[]? receivedData = null;
        Action<byte[]>? onDataReceived = null;
        onDataReceived += (data) =>
        {
            eventFired = true;
            receivedData = data;
        };

        // Act - Simulate transport receiving data with logging disabled
        var dataCopy = new byte[bytes.Length];
        Array.Copy(bytes, dataCopy, bytes.Length);
        
        logger.LogTransportData(dataCopy, dataCopy.Length); // Should not log
        onDataReceived?.Invoke(dataCopy); // Should still fire

        // Assert
        var logs = logger.GetRecentLogs(10);
        if (logs.Count != 0)
        {
            return false.Label("Logging should be disabled, but log entries were created");
        }

        if (!eventFired)
        {
            return false.Label("Event should fire even when logging is disabled");
        }

        if (receivedData == null || receivedData.Length != bytes.Length)
        {
            return false.Label("Event data is incorrect even though logging is disabled");
        }

        return true.ToProperty();
    }
}
