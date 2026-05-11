using System;
using System.Diagnostics;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Detects common failure modes in the MAVLink telemetry pipeline.
    /// </summary>
    public interface IFailureModeDetector
    {
        FailureMode DetectFailureMode(HealthMonitor healthMonitor, PerformanceMonitor performanceMonitor);
        string GetDiagnosticMessage(FailureMode mode);
    }

    public class FailureModeDetector : IFailureModeDetector
    {
        private readonly IDiagnosticLogger _logger;
        private DateTime _lastByteReceived = DateTime.MinValue;
        private DateTime _lastPacketParsed = DateTime.MinValue;
        private int _crcFailureCount = 0;
        private const int CRC_FAILURE_THRESHOLD = 10;

        public FailureModeDetector(IDiagnosticLogger logger)
        {
            _logger = logger;
        }

        public void RecordByteReception()
        {
            _lastByteReceived = DateTime.Now;
        }

        public void RecordPacketParsed()
        {
            _lastPacketParsed = DateTime.Now;
        }

        public void RecordCrcFailure()
        {
            _crcFailureCount++;
        }

        public FailureMode DetectFailureMode(HealthMonitor healthMonitor, PerformanceMonitor performanceMonitor)
        {
            var now = DateTime.Now;

            // Check for disconnected hardware (no bytes for 5+ seconds)
            if (_lastByteReceived != DateTime.MinValue &&
                (now - _lastByteReceived).TotalSeconds > 5)
            {
                _logger.LogFlightDataUpdate("FailureMode", "None", "DisconnectedHardware");
                return FailureMode.DisconnectedHardware;
            }

            // Check for wrong baud rate (bytes received but no valid packets)
            if (_lastByteReceived != DateTime.MinValue &&
                _lastPacketParsed == DateTime.MinValue &&
                (now - _lastByteReceived).TotalSeconds < 5)
            {
                _logger.LogFlightDataUpdate("FailureMode", "None", "WrongBaudRate");
                return FailureMode.WrongBaudRate;
            }

            // Check for corrupted packets (high CRC failure rate)
            if (_crcFailureCount > CRC_FAILURE_THRESHOLD)
            {
                _logger.LogFlightDataUpdate("FailureMode", "None", "CorruptedPackets");
                return FailureMode.CorruptedPackets;
            }

            // Check for parser failure (bytes and packets but no telemetry)
            var transportHealth = healthMonitor.GetTransportHealth();
            var parserHealth = healthMonitor.GetParserHealth();
            if (transportHealth.Level == HealthStatusLevel.Healthy &&
                parserHealth.Level == HealthStatusLevel.Critical)
            {
                _logger.LogFlightDataUpdate("FailureMode", "None", "ParserFailure");
                return FailureMode.ParserFailure;
            }

            return FailureMode.None;
        }

        public string GetDiagnosticMessage(FailureMode mode)
        {
            return mode switch
            {
                FailureMode.DisconnectedHardware => 
                    "No data received for 5+ seconds. Check:\n" +
                    "- Hardware connection (USB cable, serial port)\n" +
                    "- Port name is correct\n" +
                    "- Pixhawk is powered on\n" +
                    "- No other application is using the port",

                FailureMode.WrongBaudRate => 
                    "Receiving bytes but no valid packets. Check:\n" +
                    "- Baud rate matches Pixhawk configuration (usually 57600 or 115200)\n" +
                    "- Serial port settings (8N1)\n" +
                    "- MAVLink protocol version matches",

                FailureMode.CorruptedPackets => 
                    "High rate of CRC failures. Check:\n" +
                    "- Cable quality (try different cable)\n" +
                    "- Electromagnetic interference\n" +
                    "- Baud rate too high for cable/hardware\n" +
                    "- Ground loop issues",

                FailureMode.ParserFailure => 
                    "Packets received but not parsed correctly. Check:\n" +
                    "- MAVLink message definitions match Pixhawk firmware\n" +
                    "- Parser initialization\n" +
                    "- Event handler connections",

                _ => "No failure detected. System operating normally."
            };
        }
    }

    public enum FailureMode
    {
        None,
        DisconnectedHardware,
        WrongBaudRate,
        CorruptedPackets,
        ParserFailure
    }
}
