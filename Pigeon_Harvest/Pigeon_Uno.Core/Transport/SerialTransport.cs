using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
#if __ANDROID__
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using AndroidApplication = Android.App.Application;
#endif
using System.Linq;

namespace Pigeon_Uno.Core.Transport;

/// <summary>
/// Serial port transport implementation for MAVLink communication.
/// </summary>
public class SerialTransport : ITransport
{
    private const int ConnectTimeoutMs = 10000;
    private readonly string _portName;
    private readonly int _baudRate;
    private SerialPort? _serialPort;
    private bool _disposed;

#if __ANDROID__
    private const int Cp210xVendorId = 0x10C4;
    private const int UsbClassComm = 0x02;
    private const int UsbClassCdcData = 0x0A;
    private const int UsbClassVendorSpecific = 0xFF;
    private const int UsbRequestSetControlLineState = 0x22;
    private const int UsbControlLineDtrRts = 0x03;

    private UsbDeviceConnection? _usbConnection;
    private UsbInterface? _usbInterface;
    private UsbEndpoint? _usbReadEndpoint;
    private UsbEndpoint? _usbWriteEndpoint;
    private bool _isAndroidUsbConnection;
#endif

    /// <summary>
    /// Initializes a new instance of the SerialTransport class.
    /// </summary>
    /// <param name="portName">The name of the serial port (e.g., "COM3" or "/dev/ttyUSB0").</param>
    /// <param name="baudRate">The baud rate for serial communication (e.g., 57600, 115200).</param>
    public SerialTransport(string portName, int baudRate)
    {
        _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        _baudRate = baudRate;
    }

    /// <inheritdoc/>
    public bool IsConnected => (_serialPort?.IsOpen ?? false)
#if __ANDROID__
        || IsAndroidUsbConnected
#endif
        ;

#if __ANDROID__
    private bool IsAndroidUsbConnected => _isAndroidUsbConnection && _usbConnection != null && _usbReadEndpoint != null && _usbWriteEndpoint != null;
#endif

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                return true;
            }

#if __ANDROID__
            if (await TryConnectAndroidUsbAsync())
            {
                return true;
            }
#endif

            var serialPort = new SerialPort(_portName, _baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            var openTask = Task.Run(() =>
            {
                serialPort.Open();
                return serialPort.IsOpen;
            });

            var completedTask = await Task.WhenAny(openTask, Task.Delay(ConnectTimeoutMs));
            if (completedTask != openTask)
            {
                serialPort.Dispose();
                return false;
            }

            var opened = await openTask;
            if (!opened)
            {
                serialPort.Dispose();
                return false;
            }

            _serialPort = serialPort;
            return true;
        }
        catch (Exception)
        {
            _serialPort?.Dispose();
            _serialPort = null;

#if __ANDROID__
            CleanupAndroidUsbConnection();
#endif
            return false;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync()
    {
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }

#if __ANDROID__
            CleanupAndroidUsbConnection();
#endif
        }
        catch (Exception)
        {
            // Ignore exceptions during disconnect
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if __ANDROID__
        if (IsAndroidUsbConnected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (count <= 0)
            {
                return 0;
            }

            var readBuffer = offset == 0 ? buffer : new byte[count];
            var bytesRead = _usbConnection!.BulkTransfer(_usbReadEndpoint!, readBuffer, count, 1000);
            if (bytesRead <= 0)
            {
                return 0;
            }

            if (offset > 0)
            {
                Array.Copy(readBuffer, 0, buffer, offset, bytesRead);
            }

            return bytesRead;
        }
#endif

        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var available = _serialPort.BytesToRead;
                if (available > 0)
                {
                    var toRead = Math.Min(count, available);
                    return _serialPort.Read(buffer, offset, toRead);
                }

                await Task.Delay(10, cancellationToken);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read from serial port.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(byte[] buffer, int offset, int count)
    {
#if __ANDROID__
        if (IsAndroidUsbConnected)
        {
            if (count <= 0)
            {
                return;
            }

            var writeBuffer = buffer;
            if (offset > 0)
            {
                writeBuffer = new byte[count];
                Array.Copy(buffer, offset, writeBuffer, 0, count);
            }

            var lastWrite = 0;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                lastWrite = _usbConnection!.BulkTransfer(_usbWriteEndpoint!, writeBuffer, count, 1000);
                if (lastWrite > 0)
                {
                    return;
                }

                await Task.Delay(30);
            }

            throw new InvalidOperationException($"Failed to write to Android USB serial endpoint (last result {lastWrite}).");
        }
#endif

        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }

        try
        {
            await _serialPort.BaseStream.WriteAsync(buffer, offset, count);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to write to serial port.", ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SerialTransport and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }

#if __ANDROID__
            CleanupAndroidUsbConnection();
#endif
        }

        _disposed = true;
    }

