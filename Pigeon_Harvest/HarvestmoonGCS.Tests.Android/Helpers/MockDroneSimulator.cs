using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Tests.Android.Helpers;

/// <summary>
/// Simulates drone behavior for testing MAVLink communication
/// </summary>
public class MockDroneSimulator : IDisposable
{
    private CancellationTokenSource? _telemetryCts;
    private bool _isConnected;
    private readonly Random _random = new();

    public bool IsConnected => _isConnected;
    public int HeartbeatCount { get; private set; }
    public int MessagesReceived { get; private set; }
    public int MessagesSent { get; private set; }

    /// <summary>
    /// Simulates drone connection
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        await Task.Delay(100); // Simulate connection delay
        _isConnected = true;
        return true;
    }

    /// <summary>
    /// Simulates drone disconnection
    /// </summary>
    public async Task DisconnectAsync()
    {
        await Task.Delay(50);
        _isConnected = false;
        StopTelemetryStream();
    }

    /// <summary>
    /// Generates a realistic MAVLink heartbeat message
    /// </summary>
    public byte[] GenerateHeartbeat()
    {
        HeartbeatCount++;
        // TODO: Generate actual MAVLink heartbeat packet
        return new byte[] { 0xFE, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    }

    /// <summary>
    /// Generates realistic telemetry data
    /// </summary>
    public MockTelemetryData GenerateTelemetry()
    {
        return new MockTelemetryData
        {
            Latitude = -6.2 + (_random.NextDouble() * 0.01),
            Longitude = 106.8 + (_random.NextDouble() * 0.01),
            Altitude = 100 + (_random.NextDouble() * 50),
            Speed = 5 + (_random.NextDouble() * 10),
            Heading = _random.Next(0, 360),
            BatteryPercent = 75 + _random.Next(-10, 10),
            FlightMode = "GUIDED",
            Armed = true
        };
    }

    /// <summary>
    /// Starts streaming telemetry data
    /// </summary>
    public void StartTelemetryStream(Action<MockTelemetryData> onTelemetry, int intervalMs = 100)
    {
        _telemetryCts = new CancellationTokenSource();
        var token = _telemetryCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _isConnected)
            {
                var telemetry = GenerateTelemetry();
                onTelemetry?.Invoke(telemetry);
                MessagesSent++;
                await Task.Delay(intervalMs, token);
            }
        }, token);
    }

    /// <summary>
    /// Stops telemetry stream
    /// </summary>
    public void StopTelemetryStream()
    {
        _telemetryCts?.Cancel();
        _telemetryCts?.Dispose();
        _telemetryCts = null;
    }

    /// <summary>
    /// Simulates receiving a command from ground station
    /// </summary>
    public async Task<bool> ReceiveCommandAsync(byte[] command)
    {
        MessagesReceived++;
        await Task.Delay(10); // Simulate processing
        return true;
    }

    /// <summary>
    /// Simulates mission upload response
    /// </summary>
    public async Task<bool> ReceiveMissionAsync(List<MockWaypoint> waypoints)
    {
        MessagesReceived += waypoints.Count;
        await Task.Delay(waypoints.Count * 10); // Simulate upload time
        return true;
    }

    /// <summary>
    /// Simulates mission download
    /// </summary>
    public async Task<List<MockWaypoint>> SendMissionAsync(int waypointCount)
    {
        var waypoints = new List<MockWaypoint>();
        for (int i = 0; i < waypointCount; i++)
        {
            waypoints.Add(new MockWaypoint
            {
                Index = i,
                Latitude = -6.2 + (i * 0.001),
                Longitude = 106.8 + (i * 0.001),
                Altitude = 100 + (i * 10)
            });
            MessagesSent++;
        }
        await Task.Delay(waypointCount * 10);
        return waypoints;
    }

    public void Dispose()
    {
        StopTelemetryStream();
        _telemetryCts?.Dispose();
    }
}

public class MockTelemetryData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Speed { get; set; }
    public double Heading { get; set; }
    public int BatteryPercent { get; set; }
    public string FlightMode { get; set; } = string.Empty;
    public bool Armed { get; set; }
}

public class MockWaypoint
{
    public int Index { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
}
