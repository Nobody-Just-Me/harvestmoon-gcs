using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for managing drone parameters via MAVLink.
/// </summary>
public interface IParameterService
{
    /// <summary>
    /// Gets whether parameters are currently loading.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Gets the total number of parameters.
    /// </summary>
    int TotalParameters { get; }

    /// <summary>
    /// Gets the number of loaded parameters.
    /// </summary>
    int LoadedParameters { get; }

    /// <summary>
    /// Raised when a parameter is received.
    /// </summary>
    event EventHandler<Parameter> ParameterReceived;

    /// <summary>
    /// Raised when a parameter is updated.
    /// </summary>
    event EventHandler<Parameter> ParameterUpdated;

    /// <summary>
    /// Raised when loading progress changes.
    /// </summary>
    event EventHandler<int> LoadingProgressChanged;

    /// <summary>
    /// Raised when an error occurs.
    /// </summary>
    event EventHandler<string> ErrorOccurred;

    /// <summary>
    /// Requests all parameters from the drone.
    /// </summary>
    Task<bool> RequestAllParametersAsync();

    /// <summary>
    /// Requests a specific parameter by name.
    /// </summary>
    Task<Parameter> RequestParameterAsync(string parameterName);

    /// <summary>
    /// Requests a specific parameter by index.
    /// </summary>
    Task<Parameter> RequestParameterAsync(int parameterIndex);

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    Task<bool> SetParameterAsync(string parameterName, float value);

    /// <summary>
    /// Gets all loaded parameters.
    /// </summary>
    Task<IReadOnlyList<Parameter>> GetAllParametersAsync();

    /// <summary>
    /// Gets a parameter by name.
    /// </summary>
    Task<Parameter> GetParameterAsync(string parameterName);

    /// <summary>
    /// Searches parameters by name pattern.
    /// </summary>
    Task<IReadOnlyList<Parameter>> SearchParametersAsync(string searchPattern);

    /// <summary>
    /// Gets modified parameters (different from default).
    /// </summary>
    Task<IReadOnlyList<Parameter>> GetModifiedParametersAsync();

    /// <summary>
    /// Exports parameters to a file.
    /// </summary>
    Task<bool> ExportParametersAsync(string filePath);

    /// <summary>
    /// Imports parameters from a file.
    /// </summary>
    Task<bool> ImportParametersAsync(string filePath);

    /// <summary>
    /// Resets a parameter to its default value.
    /// </summary>
    Task<bool> ResetParameterAsync(string parameterName);

    /// <summary>
    /// Validates a parameter value against constraints.
    /// </summary>
    Task<ParameterValidationResult> ValidateParameterAsync(string parameterName, float value);

    /// <summary>
    /// Clears all cached parameters.
    /// </summary>
    void ClearParameters();
}

/// <summary>
/// Represents a drone parameter.
/// </summary>
public class Parameter
{
    public string Name { get; set; } = "";
    public float Value { get; set; }
    public float DefaultValue { get; set; }
    public float MinValue { get; set; } = float.MinValue;
    public float MaxValue { get; set; } = float.MaxValue;
    public int Index { get; set; }
    public ParameterType Type { get; set; }
    public string Description { get; set; } = "";
    public string Group { get; set; } = "";
    public bool IsModified => Math.Abs(Value - DefaultValue) > 0.0001f;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

/// <summary>
/// Parameter types.
/// </summary>
public enum ParameterType
{
    Int8,
    Int16,
    Int32,
    UInt8,
    UInt16,
    UInt32,
    Float,
    Double
}

/// <summary>
/// Parameter validation result.
/// </summary>
public class ParameterValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
    public float SuggestedValue { get; set; }
}
