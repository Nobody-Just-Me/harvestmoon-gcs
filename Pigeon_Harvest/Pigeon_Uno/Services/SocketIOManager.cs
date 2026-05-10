using System;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services
{
    /// <summary>
    /// Manages Socket.IO connection for real-time telemetry streaming
    /// Provides connection lifecycle management with automatic reconnection
    /// </summary>
    public class SocketIOManager : IDisposable
    {
        private bool _isConnected;
        private bool _isDisposed;
        private CancellationTokenSource? _reconnectCts;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 10;
        private const int ReconnectDelayMs = 5000;
        private readonly ILoggingService _logger;

        /// <summary>
        /// Event fired when connection is established
        /// </summary>
        public event EventHandler? Connected;

        /// <summary>
        /// Event fired when connection is lost
        /// </summary>
        public event EventHandler? Disconnected;

        /// <summary>
        /// Event fired when a connection error occurs
        /// </summary>
        public event EventHandler<string>? ConnectionError;

        /// <summary>
        /// Event fired when telemetry is successfully sent
        /// </summary>
        public event EventHandler<TelemetryData>? TelemetrySent;

        /// <summary>
        /// Gets whether the client is currently connected
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Gets the current server URL
        /// </summary>
        public string ServerUrl { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether automatic reconnection is enabled
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        public SocketIOManager(ILoggingService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Connects to Socket.IO server
        /// </summary>
        /// <param name="serverUrl">Server URL (e.g., "http://localhost:3000")</param>
        /// <returns>True if connection successful</returns>
        public async Task<bool> ConnectAsync(string serverUrl)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SocketIOManager));

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                ConnectionError?.Invoke(this, "Server URL cannot be empty");
                return false;
            }

            try
            {
                _logger.LogInfo($"Connecting to Socket.IO server: {serverUrl}", nameof(SocketIOManager));
                ServerUrl = serverUrl;
                
                // Simulate connection (actual Socket.IO implementation would go here)
                await Task.Delay(100);
                
                _isConnected = true;
                _reconnectAttempts = 0;
                
                _logger.LogInfo("Socket.IO connected successfully", nameof(SocketIOManager));
                Connected?.Invoke(this, EventArgs.Empty);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Socket.IO connection failed: {ex.Message}", nameof(SocketIOManager));
                ConnectionError?.Invoke(this, ex.Message);
                
                if (AutoReconnect)
                    _ = StartReconnectAsync();
                
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (!_isConnected) return;

            _logger.LogInfo("Disconnecting from Socket.IO server", nameof(SocketIOManager));
            
            _reconnectCts?.Cancel();
            _isConnected = false;
            
            await Task.Delay(100);
            
            _logger.LogInfo("Socket.IO disconnected", nameof(SocketIOManager));
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sends telemetry data to the server
        /// </summary>
        public async Task<bool> SendTelemetryAsync(TelemetryData telemetry)
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Cannot send telemetry - not connected", nameof(SocketIOManager));
                return false;
            }

            try
            {
                // Simulate sending telemetry (actual implementation would serialize and send)
                await Task.Delay(10);
                
                TelemetrySent?.Invoke(this, telemetry);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send telemetry: {ex.Message}", nameof(SocketIOManager));
                return false;
            }
        }

        /// <summary>
        /// Starts automatic reconnection process
        /// </summary>
        private async Task StartReconnectAsync()
        {
            if (!AutoReconnect || _isDisposed) return;

            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            while (!token.IsCancellationRequested && _reconnectAttempts < MaxReconnectAttempts)
            {
                _reconnectAttempts++;
                _logger.LogInfo($"Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts}", nameof(SocketIOManager));

                try
                {
                    await Task.Delay(ReconnectDelayMs, token);
                    
                    if (await ConnectAsync(ServerUrl))
                    {
                        _logger.LogInfo("Reconnection successful", nameof(SocketIOManager));
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Reconnection attempt failed: {ex.Message}", nameof(SocketIOManager));
                }
            }

            _logger.LogError("Max reconnection attempts reached", nameof(SocketIOManager));
            ConnectionError?.Invoke(this, "Failed to reconnect after maximum attempts");
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _isDisposed = true;
            
            _logger.LogInfo("SocketIOManager disposed", nameof(SocketIOManager));
        }
    }
}
