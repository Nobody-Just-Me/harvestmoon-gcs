#if __ANDROID__
using HarvestmoonGCS.Platforms.Android.Services;
using HarvestmoonGCS.Core.Services;
using System;
using System.Text;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Platforms.Android;

/// <summary>
/// Example usage of AndroidDualSerialPortService
/// Demonstrates how to use 2 USB serial devices simultaneously:
/// 1. MAVLink (autopilot connection)
/// 2. LoRa (telemetry radio)
/// </summary>
public class DualSerialExample
{
    private AndroidDualSerialPortService? _dualSerial;

    public async Task InitializeAsync()
    {
        _dualSerial = new AndroidDualSerialPortService();

        // Subscribe to events
        _dualSerial.MavLinkDataReceived += OnMavLinkDataReceived;
        _dualSerial.LoRaDataReceived += OnLoRaDataReceived;
        _dualSerial.ErrorOccurred += OnErrorOccurred;

        // Get available USB devices
        var devices = await _dualSerial.GetAvailableDevicesAsync();
        
        Console.WriteLine($"Found {devices.Count} USB devices:");
        foreach (var device in devices)
        {
            Console.WriteLine($"  - {device.DisplayName}");
        }

        if (devices.Count >= 2)
        {
            // Open MAVLink port (first device)
            var mavlinkDevice = devices[0].DeviceName;
            var mavlinkOpened = await _dualSerial.OpenMavLinkPortAsync(mavlinkDevice, 57600);
            
            if (mavlinkOpened)
            {
                Console.WriteLine($"✅ MAVLink port opened: {mavlinkDevice}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to open MAVLink port: {mavlinkDevice}");
            }

            // Open LoRa port (second device)
            var loraDevice = devices[1].DeviceName;
            var loraOpened = await _dualSerial.OpenLoRaPortAsync(loraDevice, 57600);
            
            if (loraOpened)
            {
                Console.WriteLine($"✅ LoRa port opened: {loraDevice}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to open LoRa port: {loraDevice}");
            }

            // Check if both ports are open
            if (_dualSerial.AreBothOpen)
            {
                Console.WriteLine("✅ Both serial ports are open and ready!");
                
                // Example: Send test data
                await SendTestDataAsync();
            }
        }
        else
        {
            Console.WriteLine("⚠️ Need at least 2 USB devices connected");
        }
    }

    private async Task SendTestDataAsync()
    {
        if (_dualSerial == null) return;

        // Send MAVLink heartbeat (example)
        byte[] mavlinkHeartbeat = new byte[] { 
            0xFE, 0x09, 0x00, 0x01, 0x01, 0x00, // MAVLink header
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Payload
            0x00, 0x00 // Checksum
        };
        await _dualSerial.WriteMavLinkAsync(mavlinkHeartbeat);
        Console.WriteLine("📤 Sent MAVLink heartbeat");

        // Send LoRa command (example)
        byte[] loraCommand = Encoding.ASCII.GetBytes("AT+MODE=0\r\n");
        await _dualSerial.WriteLoRaAsync(loraCommand);
        Console.WriteLine("📤 Sent LoRa command");
    }

    private void OnMavLinkDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        Console.WriteLine($"📥 MAVLink received {e.BytesReceived} bytes");
        
        // Process MAVLink data here
        // Example: Parse MAVLink packets, update telemetry, etc.
        
        // Forward to LoRa if needed (telemetry relay)
        if (_dualSerial != null && _dualSerial.IsLoRaOpen)
        {
            _ = _dualSerial.WriteLoRaAsync(e.Data);
            Console.WriteLine($"📡 Forwarded {e.BytesReceived} bytes to LoRa");
        }
    }

    private void OnLoRaDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        Console.WriteLine($"📥 LoRa received {e.BytesReceived} bytes");
        
        // Process LoRa data here
        // Example: Parse LoRa commands, handle remote control, etc.
        
        // Forward to MAVLink if needed (command relay)
        if (_dualSerial != null && _dualSerial.IsMavLinkOpen)
        {
            _ = _dualSerial.WriteMavLinkAsync(e.Data);
            Console.WriteLine($"📡 Forwarded {e.BytesReceived} bytes to MAVLink");
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        Console.WriteLine($"❌ Error: {error}");
    }

    public async Task CleanupAsync()
    {
        if (_dualSerial != null)
        {
            await _dualSerial.CloseAllPortsAsync();
            _dualSerial.Dispose();
            Console.WriteLine("✅ All serial ports closed");
        }
    }
}

/// <summary>
/// Advanced example: Bidirectional relay between MAVLink and LoRa
/// Use case: Ground station communicates with drone via LoRa radio
/// </summary>
public class MavLinkLoRaRelay
{
    private AndroidDualSerialPortService? _dualSerial;
    private bool _relayEnabled = true;

    public async Task StartRelayAsync(string mavlinkDevice, string loraDevice)
    {
        _dualSerial = new AndroidDualSerialPortService();

        // Subscribe to events
        _dualSerial.MavLinkDataReceived += (s, e) => RelayMavLinkToLoRa(e.Data);
        _dualSerial.LoRaDataReceived += (s, e) => RelayLoRaToMavLink(e.Data);
        _dualSerial.ErrorOccurred += (s, error) => Console.WriteLine($"Relay Error: {error}");

        // Open both ports
        var mavlinkOpened = await _dualSerial.OpenMavLinkPortAsync(mavlinkDevice, 57600);
        var loraOpened = await _dualSerial.OpenLoRaPortAsync(loraDevice, 57600);

        if (mavlinkOpened && loraOpened)
        {
            Console.WriteLine("✅ MAVLink ↔ LoRa relay started");
            Console.WriteLine("   MAVLink → LoRa: Telemetry forwarding");
            Console.WriteLine("   LoRa → MAVLink: Command forwarding");
        }
        else
        {
            Console.WriteLine("❌ Failed to start relay");
        }
    }

    private async void RelayMavLinkToLoRa(byte[] data)
    {
        if (!_relayEnabled || _dualSerial == null || !_dualSerial.IsLoRaOpen)
            return;

        await _dualSerial.WriteLoRaAsync(data);
        Console.WriteLine($"MAVLink → LoRa: {data.Length} bytes");
    }

    private async void RelayLoRaToMavLink(byte[] data)
    {
        if (!_relayEnabled || _dualSerial == null || !_dualSerial.IsMavLinkOpen)
            return;

        await _dualSerial.WriteMavLinkAsync(data);
        Console.WriteLine($"LoRa → MAVLink: {data.Length} bytes");
    }

    public void EnableRelay() => _relayEnabled = true;
    public void DisableRelay() => _relayEnabled = false;

    public async Task StopRelayAsync()
    {
        _relayEnabled = false;
        if (_dualSerial != null)
        {
            await _dualSerial.CloseAllPortsAsync();
            _dualSerial.Dispose();
            Console.WriteLine("✅ Relay stopped");
        }
    }
}
#endif
