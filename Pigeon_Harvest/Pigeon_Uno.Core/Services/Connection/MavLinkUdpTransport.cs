using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Pigeon_Uno.Core.Services.Connection
{
    /// <summary>
    /// UDP transport implementation for MAVLink communication.
    /// Handles UDP socket connection, reading, and writing.
    /// </summary>
    public class MavLinkUdpTransport : IMavLinkTransport
    {
        private UdpClient? _udpClient;
        private IPEndPoint _remoteEndPoint;
        private readonly int _localPort;
        private Thread? _readThread;
        private bool _isRunning;

        public bool IsConnected => _isRunning; // UDP is connectionless, but we track running state
        public string ConnectionName => $"UDP:{_localPort}";

        public event Action<byte[]>? OnDataReceived;

        public MavLinkUdpTransport(int localPort = 14550)
        {
            _localPort = localPort;
            // Remote endpoint (GCS usually listens, Drone sends to GCS)
            // But for sending back commands, we need to know where to send.
            // Usually we send to the sender of the last packet.
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0); 
        }

        public void Connect()
        {
            if (_isRunning) return;

            _udpClient = new UdpClient(_localPort);
            _isRunning = true;

            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Start();
            
            System.Diagnostics.Debug.WriteLine($"[MavLinkUdpTransport] Connected to {ConnectionName}");
        }

        public void Disconnect()
        {
            _isRunning = false;
            
            try
            {
                _udpClient?.Close();
                System.Diagnostics.Debug.WriteLine($"[MavLinkUdpTransport] Disconnected from {ConnectionName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MavLinkUdpTransport] Disconnect error: {ex.Message}");
            }
        }

        public void SendPacket(byte[] packet)
        {
            if (_isRunning && _udpClient != null)
            {
                try
                {
                    // We typically send to a known Target IP if configured, or the last received address.
                    // For now, let's assume we discovered the endpoint or configured it.
                    // In simple UDP telemetry, often we just broadcast or reply.
                    
                    // For simplicity, if we have received a packet, _remoteEndPoint contains the sender.
                    // If it's 0.0.0.0, we can't send yet.
                    if (_remoteEndPoint.Address != IPAddress.Any)
                    {
                        _udpClient.Send(packet, packet.Length, _remoteEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MavLinkUdpTransport] Send error: {ex.Message}");
                }
            }
        }

        private void ReadLoop()
        {
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
             
            while (_isRunning && _udpClient != null)
            {
                try
                {
                    byte[] data = _udpClient.Receive(ref anyIP);
                    
                    // Update remote endpoint to reply to
                    _remoteEndPoint = anyIP; 
                    
                    // Send raw data to be parsed by MavLinkService
                    OnDataReceived?.Invoke(data);
                }
                catch (SocketException)
                {
                    // Happens on Close
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MavLinkUdpTransport] Read error: {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            _udpClient?.Dispose();
        }
    }
}
