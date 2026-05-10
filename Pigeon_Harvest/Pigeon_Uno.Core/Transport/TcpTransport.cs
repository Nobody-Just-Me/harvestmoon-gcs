using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Transport;

/// <summary>
/// TCP transport implementation for MAVLink communication.
/// </summary>
public class TcpTransport : ITransport
{
    private const int ConnectTimeoutMs = 10000;
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the TcpTransport class.
    /// </summary>
    /// <param name="host">The IP address or hostname to connect to.</param>
    /// <param name="port">The TCP port number.</param>
    public TcpTransport(string host, int port)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _port = port;
    }

    /// <inheritdoc/>
    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_tcpClient != null && _tcpClient.Connected)
            {
                return true;
            }

            _tcpClient = new TcpClient();
            var connectTask = _tcpClient.ConnectAsync(_host, _port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs));
            if (completedTask != connectTask)
            {
                _tcpClient.Dispose();
                _tcpClient = null;
                return false;
            }

            await connectTask;
            _stream = _tcpClient.GetStream();

            return _tcpClient.Connected;
        }
        catch (Exception)
        {
            _stream?.Dispose();
            _stream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        try
        {
            _stream?.Close();
            _stream = null;

            if (_tcpClient != null)
            {
                _tcpClient.Close();
            }
        }
        catch (Exception)
        {
            // Ignore exceptions during disconnect
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_stream == null || _tcpClient == null || !_tcpClient.Connected)
        {
            throw new InvalidOperationException("TCP client is not connected.");
        }

        try
        {
            return await _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read from TCP stream.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
        if (_stream == null || _tcpClient == null || !_tcpClient.Connected)
        {
            throw new InvalidOperationException("TCP client is not connected.");
        }

        try
        {
            await _stream.WriteAsync(buffer, offset, count);
            await _stream.FlushAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write to TCP stream.", ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the TcpTransport and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _stream?.Dispose();
            _stream = null;

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient.Dispose();
                _tcpClient = null;
            }
        }

        _disposed = true;
    }
}
