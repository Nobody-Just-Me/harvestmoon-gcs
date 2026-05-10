using System;
using System.Threading;
using MavLinkNet;

namespace Pigeon_Uno.Services;

/// <summary>
/// Manages heartbeat sending and connection health monitoring
/// </summary>
internal class HeartbeatManager : IDisposable
{
    private readonly MavLinkService _service;
    private Timer? _heartbeatTimer;
    private DateTime _lastHeartbeatReceived = DateTime.MinValue;
    private DateTime _lastHeartbeatSent = DateTime.MinValue;
    
    public TimeSpan TimeSinceLastHeartbeat
    {
        get
        {
            if (_lastHeartbeatReceived == DateTime.MinValue)
                return TimeSpan.MaxValue;
            return DateTime.Now - _lastHeartbeatReceived;
        }
    }
    
    public HeartbeatManager(MavLinkService service)
    {
        _service = service;
    }
    
    public void Start()
    {
        Stop(); // Stop existing timer if any
        
        // Start 1-second timer for heartbeat
        _heartbeatTimer = new Timer(SendHeartbeat, null, 0, 1000);
    }
    
    public void Stop()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }
    
    public void OnHeartbeatReceived()
    {
        _lastHeartbeatReceived = DateTime.Now;
    }
    
    public bool IsConnectionHealthy()
    {
        // Connection is healthy if heartbeat received within 10 seconds
        return TimeSinceLastHeartbeat.TotalSeconds < 10;
    }
    
    private void SendHeartbeat(object? state)
    {
        try
        {
            var transport = _service.GetTransport();
            if (transport == null || !_service.IsConnected)
                return;
            
            // Create HEARTBEAT message
            var heartbeat = new UasHeartbeat
            {
                Type = MavType.Gcs,
                Autopilot = MavAutopilot.Invalid,
                BaseMode = 0,
                CustomMode = 0,
                SystemStatus = MavState.Active,
                MavlinkVersion = 3
            };
            
            // Send heartbeat
            transport.SendMessage(heartbeat);
            _lastHeartbeatSent = DateTime.Now;
            
            // Check connection health
            if (!IsConnectionHealthy() && _lastHeartbeatReceived != DateTime.MinValue)
            {
                System.Diagnostics.Debug.WriteLine("[HeartbeatManager] Connection appears unhealthy - no heartbeat received");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HeartbeatManager] Failed to send heartbeat: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
    }
}
