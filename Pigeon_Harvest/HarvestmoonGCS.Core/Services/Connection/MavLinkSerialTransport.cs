using System;
using System.IO.Ports;
using System.Threading;
using HarvestmoonGCS.Core.Diagnostics;

namespace HarvestmoonGCS.Core.Services.Connection
{
    /// <summary>
    /// Serial port transport implementation for MAVLink communication.
    /// Handles serial port connection, reading, and writing.
    /// INSTRUMENTED: Includes diagnostic logging for debugging data flow issues.
    /// </summary>
    public class MavLinkSerialTransport : IMavLinkTransport
    {
        private SerialPort? _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private Thread? _readThread;
        private bool _isRunning;
        private readonly IDiagnosticLogger? _diagnosticLogger;

        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public string ConnectionName => $"Serial:{_portName}@{_baudRate}";

        public event Action<byte[]>? OnDataReceived;

        public MavLinkSerialTransport(string portName, int baudRate, IDiagnosticLogger? diagnosticLogger = null)
        {
            _portName = portName;
            _baudRate = baudRate;
            _diagnosticLogger = diagnosticLogger;
        }

        public void Connect()
        {
            if (IsConnected) return;

            _serialPort = new SerialPort(_portName, _baudRate);
            _serialPort.Open();
            _isRunning = true;

            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Start();
            
            System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Connected to {ConnectionName}");
        }

        public void Disconnect()
        {
            _isRunning = false;
            
            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Close();
                    System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Disconnected from {ConnectionName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Disconnect error: {ex.Message}");
                }
            }
        }

        public void SendPacket(byte[] packet)
        {
            if (IsConnected && _serialPort != null)
            {
                try
                {
                    _serialPort.Write(packet, 0, packet.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Send error: {ex.Message}");
                }
            }
        }

        private void ReadLoop()
        {
            var buffer = new byte[1024];
            try
            {
                while (_isRunning && _serialPort != null && _serialPort.IsOpen)
                {
                    // Read available bytes
                    int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        
                        // DIAGNOSTIC: Log raw bytes read from serial port
                        _diagnosticLogger?.LogTransportData(data, bytesRead);
                        
                        System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Read {bytesRead} bytes from serial port");
                        
                        // DIAGNOSTIC: Log before firing OnDataReceived event
                        if (OnDataReceived != null)
                        {
                            var subscriberCount = OnDataReceived.GetInvocationList().Length;
                            System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Firing OnDataReceived event to {subscriberCount} subscriber(s)");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] WARNING: OnDataReceived has no subscribers!");
                        }
                        
                        OnDataReceived?.Invoke(data);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle disconnection or read errors
                if (_isRunning)
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkSerialTransport] Read error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }
    }
}
