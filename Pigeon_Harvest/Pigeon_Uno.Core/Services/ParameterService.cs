using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using MavLinkNet;

namespace Pigeon_Uno.Core.Services;

/// <summary>
/// Service for managing drone parameters via MAVLink protocol.
/// Implements complete parameter read/write/validation functionality.
/// </summary>
public class ParameterService : IParameterService, IDisposable
{
    private readonly IMavLinkService _mavLinkService;
    private readonly Dictionary<string, Parameter> _parameters = new();
    private readonly Dictionary<string, Parameter> _defaultParameters = new();
    private readonly SemaphoreSlim _paramLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    
    private bool _isLoading;
    private int _totalParameters;
    private int _loadedParameters;
    private CancellationTokenSource? _loadCancellation;
    private TaskCompletionSource<bool>? _loadCompletionSource;
    private TaskCompletionSource<Parameter>? _singleParamTcs;
    private TaskCompletionSource<bool>? _setParamTcs;
    private string? _requestedParamName;
    private int _requestedParamIndex = -1;
    
    // Events
    public event EventHandler<Parameter>? ParameterReceived;
    public event EventHandler<Parameter>? ParameterUpdated;
    public event EventHandler<int>? LoadingProgressChanged;
    public event EventHandler<string>? ErrorOccurred;
    
    // Properties
    public bool IsLoading
    {
        get
        {
            lock (_paramLock)
            {
                return _isLoading;
            }
        }
    }
    
    public int TotalParameters
    {
        get
        {
            lock (_paramLock)
            {
                return _totalParameters;
            }
        }
    }
    
    public int LoadedParameters
    {
        get
        {
            lock (_paramLock)
            {
                return _loadedParameters;
            }
        }
    }
    
    public ParameterService(IMavLinkService mavLinkService)
    {
        _mavLinkService = mavLinkService ?? throw new ArgumentNullException(nameof(mavLinkService));
        
        // Subscribe to MAVLink packets
        _mavLinkService.PacketReceived += OnPacketReceived;
    }
    
    private void OnPacketReceived(object? sender, MavLinkPacketBase packet)
    {
        try
        {
            if (packet.Message is UasParamValue paramValue)
            {
                HandleParamValue(paramValue);
            }
        }
        catch (Exception ex)
        {
            RaiseError($"Error processing parameter packet: {ex.Message}");
        }
    }
    
