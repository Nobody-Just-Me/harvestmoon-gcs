using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Services.Optimization;

namespace HarvestmoonGCS.Services
{
    /// <summary>
    /// Optimized network manager with connection pooling and bandwidth management
    /// </summary>
    public class OptimizedNetworkManager : IOptimizedNetworkManager
    {
        private readonly IEmulatorDetector? _emulatorDetector;
        private readonly ILoggingService _logger;
        
        private NetworkOptimizationLevel _optimizationLevel = NetworkOptimizationLevel.Basic;
        private bool _isEmulatorMode;
        private int _connectionTimeoutMs = 5000;
        private int _maxConcurrentConnections = 10;
        private bool _compressionEnabled = true;
        private bool _connectionPoolingEnabled = true;
        private int _bufferSize = 16 * 1024;
        private readonly object _connectionLock = new();
        private TcpClient? _activeClient;
        
        // Performance tracking
        private readonly List<double> _latencyReadings = new();
        private readonly List<double> _bandwidthReadings = new();
        private DateTime _lastMetricsUpdate = DateTime.MinValue;

        public event EventHandler<NetworkPerformanceEventArgs>? NetworkPerformanceChanged;

        public OptimizedNetworkManager(
            IEmulatorDetector? emulatorDetector,
            ILoggingService logger)
        {
            _emulatorDetector = emulatorDetector;
            _logger = logger;
        }

        /// <summary>
        /// Initialize network manager with emulator detection
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (_emulatorDetector != null)
                {
                    _isEmulatorMode = await _emulatorDetector.IsRunningInEmulatorAsync();
                    
                    if (_isEmulatorMode)
                    {
                        // Apply emulator-specific network optimizations
                        _optimizationLevel = NetworkOptimizationLevel.Maximum;
                        _connectionTimeoutMs = 10000; // Longer timeout for emulator
                        _maxConcurrentConnections = 5; // Reduce concurrent connections
                        _compressionEnabled = true;
                        
                        _logger.LogInfo("Emulator network optimizations applied", nameof(OptimizedNetworkManager));
                    }
                }
                
                // Start network monitoring
                _ = Task.Run(MonitorNetworkAsync);
                
