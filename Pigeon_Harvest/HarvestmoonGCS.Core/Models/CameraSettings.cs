namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Camera settings and configuration
/// </summary>
public class CameraSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public int FrameRate { get; set; } = 30;
    public int Brightness { get; set; } = 50;
    public int Contrast { get; set; } = 50;
    public int Saturation { get; set; } = 50;
    public bool AutoFocus { get; set; } = true;
    public int Zoom { get; set; } = 1;
    public string Format { get; set; } = "H264";
}

/// <summary>
/// Video format information
/// </summary>
public class VideoFormat
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Fps { get; set; }
    public string Codec { get; set; } = "H264";
}

/// <summary>
/// Video codec types
/// </summary>
public enum VideoCodec
{
    H264,
    H265,
    MJPEG,
    VP8,
    VP9
}
