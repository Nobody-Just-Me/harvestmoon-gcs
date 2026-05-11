using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Implements MAVLink parameter protocol for reading/writing parameters
/// </summary>
internal class ParameterProtocol
{
    private readonly MavLinkService _service;
    private readonly Dictionary<string, float> _parameters = new Dictionary<string, float>();
    private readonly SemaphoreSlim _paramLock = new SemaphoreSlim(1, 1);
    private TaskCompletionSource<Dictionary<string, float>>? _requestTcs;
    private TaskCompletionSource<bool>? _setTcs;
    private int _expectedCount;
    private int _receivedCount;
    
    public ParameterProtocol(MavLinkService service)
    {
        _service = service;
    }
    
    public async Task RequestParametersAsync()
    {
        await _paramLock.WaitAsync();
        try
        {
            var transport = _service.GetTransport();
            if (transport == null) return;
            
            // Send PARAM_REQUEST_LIST
            var request = new UasParamRequestList
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId()
            };
            
            transport.SendMessage(request);
            
            // For MVP, just send the request
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ParameterProtocol] Request failed: {ex.Message}");
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public Task RequestAllParametersAsync() => RequestParametersAsync();
    
    public async Task<Dictionary<string, float>> GetParametersAsync()
    {
        await _paramLock.WaitAsync();
        try
        {
            return new Dictionary<string, float>(_parameters);
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public Task<Dictionary<string, float>> GetAllParametersAsync() => GetParametersAsync();
    
    public async Task<bool> SetParameterAsync(string name, float value)
    {
        await _paramLock.WaitAsync();
        try
        {
            var transport = _service.GetTransport();
            if (transport == null) return false;
            
            // Send PARAM_SET
            var paramSet = new UasParamSet
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                ParamId = name.PadRight(16, '\0').ToCharArray(),
                ParamValue = value,
                ParamType = MavParamType.Real32
            };
            
            transport.SendMessage(paramSet);
            
            // For MVP, return true immediately
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ParameterProtocol] Set parameter failed: {ex.Message}");
            return false;
        }
        finally
        {
            _paramLock.Release();
        }
    }
    
    public void HandleParamValue(UasParamValue paramValue)
    {
        try
        {
            var paramName = new string(paramValue.ParamId).TrimEnd('\0');
            
            lock (_parameters)
            {
                _parameters[paramName] = paramValue.ParamValue;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ParameterProtocol] Received parameter: {paramName} = {paramValue.ParamValue}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ParameterProtocol] Handle param value failed: {ex.Message}");
        }
    }
}
