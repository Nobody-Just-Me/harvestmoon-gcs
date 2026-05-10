using System.Threading.Tasks;

namespace Pigeon_Uno.Services;

public interface ITlogPlayerService
{
    Task LoadFileAsync(string filePath);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task SeekAsync(double position);
    void SetPlaybackSpeed(double speed);
    
    event System.EventHandler<byte[]>? TelemetryEmitted;
    
    bool IsPlaying { get; }
    double Duration { get; }
    double CurrentPosition { get; }
}
