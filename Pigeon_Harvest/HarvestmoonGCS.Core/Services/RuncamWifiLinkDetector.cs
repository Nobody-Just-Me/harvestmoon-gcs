using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using Serilog;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Mendeteksi RunCam WiFi Link 2 di jaringan lokal secara otomatis.
///
/// Cara kerja:
///  1. Ambil semua IP lokal dari interface aktif
///  2. Untuk setiap subnet /24, probe IP .1 sampai .10 (gateway range)
///  3. Cek apakah port RTSP 554 atau MJPEG 8080 terbuka (TCP connect)
///  4. Jika terbuka → kembalikan config RunCam
///
/// RunCam WiFi Link 2 default:
///  - Hotspot IP: 192.168.0.1
///  - RTSP: rtsp://192.168.0.1:554/live
///  - MJPEG: http://192.168.0.1:8080/?action=stream
/// </summary>
public class RuncamWifiLinkDetector
{
    private const int ProbeTimeoutMs = 800;
    private const int MaxParallelProbes = 16;

    // Subnet-subnet yang biasa digunakan RunCam sebagai hotspot
    private static readonly string[] PrioritySubnets =
    {
        "192.168.0.", // default RunCam WiFi Link 2
        "192.168.1.", // beberapa variant
        "10.0.0."     // varian lain
    };

    // Port yang dicek keberadaannya
    private static readonly int[] ProbePorts = { 554, 8080, 80 };

    /// <summary>
    /// Scan jaringan dan kembalikan daftar konfigurasi RunCam yang ditemukan.
    /// </summary>
    public async Task<List<RuncamWifiLinkConfig>> DetectAsync(CancellationToken ct = default)
    {
        var found = new List<RuncamWifiLinkConfig>();
        var tasks = new List<Task<RuncamWifiLinkConfig?>>();
        var sem = new SemaphoreSlim(MaxParallelProbes);

        // Kumpulkan semua IP yang perlu di-probe
        var candidates = GetCandidateIps();

        foreach (var ip in candidates)
        {
            ct.ThrowIfCancellationRequested();

            await sem.WaitAsync(ct);
            var ipCopy = ip;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    return await ProbeIpAsync(ipCopy, ct);
                }
                finally
                {
                    sem.Release();
                }
            }, ct));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var cfg in results)
        {
            if (cfg != null)
                found.Add(cfg);
        }

        return found;
    }

    /// <summary>
    /// Probe satu IP, kembalikan config jika RunCam terdeteksi, null jika tidak.
    /// </summary>
    public async Task<RuncamWifiLinkConfig?> ProbeIpAsync(string ip, CancellationToken ct = default)
    {
        try
        {
            // Cek RTSP port 554 dulu (lebih spesifik ke RunCam)
            if (await IsTcpPortOpenAsync(ip, 554, ct))
            {
                Log.Debug("[RuncamDetector] RunCam ditemukan di {Ip}:554 (RTSP)", ip);
                return BuildConfig(ip, RuncamStreamProtocol.Rtsp);
            }

            // Fallback: cek MJPEG port 8080
            if (await IsTcpPortOpenAsync(ip, 8080, ct))
            {
                Log.Debug("[RuncamDetector] RunCam ditemukan di {Ip}:8080 (MJPEG)", ip);
                return BuildConfig(ip, RuncamStreamProtocol.Mjpeg);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Verbose("[RuncamDetector] Probe {Ip} gagal: {Err}", ip, ex.Message);
            return null;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static List<string> GetCandidateIps()
    {
        var result = new List<string>();

        // Priority: subnet yang paling umum digunakan RunCam
        foreach (var subnet in PrioritySubnets)
        {
            for (int i = 1; i <= 10; i++)
                result.Add($"{subnet}{i}");
        }

        // Tambah subnet dari interface aktif di mesin ini
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var parts = addr.Address.ToString().Split('.');
                    if (parts.Length != 4) continue;

                    var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}.";
                    // Hindari duplikat dengan priority list
                    if (Array.IndexOf(PrioritySubnets, subnet) >= 0) continue;

                    for (int i = 1; i <= 10; i++)
                        result.Add($"{subnet}{i}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[RuncamDetector] Gagal enum network interfaces: {Err}", ex.Message);
        }

        return result;
    }

    private static async Task<bool> IsTcpPortOpenAsync(string ip, int port, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ProbeTimeoutMs);

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            await connectTask.WaitAsync(cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static RuncamWifiLinkConfig BuildConfig(string ip, RuncamStreamProtocol protocol)
    {
        return new RuncamWifiLinkConfig
        {
            CameraIp = ip,
            StreamProtocol = protocol,
            MavLinkRemoteIp = ip,
            AutoDetect = true
        };
    }
}
