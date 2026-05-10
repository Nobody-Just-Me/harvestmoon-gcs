#if __ANDROID__
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using AndroidApplication = Android.App.Application;
using Pigeon_Uno.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pigeon_Uno.Platforms.Android.Services;

/// <summary>
/// Android Dual Serial Port Service
/// Supports 2 USB serial devices simultaneously:
/// 1. MAVLink connection (autopilot)
/// 2. LoRa communication (telemetry radio)
/// </summary>
public class AndroidDualSerialPortService : IDisposable
{
    private readonly UsbManager? _usbManager;
    
    // MAVLink Serial Port (Primary)
    private UsbDevice? _mavlinkDevice;
    private UsbDeviceConnection? _mavlinkConnection;
    private UsbInterface? _mavlinkInterface;
    private UsbEndpoint? _mavlinkReadEndpoint;
    private UsbEndpoint? _mavlinkWriteEndpoint;
    private CancellationTokenSource? _mavlinkCancellationToken;
    private Task? _mavlinkReadTask;
    private bool _mavlinkIsOpen;
    
    // LoRa Serial Port (Secondary)
    private UsbDevice? _loraDevice;
    private UsbDeviceConnection? _loraConnection;
    private UsbInterface? _loraInterface;
    private UsbEndpoint? _loraReadEndpoint;
    private UsbEndpoint? _loraWriteEndpoint;
    private CancellationTokenSource? _loraCancellationToken;
    private Task? _loraReadTask;
    private bool _loraIsOpen;

    // Events
    public event EventHandler<SerialDataReceivedEventArgs>? MavLinkDataReceived;
    public event EventHandler<SerialDataReceivedEventArgs>? LoRaDataReceived;
    public event EventHandler<string>? ErrorOccurred;

    // Properties
    public bool IsMavLinkOpen => _mavlinkIsOpen;
    public bool IsLoRaOpen => _loraIsOpen;
    public bool AreBothOpen => _mavlinkIsOpen && _loraIsOpen;

    public AndroidDualSerialPortService()
    {
        var context = AndroidApplication.Context;
        _usbManager = (UsbManager?)context.GetSystemService(Context.UsbService);
    }

