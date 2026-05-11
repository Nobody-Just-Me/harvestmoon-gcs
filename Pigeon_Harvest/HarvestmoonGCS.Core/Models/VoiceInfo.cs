namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Information about an available voice for text-to-speech
/// </summary>
public class VoiceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Language { get; set; } = "";
    public string Gender { get; set; } = "";
}
