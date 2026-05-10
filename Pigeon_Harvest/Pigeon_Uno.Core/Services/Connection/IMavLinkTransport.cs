using System;

namespace Pigeon_Uno.Core.Services.Connection
{
    /// <summary>
    /// Interface for MAVLink transport layer abstraction.
    /// Separates transport mechanism (Serial/TCP/UDP) from MAVLink protocol handling.
    /// </summary>
    public interface IMavLinkTransport : IDisposable
    {
        /// <summary>
        /// Gets whether the transport is currently connected.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Gets a human-readable name for this connection.
        /// </summary>
        string ConnectionName { get; }

        /// <summary>
        /// Establishes the connection.
        /// </summary>
        void Connect();
        
        /// <summary>
        /// Closes the connection.
        /// </summary>
        void Disconnect();
        
        /// <summary>
        /// Sends a MAVLink message packet over the transport.
        /// </summary>
        /// <param name="packet">The byte array of the packet to send.</param>
        void SendPacket(byte[] packet);

        /// <summary>
        /// Event raised when raw data bytes are received from the transport.
        /// The MavLinkService will parse these bytes into packets.
        /// </summary>
        event Action<byte[]> OnDataReceived;
    }
}
