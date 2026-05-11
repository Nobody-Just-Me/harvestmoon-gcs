using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services.Optimization;

public interface IOptimizedNetworkManager
{
    Task<bool> ConnectAsync(string address, int port);
    Task DisconnectAsync();
    void SetBufferSize(int size);
    void EnableCompression(bool enable);
}
