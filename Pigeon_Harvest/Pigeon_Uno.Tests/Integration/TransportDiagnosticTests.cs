using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Pigeon_Uno.Core.Services.Connection;
using Pigeon_Uno.Core.Diagnostics;

namespace Pigeon_Uno.Tests.Integration
{
    /// <summary>
    /// Integration tests for transport layer diagnostic logging.
    /// Verifies that diagnostic logs appear when data is received from transport.
    /// </summary>
    public class TransportDiagnosticTests
    {
        [Fact]
        public void DiagnosticLogger_LogsTransportData_WhenBytesReceived()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(true);
            
            var testData = new byte[] { 0xFE, 0x09, 0x00, 0x01, 0x01, 0x00, 0x01, 0x02, 0x03 };
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            
            // Assert
            var logs = logger.GetRecentLogs(10);
            Assert.NotEmpty(logs);
            Assert.Contains(logs, log => 
                log.Stage == "Transport" && 
                log.Message.Contains($"{testData.Length} bytes"));
        }
        
        [Fact]
        public void DiagnosticLogger_IncludesHexDump_InTransportLogs()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(true);
            
            var testData = new byte[] { 0xFE, 0x09, 0x00, 0x01 };
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            
            // Assert
            var logs = logger.GetRecentLogs(10);
            var transportLog = logs.Find(log => log.Stage == "Transport");
            
            Assert.NotNull(transportLog);
            // Should contain hex representation (FE 09 00 01)
            Assert.Contains("FE", transportLog.Message);
        }
        
        [Fact]
        public void DiagnosticLogger_LogsFirst16Bytes_WhenMoreBytesReceived()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(true);
            
            // Create 32 bytes of test data
            var testData = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                testData[i] = (byte)i;
            }
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            
            // Assert
            var logs = logger.GetRecentLogs(10);
            var transportLog = logs.Find(log => log.Stage == "Transport");
            
            Assert.NotNull(transportLog);
            Assert.Contains("32 bytes", transportLog.Message);
            // Should only log first 16 bytes (0-15), not all 32
            Assert.Contains("00", transportLog.Message); // First byte
            Assert.Contains("0F", transportLog.Message); // 16th byte (index 15)
        }
        
        [Fact]
        public void DiagnosticLogger_DoesNotLog_WhenDisabled()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(false);
            
            var testData = new byte[] { 0xFE, 0x09, 0x00, 0x01 };
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            
            // Assert
            var logs = logger.GetRecentLogs(10);
            Assert.Empty(logs);
        }
        
        [Fact]
        public void DiagnosticLogger_LogsTimestamp_WithMillisecondPrecision()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(true);
            
            var testData = new byte[] { 0xFE };
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            
            // Assert
            var logs = logger.GetRecentLogs(10);
            var transportLog = logs.Find(log => log.Stage == "Transport");
            
            Assert.NotNull(transportLog);
            // Timestamp should have millisecond precision
            Assert.NotEqual(DateTime.MinValue, transportLog.Timestamp);
            
            // ToString format should include milliseconds (HH:mm:ss.fff)
            var timestampStr = transportLog.ToString();
            Assert.Matches(@"\d{2}:\d{2}:\d{2}\.\d{3}", timestampStr);
        }
        
        [Fact]
        public void DiagnosticLogger_GetLogSummary_ShowsTransportStage()
        {
            // Arrange
            var logger = new DiagnosticLogger();
            logger.SetEnabled(true);
            
            var testData = new byte[] { 0xFE, 0x09 };
            
            // Act
            logger.LogTransportData(testData, testData.Length);
            logger.LogTransportData(testData, testData.Length);
            logger.LogTransportData(testData, testData.Length);
            
            var summary = logger.GetLogSummary();
            
            // Assert
            Assert.Contains("Transport:", summary);
            Assert.Contains("3", summary); // Should show 3 transport entries
        }
    }
}
