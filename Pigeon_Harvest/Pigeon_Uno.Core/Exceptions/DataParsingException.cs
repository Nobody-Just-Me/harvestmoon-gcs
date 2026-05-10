using System;

namespace Pigeon_Uno.Core.Exceptions;

/// <summary>
/// Exception thrown when data parsing fails
/// </summary>
public class DataParsingException : PigeonException
{
    public string? DataType { get; set; }
    public byte[]? RawData { get; set; }

    public DataParsingException()
    {
    }

    public DataParsingException(string message) : base(message)
    {
    }

    public DataParsingException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DataParsingException(string message, string dataType) : base(message)
    {
        DataType = dataType;
    }

    public DataParsingException(string message, string dataType, byte[] rawData) : base(message)
    {
        DataType = dataType;
        RawData = rawData;
    }

    public DataParsingException(string message, string dataType, Exception innerException) 
        : base(message, innerException)
    {
        DataType = dataType;
    }
}
