using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Services.Connection;

public interface IConnectionTransport : IDisposable
{
    Task ConnectAsync();
    Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
    bool IsConnected { get; }
}
