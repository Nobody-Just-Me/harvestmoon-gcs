using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// LoRa radio communication service implementation
/// Supports AT command-based LoRa modules (e.g., REYAX RYLR896, HC-12)
/// </summary>
public class LoRaService : ILoRaService
{
    private readonly ISerialPortService? _platformSerialPort;
    private SerialPort? _serialPort;
    private bool _isConnected;
    private CancellationTokenSource? _receiveCts;
    private LoRaDevice? _currentDevice;
    private readonly object _lock = new object();
    private readonly StringBuilder _platformReceiveBuffer = new();
    private bool _usingPlatformSerialPort;

    public bool IsConnected => _isConnected;

    public event EventHandler<LoRaDevice>? DeviceDiscovered;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<byte[]>? DataReceived;

    public LoRaService()
    {
    }

    public LoRaService(ISerialPortService platformSerialPort)
    {
        _platformSerialPort = platformSerialPort;
        _platformSerialPort.DataReceived += OnPlatformSerialDataReceived;
        _platformSerialPort.ErrorReceived += OnPlatformSerialErrorReceived;
    }

    /// <summary>
    /// Scan for available LoRa devices on serial ports
    /// </summary>
    public async Task<List<LoRaDevice>> ScanDevicesAsync()
    {
        var devices = new List<LoRaDevice>();

        if (_platformSerialPort != null)
        {
            var ports = await _platformSerialPort.GetAvailablePortsAsync();
            foreach (var portName in ports)
            {
                var device = CreatePlatformSerialDevice(portName);
                if (devices.All(d => d.PortName != device.PortName))
                {
                    devices.Add(device);
                    DeviceDiscovered?.Invoke(this, device);
                }
            }

            return devices;
        }

        try
        {
            var portNames = SerialPort.GetPortNames();

            foreach (var portName in portNames)
            {
                LoRaDevice? discoveredDevice = null;

                try
                {
                    using var testPort = new SerialPort(portName, 115200)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        DtrEnable = true,
                        RtsEnable = true,
                        Encoding = Encoding.UTF8,
                        NewLine = "\n"
                    };

                    testPort.Open();

                    // Send AT command to check if it's a LoRa module
                    testPort.WriteLine("AT");
                    await Task.Delay(100);

                    if (testPort.BytesToRead > 0)
                    {
                        var response = testPort.ReadExisting();
                        
                        // Check for AT command response
                        if (response.Contains("OK") || response.Contains("AT"))
                        {
                            // Query device information
                            var device = await QueryDeviceInfoAsync(testPort, portName);
                            if (device != null)
                            {
                                discoveredDevice = device;
                            }
                        }
                    }

                    testPort.Close();

                    discoveredDevice ??= CreateRawSerialDevice(portName);
                }
                catch
                {
                    // Port not accessible or not a LoRa device, continue
                }

                if (discoveredDevice != null && devices.All(d => d.PortName != discoveredDevice.PortName))
                {
                    devices.Add(discoveredDevice);
                    DeviceDiscovered?.Invoke(this, discoveredDevice);
                }
            }
        }
        catch (Exception)
        {
            // Error scanning ports
        }

