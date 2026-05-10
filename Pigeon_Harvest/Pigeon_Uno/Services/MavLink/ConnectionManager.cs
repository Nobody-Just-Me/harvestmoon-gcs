using System;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.Threading.Tasks;
using MavLinkNet;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Services.MavLink;

namespace Pigeon_Uno.Services;

/// <summary>
/// Manages MAVLink transport connections (TCP, UDP, Serial)
/// </summary>
internal class ConnectionManager
{
    private readonly MavLinkService _service;
    private MavLinkGenericTransport? _transport;
    
    public ConnectionManager(MavLinkService service)
    {
        _service = service;
    }
    
    public async Task<MavLinkGenericTransport?> CreateTransportAsync(ConnectionConfig config)
    {
        try
        {
            // Disconnect existing connection first
            await DisconnectAsync();
            
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Creating transport: Type={config.Type}, Address={config.Address}, Port={config.Port}");
            
            MavLinkGenericTransport? transport = null;
            
            switch (config.Type)
            {
                case Core.Models.ConnectionType.TCP:
                    // TCP not yet implemented - use UDP for now
                    System.Diagnostics.Debug.WriteLine($"[ConnectionManager] TCP not yet implemented, falling back to UDP");
                    transport = await CreateUdpTransportAsync(config.Address, config.Port);
                    break;
                    
                case Core.Models.ConnectionType.UDP:
                    transport = await CreateUdpTransportAsync(config.Address, config.Port);
                    break;
                    
                case Core.Models.ConnectionType.Serial:
                    transport = await CreateSerialTransportAsync(config.SerialPort, config.BaudRate);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported connection type: {config.Type}");
            }
            
            if (transport != null)
            {
                _transport = transport;
                _service.SetTransport(transport);
                System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Transport created successfully");
            }
            
            return transport;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Error creating transport: {ex.Message}");
            throw;
        }
    }
    
    private async Task<MavLinkGenericTransport?> CreateUdpTransportAsync(string address, int port)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Creating UDP transport to {address}:{port}");
            Console.WriteLine($"[ConnectionManager] Creating UDP transport to {address}:{port}");
            
            var transport = new MavLinkUdpClientTransport();
            transport.TargetIpAddress = IPAddress.Parse(address);
            transport.TargetPort = port;
            
            // Initialize the transport (this starts threads and connects)
            await Task.Run(() => transport.Initialize());
            
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] UDP transport initialized");
            Console.WriteLine($"[ConnectionManager] UDP transport initialized successfully");
            return transport;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] UDP connection failed: {ex.Message}");
            Console.WriteLine($"[ConnectionManager] UDP connection failed: {ex.Message}");
            throw;
        }
    }
    
    private async Task<MavLinkGenericTransport?> CreateSerialTransportAsync(string portName, int baudRate)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] CreateSerialTransportAsync START: {portName} @ {baudRate}");
            Console.WriteLine($"[ConnectionManager] CreateSerialTransportAsync START: {portName} @ {baudRate}");
            
            if (string.IsNullOrEmpty(portName))
            {
                var error = "[ConnectionManager] ERROR: Serial port name is null or empty!";
                System.Diagnostics.Debug.WriteLine(error);
                Console.WriteLine(error);
                throw new ArgumentException("Serial port name cannot be null or empty", nameof(portName));
            }
            
            if (baudRate <= 0)
            {
                var error = $"[ConnectionManager] ERROR: Invalid baudrate: {baudRate}";
                System.Diagnostics.Debug.WriteLine(error);
                Console.WriteLine(error);
                throw new ArgumentException($"Invalid baudrate: {baudRate}", nameof(baudRate));
            }
            
            Console.WriteLine($"[ConnectionManager] Creating MavLinkSerialPortTransport instance...");
            var transport = new MavLinkSerialPortTransport();
            transport.SerialPortName = portName;
            transport.BaudRate = baudRate;
            
            Console.WriteLine($"[ConnectionManager] Transport configured: Port={transport.SerialPortName}, Baud={transport.BaudRate}");
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Transport configured: Port={transport.SerialPortName}, Baud={transport.BaudRate}");
            
            // Initialize the transport (this opens port and starts threads)
            Console.WriteLine($"[ConnectionManager] Calling transport.Initialize()...");
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Calling transport.Initialize()...");
            
            await Task.Run(() => transport.Initialize());
            
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Serial transport initialized SUCCESSFULLY");
            Console.WriteLine($"[ConnectionManager] Serial transport initialized SUCCESSFULLY");
            return transport;
        }
        catch (UnauthorizedAccessException ex)
        {
            var error = $"[ConnectionManager] Serial connection PERMISSION DENIED: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(error);
            Console.WriteLine(error);
            Console.WriteLine($"[ConnectionManager] Make sure you have permission to access {portName}");
            Console.WriteLine($"[ConnectionManager] Try: sudo usermod -a -G dialout $USER");
            throw;
        }
        catch (System.IO.IOException ex)
        {
            var error = $"[ConnectionManager] Serial connection IO ERROR: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(error);
            Console.WriteLine(error);
            Console.WriteLine($"[ConnectionManager] Port {portName} may be in use by another application");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Serial connection failed: {ex.Message}");
            Console.WriteLine($"[ConnectionManager] Serial connection FAILED: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
    
    public async Task DisconnectAsync()
    {
        try
        {
            if (_transport != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Disconnecting transport");
                
                await Task.Run(() =>
                {
                    _transport.Dispose();
                });
                
                _transport = null;
                _service.SetTransport(null);
                
                System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Transport disconnected");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionManager] Error disconnecting: {ex.Message}");
        }
    }
    
    public MavLinkGenericTransport? GetTransport()
    {
        return _service.GetTransport();
    }
}
