using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Diagnostics;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Real MAVLink service implementation providing complete drone communication capabilities
/// </summary>
public class MavLinkService : IMavLinkService, IDisposable
{
    // Dependencies
    private readonly IDispatcherService? _dispatcherService;
    private readonly IDiagnosticLogger _diagnosticLogger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ObservabilityService? _observabilityService;
    
    // Sub-components
    private readonly ConnectionManager _connectionManager;
    private readonly TelemetryParser _telemetryParser;
    private readonly CommandSender _commandSender;
    private readonly MissionProtocol _missionProtocol;
    private readonly ParameterProtocol _parameterProtocol;
    private readonly HeartbeatManager _heartbeatManager;
    private readonly ConnectionQualityMonitor _qualityMonitor;
    private readonly AutoReconnectManager _autoReconnectManager;
    private readonly StreamRequestManager _streamRequestManager;
    
    // State
    private MavLinkGenericTransport? _transport;
    private ConnectionConfig? _currentConfig;
    private bool _isConnected;
    private bool _isInPlaybackMode;
    private byte _targetSystemId = 1;
    private byte _targetComponentId = 1;
    
    // Thread safety
    private readonly object _stateLock = new object();
    private readonly object _telemetryDispatchLock = new object();
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
    private FlightData? _pendingTelemetryData;
    private bool _telemetryDispatchScheduled;
    private DateTime _lastTelemetryDispatchTime = DateTime.MinValue;
    private const int TelemetryDispatchIntervalMs = 100;
    
    // Events
    public event EventHandler<FlightData>? TelemetryReceived;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<MavLinkPacketBase>? PacketReceived;
    public event EventHandler? HeartbeatReceived;
    
