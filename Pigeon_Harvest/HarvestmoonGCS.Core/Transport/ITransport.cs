using System;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Transport;

/// <summary>
/// Defines the interface for transport layer communication (Serial, UDP, TCP).
/// </summary>
public interface ITransport : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Establishes a connection using the transport.
    /// </summary>
    /// <returns>True if connection was successful, false otherwise.</returns>
    Task<bool> ConnectAsync();

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Reads data from the transport asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="offset">The offset in the buffer to start writing data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the read operation.</param>
    /// <returns>The number of bytes read.</returns>
    Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Writes data to the transport asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="offset">The offset in the buffer to start reading data.</param>
    /// <param name="count">The number of bytes to write.</param>
    Task WriteAsync(byte[] buffer, int offset, int count);
}
