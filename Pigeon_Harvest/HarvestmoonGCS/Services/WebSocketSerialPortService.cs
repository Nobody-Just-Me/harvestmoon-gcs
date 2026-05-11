using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

/// <summary>
/// WebAssembly implementation of ISerialPortService using WebSocket as a proxy.
/// Requires a WebSocket server that bridges to actual serial ports.
/// </summary>
public class WebSocketSerialPortService : ISerialPortService
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private string? _currentPortName;
    private readonly string _webSocketServerUrl;

    public bool IsOpen => _webSocket?.State == WebSocketState.Open;

    public event EventHandler<SerialDataReceivedEventArgs>? DataReceived;
    public event EventHandler<SerialErrorEventArgs>? ErrorReceived;

    /// <summary>
    /// Creates a new WebSocketSerialPortService.
    /// </summary>
    /// <param name="webSocketServerUrl">URL of the WebSocket server that provides serial port access (e.g., "ws://localhost:9000")</param>
    public WebSocketSerialPortService(string webSocketServerUrl = "ws://localhost:9000")
    {
        _webSocketServerUrl = webSocketServerUrl;
    }

    public Task<List<string>> GetAvailablePortsAsync()
    {
        // In WebAssembly, we cannot directly enumerate serial ports
        // This would need to be implemented via a WebSocket command to the server
        // For now, return a placeholder list
        var ports = new List<string>
        {
            "WebSocket Proxy (Configure on server)"
        };
        return Task.FromResult(ports);
    }

    public async Task<bool> OpenAsync(string portName, int baudRate)
    {
        try
        {
            // Close existing connection if open
            if (_webSocket?.State == WebSocketState.Open)
            {
                await CloseAsync();
            }

            _currentPortName = portName;
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            // Connect to WebSocket server (simple URL without query params)
            var uri = new Uri(_webSocketServerUrl);
            System.Diagnostics.Debug.WriteLine($"[WebSocketSerialPortService] Connecting to {uri}");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            System.Diagnostics.Debug.WriteLine($"[WebSocketSerialPortService] Connected successfully");

            // Start receiving data
            _receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebSocketSerialPortService] Connection failed: {ex.Message}");
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Failed to connect to WebSocket server: {ex.Message}", ex));
            return false;
        }
    }

    public async Task CloseAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }

            _webSocket?.Dispose();
            _webSocket = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            if (_receiveTask != null)
            {
                await _receiveTask;
                _receiveTask = null;
            }
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to close WebSocket connection", ex));
        }
    }

    public async Task<byte[]> ReadAsync(int count)
    {
        // In WebSocket mode, reading is event-driven via DataReceived event
        // This method is not typically used, but we'll provide a basic implementation
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        var buffer = new byte[count];
        var segment = new ArraySegment<byte>(buffer);
        var result = await _webSocket.ReceiveAsync(segment, CancellationToken.None);
        
        if (result.Count < count)
        {
            Array.Resize(ref buffer, result.Count);
        }

        return buffer;
    }

    public async Task WriteAsync(byte[] data)
    {
        try
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            var segment = new ArraySegment<byte>(data);
            await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to send data via WebSocket", ex));
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _webSocket.ReceiveAsync(segment, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", cancellationToken);
                    break;
                }

                if (result.Count > 0)
                {
                    var data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    
                    DataReceived?.Invoke(this, new SerialDataReceivedEventArgs(data, result.Count));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Error in receive loop", ex));
        }
    }
}
