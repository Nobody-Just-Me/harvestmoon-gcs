using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Desktop (Windows, Linux, macOS) implementation of ISerialPortService using System.IO.Ports.
/// </summary>
public class DesktopSerialPortService : ISerialPortService
{
    private SerialPort? _serialPort;
    private readonly object _lock = new();

    public bool IsOpen => _serialPort?.IsOpen ?? false;

    public event EventHandler<Core.Services.SerialDataReceivedEventArgs>? DataReceived;
    public event EventHandler<Core.Services.SerialErrorEventArgs>? ErrorReceived;

    public Task<List<string>> GetAvailablePortsAsync()
    {
        try
        {
            var ports = SerialPort.GetPortNames().ToList();
            return Task.FromResult(ports);
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to get available ports", ex));
            return Task.FromResult(new List<string>());
        }
    }

    public Task<bool> OpenAsync(string portName, int baudRate)
    {
        try
        {
            lock (_lock)
            {
                // Close existing port if open
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                // Create and configure new serial port
                _serialPort = new SerialPort(portName, baudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    // Use a larger buffer for high-rate telemetry data
                    ReadBufferSize = 4096,
                    WriteBufferSize = 2048
                };

                // Subscribe to data received event
                _serialPort.DataReceived += OnSerialPortDataReceived;
                _serialPort.ErrorReceived += OnSerialPortErrorReceived;

                // Open the port
                _serialPort.Open();

                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Failed to open port {portName}", ex));
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.DataReceived -= OnSerialPortDataReceived;
                    _serialPort.ErrorReceived -= OnSerialPortErrorReceived;
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to close port", ex));
            return Task.CompletedTask;
        }
    }

    public Task<byte[]> ReadAsync(int count)
    {
        try
        {
            if (_serialPort?.IsOpen != true)
            {
                throw new InvalidOperationException("Serial port is not open");
            }

            var buffer = new byte[count];
            var bytesRead = _serialPort.Read(buffer, 0, count);
            
            if (bytesRead < count)
            {
                Array.Resize(ref buffer, bytesRead);
            }

            return Task.FromResult(buffer);
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to read from port", ex));
            return Task.FromResult(Array.Empty<byte>());
        }
    }

    public Task WriteAsync(byte[] data)
    {
        try
        {
            if (_serialPort?.IsOpen != true)
            {
                throw new InvalidOperationException("Serial port is not open");
            }

            _serialPort.Write(data, 0, data.Length);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to write to port", ex));
            return Task.CompletedTask;
        }
    }

    private void OnSerialPortDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort?.IsOpen == true && _serialPort.BytesToRead > 0)
            {
                var buffer = new byte[_serialPort.BytesToRead];
                var bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    DataReceived?.Invoke(this, new Core.Services.SerialDataReceivedEventArgs(buffer, bytesRead));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Error reading data", ex));
        }
    }

    private void OnSerialPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Serial port error: {e.EventType}"));
    }
}
