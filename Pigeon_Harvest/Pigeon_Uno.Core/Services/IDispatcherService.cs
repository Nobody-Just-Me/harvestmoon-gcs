using System;
using System.Threading.Tasks;

namespace Pigeon_Uno.Services;

public interface IDispatcherService
{
    void Enqueue(Action action);
    Task RunOnUIThreadAsync(Action action);
    bool IsUIThread { get; }
}
