using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;
using MavLinkNet;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Fallback MAVLink parser that uses MavLinkSerialPortTransport directly.
    /// This mimics the Avalonia approach where packets are pre-parsed by the transport layer.
    /// Provides a safety net if MavLinkAsyncWalker integration doesn't work correctly.
    /// </summary>
    public class DirectMavLinkParser : IDisposable
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly IDiagnosticLogger _logger;
        
        private SerialPort? _serialPort;
        private MavLinkAsyncWalker? _mavLink;
        private bool _isActive = false;
        
        private const byte MAVLINK_SYSTEM_ID = 200;
        private const byte MAVLINK_COMPONENT_ID = 1;
        
        // Events - compatible with existing MavLinkService interface
        public event EventHandler<MavLinkPacketBase>? PacketReceived;
        public event EventHandler<byte[]>? OtherPacketReceived;
        public event EventHandler? ReceptionEnded;
        
        /// <summary>
        /// Creates a new DirectMavLinkParser instance.
        /// </summary>
        /// <param name="portName">Serial port name (e.g., "COM3")</param>
        /// <param name="baudRate">Baud rate (e.g., 57600, 115200)</param>
        /// <param name="logger">Diagnostic logger for instrumentation</param>
        public DirectMavLinkParser(string portName, int baudRate, IDiagnosticLogger logger)
        {
            _portName = portName;
            _baudRate = baudRate;
            _logger = logger;
        }
        
        /// <summary>
        /// Initializes and connects the parser.
        /// </summary>
        public void Connect()
        {
            if (_isActive)
            {
                _logger.LogTransportData(Array.Empty<byte>(), 0);
                System.Diagnostics.Debug.WriteLine("[DirectMavLinkParser] Already connected");
                return;
            }
            
            try
            {
                // Initialize MAVLink walker
                _mavLink = new MavLinkAsyncWalker();
                _mavLink.PacketReceived += OnMavLinkPacketReceived;
                _mavLink.OtherPacketReceived += OnMavLinkOtherPacketReceived;
                
                _logger.LogWalkerProcessing(0, true);
                System.Diagnostics.Debug.WriteLine("[DirectMavLinkParser] MAVLink walker initialized");
                
                // Initialize serial port
                _serialPort = new SerialPort(_portName)
                {
                    BaudRate = _baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                
                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.Open();
                
                _isActive = true;
                
                _logger.LogTransportData(Array.Empty<byte>(), 0);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Connected to {_portName} at {_baudRate} baud");
            }
            catch (Exception ex)
            {
                _logger.LogWalkerProcessing(0, false);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Connection error: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Disconnects and cleans up resources.
        /// </summary>
        public void Disconnect()
        {
            _isActive = false;
            
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DataReceived -= OnSerialDataReceived;
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Disconnect error: {ex.Message}");
                }
            }
            
            if (_mavLink != null)
            {
                _mavLink.PacketReceived -= OnMavLinkPacketReceived;
                _mavLink.OtherPacketReceived -= OnMavLinkOtherPacketReceived;
                _mavLink = null;
            }
            
            ReceptionEnded?.Invoke(this, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine("[DirectMavLinkParser] Disconnected");
        }
        
        /// <summary>
        /// Sends a MAVLink message.
        /// </summary>
        public void SendMessage(UasMessage message)
        {
            if (!_isActive || _serialPort == null || _mavLink == null)
            {
                System.Diagnostics.Debug.WriteLine("[DirectMavLinkParser] Cannot send: not connected");
                return;
            }
            
            try
            {
                byte[] buffer = _mavLink.SerializeMessage(message, MAVLINK_SYSTEM_ID, MAVLINK_COMPONENT_ID, true);
                _serialPort.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Send error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Serial port data received event handler.
        /// Processes raw bytes through MAVLink walker.
        /// </summary>
        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_isActive || _serialPort == null || _mavLink == null)
                return;
            
            try
            {
                var bytesToRead = _serialPort.BytesToRead;
                
                // Discard buffer if too large (prevents memory issues)
                if (bytesToRead > 4096)
                {
                    _serialPort.DiscardInBuffer();
                    _logger.LogTransportData(Array.Empty<byte>(), bytesToRead);
                    System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Discarded {bytesToRead} bytes (buffer overflow)");
                    return;
                }
                
                // Read bytes from serial port
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                
                // Log received data
                _logger.LogTransportData(buffer, bytesRead);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Received {bytesRead} bytes");
                
                // Process through MAVLink walker (this will trigger PacketReceived events)
                _mavLink.ProcessReceivedBytes(buffer, 0, bytesRead);
                _logger.LogWalkerProcessing(bytesRead, true);
            }
            catch (TimeoutException)
            {
                _logger.LogWalkerProcessing(0, false);
                System.Diagnostics.Debug.WriteLine("[DirectMavLinkParser] Read timeout");
                _isActive = false;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWalkerProcessing(0, false);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Invalid operation: {ex.Message}");
                _isActive = false;
            }
            catch (Exception ex)
            {
                _logger.LogWalkerProcessing(0, false);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Data reception error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// MAVLink packet received event handler.
        /// Forwards pre-parsed packets to subscribers.
        /// </summary>
        private void OnMavLinkPacketReceived(object? sender, MavLinkPacketBase packet)
        {
            try
            {
                _logger.LogPacketParsed((int)packet.MessageId, packet.PacketSequenceNumber, packet.SystemId);
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Packet parsed: ID={packet.MessageId}, Seq={packet.PacketSequenceNumber}, SysID={packet.SystemId}");
                
                // Emit PacketReceived event with pre-parsed packet
                PacketReceived?.Invoke(this, packet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Packet event error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// MAVLink other packet received event handler.
        /// Handles non-standard packets.
        /// </summary>
        private void OnMavLinkOtherPacketReceived(object? sender, byte[] buffer)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Other packet received: {buffer.Length} bytes");
                OtherPacketReceived?.Invoke(this, buffer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DirectMavLinkParser] Other packet event error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
