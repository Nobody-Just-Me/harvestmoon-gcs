using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Transport;

/// <summary>
/// UDP transport implementation for MAVLink communication.
/// </summary>
public class UdpTransport : ITransport
{
    private const int ConnectTimeoutMs = 10000;
    private readonly string _host;
    private readonly int _port;
    private UdpClient? _udpClient;
    private IPEndPoint? _remoteEndPoint;
    private bool _disposed;
    private bool _isConnected;

    /// <summary>
    /// Initializes a new instance of the UdpTransport class.
    /// </summary>
    /// <param name="host">The IP address or hostname to connect to.</param>
    /// <param name="port">The UDP port number.</param>
    public UdpTransport(string host, int port)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _port = port;
    }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected && _udpClient != null;

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_udpClient != null && _isConnected)
            {
                return true;
            }

            var resolveTask = Task.Run(() =>
            {
                if (IPAddress.TryParse(_host, out IPAddress? parsed))
                {
                    return parsed;
                }

                var addresses = Dns.GetHostAddresses(_host);
                return addresses.Length > 0 ? addresses[0] : null;
            });

            var completedTask = await Task.WhenAny(resolveTask, Task.Delay(ConnectTimeoutMs));
            if (completedTask != resolveTask)
            {
                return false;
            }

            var ipAddress = await resolveTask;
            if (ipAddress == null)
            {
                return false;
            }

            _remoteEndPoint = new IPEndPoint(ipAddress, _port);
            _udpClient = new UdpClient();
            _udpClient.Connect(_remoteEndPoint);
            _isConnected = true;

            return true;
        }
        catch (Exception)
        {
            _udpClient?.Dispose();
            _udpClient = null;
            _isConnected = false;
            return false;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        try
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
                _isConnected = false;
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
        if (_udpClient == null || !_isConnected)
        {
            throw new InvalidOperationException("UDP client is not connected.");
        }

        try
        {
            // Use ReceiveAsync with cancellation token
            var receiveTask = _udpClient.ReceiveAsync();
            var completedTask = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cancellationToken));

            if (completedTask != receiveTask)
            {
                // Cancellation was requested
                cancellationToken.ThrowIfCancellationRequested();
            }

            var result = await receiveTask;
            int bytesToCopy = Math.Min(result.Buffer.Length, count);
            Array.Copy(result.Buffer, 0, buffer, offset, bytesToCopy);

            return bytesToCopy;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read from UDP client.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
        if (_udpClient == null || !_isConnected)
        {
            throw new InvalidOperationException("UDP client is not connected.");
        }

        try
        {
            // Create a new buffer with just the data to send
            byte[] dataToSend;
            if (offset == 0 && count == buffer.Length)
            {
                dataToSend = buffer;
            }
            else
            {
                dataToSend = new byte[count];
                Array.Copy(buffer, offset, dataToSend, 0, count);
            }

            await _udpClient.SendAsync(dataToSend, count);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write to UDP client.", ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the UdpTransport and optionally releases the managed resources.
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
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient.Dispose();
                _udpClient = null;
            }
            _isConnected = false;
        }

        _disposed = true;
    }
}
