using System;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Core.Services.Optimization;

public interface IOptimizedTelemetryHandler
{
    Task ProcessTelemetryAsync(byte[] data);
    bool ShouldProcessMessage(int messageId, DateTime utcNow);
    int GetRecommendedDispatchIntervalMs();
    void SetUpdateRate(int updatesPerSecond);
    void EnableBatching(bool enable);
    TelemetryProcessingStats GetStats();
}

public class TelemetryProcessingStats
{
    public int PacketsProcessed { get; set; }
    public double AverageProcessingTime { get; set; }
    public int QueuedPackets { get; set; }
    
    // Additional properties for compatibility
    public long TotalPacketsProcessed { get; set; }
    public long TotalPacketsFiltered { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double LastProcessingTime { get; set; }
    public int CurrentBatchSize { get; set; }
    public int CurrentPollingRateMs { get; set; }
}
