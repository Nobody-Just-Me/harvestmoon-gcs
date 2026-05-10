using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

public class WinUIDispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;

    public WinUIDispatcherService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public bool IsUIThread => DispatcherQueue.GetForCurrentThread() != null;

    public async Task RunOnUIThreadAsync(Action action)
    {
        if (_dispatcherQueue != null)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;
        }
        else
        {
            action();
        }
    }

    public void Enqueue(Action action)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }
}
