using System;
using System.IO;
using MavLinkNet;
using HarvestmoonGCS.Core.Helpers;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Helpers;

namespace HarvestmoonGCS.Services;

public sealed class MissionLoggingService : IDisposable
{
    private readonly IMavLinkService _mavLinkService;
    private readonly HarvestFunctionalService _harvestFunctionalService;
    private readonly GeofenceMonitor _geofenceMonitor;
    private readonly IncidentTimelineService _timelineService;
    private readonly TlogWriter _tlogWriter;
    private bool _disposed;

    public MissionLoggingService(
        IMavLinkService mavLinkService,
        HarvestFunctionalService harvestFunctionalService,
        GeofenceMonitor geofenceMonitor,
        IncidentTimelineService timelineService,
        ILoggingService logger)
    {
        _mavLinkService = mavLinkService;
        _harvestFunctionalService = harvestFunctionalService;
        _geofenceMonitor = geofenceMonitor;
        _timelineService = timelineService;
        _tlogWriter = new TlogWriter(logger);

        _mavLinkService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _mavLinkService.PacketReceived += OnPacketReceived;
        _geofenceMonitor.GeofenceViolated += OnGeofenceViolated;
        _geofenceMonitor.GeofenceRestored += OnGeofenceRestored;
    }

    public bool IsRecording => _tlogWriter.IsRecording;
    public string? CurrentTlogPath => _tlogWriter.CurrentFilePath;

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        if (isConnected)
        {
            _timelineService.Add("connected", "MAVLink connected. Auto TLOG recorder armed.", "success");
            StartTlogRecording();
            return;
        }

        _timelineService.Add("disconnected", "MAVLink disconnected. TLOG recorder stopped.", "warning");
        StopTlogRecording();
    }

    private void StartTlogRecording()
    {
        if (_tlogWriter.IsRecording)
        {
            return;
        }

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HarvestmoonGCS",
            "TLogs");
        _tlogWriter.StartRecording(folder);
        if (!string.IsNullOrWhiteSpace(_tlogWriter.CurrentFilePath))
        {
            _timelineService.Add("tlog", $"Telemetry recording started: {_tlogWriter.CurrentFilePath}", "success");
        }
    }

    private void StopTlogRecording()
    {
        var path = _tlogWriter.CurrentFilePath;
        if (!_tlogWriter.IsRecording)
        {
            return;
        }

        _tlogWriter.StopRecording();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = _harvestFunctionalService.AttachTlogToLatestReportAsync(path);
            _timelineService.Add("tlog", $"Telemetry recording saved: {path}", "success");
        }
    }

    private void OnPacketReceived(object? sender, MavLinkPacketBase packet)
    {
        if (!_tlogWriter.IsRecording || packet == null)
        {
            return;
        }

        var rawPacket = SerializePacket(packet);
        if (rawPacket.Length > 0)
        {
            _tlogWriter.WritePacket(rawPacket);
        }
    }

    private void OnGeofenceViolated(object? sender, GeofenceViolationEventArgs e)
    {
        _timelineService.Add("geofence", e.Message, "critical");
        _ = _harvestFunctionalService.AddGeofenceAlertToLatestReportAsync(e.Message);
    }

    private void OnGeofenceRestored(object? sender, GeofenceViolationEventArgs e)
    {
        _timelineService.Add("geofence", e.Message, "success");
        _ = _harvestFunctionalService.AddGeofenceAlertToLatestReportAsync(e.Message);
    }

    private static byte[] SerializePacket(MavLinkPacketBase packet)
    {
        try
        {
            using var stream = new MemoryStream(packet.GetPacketSize() + 1);
            using var writer = new BinaryWriter(stream);
            writer.Write(packet.WireProtocolVersion == WireProtocolVersion.v20 ? (byte)0xFD : (byte)0xFE);
            packet.Serialize(writer);
            return stream.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mavLinkService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _mavLinkService.PacketReceived -= OnPacketReceived;
        _geofenceMonitor.GeofenceViolated -= OnGeofenceViolated;
        _geofenceMonitor.GeofenceRestored -= OnGeofenceRestored;
        _tlogWriter.Dispose();
    }
}
