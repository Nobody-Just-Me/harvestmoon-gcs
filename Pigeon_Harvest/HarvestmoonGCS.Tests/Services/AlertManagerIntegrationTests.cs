using System;
using System.Threading.Tasks;
using Xunit;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Tests.Services;

/// <summary>
/// Tests for AlertManager integration with MAVLink service
/// </summary>
public class AlertManagerIntegrationTests
{
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000, int pollMs = 25)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(pollMs);
        }

        return condition();
    }

    private class MockSpeechService : ISpeechService
    {
        public bool IsInitialized { get; private set; }
        public string LastSpokenText { get; private set; } = "";
        public bool LastInterruptFlag { get; private set; }
        public int SpeakCount { get; private set; }

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task SpeakAsync(string text)
        {
            LastSpokenText = text;
            LastInterruptFlag = false;
            SpeakCount++;
            return Task.CompletedTask;
        }

        public Task SpeakAsync(string text, bool interrupt)
        {
            LastSpokenText = text;
            LastInterruptFlag = interrupt;
            SpeakCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task BatteryWarning_TriggeredWhenBelowThreshold()
    {
        // Arrange
        var mockSpeech = new MockSpeechService();
        var alertManager = new AlertManager(mockSpeech);
        alertManager.Settings.BatteryWarningThreshold = 20;
        await alertManager.InitializeAsync();

        var mavLinkService = new MavLinkService();
        mavLinkService.EnterPlaybackMode();

        bool batteryWarningTriggered = false;
        int triggeredBatteryPercent = 0;

        // Subscribe to telemetry and check battery
        mavLinkService.TelemetryReceived += (sender, flightData) =>
        {
            if (flightData.MavlinkMiliVolt > 0)
            {
                float voltage = flightData.MavlinkMiliVolt / 1000.0f;
                int batteryPercent = (int)((voltage - 14.0f) / (16.8f - 14.0f) * 100);
                batteryPercent = Math.Clamp(batteryPercent, 0, 100);

                if (batteryPercent <= alertManager.Settings.BatteryWarningThreshold)
                {
                    batteryWarningTriggered = true;
                    triggeredBatteryPercent = batteryPercent;
                    _ = alertManager.QueueBatteryWarningAsync(batteryPercent);
                }
            }
        };

        // Act - Simulate low battery telemetry using SYS_STATUS packet
        var sysStatusPacket = CreateSysStatusPacket(14500);
        mavLinkService.InjectPacket(sysStatusPacket);

        // Wait for alert to be processed
        var processed = await WaitUntilAsync(() => mockSpeech.SpeakCount > 0);

        // Assert
        Assert.True(processed);
        Assert.True(batteryWarningTriggered);
        Assert.True(triggeredBatteryPercent <= 20);
        Assert.Contains("Battery low", mockSpeech.LastSpokenText);
    }

    [Fact]
    public async Task GpsLostAlert_TriggeredWhenSatellitesBelow6()
    {
        // Arrange
        var mockSpeech = new MockSpeechService();
        var alertManager = new AlertManager(mockSpeech);
        await alertManager.InitializeAsync();

        var mavLinkService = new MavLinkService();
        mavLinkService.EnterPlaybackMode();

        bool gpsLostTriggered = false;

        // Subscribe to telemetry and check GPS
        mavLinkService.TelemetryReceived += (sender, flightData) =>
        {
            bool gpsLost = flightData.GPS.Sats < 6;
            if (gpsLost)
            {
                gpsLostTriggered = true;
                _ = alertManager.QueueGpsLostAsync();
            }
        };

        // Act - Simulate GPS lost using GPS_RAW_INT packet (3 satellites)
        var gpsPacket = CreateGpsRawIntPacket(3);
        mavLinkService.InjectPacket(gpsPacket);

        // Wait for alert to be processed
        var processed = await WaitUntilAsync(() => mockSpeech.SpeakCount > 0);

        // Assert
        Assert.True(processed);
        Assert.True(gpsLostTriggered);
        Assert.Contains("GPS signal lost", mockSpeech.LastSpokenText);
    }

    [Fact]
    public async Task ConnectionLostAlert_TriggeredWhenDisconnected()
    {
        // Arrange
        var mockSpeech = new MockSpeechService();
        var alertManager = new AlertManager(mockSpeech);
        await alertManager.InitializeAsync();

        var mavLinkService = new MavLinkService();

        bool connectionLostTriggered = false;

        // Subscribe to connection status
        mavLinkService.ConnectionStatusChanged += (sender, isConnected) =>
        {
            if (!isConnected)
            {
                connectionLostTriggered = true;
                _ = alertManager.QueueConnectionLostAsync();
            }
        };

        // Act - Simulate connection by connecting then disconnecting
        await mavLinkService.ConnectAsync(Core.Models.ConnectionType.UDP, "127.0.0.1", 14550);
        await mavLinkService.DisconnectAsync();

        // Wait for alert to be processed
        var processed = await WaitUntilAsync(() => mockSpeech.SpeakCount > 0);

        // Assert
        Assert.True(processed);
        Assert.True(connectionLostTriggered);
        Assert.Contains("Connection lost", mockSpeech.LastSpokenText);
    }

    [Fact]
    public async Task BatteryWarning_NotTriggeredWhenAboveThreshold()
    {
        // Arrange
        var mockSpeech = new MockSpeechService();
        var alertManager = new AlertManager(mockSpeech);
        alertManager.Settings.BatteryWarningThreshold = 20;
        await alertManager.InitializeAsync();

        var mavLinkService = new MavLinkService();
        mavLinkService.EnterPlaybackMode();

        bool batteryWarningTriggered = false;

        // Subscribe to telemetry and check battery
        mavLinkService.TelemetryReceived += (sender, flightData) =>
        {
            if (flightData.MavlinkMiliVolt > 0)
            {
                float voltage = flightData.MavlinkMiliVolt / 1000.0f;
                int batteryPercent = (int)((voltage - 14.0f) / (16.8f - 14.0f) * 100);
                batteryPercent = Math.Clamp(batteryPercent, 0, 100);

                if (batteryPercent <= alertManager.Settings.BatteryWarningThreshold)
                {
                    batteryWarningTriggered = true;
                    _ = alertManager.QueueBatteryWarningAsync(batteryPercent);
                }
            }
        };

        // Act - Simulate good battery using SYS_STATUS packet (16V = ~71% for 4S LiPo)
        var sysStatusPacket = CreateSysStatusPacket(16000); // 16V
        mavLinkService.InjectPacket(sysStatusPacket);

        // Wait
        await Task.Delay(100);

        // Assert
        Assert.False(batteryWarningTriggered);
    }

    [Fact]
    public async Task GpsLostAlert_NotTriggeredWhenSatellitesAbove6()
    {
        // Arrange
        var mockSpeech = new MockSpeechService();
        var alertManager = new AlertManager(mockSpeech);
        await alertManager.InitializeAsync();

        var mavLinkService = new MavLinkService();
        mavLinkService.EnterPlaybackMode();

        bool gpsLostTriggered = false;

        // Subscribe to telemetry and check GPS
        mavLinkService.TelemetryReceived += (sender, flightData) =>
        {
            bool gpsLost = flightData.GPS.Sats < 6;
            if (gpsLost)
            {
                gpsLostTriggered = true;
                _ = alertManager.QueueGpsLostAsync();
            }
        };

        // Act - Simulate good GPS using GPS_RAW_INT packet (10 satellites)
        var gpsPacket = CreateGpsRawIntPacket(10);
        mavLinkService.InjectPacket(gpsPacket);

        // Wait
        await Task.Delay(100);

        // Assert
        Assert.False(gpsLostTriggered);
    }

    // Helper class to create test packets
    private class TestMavLinkPacket : MavLinkNet.MavLinkPacketBase
    {
        public TestMavLinkPacket(MavLinkNet.UasMessage message, byte systemId, byte componentId, byte sequenceNumber)
        {
            Message = message;
            SystemId = systemId;
            ComponentId = componentId;
            PacketSequenceNumber = sequenceNumber;
            IsValid = true;
        }

        public override int GetPacketSize()
        {
            return 0; // Not used in tests
        }

        public override void Serialize(System.IO.BinaryWriter w)
        {
            // Not used in tests
        }
    }

    // Helper methods to create MAVLink packets
    private MavLinkNet.MavLinkPacketBase CreateSysStatusPacket(ushort voltageMillivolts)
    {
        var sysStatus = new MavLinkNet.UasSysStatus
        {
            OnboardControlSensorsPresent = 0,
            OnboardControlSensorsEnabled = 0,
            OnboardControlSensorsHealth = 0,
            Load = 500,
            VoltageBattery = voltageMillivolts,
            CurrentBattery = 1500,
            BatteryRemaining = 75,
            DropRateComm = 100,
            ErrorsComm = 0,
            ErrorsCount1 = 0,
            ErrorsCount2 = 0,
            ErrorsCount3 = 0,
            ErrorsCount4 = 0
        };

        return new TestMavLinkPacket(sysStatus, 1, 1, 0);
    }

    private MavLinkNet.MavLinkPacketBase CreateGpsRawIntPacket(byte satellitesVisible)
    {
        var gpsRaw = new MavLinkNet.UasGpsRawInt
        {
            TimeUsec = 1000000,
            FixType = MavLinkNet.GpsFixType._3dFix,
            Lat = -62000000,
            Lon = 1068000000,
            Alt = 100000,
            Eph = 150,
            Epv = 200,
            Vel = 500,
            Cog = 18000,
            SatellitesVisible = satellitesVisible
        };

        return new TestMavLinkPacket(gpsRaw, 1, 1, 0);
    }
}
