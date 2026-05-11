using System;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Manages automatic MAVLink stream requests to ensure telemetry data is received
/// </summary>
internal class StreamRequestManager : IDisposable
{
    private readonly MavLinkService _service;
    private Timer? _requestTimer;
    private bool _isRunning;
    
    public StreamRequestManager(MavLinkService service)
    {
        _service = service;
    }
    
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;

        // Send initial request immediately
        Task.Run(() => RequestStreams());
        
        // Then send every 3 seconds to maintain streams (more aggressive)
        _requestTimer = new Timer(_ => RequestStreams(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }
    
    public void Stop()
    {
        _isRunning = false;
        _requestTimer?.Dispose();
        _requestTimer = null;
    }
    
    private void RequestStreams()
    {
        try
        {
            var transport = _service.GetTransport();
            if (transport == null)
            {
                return;
            }
            
            byte targetSystem = _service.GetTargetSystemId();
            byte targetComponent = _service.GetTargetComponentId();
            
            // Request ALL streams to ensure we get data
            // Stream IDs from MAVLink protocol
            RequestDataStream(transport, targetSystem, targetComponent, 0, 10, 1);  // ALL: All streams @ 10Hz
            RequestDataStream(transport, targetSystem, targetComponent, 1, 5, 1);   // RAW_SENSORS @ 5Hz
            RequestDataStream(transport, targetSystem, targetComponent, 2, 5, 1);   // EXTENDED_STATUS @ 5Hz
            RequestDataStream(transport, targetSystem, targetComponent, 3, 5, 1);   // RC_CHANNELS @ 5Hz
            RequestDataStream(transport, targetSystem, targetComponent, 6, 5, 1);   // POSITION: GPS @ 5Hz
            RequestDataStream(transport, targetSystem, targetComponent, 10, 10, 1); // EXTRA1: ATTITUDE @ 10Hz
            RequestDataStream(transport, targetSystem, targetComponent, 11, 10, 1); // EXTRA2: VFR_HUD @ 10Hz
            RequestDataStream(transport, targetSystem, targetComponent, 12, 5, 1);  // EXTRA3: AHRS @ 5Hz
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamRequestManager] Error requesting streams: {ex.Message}");
        }
    }
    
    private void RequestDataStream(MavLinkGenericTransport transport, byte targetSystem, byte targetComponent, 
                                   byte streamId, ushort rate, byte startStop)
    {
        try
        {
            // Create REQUEST_DATA_STREAM message (message ID 66)
            var message = new UasRequestDataStream
            {
                TargetSystem = targetSystem,
                TargetComponent = targetComponent,
                ReqStreamId = streamId,
                ReqMessageRate = rate,
                StartStop = startStop
            };
            
            // Send the message
            transport.SendMessage(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamRequestManager] Error sending stream request {streamId}: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        Stop();
    }
}
