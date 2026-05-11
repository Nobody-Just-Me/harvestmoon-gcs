using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Services.Connection.Transports;

public class UdpConnectionTransport : IConnectionTransport
{
    private readonly int _port;
    private UdpClient? _udpClient;
    private IPEndPoint? _remoteEndPoint;

    public UdpConnectionTransport(int port)
    {
        _port = port;
    }

    public bool IsConnected => _udpClient != null;

    public Task ConnectAsync()
    {
        _udpClient = new UdpClient(_port);
        // For UDP, "Connect" acts as a filter or default send address if host provided.
        // But here we listen on _port.
        // If we want to send, we need a target. 
        // Usually GCS listens on port 14550.
        // And sends back to sender.
        return Task.CompletedTask;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_udpClient == null) return 0;
        
        try 
        {
            // UdpClient.ReceiveAsync() cancellation is tricky before .NET 6.
            // But we are on .NET 8.
            var result = await _udpClient.ReceiveAsync(token);
            _remoteEndPoint = result.RemoteEndPoint; // Store sender to reply

            int length = Math.Min(count, result.Buffer.Length);
            Array.Copy(result.Buffer, 0, buffer, offset, length);
            return length;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_udpClient == null) return;
        
        // If we don't know where to send, we can't send.
        // Usually we send to the last received address or broadcast?
        // Or connection string should specify target IP?
        // "udp:14550" usually means listen on 14550.
        // "udp:192.168.1.1:14550" means send to target.
        // Current parser only extracts port for UDP.
        
        if (_remoteEndPoint != null)
        {
            // Create a slice
            var data = new byte[count];
            Array.Copy(buffer, offset, data, 0, count);
            await _udpClient.SendAsync(data, count, _remoteEndPoint);
        }
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
    }
}
