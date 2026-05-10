namespace Pigeon_Uno.Core.Models;

/// <summary>
/// Configuration for LoRa radio parameters
/// </summary>
public class LoRaConfig
{
    /// <summary>
    /// Operating frequency in MHz (e.g., 433, 868, 915)
    /// </summary>
    public float Frequency { get; set; } = 915.0f;

    /// <summary>
    /// Bandwidth in kHz (125, 250, 500)
    /// </summary>
    public int Bandwidth { get; set; } = 125;

    /// <summary>
    /// Spreading factor (6-12)
    /// Higher values = longer range but slower data rate
    /// </summary>
    public int SpreadingFactor { get; set; } = 7;

    /// <summary>
    /// Coding rate (5-8)
    /// Higher values = more error correction but slower data rate
    /// </summary>
    public int CodingRate { get; set; } = 5;

    /// <summary>
    /// Transmission power in dBm (2-20)
    /// </summary>
    public int TxPower { get; set; } = 17;

    /// <summary>
    /// Preamble length (6-65535)
    /// </summary>
    public int PreambleLength { get; set; } = 8;

    /// <summary>
    /// Sync word (0x00-0xFF)
    /// </summary>
    public byte SyncWord { get; set; } = 0x12;

    /// <summary>
    /// Enable CRC checking
    /// </summary>
    public bool EnableCRC { get; set; } = true;

    /// <summary>
    /// Enable low data rate optimization
    /// </summary>
    public bool LowDataRateOptimize { get; set; } = false;
}