        return devices;
    }

    /// <summary>
    /// Connect to a LoRa device
    /// </summary>
    public async Task<bool> ConnectAsync(LoRaDevice device)
    {
        if (device == null || string.IsNullOrWhiteSpace(device.PortName))
        {
            return false;
        }

        if (_isConnected)
        {
            await DisconnectAsync();
        }

        if (_platformSerialPort != null)
        {
            var opened = await _platformSerialPort.OpenAsync(device.PortName, 115200);
            if (!opened)
            {
                return false;
            }

            _usingPlatformSerialPort = true;
            _currentDevice = device;
            _isConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);
            return true;
        }

        try
        {
            lock (_lock)
            {
                _serialPort = new SerialPort(device.PortName, 115200)
                {
                    ReadTimeout = 5000,
                    WriteTimeout = 5000,
                    DtrEnable = true,
                    RtsEnable = true,
                    Encoding = Encoding.UTF8,
                    NewLine = "\n"
                };

                _serialPort.Open();
            }

            if (!device.SupportsAtCommands)
            {
                _currentDevice = device;
                _isConnected = true;
                _receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

                ConnectionStatusChanged?.Invoke(this, true);
                return true;
            }

            // Test connection with AT command
            _serialPort.WriteLine("AT");
            await Task.Delay(100);

            if (_serialPort.BytesToRead > 0)
            {
                var response = _serialPort.ReadExisting();
                if (response.Contains("OK") || response.Contains("AT"))
                {
                    _currentDevice = device;
                    _isConnected = true;

                    // Start receive loop
                    _receiveCts = new CancellationTokenSource();
                    _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

                    ConnectionStatusChanged?.Invoke(this, true);
                    return true;
                }
            }

            // Connection failed
            lock (_lock)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
            }

            return false;
        }
        catch (Exception)
        {
            lock (_lock)
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;
            }
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the LoRa device
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_usingPlatformSerialPort && _platformSerialPort != null)
            {
                await _platformSerialPort.CloseAsync();
                lock (_lock)
                {
                    _platformReceiveBuffer.Clear();
                }
            }

            // Cancel receive loop
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _receiveCts = null;

            // Close serial port
            lock (_lock)
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }

            _isConnected = false;
            _currentDevice = null;
            _usingPlatformSerialPort = false;

            ConnectionStatusChanged?.Invoke(this, false);
        }
        catch (Exception)
        {
            // Ignore errors during disconnect
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Send data over LoRa connection
    /// </summary>
    public async Task<bool> SendDataAsync(byte[] data)
    {
        if (!_isConnected)
        {
            return false;
        }

        try
        {
            if (_usingPlatformSerialPort && _platformSerialPort != null)
            {
                if (_currentDevice?.SupportsAtCommands == false)
                {
                    await _platformSerialPort.WriteAsync(data);
                }
                else
                {
                    var payload = Encoding.UTF8.GetString(data).TrimEnd('\r', '\n');
                    var command = $"AT+SEND=0,{Encoding.UTF8.GetByteCount(payload)},{payload}\r\n";
                    await _platformSerialPort.WriteAsync(Encoding.UTF8.GetBytes(command));
                }

                return true;
            }

            if (_serialPort == null)
            {
                return false;
            }

            if (_currentDevice?.SupportsAtCommands == false)
            {
                lock (_lock)
                {
                    _serialPort.Write(data, 0, data.Length);
                }

                await Task.CompletedTask;
                return true;
            }

            var serialPayload = Encoding.UTF8.GetString(data).TrimEnd('\r', '\n');

            // Send using AT+SEND command (REYAX format)
            var serialCommand = $"AT+SEND=0,{Encoding.UTF8.GetByteCount(serialPayload)},{serialPayload}";
            
            lock (_lock)
            {
                _serialPort.WriteLine(serialCommand);
            }

            // Wait for acknowledgment
            await Task.Delay(100);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Configure LoRa parameters
    /// </summary>
    public async Task<bool> ConfigureAsync(LoRaConfig config)
    {
        if (!_isConnected || _currentDevice?.SupportsAtCommands == false)
        {
            return false;
        }

        try
        {
            if (_usingPlatformSerialPort && _platformSerialPort != null)
            {
                await WritePlatformLineAsync($"AT+BAND={(int)config.Frequency}");
                await WritePlatformLineAsync("AT+NETWORKID=0");
                await WritePlatformLineAsync("AT+ADDRESS=0");

                var platformBwValue = config.Bandwidth switch
                {
                    125 => 7,
                    250 => 8,
                    500 => 9,
                    _ => 7
                };

                await WritePlatformLineAsync($"AT+PARAMETER={config.SpreadingFactor},{platformBwValue},{config.CodingRate},{config.PreambleLength}");
                await WritePlatformLineAsync($"AT+CRFOP={config.TxPower}");
                return true;
            }

            if (_serialPort == null)
            {
                return false;
            }

            // Set frequency (AT+BAND command)
            var freqMHz = (int)config.Frequency;
            await SendATCommandAsync($"AT+BAND={freqMHz}");

            // Set network ID (AT+NETWORKID command)
            await SendATCommandAsync("AT+NETWORKID=0");

            // Set address (AT+ADDRESS command)
            await SendATCommandAsync("AT+ADDRESS=0");

            // Set parameters (AT+PARAMETER command)
            // Format: AT+PARAMETER=SF,BW,CR,Preamble
            var bwValue = config.Bandwidth switch
            {
                125 => 7,
                250 => 8,
                500 => 9,
                _ => 7
            };

            var command = $"AT+PARAMETER={config.SpreadingFactor},{bwValue},{config.CodingRate},{config.PreambleLength}";
            await SendATCommandAsync(command);

            // Set output power (AT+CRFOP command)
            await SendATCommandAsync($"AT+CRFOP={config.TxPower}");

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Send AT command and wait for response
    /// </summary>
    private async Task<string> SendATCommandAsync(string command)
    {
        if (_serialPort == null)
        {
            return string.Empty;
        }

        try
        {
            lock (_lock)
            {
                // Clear input buffer
                _serialPort.DiscardInBuffer();
                
                // Send command
                _serialPort.WriteLine(command);
            }

            // Wait for response
            await Task.Delay(200);

            lock (_lock)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    return _serialPort.ReadExisting();
                }
            }

            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Query device information using AT commands
    /// </summary>
    private async Task<LoRaDevice?> QueryDeviceInfoAsync(SerialPort port, string portName)
    {
        try
        {
            var device = new LoRaDevice
            {
                Name = $"LoRa Device ({portName})",
                PortName = portName,
                SupportsAtCommands = true
            };

            // Query firmware version
            port.WriteLine("AT+VER?");
            await Task.Delay(100);
            if (port.BytesToRead > 0)
            {
                var response = port.ReadExisting();
                device.FirmwareVersion = response.Trim();
            }

            // Query frequency band
            port.WriteLine("AT+BAND?");
            await Task.Delay(100);
            if (port.BytesToRead > 0)
            {
                var response = port.ReadExisting();
                if (float.TryParse(response.Replace("+BAND=", "").Trim(), out float freq))
                {
                    device.Frequency = freq;
                }
            }

            // Query parameters
            port.WriteLine("AT+PARAMETER?");
            await Task.Delay(100);
            if (port.BytesToRead > 0)
            {
                var response = port.ReadExisting();
                var parts = response.Replace("+PARAMETER=", "").Split(',');
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[0].Trim(), out int sf))
                        device.SpreadingFactor = sf;
                    if (int.TryParse(parts[1].Trim(), out int bw))
                        device.Bandwidth = bw;
                    if (int.TryParse(parts[2].Trim(), out int cr))
                        device.CodingRate = cr;
                }
            }

            // Set default values if not retrieved
            if (device.Frequency == 0)
                device.Frequency = 915.0f;
            if (device.SpreadingFactor == 0)
                device.SpreadingFactor = 7;
            if (device.Bandwidth == 0)
                device.Bandwidth = 125;

            device.RSSI = -50; // Default RSSI
            device.LinkQuality = 80; // Default link quality

            return device;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Receive loop for incoming data
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new StringBuilder();

        while (!ct.IsCancellationRequested && _isConnected)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    lock (_lock)
                    {
                        var data = _serialPort.ReadExisting();
                        buffer.Append(data);
                    }

                    if (buffer.Length > 2048)
                    {
                        buffer.Clear();
                        continue;
                    }

                    // Process complete lines
                    var lines = buffer.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (buffer.ToString().EndsWith("\n") || buffer.ToString().EndsWith("\r"))
                    {
                        buffer.Clear();
                        
                        foreach (var line in lines)
                        {
                            ProcessReceivedLine(line);
                        }
                    }
                    else
                    {
                        // Keep last incomplete line in buffer
                        if (lines.Length > 0)
                        {
                            buffer.Clear();
                            for (int i = 0; i < lines.Length - 1; i++)
                            {
                                ProcessReceivedLine(lines[i]);
                            }
                            buffer.Append(lines[lines.Length - 1]);
                        }
                    }
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Continue on error
                await Task.Delay(100, ct);
            }
        }
    }

    /// <summary>
    /// Process received line from LoRa module
    /// </summary>
    private void ProcessReceivedLine(string line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            // Check for received data (format: +RCV=address,length,data,RSSI,SNR)
            if (line.StartsWith("+RCV="))
            {
                if (TryParseReceiveLine(line.Substring(5), out var payload, out var rssi))
                {
                    if (_currentDevice != null && rssi.HasValue)
                    {
                        _currentDevice.RSSI = rssi.Value;
                    }

                    DataReceived?.Invoke(this, DecodeReceivePayload(payload));
                }
                return;
            }

            // Raw serial LoRa firmware can emit JSON/debug text directly.
            if (_currentDevice?.SupportsAtCommands == false || line.Contains('{'))
            {
                DataReceived?.Invoke(this, Encoding.UTF8.GetBytes(line));
            }
        }
        catch (Exception)
        {
            // Ignore parsing errors
        }
    }

    private static bool TryParseReceiveLine(string value, out string payload, out int? rssi)
    {
        payload = string.Empty;
        rssi = null;

        var firstComma = value.IndexOf(',');
        if (firstComma < 0)
        {
            return false;
        }

        var secondComma = value.IndexOf(',', firstComma + 1);
        if (secondComma < 0)
        {
            return false;
        }

        var remaining = value.Substring(secondComma + 1);
        var lastComma = remaining.LastIndexOf(',');
        if (lastComma < 0)
        {
            payload = remaining;
            return true;
        }

        var previousComma = remaining.LastIndexOf(',', lastComma - 1);
        if (previousComma < 0)
        {
            payload = remaining.Substring(0, lastComma);
            _ = int.TryParse(remaining.Substring(lastComma + 1).Trim(), out var singleMetricRssi);
            rssi = singleMetricRssi;
            return true;
        }

        payload = remaining.Substring(0, previousComma);
        if (int.TryParse(remaining.Substring(previousComma + 1, lastComma - previousComma - 1).Trim(), out var parsedRssi))
        {
            rssi = parsedRssi;
        }

        return true;
    }

    private static byte[] DecodeReceivePayload(string payload)
    {
        if (payload.Length > 0 && payload.Length % 2 == 0 && payload.All(Uri.IsHexDigit))
        {
            try
            {
                var bytes = new byte[payload.Length / 2];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(payload.Substring(i * 2, 2), 16);
                }

                return bytes;
            }
            catch
            {
                // Fall through to UTF-8 payload handling.
            }
        }

        return Encoding.UTF8.GetBytes(payload);
    }

    private static LoRaDevice CreateRawSerialDevice(string portName)
    {
        return new LoRaDevice
        {
            Name = $"LoRa Serial ({portName})",
            PortName = portName,
            Frequency = 915.0f,
            Bandwidth = 125,
            SpreadingFactor = 7,
            CodingRate = 5,
            TxPower = 17,
            RSSI = -100,
            LinkQuality = 0,
            SupportsAtCommands = false
        };
    }

    private static LoRaDevice CreatePlatformSerialDevice(string portName)
    {
        return new LoRaDevice
        {
            Name = $"LoRa USB ({portName})",
            PortName = portName,
            Frequency = 915.0f,
            Bandwidth = 125,
            SpreadingFactor = 7,
            CodingRate = 5,
            TxPower = 17,
            RSSI = -100,
            LinkQuality = 0,
            SupportsAtCommands = true
        };
    }

    private async Task WritePlatformLineAsync(string line)
    {
        if (_platformSerialPort == null)
        {
            return;
        }

        await _platformSerialPort.WriteAsync(Encoding.UTF8.GetBytes(line + "\r\n"));
        await Task.Delay(120);
    }

    private void OnPlatformSerialDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (!_usingPlatformSerialPort || !_isConnected || e.BytesReceived <= 0)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(e.Data, 0, e.BytesReceived);
        List<string> completeLines = new();

        lock (_lock)
        {
            _platformReceiveBuffer.Append(text);

            if (_platformReceiveBuffer.Length > 4096)
            {
                _platformReceiveBuffer.Clear();
                return;
            }

            var buffered = _platformReceiveBuffer.ToString();
            var parts = buffered.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var endsWithLineBreak = buffered.EndsWith('\r') || buffered.EndsWith('\n');

            if (endsWithLineBreak)
            {
                completeLines.AddRange(parts);
                _platformReceiveBuffer.Clear();
            }
            else if (parts.Length > 1)
            {
                completeLines.AddRange(parts.Take(parts.Length - 1));
                _platformReceiveBuffer.Clear();
                _platformReceiveBuffer.Append(parts[^1]);
            }
        }

        foreach (var line in completeLines)
        {
            ProcessReceivedLine(line);
        }
    }

    private void OnPlatformSerialErrorReceived(object? sender, SerialErrorEventArgs e)
    {
        if (!_usingPlatformSerialPort || !_isConnected)
        {
            return;
        }

        _isConnected = false;
        _usingPlatformSerialPort = false;
        ConnectionStatusChanged?.Invoke(this, false);
    }
}
