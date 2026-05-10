using System;

namespace Pigeon_Uno.Core.Exceptions;

/// <summary>
/// Exception thrown when file operations fail
/// </summary>
public class FileOperationException : PigeonException
{
    public string? FilePath { get; set; }
    public string? Operation { get; set; }

    public FileOperationException()
    {
    }

    public FileOperationException(string message) : base(message)
    {
    }

    public FileOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public FileOperationException(string message, string filePath, string operation) : base(message)
    {
        FilePath = filePath;
        Operation = operation;
    }

    public FileOperationException(string message, string filePath, string operation, Exception innerException) 
        : base(message, innerException)
    {
        FilePath = filePath;
        Operation = operation;
    }
}