#if __ANDROID__
    private async Task<bool> TryConnectAndroidUsbAsync()
    {
        try
        {
            var context = AndroidApplication.Context;
            var usbManager = (UsbManager?)context.GetSystemService(Context.UsbService);
            if (usbManager == null)
            {
                return false;
            }

            var deviceList = usbManager.DeviceList;
            if (deviceList == null || deviceList.Count == 0)
            {
                return false;
            }

            var targetName = NormalizeAndroidPortName(_portName);
            UsbDevice? device = null;

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                device = deviceList.Values.FirstOrDefault(d => d?.DeviceName == targetName);
            }

            device ??= deviceList.Values.FirstOrDefault();

            if (device == null)
            {
                return false;
            }

            if (!usbManager.HasPermission(device))
            {
                var permissionIntent = PendingIntent.GetBroadcast(
                    AndroidApplication.Context,
                    0,
                    new Android.Content.Intent("com.pigeon.gcs.USB_PERMISSION"),
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

                usbManager.RequestPermission(device, permissionIntent);

                var waitUntil = DateTime.UtcNow.AddSeconds(8);
                while (DateTime.UtcNow < waitUntil)
                {
                    await Task.Delay(200);
                    if (usbManager.HasPermission(device))
                    {
                        break;
                    }
                }

                if (!usbManager.HasPermission(device))
                {
                    return false;
                }
            }

            var connection = usbManager.OpenDevice(device);
            if (connection == null)
            {
                return false;
            }

            var interfaceIndices = Enumerable.Range(0, device.InterfaceCount)
                .OrderBy(index => GetInterfacePriority(device.GetInterface(index)))
                .ToArray();

            foreach (var ifaceIndex in interfaceIndices)
            {
                var usbInterface = device.GetInterface(ifaceIndex);
                if (usbInterface == null)
                {
                    continue;
                }

                if (!connection.ClaimInterface(usbInterface, true))
                {
                    continue;
                }

                UsbEndpoint? bulkReadEndpoint = null;
                UsbEndpoint? bulkWriteEndpoint = null;
                UsbEndpoint? fallbackReadEndpoint = null;
                UsbEndpoint? fallbackWriteEndpoint = null;

                for (int endpointIndex = 0; endpointIndex < usbInterface.EndpointCount; endpointIndex++)
                {
                    var endpoint = usbInterface.GetEndpoint(endpointIndex);
                    if (endpoint == null)
                    {
                        continue;
                    }

                    if (endpoint.Direction == UsbAddressing.In)
                    {
                        fallbackReadEndpoint ??= endpoint;
                        if ((int)endpoint.Type == (int)UsbAddressing.XferBulk)
                        {
                            bulkReadEndpoint ??= endpoint;
                        }
                    }
                    else if (endpoint.Direction == UsbAddressing.Out)
                    {
                        fallbackWriteEndpoint ??= endpoint;
                        if ((int)endpoint.Type == (int)UsbAddressing.XferBulk)
                        {
                            bulkWriteEndpoint ??= endpoint;
                        }
                    }
                }

                var readEndpoint = bulkReadEndpoint ?? fallbackReadEndpoint;
                var writeEndpoint = bulkWriteEndpoint ?? fallbackWriteEndpoint;

                if (readEndpoint != null && writeEndpoint != null)
                {
                    ConfigureAndroidUsbConnection(connection, device, _baudRate);
                    ConfigureAndroidControlLines(connection, usbInterface);

                    _usbConnection = connection;
                    _usbInterface = usbInterface;
                    _usbReadEndpoint = readEndpoint;
                    _usbWriteEndpoint = writeEndpoint;
                    _isAndroidUsbConnection = true;
                    return true;
                }

                connection.ReleaseInterface(usbInterface);
            }

            connection.Close();
            return false;
        }
        catch
        {
            CleanupAndroidUsbConnection();
            return false;
        }
    }

    private static string NormalizeAndroidPortName(string rawPortName)
    {
        if (string.IsNullOrWhiteSpace(rawPortName))
        {
            return string.Empty;
        }

        var separatorIndex = rawPortName.IndexOf(" - ", StringComparison.Ordinal);
        return separatorIndex > 0
            ? rawPortName.Substring(0, separatorIndex)
            : rawPortName;
    }

    private static void ConfigureAndroidUsbConnection(UsbDeviceConnection connection, UsbDevice device, int baudRate)
    {
        if (device.VendorId == Cp210xVendorId)
        {
            ConfigureCp210x(connection, baudRate);
            return;
        }

        ConfigureCdcAcmBaudRate(connection, baudRate);
    }

    private static int GetInterfacePriority(UsbInterface? usbInterface)
    {
        if (usbInterface == null)
        {
            return int.MaxValue;
        }

        var score = 100;

        if ((int)usbInterface.InterfaceClass == UsbClassCdcData)
        {
            score = 0;
        }
        else if ((int)usbInterface.InterfaceClass == UsbClassComm)
        {
            score = 10;
        }
        else if ((int)usbInterface.InterfaceClass == UsbClassVendorSpecific)
        {
            score = 20;
        }

        var hasBulkIn = false;
        var hasBulkOut = false;
        for (var i = 0; i < usbInterface.EndpointCount; i++)
        {
            var endpoint = usbInterface.GetEndpoint(i);
            if (endpoint == null)
            {
                continue;
            }

            var isBulk = (int)endpoint.Type == (int)UsbAddressing.XferBulk;
            if (!isBulk)
            {
                continue;
            }

            if (endpoint.Direction == UsbAddressing.In)
            {
                hasBulkIn = true;
            }
            else if (endpoint.Direction == UsbAddressing.Out)
            {
                hasBulkOut = true;
            }
        }

        if (hasBulkIn && hasBulkOut)
        {
            score -= 5;
        }

        return score;
    }

    private static void ConfigureAndroidControlLines(UsbDeviceConnection connection, UsbInterface usbInterface)
    {
        try
        {
            connection.ControlTransfer(
                (UsbAddressing)((int)UsbAddressing.Out | (int)UsbConstants.UsbTypeClass | 0x01),
                UsbRequestSetControlLineState,
                UsbControlLineDtrRts,
                usbInterface.Id,
                null,
                0,
                1000);
        }
        catch
        {
        }
    }

    private static void ConfigureCdcAcmBaudRate(UsbDeviceConnection connection, int baudRate)
    {
        try
        {
            var baudBytes = BitConverter.GetBytes(baudRate);
            connection.ControlTransfer(
                (UsbAddressing)((int)UsbAddressing.Out | (int)UsbConstants.UsbTypeClass | 0x01),
                0x20,
                0,
                0,
                new byte[]
                {
                    baudBytes[0], baudBytes[1], baudBytes[2], baudBytes[3],
                    0x00,
                    0x00,
                    0x08
                },
                7,
                1000);
        }
        catch
        {
        }
    }

    private static void ConfigureCp210x(UsbDeviceConnection connection, int baudRate)
    {
        try
        {
            connection.ControlTransfer((UsbAddressing)0x41, 0x00, 0x0001, 0, null, 0, 1000);

            connection.ControlTransfer((UsbAddressing)0x41, 0x03, 0x0800, 0, null, 0, 1000);

            connection.ControlTransfer((UsbAddressing)0x41, 0x07, 0x0303, 0, null, 0, 1000);

            var baudBytes = BitConverter.GetBytes(baudRate);
            connection.ControlTransfer((UsbAddressing)0x41, 0x1E, 0, 0, baudBytes, 4, 1000);

#if DEBUG
            Log.Debug("SerialTransport", $"Configured CP210x at {baudRate} baud");
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            Log.Warn("SerialTransport", $"CP210x config failed, fallback to CDC ACM: {ex.Message}");
#endif
            ConfigureCdcAcmBaudRate(connection, baudRate);
        }
    }

    private void CleanupAndroidUsbConnection()
    {
        try
        {
            if (_usbConnection != null && _usbInterface != null)
            {
                _usbConnection.ReleaseInterface(_usbInterface);
            }
        }
        catch
        {
        }

        try
        {
            _usbConnection?.Close();
        }
        catch
        {
        }

        _usbConnection = null;
        _usbInterface = null;
        _usbReadEndpoint = null;
        _usbWriteEndpoint = null;
        _isAndroidUsbConnection = false;
    }
#endif
}
