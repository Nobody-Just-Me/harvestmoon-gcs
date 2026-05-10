using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Services.Connection.Transports;

public class TcpConnectionTransport : IConnectionTransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpConnectionTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port);
        _stream = _client.GetStream();
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_stream == null) return 0;
        return await _stream.ReadAsync(buffer, offset, count, token);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_stream == null) return;
        await _stream.WriteAsync(buffer, offset, count, token);
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }
}
