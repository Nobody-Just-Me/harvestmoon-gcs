using System;

namespace HarvestmoonGCS.Core.Exceptions;

/// <summary>
/// Base exception class for all Pigeon application exceptions
/// </summary>
public class PigeonException : Exception
{
    public PigeonException()
    {
    }

    public PigeonException(string message) : base(message)
    {
    }

    public PigeonException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
