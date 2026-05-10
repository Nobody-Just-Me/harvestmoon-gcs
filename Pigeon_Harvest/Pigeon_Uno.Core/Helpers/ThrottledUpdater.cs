using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Helpers;

public class ThrottledUpdater
{
    private readonly int _intervalMs;
    private readonly Timer _timer;
    private Action? _pendingAction;
    private readonly object _lock = new();
    private bool _bypassMode = false;

    public ThrottledUpdater(int intervalMs = 33) // ~30 Hz
    {
        _intervalMs = intervalMs;
        _timer = new Timer(ExecutePendingAction, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Gets or sets bypass mode. When true, actions execute immediately without throttling.
    /// </summary>
    public bool BypassMode
    {
        get => _bypassMode;
        set => _bypassMode = value;
    }

    public void Schedule(Action action)
    {
        // If bypass mode is enabled, execute immediately
        if (_bypassMode)
        {
            action();
            return;
        }

        // Otherwise, throttle as normal
        lock (_lock)
        {
            _pendingAction = action;
            _timer.Change(_intervalMs, Timeout.Infinite);
        }
    }

    private void ExecutePendingAction(object? state)
    {
        Action? actionToExecute = null;
        
        lock (_lock)
        {
            actionToExecute = _pendingAction;
            _pendingAction = null;
        }
        
        actionToExecute?.Invoke();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
