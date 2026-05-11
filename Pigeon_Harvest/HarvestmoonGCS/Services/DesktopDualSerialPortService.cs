using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Desktop Dual Serial Port Service
/// Supports 2 serial ports simultaneously:
/// 1. MAVLink connection (autopilot)
/// 2. LoRa communication (telemetry radio)
/// Works on Windows, Linux, and macOS
/// </summary>
public class DesktopDualSerialPortService : IDisposable
{
    // MAVLink Serial Port (Primary)
    private SerialPort? _mavlinkPort;
    private readonly object _mavlinkLock = new();
    private bool _mavlinkIsOpen;
    
    // LoRa Serial Port (Secondary)
    private SerialPort? _loraPort;
    private readonly object _loraLock = new();
    private bool _loraIsOpen;

    // Events
    public event EventHandler<Core.Services.SerialDataReceivedEventArgs>? MavLinkDataReceived;
    public event EventHandler<Core.Services.SerialDataReceivedEventArgs>? LoRaDataReceived;
    public event EventHandler<string>? ErrorOccurred;

    // Properties
    public bool IsMavLinkOpen => _mavlinkIsOpen;
    public bool IsLoRaOpen => _loraIsOpen;
    public bool AreBothOpen => _mavlinkIsOpen && _loraIsOpen;

    /// <summary>
    /// Get list of all available serial ports
    /// </summary>
    public Task<List<SerialPortInfo>> GetAvailablePortsAsync()
    {
        var ports = new List<SerialPortInfo>();

        try
        {
            var portNames = SerialPort.GetPortNames();
            
            foreach (var portName in portNames)
            {
                ports.Add(new SerialPortInfo
                {
                    PortName = portName,
                    DisplayName = portName
                });
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error enumerating serial ports: {ex.Message}");
        }

        return Task.FromResult(ports);
    }

    /// <summary>
    /// Open MAVLink serial port (Primary - for autopilot)
    /// </summary>
    public Task<bool> OpenMavLinkPortAsync(string portName, int baudRate = 57600)
    {
        if (_mavlinkIsOpen)
        {
            CloseMavLinkPortAsync().Wait();
        }

        try
        {
            lock (_mavlinkLock)
            {
                _mavlinkPort = new SerialPort(portName, baudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    ReadBufferSize = 4096,
                    WriteBufferSize = 2048,
                    DtrEnable = true,
                    RtsEnable = true
                };

                // Subscribe to events
                _mavlinkPort.DataReceived += OnMavLinkDataReceived;
                _mavlinkPort.ErrorReceived += OnMavLinkErrorReceived;

                // Open the port
                _mavlinkPort.Open();
                _mavlinkIsOpen = true;

                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error opening MAVLink serial port {portName}: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Open LoRa serial port (Secondary - for telemetry radio)
    /// </summary>
    public Task<bool> OpenLoRaPortAsync(string portName, int baudRate = 57600)
    {
        if (_loraIsOpen)
        {
            CloseLoRaPortAsync().Wait();
        }

        try
        {
            lock (_loraLock)
            {
                _loraPort = new SerialPort(portName, baudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    ReadBufferSize = 4096,
                    WriteBufferSize = 2048,
                    DtrEnable = true,
                    RtsEnable = true
                };

                // Subscribe to events
                _loraPort.DataReceived += OnLoRaDataReceived;
                _loraPort.ErrorReceived += OnLoRaErrorReceived;

                // Open the port
                _loraPort.Open();
                _loraIsOpen = true;

                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error opening LoRa serial port {portName}: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Write data to MAVLink port
    /// </summary>
    public Task<bool> WriteMavLinkAsync(byte[] data)
    {
        if (!_mavlinkIsOpen || _mavlinkPort == null)
        {
            ErrorOccurred?.Invoke(this, "MAVLink serial port is not open");
            return Task.FromResult(false);
        }

        try
        {
            lock (_mavlinkLock)
            {
                if (_mavlinkPort?.IsOpen == true)
                {
                    _mavlinkPort.Write(data, 0, data.Length);
                    return Task.FromResult(true);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error writing to MAVLink port: {ex.Message}");
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Write data to LoRa port
    /// </summary>
    public Task<bool> WriteLoRaAsync(byte[] data)
    {
        if (!_loraIsOpen || _loraPort == null)
        {
            ErrorOccurred?.Invoke(this, "LoRa serial port is not open");
            return Task.FromResult(false);
        }

        try
        {
            lock (_loraLock)
            {
                if (_loraPort?.IsOpen == true)
                {
                    _loraPort.Write(data, 0, data.Length);
                    return Task.FromResult(true);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error writing to LoRa port: {ex.Message}");
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Close MAVLink port
    /// </summary>
    public Task CloseMavLinkPortAsync()
    {
        if (!_mavlinkIsOpen)
        {
            return Task.CompletedTask;
        }

        try
        {
            lock (_mavlinkLock)
            {
                if (_mavlinkPort != null)
                {
                    if (_mavlinkPort.IsOpen)
                    {
                        _mavlinkPort.DataReceived -= OnMavLinkDataReceived;
                        _mavlinkPort.ErrorReceived -= OnMavLinkErrorReceived;
                        _mavlinkPort.Close();
                    }
                    _mavlinkPort.Dispose();
                    _mavlinkPort = null;
                }
                _mavlinkIsOpen = false;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error closing MAVLink port: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Close LoRa port
    /// </summary>
    public Task CloseLoRaPortAsync()
    {
        if (!_loraIsOpen)
        {
            return Task.CompletedTask;
        }

        try
        {
            lock (_loraLock)
            {
                if (_loraPort != null)
                {
                    if (_loraPort.IsOpen)
                    {
                        _loraPort.DataReceived -= OnLoRaDataReceived;
                        _loraPort.ErrorReceived -= OnLoRaErrorReceived;
                        _loraPort.Close();
                    }
                    _loraPort.Dispose();
                    _loraPort = null;
                }
                _loraIsOpen = false;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error closing LoRa port: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Close both ports
    /// </summary>
    public async Task CloseAllPortsAsync()
    {
        await CloseMavLinkPortAsync();
        await CloseLoRaPortAsync();
    }

    private void OnMavLinkDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        try
        {
            lock (_mavlinkLock)
            {
                if (_mavlinkPort?.IsOpen == true && _mavlinkPort.BytesToRead > 0)
                {
                    var buffer = new byte[_mavlinkPort.BytesToRead];
                    var bytesRead = _mavlinkPort.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        MavLinkDataReceived?.Invoke(this, new Core.Services.SerialDataReceivedEventArgs(data, bytesRead));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error reading from MAVLink port: {ex.Message}");
        }
    }

    private void OnLoRaDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        try
        {
            lock (_loraLock)
            {
                if (_loraPort?.IsOpen == true && _loraPort.BytesToRead > 0)
                {
                    var buffer = new byte[_loraPort.BytesToRead];
                    var bytesRead = _loraPort.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        LoRaDataReceived?.Invoke(this, new Core.Services.SerialDataReceivedEventArgs(data, bytesRead));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error reading from LoRa port: {ex.Message}");
        }
    }

    private void OnMavLinkErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"MAVLink serial port error: {e.EventType}");
    }

    private void OnLoRaErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"LoRa serial port error: {e.EventType}");
    }

    public void Dispose()
    {
        CloseAllPortsAsync().Wait();
    }
}

/// <summary>
/// Serial Port Information
/// </summary>
public class SerialPortInfo
{
    public string PortName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
