using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Services;

public interface IDispatcherService
{
    void Enqueue(Action action);
    Task RunOnUIThreadAsync(Action action);
    bool IsUIThread { get; }
}
