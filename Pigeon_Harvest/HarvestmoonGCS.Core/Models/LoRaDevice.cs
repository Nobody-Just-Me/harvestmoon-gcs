namespace HarvestmoonGCS.Core.Models;

/// <summary>
/// Represents a LoRa radio device
/// </summary>
public class LoRaDevice
{
    /// <summary>
    /// Device name or identifier
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Serial port name (e.g., COM3, /dev/ttyUSB0)
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// Received Signal Strength Indicator (RSSI) in dBm
    /// </summary>
    public int RSSI { get; set; }

    /// <summary>
    /// Operating frequency in MHz
    /// </summary>
    public float Frequency { get; set; }

    /// <summary>
    /// Bandwidth in kHz
    /// </summary>
    public int Bandwidth { get; set; }

    /// <summary>
    /// Spreading factor (6-12)
    /// </summary>
    public int SpreadingFactor { get; set; }

    /// <summary>
    /// Coding rate (5-8)
    /// </summary>
    public int CodingRate { get; set; }

    /// <summary>
    /// Transmission power in dBm
    /// </summary>
    public int TxPower { get; set; }

    /// <summary>
    /// Device firmware version
    /// </summary>
    public string FirmwareVersion { get; set; } = string.Empty;

    /// <summary>
    /// Link quality indicator (0-100%)
    /// </summary>
    public int LinkQuality { get; set; }

    /// <summary>
    /// True when the serial device responded to AT commands. False means raw serial telemetry mode.
    /// </summary>
    public bool SupportsAtCommands { get; set; } = true;
}
