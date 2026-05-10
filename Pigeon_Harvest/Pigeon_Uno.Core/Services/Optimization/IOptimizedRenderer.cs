namespace Pigeon_Uno.Core.Services.Optimization;

public interface IOptimizedRenderer
{
    void RecordFrameTime(double frameTimeMs);
    void EnableEmulatorMode(bool enable);
    void SetRenderingQuality(RenderingQuality quality);
    void SetTargetFPS(int fps);
    void EnableHardwareAcceleration(bool enable);
    void ReduceVisualEffects(bool reduce);
    void EnableVirtualization(bool enable);
    RenderingMetrics GetMetrics();
}

public class RenderingMetrics
{
    public double AverageFPS { get; set; }
    public double FrameTime { get; set; }
    public int DroppedFrames { get; set; }
    
    // Additional properties for compatibility
    public double CurrentFPS { get; set; }
    public int TargetFPS { get; set; }
    public int QualityLevel { get; set; }
    public DateTime LastUpdated { get; set; }
}
