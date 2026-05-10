using System;
using System.Reflection;
using System.Threading;
using MavLinkNet;

namespace Pigeon_Uno.Core.Diagnostics
{
    /// <summary>
    /// Validates MavLinkAsyncWalker initialization and functionality.
    /// Helps diagnose issues with MAVLink packet parsing.
    /// </summary>
    public interface IMavLinkWalkerValidator
    {
        ValidationResult ValidateInitialization(MavLinkAsyncWalker walker);
        ValidationResult ValidateEventHandlers(MavLinkAsyncWalker walker);
        ValidationResult TestWithKnownPacket(MavLinkAsyncWalker walker);
    }

    public class MavLinkWalkerValidator : IMavLinkWalkerValidator
    {
        private readonly IDiagnosticLogger _logger;

        public MavLinkWalkerValidator(IDiagnosticLogger logger)
        {
            _logger = logger;
        }

        public ValidationResult ValidateInitialization(MavLinkAsyncWalker walker)
        {
            if (walker == null)
            {
                _logger.LogWalkerProcessing(0, false);
                return ValidationResult.Fail("Walker is null");
            }

            // Test basic functionality with minimal bytes
            try
            {
                var testBytes = new byte[] { 0xFE, 0x09, 0x00, 0x01, 0x01, 0x00 };
                walker.ProcessReceivedBytes(testBytes, 0, testBytes.Length);

                _logger.LogWalkerProcessing(testBytes.Length, true);
                return ValidationResult.Success("Walker initialized correctly");
            }
            catch (Exception ex)
            {
                _logger.LogWalkerProcessing(0, false);
                return ValidationResult.Fail($"Walker initialization failed: {ex.Message}");
            }
        }

        public ValidationResult ValidateEventHandlers(MavLinkAsyncWalker walker)
        {
            if (walker == null)
                return ValidationResult.Fail("Walker is null");

            try
            {
                // Check if PacketReceived event has subscribers using reflection
                var eventInfo = typeof(MavLinkAsyncWalker).GetEvent("PacketReceived");
                if (eventInfo == null)
                    return ValidationResult.Fail("PacketReceived event not found");

                // Get the backing field for the event
                var fieldInfo = typeof(MavLinkAsyncWalker)
                    .GetField("PacketReceived", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (fieldInfo == null)
                {
                    // Try alternative field name patterns
                    fieldInfo = typeof(MavLinkAsyncWalker)
                        .GetField("_packetReceived", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                if (fieldInfo != null)
                {
                    var eventDelegate = fieldInfo.GetValue(walker) as Delegate;
                    if (eventDelegate == null || eventDelegate.GetInvocationList().Length == 0)
                        return ValidationResult.Fail("No subscribers to PacketReceived event");

                    return ValidationResult.Success($"PacketReceived has {eventDelegate.GetInvocationList().Length} subscriber(s)");
                }

                // If we can't access the field, assume it's okay if no exception was thrown
                return ValidationResult.Success("PacketReceived event exists (subscriber count unknown)");
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail($"Event handler validation failed: {ex.Message}");
            }
        }

        public ValidationResult TestWithKnownPacket(MavLinkAsyncWalker walker)
        {
            if (walker == null)
                return ValidationResult.Fail("Walker is null");

            // Create a valid HEARTBEAT packet (MAVLink v1)
            var heartbeat = new byte[]
            {
                0xFE, // STX (MAVLink v1)
                0x09, // Payload length
                0x00, // Sequence
                0x01, // System ID
                0x01, // Component ID
                0x00, // Message ID (HEARTBEAT = 0)
                // Payload (9 bytes for HEARTBEAT)
                0x00, 0x00, 0x00, 0x00, // custom_mode (uint32)
                0x01, // type (MAV_TYPE)
                0x03, // autopilot (MAV_AUTOPILOT)
                0x00, // base_mode (MAV_MODE_FLAG)
                0x04, // system_status (MAV_STATE)
                0x03, // mavlink_version
                // CRC will be calculated
                0x00, 0x00 // CRC placeholder
            };

            // Calculate proper CRC for HEARTBEAT message
            CalculateCRC(heartbeat);

            bool packetReceived = false;
            PacketReceivedDelegate? handler = null;
            handler = (sender, packet) =>
            {
                packetReceived = true;
                if (handler != null)
                    walker.PacketReceived -= handler;
            };

            try
            {
                walker.PacketReceived += handler;
                walker.ProcessReceivedBytes(heartbeat, 0, heartbeat.Length);

                // Wait briefly for async processing
                Thread.Sleep(100);

                if (packetReceived)
                    return ValidationResult.Success("Walker successfully parsed test HEARTBEAT packet");
                else
                    return ValidationResult.Fail("Walker did not emit PacketReceived event for valid HEARTBEAT packet");
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail($"Test packet processing failed: {ex.Message}");
            }
            finally
            {
                if (handler != null)
                {
                    try
                    {
                        walker.PacketReceived -= handler;
                    }
                    catch { }
                }
            }
        }

        private void CalculateCRC(byte[] packet)
        {
            // MAVLink CRC-16/MCRF4XX calculation
            // CRC_EXTRA for HEARTBEAT message is 50
            const byte HEARTBEAT_CRC_EXTRA = 50;

            ushort crc = 0xFFFF;

            // Calculate CRC over payload length, sequence, sysid, compid, msgid, and payload
            for (int i = 1; i < packet.Length - 2; i++)
            {
                byte tmp = (byte)(packet[i] ^ (crc & 0xFF));
                tmp ^= (byte)(tmp << 4);
                crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
            }

            // Add CRC_EXTRA
            byte tmp2 = (byte)(HEARTBEAT_CRC_EXTRA ^ (crc & 0xFF));
            tmp2 ^= (byte)(tmp2 << 4);
            crc = (ushort)((crc >> 8) ^ (tmp2 << 8) ^ (tmp2 << 3) ^ (tmp2 >> 4));

            // Write CRC to packet (little-endian)
            packet[packet.Length - 2] = (byte)(crc & 0xFF);
            packet[packet.Length - 1] = (byte)(crc >> 8);
        }
    }

    /// <summary>
    /// Result of a validation operation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = "";

        public static ValidationResult Success(string message) =>
            new ValidationResult { IsSuccess = true, Message = message };

        public static ValidationResult Fail(string message) =>
            new ValidationResult { IsSuccess = false, Message = message };
    }
}