                _logger.LogInfo($"Network manager initialized - Optimization: {_optimizationLevel}", nameof(OptimizedNetworkManager));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing network manager: {ex.Message}", nameof(OptimizedNetworkManager));
            }
        }

        /// <summary>
        /// Set network optimization level
        /// </summary>
        public void SetOptimizationLevel(NetworkOptimizationLevel level)
        {
            _optimizationLevel = level;
            
            switch (level)
            {
                case NetworkOptimizationLevel.None:
                    _compressionEnabled = false;
                    _connectionPoolingEnabled = false;
                    _maxConcurrentConnections = 20;
                    break;
                    
                case NetworkOptimizationLevel.Basic:
                    _compressionEnabled = true;
                    _connectionPoolingEnabled = true;
                    _maxConcurrentConnections = 15;
                    break;
                    
                case NetworkOptimizationLevel.Advanced:
                    _compressionEnabled = true;
                    _connectionPoolingEnabled = true;
                    _maxConcurrentConnections = 10;
                    break;
                    
                case NetworkOptimizationLevel.Maximum:
                    _compressionEnabled = true;
                    _connectionPoolingEnabled = true;
                    _maxConcurrentConnections = 5;
                    _connectionTimeoutMs = 10000;
                    break;
            }
            
            _logger.LogInfo($"Network optimization level set to: {level}", nameof(OptimizedNetworkManager));
        }

        /// <summary>
        /// Measure network latency to endpoint
        /// </summary>
        public async Task<double> MeasureLatencyAsync(string host)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, _connectionTimeoutMs);
                
                stopwatch.Stop();
                
                if (reply.Status == IPStatus.Success)
                {
                    var latency = reply.RoundtripTime;
                    RecordLatency(latency);
                    return latency;
                }
                
                return -1; // Failed
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error measuring latency: {ex.Message}", nameof(OptimizedNetworkManager));
                return -1;
            }
        }

        /// <summary>
        /// Get current network performance metrics
        /// </summary>
        public NetworkMetrics GetMetrics()
        {
            lock (_latencyReadings)
            {
                return new NetworkMetrics
                {
                    AverageLatencyMs = _latencyReadings.Count > 0 ? _latencyReadings.Average() : 0,
                    MaxLatencyMs = _latencyReadings.Count > 0 ? _latencyReadings.Max() : 0,
                    MinLatencyMs = _latencyReadings.Count > 0 ? _latencyReadings.Min() : 0,
                    OptimizationLevel = _optimizationLevel,
                    IsEmulatorMode = _isEmulatorMode,
                    CompressionEnabled = _compressionEnabled,
                    ConnectionPoolingEnabled = _connectionPoolingEnabled
                };
            }
        }

        /// <summary>
        /// Check if network is available
        /// </summary>
        public bool IsNetworkAvailable
        {
            get
            {
                try
                {
                    return NetworkInterface.GetIsNetworkAvailable();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Get connection timeout
        /// </summary>
        public int ConnectionTimeoutMs => _connectionTimeoutMs;

        /// <summary>
        /// Get max concurrent connections
        /// </summary>
        public int MaxConcurrentConnections => _maxConcurrentConnections;

        /// <summary>
        /// Check if compression is enabled
        /// </summary>
        public bool IsCompressionEnabled => _compressionEnabled;

        /// <summary>
        /// Connects to a network endpoint
        /// </summary>
        public async Task<bool> ConnectAsync(string address, int port)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    _logger.LogWarning("Connection failed: address is empty", nameof(OptimizedNetworkManager));
                    return false;
                }

                if (port <= 0 || port > 65535)
                {
                    _logger.LogWarning($"Connection failed: invalid port {port}", nameof(OptimizedNetworkManager));
                    return false;
                }

                _logger.LogInfo($"Connecting to {address}:{port}", nameof(OptimizedNetworkManager));

                var client = new TcpClient
                {
                    NoDelay = true,
                    SendBufferSize = _bufferSize,
                    ReceiveBufferSize = _bufferSize
                };

                var connectTask = client.ConnectAsync(address, port);
                var timeoutTask = Task.Delay(_connectionTimeoutMs);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask != connectTask)
                {
                    client.Dispose();
                    _logger.LogWarning($"Connection timeout after {_connectionTimeoutMs}ms", nameof(OptimizedNetworkManager));
                    return false;
                }

                await connectTask;
                if (!client.Connected)
                {
                    client.Dispose();
                    return false;
                }

                lock (_connectionLock)
                {
                    _activeClient?.Dispose();
                    _activeClient = client;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection failed: {ex.Message}", nameof(OptimizedNetworkManager));
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the network endpoint
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _logger.LogInfo("Disconnecting", nameof(OptimizedNetworkManager));

                TcpClient? clientToDispose;
                lock (_connectionLock)
                {
                    clientToDispose = _activeClient;
                    _activeClient = null;
                }

                clientToDispose?.Close();
                clientToDispose?.Dispose();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Disconnect failed: {ex.Message}", nameof(OptimizedNetworkManager));
            }
        }

        /// <summary>
        /// Sets the buffer size for network operations
        /// </summary>
        public void SetBufferSize(int size)
        {
            _bufferSize = Math.Max(1024, size);

            lock (_connectionLock)
            {
                if (_activeClient != null)
                {
                    _activeClient.SendBufferSize = _bufferSize;
                    _activeClient.ReceiveBufferSize = _bufferSize;
                }
            }

            _logger.LogInfo($"Buffer size set to {size}", nameof(OptimizedNetworkManager));
        }

        /// <summary>
        /// Enables or disables data compression
        /// </summary>
        public void EnableCompression(bool enable)
        {
            _compressionEnabled = enable;
            _logger.LogInfo($"Compression {(enable ? "enabled" : "disabled")}", nameof(OptimizedNetworkManager));
        }

        private void RecordLatency(double latency)
        {
            lock (_latencyReadings)
            {
                _latencyReadings.Add(latency);
                if (_latencyReadings.Count > 100)
                    _latencyReadings.RemoveAt(0);
            }
        }

        private async Task MonitorNetworkAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    
                    if (DateTime.Now - _lastMetricsUpdate > TimeSpan.FromMinutes(1))
                    {
                        var metrics = GetMetrics();
                        
                        NetworkPerformanceChanged?.Invoke(this, new NetworkPerformanceEventArgs
                        {
                            Metrics = metrics
                        });
                        
                        _lastMetricsUpdate = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in network monitoring: {ex.Message}", nameof(OptimizedNetworkManager));
                }
            }
        }
    }

    public class NetworkMetrics
    {
        public double AverageLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public NetworkOptimizationLevel OptimizationLevel { get; set; }
        public bool IsEmulatorMode { get; set; }
        public bool CompressionEnabled { get; set; }
        public bool ConnectionPoolingEnabled { get; set; }
    }

    public class NetworkPerformanceEventArgs : EventArgs
    {
        public NetworkMetrics Metrics { get; set; } = new();
    }
}
