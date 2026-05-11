#if __ANDROID__
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using AndroidApplication = Android.App.Application;
using HarvestmoonGCS.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Android implementation of serial port service using Android USB Host API
/// Supports USB OTG connections for MAVLink communication
/// Note: This is a basic implementation. For production use, consider integrating
/// UsbSerialForAndroid library from: https://github.com/anotherlab/UsbSerialForAndroid
/// </summary>
public class AndroidSerialPortService : ISerialPortService
{
    private UsbManager? _usbManager;
    private UsbDevice? _usbDevice;
    private UsbDeviceConnection? _connection;
    private UsbInterface? _usbInterface;
    private UsbEndpoint? _readEndpoint;
    private UsbEndpoint? _writeEndpoint;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private bool _isOpen;

    public event EventHandler<SerialDataReceivedEventArgs>? DataReceived;
    public event EventHandler<SerialErrorEventArgs>? ErrorReceived;

    public bool IsOpen => _isOpen;

    public AndroidSerialPortService()
    {
        var context = AndroidApplication.Context;
        _usbManager = (UsbManager?)context.GetSystemService(Context.UsbService);
    }

    public Task<List<string>> GetAvailablePortsAsync()
    {
        var ports = new List<string>();

        if (_usbManager == null)
        {
            return Task.FromResult(ports);
        }

        try
        {
            var deviceList = _usbManager.DeviceList;
            
            foreach (var entry in deviceList)
            {
                var device = entry.Value;
                if (device != null)
                {
                    var portName = $"{device.DeviceName} - {device.ProductName ?? "Unknown"} (VID:{device.VendorId:X4} PID:{device.ProductId:X4})";
                    ports.Add(portName);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Error enumerating USB devices: {ex.Message}", ex));
        }

        return Task.FromResult(ports);
    }

    public async Task<bool> OpenAsync(string portName, int baudRate)
    {
        if (_isOpen)
        {
            await CloseAsync();
        }

        if (_usbManager == null)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("USB Manager not available"));
            return false;
        }

        try
        {
            // Find the USB device
            var deviceList = _usbManager.DeviceList;
            
            if (!deviceList.Any())
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("No USB devices found"));
                return false;
            }

            // Get the first available device (or match by portName if needed)
            _usbDevice = deviceList.Values.FirstOrDefault();
            
            if (_usbDevice == null)
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("No USB device available"));
                return false;
            }

            // Check permission
            if (!_usbManager.HasPermission(_usbDevice))
            {
                // Request permission
                var permissionIntent = PendingIntent.GetBroadcast(
                    AndroidApplication.Context,
                    0,
                    new Intent("com.pigeon.gcs.USB_PERMISSION"),
                    PendingIntentFlags.Immutable);

                _usbManager.RequestPermission(_usbDevice, permissionIntent);
                
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("USB permission required. Please grant permission and try again."));
                return false;
            }

            // Open connection
            _connection = _usbManager.OpenDevice(_usbDevice);
            
            if (_connection == null)
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to open USB device"));
                return false;
            }

            // Get the first interface
            if (_usbDevice.InterfaceCount == 0)
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("No USB interface found"));
                return false;
            }

            _usbInterface = _usbDevice.GetInterface(0);
            
            if (!_connection.ClaimInterface(_usbInterface, true))
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to claim USB interface"));
                return false;
            }

            // Find endpoints
            for (int i = 0; i < _usbInterface.EndpointCount; i++)
            {
                var endpoint = _usbInterface.GetEndpoint(i);
                if (endpoint != null)
                {
                    if (endpoint.Direction == UsbAddressing.In)
                    {
                        _readEndpoint = endpoint;
                    }
                    else if (endpoint.Direction == UsbAddressing.Out)
                    {
                        _writeEndpoint = endpoint;
                    }
                }
            }

            if (_readEndpoint == null || _writeEndpoint == null)
            {
                ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to find USB endpoints"));
                return false;
            }

            // Start reading task
            _cancellationTokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoop(_cancellationTokenSource.Token));

            _isOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Error opening serial port: {ex.Message}"));
            return false;
        }
    }

    public async Task WriteAsync(byte[] data)
    {
        if (!_isOpen || _connection == null || _writeEndpoint == null)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Serial port is not open"));
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                int bytesWritten = _connection.BulkTransfer(_writeEndpoint, data, data.Length, 1000);
                if (bytesWritten <= 0)
                {
                    ErrorReceived?.Invoke(this, new SerialErrorEventArgs("Failed to write data to serial port"));
                }
            });
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Error writing to serial port: {ex.Message}"));
        }
    }

    public Task<byte[]> ReadAsync(int count)
    {
        // For Android USB, we use event-based reading via DataReceived event
        // This method is not typically used but required by interface
        return Task.FromResult(Array.Empty<byte>());
    }

    public async Task CloseAsync()
    {
        if (!_isOpen)
        {
            return;
        }

        try
        {
            _cancellationTokenSource?.Cancel();
            
            if (_readTask != null)
            {
                await _readTask;
            }

            if (_connection != null && _usbInterface != null)
            {
                _connection.ReleaseInterface(_usbInterface);
            }

            _connection?.Close();
            _connection = null;
            _usbDevice = null;
            _usbInterface = null;
            _readEndpoint = null;
            _writeEndpoint = null;

            _isOpen = false;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Error closing serial port: {ex.Message}"));
        }
    }

    private void ReadLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && _isOpen)
        {
            try
            {
                if (_connection == null || _readEndpoint == null)
                {
                    break;
                }

                int bytesRead = _connection.BulkTransfer(_readEndpoint, buffer, buffer.Length, 100);
                
                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(this, new SerialDataReceivedEventArgs(data, bytesRead));
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ErrorReceived?.Invoke(this, new SerialErrorEventArgs($"Error reading from serial port: {ex.Message}"));
                }
                break;
            }
        }
    }

    public void Dispose()
    {
        CloseAsync().Wait();
        _cancellationTokenSource?.Dispose();
    }
}
#endif
