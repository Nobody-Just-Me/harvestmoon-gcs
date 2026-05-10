namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Represents a camera source (device or stream)
/// </summary>
public class CameraSource
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CameraSourceType Type { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// Camera source types
/// </summary>
public enum CameraSourceType
{
    LocalCamera,
    NetworkStream,
    VideoFile,
    SimulatedCamera,
    USB,
    File
}

/// <summary>
/// Camera control commands
/// </summary>
public enum CameraControlCommand
{
    Zoom,
    Focus,
    Brightness,
    Contrast,
    Saturation,
    Exposure,
    WhiteBalance
}
