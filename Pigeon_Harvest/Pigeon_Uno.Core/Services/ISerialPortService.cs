using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Platform abstraction for serial port operations.
/// Provides cross-platform access to serial communication for MAVLink and LoRa modules.
/// </summary>
public interface ISerialPortService
{
    /// <summary>
    /// Gets a list of available serial port names on the current platform.
    /// </summary>
    /// <returns>List of port names (e.g., "COM1", "/dev/ttyUSB0", "/dev/cu.usbserial")</returns>
    Task<List<string>> GetAvailablePortsAsync();

    /// <summary>
    /// Opens a serial port connection with the specified parameters.
    /// </summary>
    /// <param name="portName">The name of the port to open</param>
    /// <param name="baudRate">The baud rate for communication (e.g., 57600, 115200)</param>
    /// <returns>True if the port was opened successfully, false otherwise</returns>
    Task<bool> OpenAsync(string portName, int baudRate);

    /// <summary>
    /// Closes the currently open serial port connection.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Reads data from the serial port.
    /// </summary>
    /// <param name="count">Maximum number of bytes to read</param>
    /// <returns>Array of bytes read from the port</returns>
    Task<byte[]> ReadAsync(int count);

    /// <summary>
    /// Writes data to the serial port.
    /// </summary>
    /// <param name="data">The data to write</param>
    Task WriteAsync(byte[] data);

    /// <summary>
    /// Gets whether the serial port is currently open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Event raised when data is received from the serial port.
    /// </summary>
    event EventHandler<SerialDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when an error occurs on the serial port.
    /// </summary>
    event EventHandler<SerialErrorEventArgs>? ErrorReceived;
}

/// <summary>
/// Event args for serial data received events.
/// </summary>
public class SerialDataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public int BytesReceived { get; }

    public SerialDataReceivedEventArgs(byte[] data, int bytesReceived)
    {
        Data = data;
        BytesReceived = bytesReceived;
    }
}

/// <summary>
/// Event args for serial error events.
/// </summary>
public class SerialErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public Exception? Exception { get; }

    public SerialErrorEventArgs(string errorMessage, Exception? exception = null)
    {
        ErrorMessage = errorMessage;
        Exception = exception;
    }
}