    // Properties
    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
            {
                return _isConnected;
            }
        }
    }
    
    public bool IsInPlaybackMode
    {
        get
        {
            lock (_stateLock)
            {
                return _isInPlaybackMode;
            }
        }
    }
    
    public Core.Models.ConnectionType ConnectionType
    {
        get
        {
            lock (_stateLock)
            {
                return _currentConfig?.Type ?? Core.Models.ConnectionType.TCP;
            }
        }
    }
    
    public string ConnectionString
    {
        get
        {
            lock (_stateLock)
            {
                if (_currentConfig == null) return "";
                return _currentConfig.Type switch
                {
                    Core.Models.ConnectionType.TCP => $"tcp://{_currentConfig.Address}:{_currentConfig.Port}",
                    Core.Models.ConnectionType.UDP => $"udp://{_currentConfig.Address}:{_currentConfig.Port}",
                    Core.Models.ConnectionType.Serial => $"serial://{_currentConfig.SerialPort}:{_currentConfig.BaudRate}",
                    _ => ""
                };
            }
        }
    }
    
    public MavLinkService(
        IDispatcherService? dispatcherService = null,
        IDiagnosticLogger? diagnosticLogger = null,
        IPerformanceMonitor? performanceMonitor = null,
        ObservabilityService? observabilityService = null)
    {
        _dispatcherService = dispatcherService;
        _diagnosticLogger = diagnosticLogger ?? new DiagnosticLogger();
        _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
        _observabilityService = observabilityService;
        
        // Initialize sub-components
        _connectionManager = new ConnectionManager(this);
        _telemetryParser = new TelemetryParser(this);
        _commandSender = new CommandSender(this);
        _missionProtocol = new MissionProtocol(this);
        _parameterProtocol = new ParameterProtocol(this);
        _heartbeatManager = new HeartbeatManager(this);
        _qualityMonitor = new ConnectionQualityMonitor(this);
        _autoReconnectManager = new AutoReconnectManager(this);
        _streamRequestManager = new StreamRequestManager(this);
    }
    
    // Connection management methods
    public async Task<bool> ConnectAsync(ConnectionConfig config)
    {
        try
        {
            var transport = await _connectionManager.CreateTransportAsync(config);
            
            if (transport == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MavLinkService] Failed to create transport");
                RaiseConnectionStatusChanged(false);
                return false;
            }
            
            // Subscribe to transport events
            transport.OnPacketReceived += OnTransportPacketReceived;
            
            // Store connection config
            lock (_stateLock)
            {
                _currentConfig = config;
                _isConnected = true;
            }
            
            // Start heartbeat manager
            _heartbeatManager.Start();
            
            // Start quality monitor
            _qualityMonitor.Start();
            
            // Start stream request manager to request telemetry data
            _streamRequestManager.Start();
            
            // Raise connection status changed event
            RaiseConnectionStatusChanged(true);
            
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] Connected successfully");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] Connection error: {ex.Message}");
            Console.WriteLine($"[MavLinkService] Connection error: {ex.Message}\n{ex.StackTrace}");
            RaiseConnectionStatusChanged(false);
            return false;
        }
    }
    
    public async Task<bool> ConnectAsync(string connectionString)
    {
        // Parse connection string (e.g., "tcp://127.0.0.1:14550", "udp://127.0.0.1:14550", "serial://COM4:57600")
        var config = ParseConnectionString(connectionString);
        return await ConnectAsync(config);
    }
    
    public async Task<bool> ConnectAsync(Core.Models.ConnectionType type, string address, int port = 14550)
    {
        var config = new ConnectionConfig
        {
            Type = type,
            Address = address,
            Port = port
        };
        return await ConnectAsync(config);
    }
    
    public async Task DisconnectAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] DisconnectAsync called");
            
            // Stop heartbeat manager
            _heartbeatManager.Stop();
            
            // Stop quality monitor
            _qualityMonitor.Stop();
            
            // Stop stream request manager
            _streamRequestManager.Stop();
            
            // Unsubscribe from transport events
            if (_transport != null)
            {
                _transport.OnPacketReceived -= OnTransportPacketReceived;
            }
            
            // Disconnect transport
            await _connectionManager.DisconnectAsync();
            
            // Update state
            lock (_stateLock)
            {
                _isConnected = false;
                _currentConfig = null;
            }
            
            // Raise connection status changed event
            RaiseConnectionStatusChanged(false);
            
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] Disconnected successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] Disconnect error: {ex.Message}");
        }
    }
    
    private ConnectionConfig ParseConnectionString(string connectionString)
    {
        // Parse connection string format: "protocol://address:port"
        var uri = new Uri(connectionString);
        
        var config = new ConnectionConfig();
        
        switch (uri.Scheme.ToLower())
        {
            case "tcp":
                config.Type = Core.Models.ConnectionType.TCP;
                config.Address = uri.Host;
                config.Port = uri.Port > 0 ? uri.Port : 14550;
                break;
                
            case "udp":
                config.Type = Core.Models.ConnectionType.UDP;
                config.Address = uri.Host;
                config.Port = uri.Port > 0 ? uri.Port : 14550;
                break;
                
            case "serial":
                config.Type = Core.Models.ConnectionType.Serial;
                config.SerialPort = uri.Host;
                config.BaudRate = uri.Port > 0 ? uri.Port : 57600;
                break;
                
            default:
                throw new ArgumentException($"Unsupported protocol: {uri.Scheme}");
        }
        
        return config;
    }
    
    private void OnTransportPacketReceived(object sender, MavLinkPacketBase packet)
    {
        try
        {
            // Update quality monitor
            _qualityMonitor.OnPacketReceived();
            
            // Parse telemetry - packet.Message contains the UasMessage
            if (packet.Message != null)
            {
                _telemetryParser.ParsePacket(packet.Message);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MavLinkService] WARNING: packet.Message is null!");
            }
            
            // Raise packet received event
            RaisePacketReceived(packet);
            
            // Check for heartbeat
            if (packet.Message is UasHeartbeat)
            {
                _heartbeatManager.OnHeartbeatReceived();
                RaiseHeartbeatReceived();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MavLinkService] Error processing packet: {ex.Message}");
            Console.WriteLine($"[MavLinkService] Error processing packet: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    // Command operations
    public async Task<bool> SendCommandAsync(string command)
    {
        // Parse command string and execute
        var parts = command.Split(' ');
        if (parts.Length == 0) return false;
        
        return parts[0].ToUpper() switch
        {
            "ARM" => await ArmDisarmAsync(true),
            "DISARM" => await ArmDisarmAsync(false),
            "TAKEOFF" => await _commandSender.SendTakeoffAsync(10),  // Default 10m
            "LAND" => await _commandSender.SendLandAsync(),
            "RTL" => await _commandSender.SendRTLAsync(),
            _ => false
        };
    }
    
    public async Task SendCommandAsync(Command command, params float[] parameters)
    {
        if (parameters.Length < 7)
        {
            Array.Resize(ref parameters, 7);
        }
        
        await _commandSender.SendCommandLongAsync(
            (int)command,
            parameters[0], parameters[1], parameters[2], parameters[3],
            parameters[4], parameters[5], parameters[6]
        );
    }
    
    public Task<bool> ArmDisarmAsync(bool arm) => _commandSender.SendArmDisarmAsync(arm);
    
    public Task<bool> SetFlightModeAsync(string mode) => _commandSender.SendSetModeAsync(mode);
    
    // VTOL specific operations
    public Task<bool> VtolTransitionAsync(MavLinkNet.MavVtolState targetState) => 
        _commandSender.SendVtolTransitionAsync(targetState);
    
    public Task<bool> VtolTakeoffAsync(double latitude, double longitude, double altitude, float heading) => 
        _commandSender.SendVtolTakeoffAsync(latitude, longitude, altitude, heading);
    
    public Task<bool> VtolLandAsync(double latitude, double longitude, double altitude) => 
        _commandSender.SendVtolLandAsync(latitude, longitude, altitude);
    
    // Mission execution
    public Task<bool> StartMissionAsync(int firstItem = 0, int lastItem = 0) => 
        _commandSender.SendStartMissionAsync(firstItem, lastItem);
    
    public Task<bool> PauseMissionAsync() => _commandSender.SendPauseMissionAsync();
    
    public Task<bool> ResumeMissionAsync() => _commandSender.SendResumeMissionAsync();
    
    public Task<bool> SetCurrentWaypointAsync(int waypointIndex) => 
        _commandSender.SendSetCurrentWaypointAsync(waypointIndex);
    
    // Payload/Gripper operations
    public Task<bool> GripperActionAsync(int action, int gripperNum = 0) => 
        _commandSender.SendGripperActionAsync(action, gripperNum);
    
    public Task<bool> DeployPayloadAsync() => _commandSender.SendDeployPayloadAsync();
    
    public Task<bool> ReleasePayloadAsync() => _commandSender.SendReleasePayloadAsync();
    
    public Task<bool> ParachuteActionAsync(MavLinkNet.ParachuteAction action) => 
        _commandSender.SendParachuteActionAsync(action);
    
    // Servo control for custom payload mechanisms
    public Task<bool> SetServoAsync(int servoNum, int pwmValue) => 
        _commandSender.SendSetServoAsync(servoNum, pwmValue);
    
    public Task<bool> SetRelayAsync(int relayNum, bool state) => 
        _commandSender.SendSetRelayAsync(relayNum, state);
    
    // Speed control (important for fixed wing)
    public Task<bool> ChangeSpeedAsync(float speedType, float speed, float throttle = -1) => 
        _commandSender.SendChangeSpeedAsync(speedType, speed, throttle);
    
    // Mission operations
    public Task<bool> UploadMissionAsync(IEnumerable<WaypointData> waypoints) => 
        _missionProtocol.UploadMissionAsync(waypoints);
    
    public Task<List<WaypointData>> DownloadMissionAsync() => 
        _missionProtocol.DownloadMissionAsync();
    
    // Parameter operations
    public Task<Dictionary<string, float>> GetParametersAsync() => 
        _parameterProtocol.GetAllParametersAsync();
    
    public Task<bool> SetParameterAsync(string name, float value) => 
        _parameterProtocol.SetParameterAsync(name, value);
    
    public Task RequestParameters() => _parameterProtocol.RequestAllParametersAsync();
    
    public Task RequestParametersAsync() => _parameterProtocol.RequestAllParametersAsync();
    
    // Calibration operations
    public Task SendCommandLongAsync(int command, float param1, float param2, float param3,
                                      float param4, float param5, float param6, float param7) => 
        _commandSender.SendCommandLongAsync(command, param1, param2, param3, param4, param5, param6, param7);
    
    // Low-level message operations
    public void SendMessage(UasMessage message)
    {
        try
        {
            if (_transport == null)
            {
                Console.WriteLine("[MavLinkService] SendMessage: No transport available");
                return;
            }
            
            Console.WriteLine($"[MavLinkService] SendMessage: {message.GetType().Name}");
            _transport.SendMessage(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MavLinkService] SendMessage error: {ex.Message}");
        }
    }
    
    public void SendRawBytes(byte[] data)
    {
        if (_transport == null) return;
        
        try
        {
            // Send raw bytes directly to transport stream
            var stream = _transport.GetType().GetField("_stream", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_transport) as System.IO.Stream;
            
            stream?.Write(data, 0, data.Length);
            stream?.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MavLinkService] SendRawBytes error: {ex.Message}");
        }
    }
    
    public void InjectPacket(MavLinkPacketBase packet)
    {
        if (_isInPlaybackMode)
        {
            OnTransportPacketReceived(this, packet);
        }
    }
    
    public void ProcessTlogPacket(byte[] rawPacket)
    {
        try
        {
            // In playback mode, inject raw bytes directly to transport
            // Transport will handle parsing
            if (_isInPlaybackMode && _transport != null)
            {
                // Transport should handle raw bytes
                System.Diagnostics.Debug.WriteLine($"[MavLinkService] Processing tlog packet: {rawPacket.Length} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MavLinkService] ProcessTlogPacket error: {ex.Message}");
        }
    }
    
    // Playback mode management
    public bool EnterPlaybackMode()
    {
        lock (_stateLock)
        {
            if (_isConnected)
            {
                return false;  // Cannot enter playback while connected
            }
            
            _isInPlaybackMode = true;
            RaiseConnectionStatusChanged(true);  // Simulate connection for UI
            return true;
        }
    }
    
    public void ExitPlaybackMode()
    {
        lock (_stateLock)
        {
            _isInPlaybackMode = false;
            RaiseConnectionStatusChanged(false);
        }
    }
    
    // Diagnostic operations
    public IDiagnosticLogger GetDiagnosticLogger() => _diagnosticLogger;
    public IPerformanceMonitor GetPerformanceMonitor() => _performanceMonitor;
    
    public string GetDiagnosticSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Connection: {(IsConnected ? "Connected" : "Disconnected")}");
        summary.AppendLine($"Connection Type: {ConnectionType}");
        summary.AppendLine($"Connection String: {ConnectionString}");
        summary.AppendLine($"Target System: {_targetSystemId}");
        summary.AppendLine($"Target Component: {_targetComponentId}");
        summary.AppendLine($"Playback Mode: {_isInPlaybackMode}");
        
        // Add performance metrics (simplified - no GetMetric method available)
        summary.AppendLine($"\nPerformance:");
        summary.AppendLine($"  Connection Status: {(_transport != null ? "Connected" : "Disconnected")}");
        summary.AppendLine($"  Heartbeat Manager: {(_heartbeatManager != null ? "Active" : "Inactive")}");
        
        return summary.ToString();
    }
    
    public void Dispose()
    {
        _heartbeatManager?.Dispose();
        _qualityMonitor?.Dispose();
        _autoReconnectManager?.Dispose();
        _streamRequestManager?.Dispose();
        _sendLock?.Dispose();
        _transport?.Dispose();
    }
    
    // Internal helper methods
    internal MavLinkGenericTransport? GetTransport() => _transport;
    internal void SetTransport(MavLinkGenericTransport? transport) => _transport = transport;
    internal byte GetTargetSystemId() => _targetSystemId;
    internal byte GetTargetComponentId() => _targetComponentId;
    internal void SetTargetIds(byte systemId, byte componentId)
    {
        _targetSystemId = systemId;
        _targetComponentId = componentId;
    }
    
    internal void RaiseConnectionStatusChanged(bool isConnected)
    {
        lock (_stateLock)
        {
            _isConnected = isConnected;
        }
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }
    
    internal void RaiseTelemetryReceived(FlightData data)
    {
        _observabilityService?.Track("mavlink.telemetry.ingest");

        FlightData? dataToDispatchImmediately = null;
        int scheduleDelayMs = 0;
        bool shouldSchedule = false;

        lock (_telemetryDispatchLock)
        {
            _pendingTelemetryData = data;

            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastTelemetryDispatchTime).TotalMilliseconds;

            if (elapsedMs >= TelemetryDispatchIntervalMs)
            {
                dataToDispatchImmediately = _pendingTelemetryData;
                _pendingTelemetryData = null;
                _lastTelemetryDispatchTime = now;
                _telemetryDispatchScheduled = false;
            }
            else if (!_telemetryDispatchScheduled)
            {
                _telemetryDispatchScheduled = true;
                shouldSchedule = true;
                scheduleDelayMs = Math.Max(1, (int)(TelemetryDispatchIntervalMs - elapsedMs));
            }
        }

        if (dataToDispatchImmediately != null)
        {
            DispatchTelemetry(dataToDispatchImmediately);
        }

        if (shouldSchedule)
        {
            _ = Task.Delay(scheduleDelayMs).ContinueWith(_ =>
            {
                FlightData? delayedData = null;
                lock (_telemetryDispatchLock)
                {
                    delayedData = _pendingTelemetryData;
                    _pendingTelemetryData = null;
                    _lastTelemetryDispatchTime = DateTime.UtcNow;
                    _telemetryDispatchScheduled = false;
                }

                if (delayedData != null)
                {
                    DispatchTelemetry(delayedData);
                }
            });
        }
    }

    private void DispatchTelemetry(FlightData data)
    {
        _observabilityService?.Track("mavlink.telemetry.dispatch");

        if (_dispatcherService != null)
        {
            _dispatcherService.ExecuteOnUIThread(() => TelemetryReceived?.Invoke(this, data));
            return;
        }

        TelemetryReceived?.Invoke(this, data);
    }
    
    internal void RaiseMessageReceived(string message)
    {
        if (_dispatcherService != null)
        {
            _dispatcherService.ExecuteOnUIThread(() => MessageReceived?.Invoke(this, message));
        }
        else
        {
            MessageReceived?.Invoke(this, message);
        }
    }
    
    internal void RaisePacketReceived(MavLinkPacketBase packet)
    {
        PacketReceived?.Invoke(this, packet);
    }
    
    internal void RaiseHeartbeatReceived()
    {
        HeartbeatReceived?.Invoke(this, EventArgs.Empty);
    }
}
