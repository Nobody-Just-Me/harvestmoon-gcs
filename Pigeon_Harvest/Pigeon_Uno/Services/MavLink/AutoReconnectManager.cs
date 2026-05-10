using System;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Manages automatic reconnection with exponential backoff
/// </summary>
internal class AutoReconnectManager : IDisposable
{
    private readonly MavLinkService _service;
    private Timer? _reconnectTimer;
    private CancellationTokenSource? _reconnectCts;
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 5;
    private const int BaseReconnectDelayMs = 1000;
    private const int MaxReconnectDelayMs = 30000;
    
    public bool AutoReconnectEnabled { get; set; } = true;
    
    public AutoReconnectManager(MavLinkService service)
    {
        _service = service;
    }
    
    public async Task ScheduleReconnectAsync(ConnectionConfig config)
    {
        if (!AutoReconnectEnabled) return;
        if (_reconnectAttempts >= MaxReconnectAttempts) return;
        
        _reconnectAttempts++;
        var delay = CalculateBackoffDelay();
        
        System.Diagnostics.Debug.WriteLine($"[AutoReconnect] Scheduling reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {delay}ms");
        
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(delay, _reconnectCts.Token);
            await _service.ConnectAsync(config);
        }
        catch (TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[AutoReconnect] Reconnect cancelled");
        }
    }
    
    public void CancelReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }
    
    public void ResetAttempts()
    {
        _reconnectAttempts = 0;
    }
    
    private int CalculateBackoffDelay()
    {
        return Math.Min(
            BaseReconnectDelayMs * (int)Math.Pow(2, _reconnectAttempts - 1),
            MaxReconnectDelayMs
        );
    }
    
    public void Dispose()
    {
        _reconnectCts?.Cancel();
        _reconnectTimer?.Dispose();
    }
}