    /// <summary>
    /// Get list of all available USB serial devices
    /// </summary>
    public Task<List<UsbDeviceInfo>> GetAvailableDevicesAsync()
    {
        var devices = new List<UsbDeviceInfo>();

        if (_usbManager == null)
        {
            return Task.FromResult(devices);
        }

        try
        {
            var deviceList = _usbManager.DeviceList;
            
            foreach (var entry in deviceList)
            {
                var device = entry.Value;
                if (device != null)
                {
                    devices.Add(new UsbDeviceInfo
                    {
                        DeviceName = device.DeviceName,
                        ProductName = device.ProductName ?? "Unknown",
                        VendorId = device.VendorId,
                        ProductId = device.ProductId,
                        DeviceId = device.DeviceId,
                        DisplayName = $"{device.DeviceName} - {device.ProductName ?? "Unknown"} (VID:{device.VendorId:X4} PID:{device.ProductId:X4})"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error enumerating USB devices: {ex.Message}");
        }

        return Task.FromResult(devices);
    }

    /// <summary>
    /// Open MAVLink serial port (Primary - for autopilot)
    /// </summary>
    public async Task<bool> OpenMavLinkPortAsync(string deviceName, int baudRate = 57600)
    {
        if (_mavlinkIsOpen)
        {
            await CloseMavLinkPortAsync();
        }

        if (_usbManager == null)
        {
            ErrorOccurred?.Invoke(this, "USB Manager not available");
            return false;
        }

        try
        {
            // Find the specific USB device by name
            var deviceList = _usbManager.DeviceList;
            _mavlinkDevice = deviceList.Values.FirstOrDefault(d => d?.DeviceName == deviceName);
            
            if (_mavlinkDevice == null)
            {
                ErrorOccurred?.Invoke(this, $"MAVLink device not found: {deviceName}");
                return false;
            }

            // Check permission
            if (!_usbManager.HasPermission(_mavlinkDevice))
            {
                RequestPermission(_mavlinkDevice, "MAVLINK");
                ErrorOccurred?.Invoke(this, "MAVLink USB permission required. Please grant permission and try again.");
                return false;
            }

            // Open connection
            _mavlinkConnection = _usbManager.OpenDevice(_mavlinkDevice);
            
            if (_mavlinkConnection == null)
            {
                ErrorOccurred?.Invoke(this, "Failed to open MAVLink USB device");
                return false;
            }

            // Get the first interface
            if (_mavlinkDevice.InterfaceCount == 0)
            {
                ErrorOccurred?.Invoke(this, "No MAVLink USB interface found");
                return false;
            }

            _mavlinkInterface = _mavlinkDevice.GetInterface(0);
            
            if (!_mavlinkConnection.ClaimInterface(_mavlinkInterface, true))
            {
                ErrorOccurred?.Invoke(this, "Failed to claim MAVLink USB interface");
                return false;
            }

            // Find endpoints
            for (int i = 0; i < _mavlinkInterface.EndpointCount; i++)
            {
                var endpoint = _mavlinkInterface.GetEndpoint(i);
                if (endpoint != null)
                {
                    if (endpoint.Direction == UsbAddressing.In)
                    {
                        _mavlinkReadEndpoint = endpoint;
                    }
                    else if (endpoint.Direction == UsbAddressing.Out)
                    {
                        _mavlinkWriteEndpoint = endpoint;
                    }
                }
            }

            if (_mavlinkReadEndpoint == null || _mavlinkWriteEndpoint == null)
            {
                ErrorOccurred?.Invoke(this, "Failed to find MAVLink USB endpoints");
                return false;
            }

            // Configure baud rate (if supported)
            ConfigureBaudRate(_mavlinkConnection, baudRate);

            // Start reading task
            _mavlinkCancellationToken = new CancellationTokenSource();
            _mavlinkReadTask = Task.Run(() => MavLinkReadLoop(_mavlinkCancellationToken.Token));

            _mavlinkIsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error opening MAVLink serial port: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Open LoRa serial port (Secondary - for telemetry radio)
    /// </summary>
    public async Task<bool> OpenLoRaPortAsync(string deviceName, int baudRate = 57600)
    {
        if (_loraIsOpen)
        {
            await CloseLoRaPortAsync();
        }

        if (_usbManager == null)
        {
            ErrorOccurred?.Invoke(this, "USB Manager not available");
            return false;
        }

        try
        {
            // Find the specific USB device by name
            var deviceList = _usbManager.DeviceList;
            _loraDevice = deviceList.Values.FirstOrDefault(d => d?.DeviceName == deviceName);
            
            if (_loraDevice == null)
            {
                ErrorOccurred?.Invoke(this, $"LoRa device not found: {deviceName}");
                return false;
            }

            // Check permission
            if (!_usbManager.HasPermission(_loraDevice))
            {
                RequestPermission(_loraDevice, "LORA");
                ErrorOccurred?.Invoke(this, "LoRa USB permission required. Please grant permission and try again.");
                return false;
            }

            // Open connection
            _loraConnection = _usbManager.OpenDevice(_loraDevice);
            
            if (_loraConnection == null)
            {
                ErrorOccurred?.Invoke(this, "Failed to open LoRa USB device");
                return false;
            }

            // Get the first interface
            if (_loraDevice.InterfaceCount == 0)
            {
                ErrorOccurred?.Invoke(this, "No LoRa USB interface found");
                return false;
            }

            _loraInterface = _loraDevice.GetInterface(0);
            
            if (!_loraConnection.ClaimInterface(_loraInterface, true))
            {
                ErrorOccurred?.Invoke(this, "Failed to claim LoRa USB interface");
                return false;
            }

            // Find endpoints
            for (int i = 0; i < _loraInterface.EndpointCount; i++)
            {
                var endpoint = _loraInterface.GetEndpoint(i);
                if (endpoint != null)
                {
                    if (endpoint.Direction == UsbAddressing.In)
                    {
                        _loraReadEndpoint = endpoint;
                    }
                    else if (endpoint.Direction == UsbAddressing.Out)
                    {
                        _loraWriteEndpoint = endpoint;
                    }
                }
            }

            if (_loraReadEndpoint == null || _loraWriteEndpoint == null)
            {
                ErrorOccurred?.Invoke(this, "Failed to find LoRa USB endpoints");
                return false;
            }

            // Configure baud rate (if supported)
            ConfigureBaudRate(_loraConnection, baudRate);

            // Start reading task
            _loraCancellationToken = new CancellationTokenSource();
            _loraReadTask = Task.Run(() => LoRaReadLoop(_loraCancellationToken.Token));

            _loraIsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error opening LoRa serial port: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Write data to MAVLink port
    /// </summary>
    public async Task<bool> WriteMavLinkAsync(byte[] data)
    {
        if (!_mavlinkIsOpen || _mavlinkConnection == null || _mavlinkWriteEndpoint == null)
        {
            ErrorOccurred?.Invoke(this, "MAVLink serial port is not open");
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                int bytesWritten = _mavlinkConnection.BulkTransfer(_mavlinkWriteEndpoint, data, data.Length, 1000);
                if (bytesWritten <= 0)
                {
                    ErrorOccurred?.Invoke(this, "Failed to write data to MAVLink port");
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error writing to MAVLink port: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Write data to LoRa port
    /// </summary>
    public async Task<bool> WriteLoRaAsync(byte[] data)
    {
        if (!_loraIsOpen || _loraConnection == null || _loraWriteEndpoint == null)
        {
            ErrorOccurred?.Invoke(this, "LoRa serial port is not open");
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                int bytesWritten = _loraConnection.BulkTransfer(_loraWriteEndpoint, data, data.Length, 1000);
                if (bytesWritten <= 0)
                {
                    ErrorOccurred?.Invoke(this, "Failed to write data to LoRa port");
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error writing to LoRa port: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Close MAVLink port
    /// </summary>
    public async Task CloseMavLinkPortAsync()
    {
        if (!_mavlinkIsOpen)
        {
            return;
        }

        try
        {
            _mavlinkCancellationToken?.Cancel();
            
            if (_mavlinkReadTask != null)
            {
                await _mavlinkReadTask;
            }

            if (_mavlinkConnection != null && _mavlinkInterface != null)
            {
                _mavlinkConnection.ReleaseInterface(_mavlinkInterface);
            }

            _mavlinkConnection?.Close();
            _mavlinkConnection = null;
            _mavlinkDevice = null;
            _mavlinkInterface = null;
            _mavlinkReadEndpoint = null;
            _mavlinkWriteEndpoint = null;

            _mavlinkIsOpen = false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error closing MAVLink port: {ex.Message}");
        }
    }

    /// <summary>
    /// Close LoRa port
    /// </summary>
    public async Task CloseLoRaPortAsync()
    {
        if (!_loraIsOpen)
        {
            return;
        }

        try
        {
            _loraCancellationToken?.Cancel();
            
            if (_loraReadTask != null)
            {
                await _loraReadTask;
            }

            if (_loraConnection != null && _loraInterface != null)
            {
                _loraConnection.ReleaseInterface(_loraInterface);
            }

            _loraConnection?.Close();
            _loraConnection = null;
            _loraDevice = null;
            _loraInterface = null;
            _loraReadEndpoint = null;
            _loraWriteEndpoint = null;

            _loraIsOpen = false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error closing LoRa port: {ex.Message}");
        }
    }

    /// <summary>
    /// Close both ports
    /// </summary>
    public async Task CloseAllPortsAsync()
    {
        await CloseMavLinkPortAsync();
        await CloseLoRaPortAsync();
    }

    private void MavLinkReadLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && _mavlinkIsOpen)
        {
            try
            {
                if (_mavlinkConnection == null || _mavlinkReadEndpoint == null)
                {
                    break;
                }

                int bytesRead = _mavlinkConnection.BulkTransfer(_mavlinkReadEndpoint, buffer, buffer.Length, 100);
                
                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    MavLinkDataReceived?.Invoke(this, new SerialDataReceivedEventArgs(data, bytesRead));
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(this, $"Error reading from MAVLink port: {ex.Message}");
                }
                break;
            }
        }
    }

    private void LoRaReadLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && _loraIsOpen)
        {
            try
            {
                if (_loraConnection == null || _loraReadEndpoint == null)
                {
                    break;
                }

                int bytesRead = _loraConnection.BulkTransfer(_loraReadEndpoint, buffer, buffer.Length, 100);
                
                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    LoRaDataReceived?.Invoke(this, new SerialDataReceivedEventArgs(data, bytesRead));
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(this, $"Error reading from LoRa port: {ex.Message}");
                }
                break;
            }
        }
    }

    private void RequestPermission(UsbDevice device, string tag)
    {
        var permissionIntent = PendingIntent.GetBroadcast(
            AndroidApplication.Context,
            0,
            new Intent($"com.pigeon.gcs.USB_PERMISSION_{tag}"),
            PendingIntentFlags.Mutable | PendingIntentFlags.UpdateCurrent);

        _usbManager?.RequestPermission(device, permissionIntent);
    }

    private void ConfigureBaudRate(UsbDeviceConnection connection, int baudRate)
    {
        try
        {
            // Standard USB CDC ACM control request for baud rate
            // This works for most USB-Serial adapters (FTDI, CP210x, CH340, etc.)
            byte[] baudRateBytes = BitConverter.GetBytes(baudRate);
            
            connection.ControlTransfer(
                (UsbAddressing)((int)UsbAddressing.Out | (int)UsbConstants.UsbTypeClass | 0x01), // RequestType
                0x20, // SET_LINE_CODING
                0,
                0,
                new byte[] { 
                    baudRateBytes[0], baudRateBytes[1], baudRateBytes[2], baudRateBytes[3], // Baud rate
                    0x00, // Stop bits: 1
                    0x00, // Parity: None
                    0x08  // Data bits: 8
                },
                7,
                1000);
        }
        catch
        {
            // Ignore if not supported
        }
    }

    public void Dispose()
    {
        CloseAllPortsAsync().Wait();
        _mavlinkCancellationToken?.Dispose();
        _loraCancellationToken?.Dispose();
    }
}

/// <summary>
/// USB Device Information
/// </summary>
public class UsbDeviceInfo
{
    public string DeviceName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public int DeviceId { get; set; }
    public string DisplayName { get; set; } = "";
}
#endif
