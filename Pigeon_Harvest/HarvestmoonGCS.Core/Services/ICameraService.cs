using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

public interface ICameraService
{
    bool IsStreaming { get; }
    bool IsRecording { get; }
    
    event EventHandler<byte[]> FrameReceived;
    event EventHandler<bool> StreamingStatusChanged;
    event EventHandler<bool> RecordingStatusChanged;
    event EventHandler<string> ConnectionError;

    Task InitializeAsync();
    Task<List<CameraSource>> GetAvailableSourcesAsync();
    Task<bool> StartCameraAsync(string source);
    Task StopCameraAsync();
    Task<bool> TakePictureAsync(string filename = null);
    Task<bool> StartRecordingAsync(string filename = null);
    Task<bool> StopRecordingAsync();
    Task SendCameraControlAsync(CameraControlCommand command, float value);
}
