using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Models;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Real-time data service that connects to MAVLink WebSocket simulator
/// and provides live telemetry data to the UI
/// </summary>
public class RealTimeDataService : IRealTimeDataService
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private readonly string _webSocketUrl;
    private bool _isConnected;
    private DateTime _lastTelemetryDispatchTime = DateTime.MinValue;
    private const int TelemetryDispatchIntervalMs = 100;
    private TelemetryData? _pendingTelemetryData;
    private readonly object _telemetryDispatchLock = new();
    private readonly ObservabilityService? _observabilityService;

    public event EventHandler<TelemetryData>? TelemetryReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _isConnected;

    public RealTimeDataService(string webSocketUrl = "ws://localhost:9000")
    {
        _webSocketUrl = webSocketUrl;
    }

    public RealTimeDataService(ObservabilityService observabilityService, string webSocketUrl = "ws://localhost:9000")
    {
        _observabilityService = observabilityService;
        _webSocketUrl = webSocketUrl;
    }

    // Interface implementation
    async Task IRealTimeDataService.ConnectAsync()
    {
        await ConnectAsync();
    }

    async Task IRealTimeDataService.DisconnectAsync()
    {
        await DisconnectAsync();
    }

    // Original methods with return values
    public async Task<bool> ConnectAsync()
    {
        return await ConnectAsync(_webSocketUrl);
    }
    
    public async Task<bool> ConnectAsync(string? customUrl = null)
    {
        try
        {
            var urlToUse = customUrl ?? _webSocketUrl;
            Console.WriteLine($"[RealTimeDataService] ========== CONNECTING TO {urlToUse} ==========");

            // Close existing connection
            if (_webSocket?.State == WebSocketState.Open)
            {
                await DisconnectAsync();
            }

            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            // Connect to WebSocket server
            var uri = new Uri(urlToUse);
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

            Console.WriteLine($"[RealTimeDataService] ✓ Connected successfully!");
            _isConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);

            // Start receiving data
            _receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RealTimeDataService] ✗ Connection failed: {ex.Message}");
            _isConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            Console.WriteLine($"[RealTimeDataService] Disconnecting...");
            
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

            _isConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            Console.WriteLine($"[RealTimeDataService] ✓ Disconnected");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RealTimeDataService] Error disconnecting: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Disconnect error: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            Console.WriteLine($"[RealTimeDataService] ========== RECEIVE LOOP STARTED ==========");
            
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _webSocket.ReceiveAsync(segment, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"[RealTimeDataService] Server closed connection");
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", cancellationToken);
                    break;
                }

                if (result.Count > 0)
                {
                    // Convert received data to string
                    var jsonData = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    try
                    {
                        // Parse JSON data
                        var attitudeData = JsonSerializer.Deserialize<AttitudeData>(jsonData);
                        if (attitudeData != null && attitudeData.type == "ATTITUDE")
                        {
                            // Convert to TelemetryData and fire event
                            var telemetryData = new TelemetryData
                            {
                                Timestamp = DateTime.Now,
                                Roll = attitudeData.roll,
                                Pitch = attitudeData.pitch,
                                Yaw = attitudeData.yaw,
                                Heading = attitudeData.yaw,
                                
                                // Set realistic varying values
                                Altitude = 100.0 + (Math.Sin(attitudeData.timestamp / 1000.0) * 20.0), // Varying altitude
                                AirSpeed = 15.0 + (Math.Sin(attitudeData.timestamp / 2000.0) * 5.0), // Varying speed
                                GroundSpeed = 14.0,
                                Latitude = -7.2754 + (attitudeData.roll * 0.0001), // Slight movement based on roll
                                Longitude = 112.7947 + (attitudeData.pitch * 0.0001), // Slight movement based on pitch
                                SatelliteCount = 12,
                                HDOP = 1.2,
                                FlightMode = FlightMode.AUTO,
                                Barometers = 1013.25,
                                RelativeAltitude = 100.0,
                                BatteryVoltage = 16.2,
                                BatteryPercentage = 85.0,
                                IsArmed = true
                            };

                            TryDispatchTelemetry(telemetryData);
                        }
                        else if (jsonData.Contains("MAVLINK_SIMULATOR_READY"))
                        {
                            Console.WriteLine($"[RealTimeDataService] ✓ Simulator ready message received");
                        }
                        else
                        {
                            Console.WriteLine($"[RealTimeDataService] Received non-attitude data: {jsonData}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[RealTimeDataService] JSON parse error: {ex.Message}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[RealTimeDataService] Receive loop cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RealTimeDataService] Error in receive loop: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
        }
        
        Console.WriteLine($"[RealTimeDataService] ========== RECEIVE LOOP ENDED ==========");
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }

    private void TryDispatchTelemetry(TelemetryData telemetryData)
    {
        _observabilityService?.Track("realtime.telemetry.ingest");

        var now = DateTime.UtcNow;
        TelemetryData? latest = null;

        lock (_telemetryDispatchLock)
        {
            _pendingTelemetryData = telemetryData;
            if ((now - _lastTelemetryDispatchTime).TotalMilliseconds < TelemetryDispatchIntervalMs)
            {
                return;
            }

            latest = _pendingTelemetryData;
            _pendingTelemetryData = null;
            _lastTelemetryDispatchTime = now;
        }

        if (latest != null)
        {
            _observabilityService?.Track("realtime.telemetry.dispatch");
            TelemetryReceived?.Invoke(this, latest);
        }
    }

    private class AttitudeData
    {
        public string type { get; set; } = "";
        public double roll { get; set; }
        public double pitch { get; set; }
        public double yaw { get; set; }
        public long timestamp { get; set; }
    }
}