    private void HandleParamValue(UasParamValue paramValue)
    {
        try
        {
            // Extract parameter name (16 bytes, null-terminated)
            var paramNameChars = paramValue.ParamId;
            var paramName = new string(paramNameChars).TrimEnd('\0');
            var paramIndex = (int)paramValue.ParamIndex;
            var paramCount = (int)paramValue.ParamCount;
            var value = paramValue.ParamValue;
            var paramType = (ParameterType)paramValue.ParamType;
            
            // Create or update parameter
            var parameter = new Parameter
            {
                Name = paramName,
                Value = value,
                Index = paramIndex,
                Type = paramType,
                LastUpdated = DateTime.Now
            };
            
            // Store in cache
            lock (_parameters)
            {
                if (_parameters.ContainsKey(paramName))
                {
                    // Update existing
                    var existing = _parameters[paramName];
                    existing.Value = value;
                    existing.LastUpdated = DateTime.Now;
                    parameter = existing;
                }
                else
                {
                    // Add new
                    _parameters[paramName] = parameter;
                }
                
                // Update total count
                if (paramCount > 0 && _totalParameters != paramCount)
                {
                    lock (_paramLock)
                    {
                        _totalParameters = paramCount;
                    }
                }
                
                // Update loaded count
                lock (_paramLock)
                {
                    _loadedParameters = _parameters.Count;
                }
            }
            
            // Raise events
            ParameterReceived?.Invoke(this, parameter);
            LoadingProgressChanged?.Invoke(this, _loadedParameters);
            
            // Check if this is the parameter we're waiting for
            if (_singleParamTcs != null)
            {
                if ((_requestedParamName != null && paramName == _requestedParamName) ||
                    (_requestedParamIndex >= 0 && paramIndex == _requestedParamIndex))
                {
                    _singleParamTcs.TrySetResult(parameter);
                    _singleParamTcs = null;
                    _requestedParamName = null;
                    _requestedParamIndex = -1;
                }
            }
            
            // Check if we're done loading all parameters
            if (_isLoading && _loadedParameters >= _totalParameters && _totalParameters > 0)
            {
                lock (_paramLock)
                {
                    _isLoading = false;
                }
                _loadCompletionSource?.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            RaiseError($"Error handling parameter value: {ex.Message}");
        }
    }
    
    public async Task<bool> RequestAllParametersAsync()
    {
        await _requestLock.WaitAsync();
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                RaiseError("Not connected to drone");
                return false;
            }
            
            // Cancel any ongoing load
            _loadCancellation?.Cancel();
            _loadCancellation = new CancellationTokenSource();
            
            // Reset state
            lock (_paramLock)
            {
                _isLoading = true;
                _loadedParameters = 0;
                _totalParameters = 0;
            }
            
            _loadCompletionSource = new TaskCompletionSource<bool>();
            
            // Send PARAM_REQUEST_LIST
            var request = new UasParamRequestList
            {
                TargetSystem = 1, // Default system ID
                TargetComponent = 1 // Default component ID
            };
            
            _mavLinkService.SendMessage(request);
            
            // Wait for completion with timeout
            var timeoutTask = Task.Delay(30000, _loadCancellation.Token); // 30 second timeout
            var completedTask = await Task.WhenAny(_loadCompletionSource.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                lock (_paramLock)
                {
                    _isLoading = false;
                }
                RaiseError("Parameter request timed out");
                return false;
            }
            
            return await _loadCompletionSource.Task;
        }
        catch (Exception ex)
        {
            lock (_paramLock)
            {
                _isLoading = false;
            }
            RaiseError($"Failed to request parameters: {ex.Message}");
            return false;
        }
        finally
        {
            _requestLock.Release();
        }
    }
    
    public async Task<Parameter> RequestParameterAsync(string parameterName)
    {
        await _requestLock.WaitAsync();
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                throw new InvalidOperationException("Not connected to drone");
            }
            
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name cannot be empty", nameof(parameterName));
            }
            
            // Check cache first
            lock (_parameters)
            {
                if (_parameters.TryGetValue(parameterName, out var cached))
                {
                    return cached;
                }
            }
            
            // Setup wait for response
            _singleParamTcs = new TaskCompletionSource<Parameter>();
            _requestedParamName = parameterName;
            _requestedParamIndex = -1;
            
            // Send PARAM_REQUEST_READ by name
            var paramIdChars = new char[16];
            var nameChars = parameterName.ToCharArray();
            Array.Copy(nameChars, paramIdChars, Math.Min(nameChars.Length, 16));
            
            var request = new UasParamRequestRead
            {
                TargetSystem = 1,
                TargetComponent = 1,
                ParamId = paramIdChars,
                ParamIndex = -1 // Use name, not index
            };
            
            _mavLinkService.SendMessage(request);
            
            // Wait for response with timeout
            using var cts = new CancellationTokenSource(5000); // 5 second timeout
            var timeoutTask = Task.Delay(5000, cts.Token);
            var completedTask = await Task.WhenAny(_singleParamTcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _singleParamTcs = null;
                _requestedParamName = null;
                throw new TimeoutException($"Request for parameter '{parameterName}' timed out");
            }
            
            return await _singleParamTcs.Task;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task<Parameter> RequestParameterAsync(int parameterIndex)
    {
        await _requestLock.WaitAsync();
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                throw new InvalidOperationException("Not connected to drone");
            }
            
            if (parameterIndex < 0)
            {
                throw new ArgumentException("Parameter index must be non-negative", nameof(parameterIndex));
            }
            
            // Setup wait for response
            _singleParamTcs = new TaskCompletionSource<Parameter>();
            _requestedParamName = null;
            _requestedParamIndex = parameterIndex;
            
            // Send PARAM_REQUEST_READ by index
            var request = new UasParamRequestRead
            {
                TargetSystem = 1,
                TargetComponent = 1,
                ParamId = new char[16], // Empty for index-based request
                ParamIndex = (short)parameterIndex
            };
            
            _mavLinkService.SendMessage(request);
            
            // Wait for response with timeout
            using var cts = new CancellationTokenSource(5000);
            var timeoutTask = Task.Delay(5000, cts.Token);
            var completedTask = await Task.WhenAny(_singleParamTcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _singleParamTcs = null;
                _requestedParamIndex = -1;
                throw new TimeoutException($"Request for parameter index {parameterIndex} timed out");
            }
            
            return await _singleParamTcs.Task;
        }
        finally
        {
            _requestLock.Release();
        }
    }
    
    public async Task<bool> SetParameterAsync(string parameterName, float value)
    {
        await _requestLock.WaitAsync();
        try
        {
            if (!_mavLinkService.IsConnected)
            {
                RaiseError("Not connected to drone");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                RaiseError("Parameter name cannot be empty");
                return false;
            }
            
            // Validate parameter
            var validation = await ValidateParameterAsync(parameterName, value);
            if (!validation.IsValid)
            {
                RaiseError($"Parameter validation failed: {validation.ErrorMessage}");
                return false;
            }
            
            // Setup wait for confirmation
            _setParamTcs = new TaskCompletionSource<bool>();
            
            // Get parameter type
            ParameterType paramType = ParameterType.Float;
            lock (_parameters)
            {
                if (_parameters.TryGetValue(parameterName, out var param))
                {
                    paramType = param.Type;
                }
            }
            
            // Send PARAM_SET
            var paramIdChars = new char[16];
            var nameChars = parameterName.ToCharArray();
            Array.Copy(nameChars, paramIdChars, Math.Min(nameChars.Length, 16));
            
            var paramSet = new UasParamSet
            {
                TargetSystem = 1,
                TargetComponent = 1,
                ParamId = paramIdChars,
                ParamValue = value,
                ParamType = (MavParamType)paramType
            };
            
            _mavLinkService.SendMessage(paramSet);
            
            // Wait for confirmation with timeout
            using var cts = new CancellationTokenSource(5000);
            var timeoutTask = Task.Delay(5000, cts.Token);
            var completedTask = await Task.WhenAny(_setParamTcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _setParamTcs = null;
                RaiseError($"Set parameter '{parameterName}' timed out");
                return false;
            }
            
            // Update local cache
            lock (_parameters)
            {
                if (_parameters.TryGetValue(parameterName, out var param))
                {
                    param.Value = value;
                    param.LastUpdated = DateTime.Now;
                    ParameterUpdated?.Invoke(this, param);
                }
            }
            
            return await _setParamTcs.Task;
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to set parameter: {ex.Message}");
            return false;
        }
        finally
        {
            _requestLock.Release();
        }
    }
    
    public async Task<IReadOnlyList<Parameter>> GetAllParametersAsync()
    {
        await _paramLock.WaitAsync();
        try
        {
            return _parameters.Values.OrderBy(p => p.Name).ToList();
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public async Task<Parameter> GetParameterAsync(string parameterName)
    {
        await _paramLock.WaitAsync();
        try
        {
            if (_parameters.TryGetValue(parameterName, out var param))
            {
                return param;
            }
            
            throw new KeyNotFoundException($"Parameter '{parameterName}' not found");
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public async Task<IReadOnlyList<Parameter>> SearchParametersAsync(string searchPattern)
    {
        await _paramLock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                return _parameters.Values.OrderBy(p => p.Name).ToList();
            }
            
            var pattern = searchPattern.ToUpperInvariant();
            return _parameters.Values
                .Where(p => p.Name.ToUpperInvariant().Contains(pattern) ||
                           p.Description.ToUpperInvariant().Contains(pattern) ||
                           p.Group.ToUpperInvariant().Contains(pattern))
                .OrderBy(p => p.Name)
                .ToList();
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public async Task<IReadOnlyList<Parameter>> GetModifiedParametersAsync()
    {
        await _paramLock.WaitAsync();
        try
        {
            return _parameters.Values
                .Where(p => p.IsModified)
                .OrderBy(p => p.Name)
                .ToList();
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public async Task<bool> ExportParametersAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                RaiseError("File path cannot be empty");
                return false;
            }
            
            var parameters = await GetAllParametersAsync();
            
            using var writer = new StreamWriter(filePath);
            
            // Write header
            await writer.WriteLineAsync("# Pigeon Parameter Export");
            await writer.WriteLineAsync($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"# Total Parameters: {parameters.Count}");
            await writer.WriteLineAsync();
            
            // Write parameters in QGC format
            foreach (var param in parameters)
            {
                await writer.WriteLineAsync($"{param.Name}\t{param.Value}");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to export parameters: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> ImportParametersAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                RaiseError("File path cannot be empty");
                return false;
            }
            
            if (!File.Exists(filePath))
            {
                RaiseError($"File not found: {filePath}");
                return false;
            }
            
            var lines = await File.ReadAllLinesAsync(filePath);
            var imported = 0;
            var failed = 0;
            
            foreach (var line in lines)
            {
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                
                // Parse parameter line (format: NAME\tVALUE)
                var parts = line.Split('\t', ' ');
                if (parts.Length < 2)
                    continue;
                
                var name = parts[0].Trim();
                if (float.TryParse(parts[1].Trim(), out var value))
                {
                    if (await SetParameterAsync(name, value))
                    {
                        imported++;
                    }
                    else
                    {
                        failed++;
                    }
                    
                    // Small delay to avoid overwhelming the drone
                    await Task.Delay(50);
                }
            }
            
            if (failed > 0)
            {
                RaiseError($"Imported {imported} parameters, {failed} failed");
            }
            
            return failed == 0;
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to import parameters: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> ResetParameterAsync(string parameterName)
    {
        try
        {
            // Get default value
            float defaultValue = 0;
            lock (_defaultParameters)
            {
                if (_defaultParameters.TryGetValue(parameterName, out var defaultParam))
                {
                    defaultValue = defaultParam.Value;
                }
                else
                {
                    // If no default stored, get current parameter's default
                    lock (_parameters)
                    {
                        if (_parameters.TryGetValue(parameterName, out var param))
                        {
                            defaultValue = param.DefaultValue;
                        }
                    }
                }
            }
            
            return await SetParameterAsync(parameterName, defaultValue);
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to reset parameter: {ex.Message}");
            return false;
        }
    }
    
    public async Task<ParameterValidationResult> ValidateParameterAsync(string parameterName, float value)
    {
        await _paramLock.WaitAsync();
        try
        {
            var result = new ParameterValidationResult
            {
                IsValid = true,
                SuggestedValue = value
            };
            
            // Check if parameter exists
            if (!_parameters.TryGetValue(parameterName, out var param))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Parameter '{parameterName}' not found";
                return result;
            }
            
            // Check min/max bounds
            if (value < param.MinValue)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Value {value} is below minimum {param.MinValue}";
                result.SuggestedValue = param.MinValue;
                return result;
            }
            
            if (value > param.MaxValue)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Value {value} is above maximum {param.MaxValue}";
                result.SuggestedValue = param.MaxValue;
                return result;
            }
            
            // Type-specific validation
            switch (param.Type)
            {
                case ParameterType.Int8:
                case ParameterType.Int16:
                case ParameterType.Int32:
                case ParameterType.UInt8:
                case ParameterType.UInt16:
                case ParameterType.UInt32:
                    // Check if value is integer
                    if (Math.Abs(value - Math.Round(value)) > 0.0001f)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Value must be an integer";
                        result.SuggestedValue = (float)Math.Round(value);
                        return result;
                    }
                    break;
            }
            
            return result;
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public void ClearParameters()
    {
        lock (_parameters)
        {
            _parameters.Clear();
        }
        
        lock (_paramLock)
        {
            _loadedParameters = 0;
            _totalParameters = 0;
            _isLoading = false;
        }
    }
    
    private void RaiseError(string message)
    {
        ErrorOccurred?.Invoke(this, message);
        System.Diagnostics.Debug.WriteLine($"[ParameterService] Error: {message}");
    }
    
    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _paramLock?.Dispose();
        _requestLock?.Dispose();
        
        if (_mavLinkService != null)
        {
            _mavLinkService.PacketReceived -= OnPacketReceived;
        }
    }
}
