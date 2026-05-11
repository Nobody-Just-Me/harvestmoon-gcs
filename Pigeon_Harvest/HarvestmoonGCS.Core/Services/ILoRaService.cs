using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Interface for LoRa radio communication service
/// </summary>
public interface ILoRaService
{
    /// <summary>
    /// Gets whether the service is connected to a LoRa device
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised when a LoRa device is discovered during scanning
    /// </summary>
    event EventHandler<LoRaDevice>? DeviceDiscovered;

    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Event raised when data is received from the LoRa device
    /// </summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// Scan for available LoRa devices
    /// </summary>
    /// <returns>List of discovered LoRa devices</returns>
    Task<List<LoRaDevice>> ScanDevicesAsync();

    /// <summary>
    /// Connect to a specific LoRa device
    /// </summary>
    /// <param name="device">The LoRa device to connect to</param>
    /// <returns>True if connection successful, false otherwise</returns>
    Task<bool> ConnectAsync(LoRaDevice device);

    /// <summary>
    /// Disconnect from the current LoRa device
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Send data over the LoRa connection
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <returns>True if send successful, false otherwise</returns>
    Task<bool> SendDataAsync(byte[] data);

    /// <summary>
    /// Configure LoRa parameters
    /// </summary>
    /// <param name="config">LoRa configuration</param>
    /// <returns>True if configuration successful, false otherwise</returns>
    Task<bool> ConfigureAsync(LoRaConfig config);
}
