using System;

namespace Pigeon_Uno.Core.Exceptions;

/// <summary>
/// Exception thrown when MAVLink operations fail
/// </summary>
public class MAVLinkException : PigeonException
{
    public string? MessageType { get; set; }
    public int? MessageId { get; set; }

    public MAVLinkException()
    {
    }

    public MAVLinkException(string message) : base(message)
    {
    }

    public MAVLinkException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public MAVLinkException(string message, string messageType) : base(message)
    {
        MessageType = messageType;
    }

    public MAVLinkException(string message, string messageType, int messageId) : base(message)
    {
        MessageType = messageType;
        MessageId = messageId;
    }

    public MAVLinkException(string message, string messageType, Exception innerException) 
        : base(message, innerException)
    {
        MessageType = messageType;
    }
}
