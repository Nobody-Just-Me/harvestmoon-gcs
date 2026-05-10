using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Diagnostics;
using Pigeon_Uno.Models;
using MavLinkNet;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Configuration for MAVLink connection
/// </summary>
public class ConnectionConfig
{
    public ConnectionType Type { get; set; }
    public string Address { get; set; } = "";
    public int Port { get; set; } = 14550;
    public string SerialPort { get; set; } = "";
    public int BaudRate { get; set; } = 57600;
    public string WebSocketUrl { get; set; } = "ws://localhost:9000";
}

/// <summary>
/// MAVLink communication service interface
/// Handles connection management, message parsing, and command encoding
/// </summary>
public interface IMavLinkService
{
    // Connection properties
    bool IsConnected { get; }
    bool IsInPlaybackMode { get; }
    ConnectionType ConnectionType { get; }
    string ConnectionString { get; }
    
    // Events
    event EventHandler<FlightData> TelemetryReceived;
    event EventHandler<string> MessageReceived;
    event EventHandler<bool> ConnectionStatusChanged;
    event EventHandler<MavLinkPacketBase> PacketReceived;
    event EventHandler HeartbeatReceived;

    // Connection management
    Task<bool> ConnectAsync(ConnectionConfig config);
    Task<bool> ConnectAsync(string connectionString);
    Task<bool> ConnectAsync(ConnectionType type, string address, int port = 14550);
    Task DisconnectAsync();
    
    // Command operations
    Task<bool> SendCommandAsync(string command);
    Task SendCommandAsync(Command command, params float[] parameters);
    Task<bool> ArmDisarmAsync(bool arm);
    Task<bool> SetFlightModeAsync(string mode);
    
    // VTOL specific operations
    Task<bool> VtolTransitionAsync(MavLinkNet.MavVtolState targetState);
    Task<bool> VtolTakeoffAsync(double latitude, double longitude, double altitude, float heading);
    Task<bool> VtolLandAsync(double latitude, double longitude, double altitude);
    
    // Mission execution
    Task<bool> StartMissionAsync(int firstItem = 0, int lastItem = 0);
    Task<bool> PauseMissionAsync();
    Task<bool> ResumeMissionAsync();
    Task<bool> SetCurrentWaypointAsync(int waypointIndex);
    
    // Payload/Gripper operations
    Task<bool> GripperActionAsync(int action, int gripperNum = 0);
    Task<bool> DeployPayloadAsync();
    Task<bool> ReleasePayloadAsync();
    Task<bool> ParachuteActionAsync(MavLinkNet.ParachuteAction action);
    
    // Servo control for custom payload mechanisms
    Task<bool> SetServoAsync(int servoNum, int pwmValue);
    Task<bool> SetRelayAsync(int relayNum, bool state);
    
    // Speed control (important for fixed wing)
    Task<bool> ChangeSpeedAsync(float speedType, float speed, float throttle = -1);
    
    // Mission operations
    Task<bool> UploadMissionAsync(IEnumerable<WaypointData> waypoints);
    Task<List<WaypointData>> DownloadMissionAsync();
    
    // Parameter operations
    Task<Dictionary<string, float>> GetParametersAsync();
    Task<bool> SetParameterAsync(string name, float value);
    Task RequestParameters();
    Task RequestParametersAsync();
    
    // Calibration operations
    Task SendCommandLongAsync(int command, float param1, float param2, float param3, 
                              float param4, float param5, float param6, float param7);
    
    // Low-level message operations
    void SendMessage(UasMessage message);
    void SendRawBytes(byte[] data);
    void InjectPacket(MavLinkPacketBase packet);
    void ProcessTlogPacket(byte[] rawPacket);
    
    // Playback mode management
    bool EnterPlaybackMode();
    void ExitPlaybackMode();
    
    // Diagnostic operations
    IDiagnosticLogger GetDiagnosticLogger();
    IPerformanceMonitor GetPerformanceMonitor();
    string GetDiagnosticSummary();
}
