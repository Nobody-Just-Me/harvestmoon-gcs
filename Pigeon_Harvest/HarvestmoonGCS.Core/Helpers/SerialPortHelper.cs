using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
#if __ANDROID__
using Android.Content;
using Android.Hardware.Usb;
using AndroidApplication = Android.App.Application;
#endif

namespace HarvestmoonGCS.Core.Helpers
{
    public static class SerialPortHelper
    {
        public static string[] GetAvailablePorts()
        {
            var ports = new List<string>();
            
            try
            {
                var systemPorts = SerialPort.GetPortNames();
                if (systemPorts != null && systemPorts.Length > 0)
                {
                    ports.AddRange(systemPorts);
                }
                System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] SerialPort.GetPortNames found {ports.Count} ports");
                Console.WriteLine($"[SerialPortHelper] SerialPort.GetPortNames found {ports.Count} ports: {string.Join(", ", ports)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] SerialPort.GetPortNames error: {ex.Message}");
                Console.WriteLine($"[SerialPortHelper] SerialPort.GetPortNames error: {ex.Message}");
            }

#if __ANDROID__
            try
            {
                var context = AndroidApplication.Context;
                var usbManager = (UsbManager?)context.GetSystemService(Context.UsbService);
                var deviceList = usbManager?.DeviceList;

                if (deviceList != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Android USB devices found: {deviceList.Count}");
                    foreach (var entry in deviceList)
                    {
                        var device = entry.Value;
                        if (device == null) continue;

                        var deviceName = device.DeviceName;
                        var vendorId = device.VendorId;
                        var productId = device.ProductId;
                        
                        System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] USB Device: {deviceName}, VendorId: {vendorId:X}, ProductId: {productId:X}");
                        
                        AddIfMissing(ports, deviceName);
                        
                        // Log all interface classes for debugging
                        if (device.InterfaceCount > 0)
                        {
                            for (int i = 0; i < device.InterfaceCount; i++)
                            {
                                var iface = device.GetInterface(i);
                                System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Interface {i}: Class={iface.InterfaceClass}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Android USB scan error: {ex.Message}");
                Console.WriteLine($"[SerialPortHelper] Android USB scan error: {ex.Message}");
            }
#endif

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try 
                {
                    if (Directory.Exists("/dev/"))
                    {
                        AddPortsFromPattern(ports, "/dev/", "ttyUSB*");
                        AddPortsFromPattern(ports, "/dev/", "ttyACM*");
                        AddPortsFromPattern(ports, "/dev/", "rfcomm*");

                        var hasPreferredPort = ports.Any(IsPreferredHardwarePort);
                        if (!hasPreferredPort)
                        {
                            AddPortsFromPattern(ports, "/dev/", "ttyS*");
                        }
                        
                        Console.WriteLine($"[SerialPortHelper] Linux total ports after scan: {ports.Count}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Linux scan error: {ex.Message}");
                    Console.WriteLine($"[SerialPortHelper] Linux scan error: {ex.Message}");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try 
                {
                    if (Directory.Exists("/dev/"))
                    {
                        AddPortsFromPattern(ports, "/dev/", "cu.*");
                        AddPortsFromPattern(ports, "/dev/", "tty.*");
                        
                        Console.WriteLine($"[SerialPortHelper] Mac total ports after scan: {ports.Count}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Mac scan error: {ex.Message}");
                    Console.WriteLine($"[SerialPortHelper] Mac scan error: {ex.Message}");
                }
            }
            
            var result = ports.Distinct().OrderBy(p => p).ToArray();
            Console.WriteLine($"[SerialPortHelper] Returning {result.Length} unique ports: {string.Join(", ", result)}");
            return result;
        }
        
        private static void AddPortsFromPattern(List<string> ports, string directory, string pattern)
        {
            try
            {
                string[] foundPorts = Directory.GetFiles(directory, pattern);
                int added = 0;
                foreach(var p in foundPorts) 
                {
                    if (!ports.Contains(p))
                    {
                        ports.Add(p);
                        added++;
                    }
                }
                if (added > 0)
                {
                    Console.WriteLine($"[SerialPortHelper] Added {added} ports from pattern {pattern}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPortHelper] Error scanning pattern {pattern}: {ex.Message}");
            }
        }

        private static void AddIfMissing(List<string> ports, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var exists = ports.Exists(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                ports.Add(value);
            }
        }

        private static bool IsPreferredHardwarePort(string? port)
        {
            if (string.IsNullOrWhiteSpace(port))
            {
                return false;
            }

            return port.Contains("ttyUSB", StringComparison.OrdinalIgnoreCase)
                   || port.Contains("ttyACM", StringComparison.OrdinalIgnoreCase)
                   || port.Contains("COM", StringComparison.OrdinalIgnoreCase)
                   || port.Contains("cu.", StringComparison.OrdinalIgnoreCase)
                   || port.Contains("tty.usb", StringComparison.OrdinalIgnoreCase)
                   || port.Contains("rfcomm", StringComparison.OrdinalIgnoreCase);
        }
    }
}
