using System;
using System.Threading;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Connection quality data
/// </summary>
public class ConnectionQuality
{
    public double PacketLossPercentage { get; set; }
    public int LatencyMs { get; set; }
    public int SignalStrength { get; set; }
}

/// <summary>
/// Monitors connection quality metrics
/// </summary>
internal class ConnectionQualityMonitor : IDisposable
{
    private readonly MavLinkService _service;
    private Timer? _monitorTimer;
    private long _packetsReceived;
    private long _packetsExpected;
    private DateTime _lastPacketTime = DateTime.MinValue;
    private readonly object _statsLock = new object();
    
    public ConnectionQuality Quality { get; private set; } = new ConnectionQuality();
    
    public ConnectionQualityMonitor(MavLinkService service)
    {
        _service = service;
    }
    
    public void Start()
    {
        Stop();
        _monitorTimer = new Timer(UpdateQuality, null, 1000, 1000);
    }
    
    public void Stop()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }
    
    public void OnPacketReceived()
    {
        lock (_statsLock)
        {
            _packetsReceived++;
            _lastPacketTime = DateTime.Now;
        }
    }
    
    private void UpdateQuality(object? state)
    {
        lock (_statsLock)
        {
            var timeSinceLastPacket = _lastPacketTime == DateTime.MinValue 
                ? double.MaxValue 
                : (DateTime.Now - _lastPacketTime).TotalSeconds;
            
            // Expected packets: roughly 10 per second
            var expectedPackets = Math.Max(1, (int)(timeSinceLastPacket * 10));
            _packetsExpected += expectedPackets;
            
            var packetLoss = 0.0;
            if (_packetsExpected > 0)
            {
                var lost = Math.Max(0, _packetsExpected - _packetsReceived);
                packetLoss = (lost / (double)_packetsExpected) * 100.0;
            }
            
            var latencyMs = (int)(timeSinceLastPacket * 1000);
            var signalStrength = Math.Max(0, Math.Min(100, (int)(100 - packetLoss)));
            
            Quality = new ConnectionQuality
            {
                PacketLossPercentage = Math.Round(packetLoss, 2),
                LatencyMs = latencyMs,
                SignalStrength = signalStrength
            };
        }
    }
    
    public void Dispose()
    {
        _monitorTimer?.Dispose();
    }
}
