using System;

namespace HarvestmoonGCS.Core.Exceptions;

/// <summary>
/// Exception thrown when connection operations fail
/// </summary>
public class ConnectionException : PigeonException
{
    public string? ConnectionType { get; set; }
    public string? Address { get; set; }
    public int? Port { get; set; }

    public ConnectionException()
    {
    }

    public ConnectionException(string message) : base(message)
    {
    }

    public ConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConnectionException(string message, string connectionType, string address, int port) 
        : base(message)
    {
        ConnectionType = connectionType;
        Address = address;
        Port = port;
    }

    public ConnectionException(string message, string connectionType, string address, int port, Exception innerException) 
        : base(message, innerException)
    {
        ConnectionType = connectionType;
        Address = address;
        Port = port;
    }
}
