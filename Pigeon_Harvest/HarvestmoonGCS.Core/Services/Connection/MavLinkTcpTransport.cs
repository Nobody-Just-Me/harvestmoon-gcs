using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace HarvestmoonGCS.Core.Services.Connection
{
    /// <summary>
    /// TCP transport implementation for MAVLink communication.
    /// Handles TCP socket connection, reading, and writing.
    /// </summary>
    public class MavLinkTcpTransport : IMavLinkTransport
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly string _host;
        private readonly int _port;
        private Thread? _readThread;
        private bool _isRunning;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        public string ConnectionName => $"TCP:{_host}:{_port}";

        public event Action<byte[]>? OnDataReceived;

        public MavLinkTcpTransport(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (IsConnected) return;

            _tcpClient = new TcpClient();
            _tcpClient.Connect(_host, _port);
            _stream = _tcpClient.GetStream();
            _isRunning = true;

            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Start();
            
            System.Diagnostics.Debug.WriteLine($"[MavLinkTcpTransport] Connected to {ConnectionName}");
        }

        public void Disconnect()
        {
            _isRunning = false;
            
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
                System.Diagnostics.Debug.WriteLine($"[MavLinkTcpTransport] Disconnected from {ConnectionName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MavLinkTcpTransport] Disconnect error: {ex.Message}");
            }
        }

        public void SendPacket(byte[] packet)
        {
            if (IsConnected && _stream != null)
            {
                try
                {
                    _stream.Write(packet, 0, packet.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkTcpTransport] Send error: {ex.Message}");
                }
            }
        }

        private void ReadLoop()
        {
            var buffer = new byte[1024];
            try
            {
                using var stream = _tcpClient?.GetStream();
                if (stream == null) return;

                while (_isRunning && _tcpClient != null && _tcpClient.Connected)
                {
                    // Read available bytes
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        OnDataReceived?.Invoke(data);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning) 
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkTcpTransport] Read error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            _tcpClient?.Dispose();
        }
    }
}
