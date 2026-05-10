using System;
using System.Text;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;

namespace Pigeon_Uno.Services;

/// <summary>
/// Example usage of DesktopDualSerialPortService
/// Demonstrates how to use 2 serial ports simultaneously:
/// 1. MAVLink (autopilot connection) - for FlightPage
/// 2. LoRa (telemetry radio) - for LoRa Menu
/// </summary>
public class DesktopDualSerialExample
{
    private DesktopDualSerialPortService? _dualSerial;

    public async Task InitializeAsync()
    {
        _dualSerial = new DesktopDualSerialPortService();

        // Subscribe to events
        _dualSerial.MavLinkDataReceived += OnMavLinkDataReceived;
        _dualSerial.LoRaDataReceived += OnLoRaDataReceived;
        _dualSerial.ErrorOccurred += OnErrorOccurred;

        // Get available serial ports
        var ports = await _dualSerial.GetAvailablePortsAsync();
        
        Console.WriteLine($"Found {ports.Count} serial ports:");
        foreach (var port in ports)
        {
            Console.WriteLine($"  {port.DisplayName}");
        }

        if (ports.Count >= 2)
        {
            // Open MAVLink port (first port) - for FlightPage
            var mavlinkPort = ports[0].PortName;
            var mavlinkOpened = await _dualSerial.OpenMavLinkPortAsync(mavlinkPort, 57600);
            
            if (mavlinkOpened)
            {
                Console.WriteLine($"✅ MAVLink port opened: {mavlinkPort}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to open MAVLink port: {mavlinkPort}");
            }

            // Open LoRa port (second port) - for LoRa Menu
            var loraPort = ports[1].PortName;
            var loraOpened = await _dualSerial.OpenLoRaPortAsync(loraPort, 57600);
            
            if (loraOpened)
            {
                Console.WriteLine($"✅ LoRa port opened: {loraPort}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to open LoRa port: {loraPort}");
            }

            // Check if both ports are open
            if (_dualSerial.AreBothOpen)
            {
                Console.WriteLine("✅ Both serial ports are open and ready!");
                Console.WriteLine("  - MAVLink port: Ready for FlightPage");
                Console.WriteLine("  - LoRa port: Ready for LoRa Menu");
                
                // Send test data
                await SendTestDataAsync();
            }
        }
        else
        {
            Console.WriteLine("❌ Need at least 2 serial ports for dual serial operation");
        }
    }

    private async Task SendTestDataAsync()
    {
        if (_dualSerial == null) return;

        // Send MAVLink heartbeat (example)
        byte[] mavlinkHeartbeat = new byte[] {
            0xFE, 0x09, 0x00, 0x01, 0x01, 0x00, // MAVLink header
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00 // Checksum
        };
        await _dualSerial.WriteMavLinkAsync(mavlinkHeartbeat);
        Console.WriteLine("📤 Sent MAVLink heartbeat to FlightPage");

        // Send LoRa command (example)
        byte[] loraCommand = Encoding.ASCII.GetBytes("AT+MODE=0\r\n");
        await _dualSerial.WriteLoRaAsync(loraCommand);
        Console.WriteLine("📤 Sent LoRa command to LoRa Menu");
    }

    private void OnMavLinkDataReceived(object? sender, Core.Services.SerialDataReceivedEventArgs e)
    {
        Console.WriteLine($"📥 MAVLink (FlightPage): {e.BytesReceived} bytes");
        
        // Forward to LoRa if needed (telemetry relay)
        if (_dualSerial != null && _dualSerial.IsLoRaOpen)
        {
            _ = _dualSerial.WriteLoRaAsync(e.Data);
            Console.WriteLine($"📡 Forwarded {e.BytesReceived} bytes to LoRa Menu");
        }
    }

    private void OnLoRaDataReceived(object? sender, Core.Services.SerialDataReceivedEventArgs e)
    {
        Console.WriteLine($"📥 LoRa (LoRa Menu): {e.BytesReceived} bytes");
        
        // Forward to MAVLink if needed (command relay)
        if (_dualSerial != null && _dualSerial.IsMavLinkOpen)
        {
            _ = _dualSerial.WriteMavLinkAsync(e.Data);
            Console.WriteLine($"📡 Forwarded {e.BytesReceived} bytes to FlightPage");
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
/// MAVLink ↔ LoRa Relay Example
/// Automatically forwards data between FlightPage and LoRa Menu
/// </summary>
public class DesktopMavLinkLoRaRelay
{
    private DesktopDualSerialPortService? _dualSerial;
    private bool _relayEnabled = true;

    public async Task StartRelayAsync(string mavlinkPort, string loraPort)
    {
        _dualSerial = new DesktopDualSerialPortService();

        // Subscribe to events for bidirectional relay
        _dualSerial.MavLinkDataReceived += (s, e) => RelayMavLinkToLoRa(e.Data);
        _dualSerial.LoRaDataReceived += (s, e) => RelayLoRaToMavLink(e.Data);
        _dualSerial.ErrorOccurred += (s, error) => Console.WriteLine($"Relay Error: {error}");

        // Open both ports
        var mavlinkOpened = await _dualSerial.OpenMavLinkPortAsync(mavlinkPort, 57600);
        var loraOpened = await _dualSerial.OpenLoRaPortAsync(loraPort, 57600);

        if (mavlinkOpened && loraOpened)
        {
            Console.WriteLine("✅ Relay started: MAVLink (FlightPage) ↔ LoRa (LoRa Menu)");
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
        Console.WriteLine($"FlightPage → LoRa Menu: {data.Length} bytes");
    }

    private async void RelayLoRaToMavLink(byte[] data)
    {
        if (!_relayEnabled || _dualSerial == null || !_dualSerial.IsMavLinkOpen)
            return;

        await _dualSerial.WriteMavLinkAsync(data);
        Console.WriteLine($"LoRa Menu → FlightPage: {data.Length} bytes");
    }

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
