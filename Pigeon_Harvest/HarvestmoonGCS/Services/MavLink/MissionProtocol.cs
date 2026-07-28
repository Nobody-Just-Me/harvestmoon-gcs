using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MavLinkNet;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Implements MAVLink mission protocol for waypoint upload/download
/// </summary>
internal class MissionProtocol
{
    private readonly MavLinkService _service;
    private readonly SemaphoreSlim _missionLock = new SemaphoreSlim(1, 1);

    private TaskCompletionSource<bool>? _uploadTcs;
    private TaskCompletionSource<List<WaypointData>>? _downloadTcs;

    private List<WaypointData>? _missionToUpload;
    private List<WaypointData>? _downloadedMission;

    private int _missionItemsExpected;
    private int _missionItemsReceived;

    private const int OperationTimeoutMs = 15000;

    public MissionProtocol(MavLinkService service)
    {
        _service = service;
    }

    public async Task<bool> UploadMissionAsync(IEnumerable<WaypointData> waypoints)
    {
        await _missionLock.WaitAsync();
        try
        {
            var waypointList = waypoints.ToList();
            _missionToUpload = waypointList;

            var transport = _service.GetTransport();
            if (transport == null)
                return false;

            // Prepare TCS to wait for MISSION_ACK
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _uploadTcs = tcs;

            // Reset state
            _missionItemsExpected = waypointList.Count;
            _missionItemsReceived = 0;

            // Send MISSION_COUNT to initiate upload
            var missionCount = new UasMissionCount
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Count = (ushort)waypointList.Count
            };

            transport.SendMessage(missionCount);

            // Wait for ACK or timeout
            var timeoutTask = Task.Delay(OperationTimeoutMs);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completed == timeoutTask)
            {
                // Timeout
                _uploadTcs = null;
                return false;
            }

            var result = await tcs.Task;

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Upload failed: {ex.Message}");
            return false;
        }
        finally
        {
            _missionToUpload = null;
            _uploadTcs = null;
            _missionLock.Release();
        }
    }

    public async Task<List<WaypointData>> DownloadMissionAsync()
    {
        await _missionLock.WaitAsync();
        try
        {
            var transport = _service.GetTransport();
            if (transport == null) return new List<WaypointData>();

            var tcs = new TaskCompletionSource<List<WaypointData>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _downloadTcs = tcs;

            _downloadedMission = new List<WaypointData>();
            _missionItemsExpected = 0;
            _missionItemsReceived = 0;

            // Request mission list from vehicle
            var requestList = new UasMissionRequestList
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId()
            };

            transport.SendMessage(requestList);

            // Wait for mission items or timeout
            var timeoutTask = Task.Delay(OperationTimeoutMs);
            var completed = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completed == timeoutTask)
            {
                _downloadTcs = null;
                return new List<WaypointData>();
            }

            var items = await tcs.Task;
            return items;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Download failed: {ex.Message}");
            return new List<WaypointData>();
        }
        finally
        {
            _downloadedMission = null;
            _downloadTcs = null;
            _missionLock.Release();
        }
    }

    /// <summary>
    /// Called when vehicle requests a mission item (upload flow) - non-int request
    /// </summary>
    public void HandleMissionRequest(UasMissionRequest request)
    {
        try
        {
            if (_missionToUpload == null)
                return;

            var seq = request.Seq;
            if (seq < 0 || seq >= _missionToUpload.Count)
                return;

            var wp = _missionToUpload[seq];

            // Build non-int mission item (float lat/lon)
            var item = new UasMissionItem
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = (ushort)seq,
                Frame = MavLinkNet.MavFrame.GlobalRelativeAlt,
                Command = (MavCmd)wp.Command,
                Current = (byte)(wp.IsCurrent ? 1 : 0),
                Autocontinue = 1,
                Param1 = (float)wp.Param1,
                Param2 = (float)wp.Param2,
                Param3 = (float)wp.Param3,
                Param4 = (float)wp.Param4,
                X = (float)wp.Latitude,
                Y = (float)wp.Longitude,
                Z = (float)wp.Altitude
            };

            var transport = _service.GetTransport();
            transport?.SendMessage(item);
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Sent MISSION_ITEM seq={seq} (non-int)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionRequest error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when vehicle requests a mission item (upload flow) - INT request
    /// </summary>
    public void HandleMissionRequest(UasMissionRequestInt request)
    {
        try
        {
            if (_missionToUpload == null)
                return;

            var seq = request.Seq;
            if (seq < 0 || seq >= _missionToUpload.Count)
                return;

            var wp = _missionToUpload[seq];

            var item = new UasMissionItemInt
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = (ushort)seq,
                Frame = MavLinkNet.MavFrame.GlobalRelativeAlt,
                Command = (MavCmd)wp.Command,
                Current = (byte)(wp.IsCurrent ? 1 : 0),
                Autocontinue = 1,
                Param1 = (float)wp.Param1,
                Param2 = (float)wp.Param2,
                Param3 = (float)wp.Param3,
                Param4 = (float)wp.Param4,
                X = (int)Math.Round(wp.Latitude * 1e7),
                Y = (int)Math.Round(wp.Longitude * 1e7),
                Z = (float)wp.Altitude
            };

            var transport = _service.GetTransport();
            transport?.SendMessage(item);
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Sent MISSION_ITEM_INT seq={seq}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionRequestInt error: {ex.Message}");
        }
    }

    public void HandleMissionAck(UasMissionAck ack)
    {
        try
        {
            if (_uploadTcs == null)
                return;

            bool success = ack.Type == MavMissionResult.MavMissionAccepted;
            _uploadTcs.TrySetResult(success);
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Received MISSION_ACK: {ack.Type}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionAck error: {ex.Message}");
        }
    }

    public void HandleMissionCount(UasMissionCount count)
    {
        try
        {
            if (_downloadTcs == null)
            {
                System.Diagnostics.Debug.WriteLine("[MissionProtocol] Received MISSION_COUNT but no download in progress");
                return;
            }

            _missionItemsExpected = count.Count;
            _missionItemsReceived = 0;
            _downloadedMission = new List<WaypointData>();

            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Received MISSION_COUNT: {_missionItemsExpected}");

            if (_missionItemsExpected == 0)
            {
                // Empty mission
                _downloadTcs.TrySetResult(new List<WaypointData>());
                return;
            }

            // Request first mission item (use INT request where supported)
            var req = new UasMissionRequestInt
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = 0
            };

            var transport = _service.GetTransport();
            transport?.SendMessage(req);
            System.Diagnostics.Debug.WriteLine("[MissionProtocol] Sent MISSION_REQUEST_INT 0");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionCount error: {ex.Message}");
        }
    }

    public void HandleMissionItemInt(UasMissionItemInt item)
    {
        try
        {
            if (_downloadedMission == null || _downloadTcs == null)
                return;

            var wp = new WaypointData
            {
                Sequence = item.Seq,
                Latitude = item.X / 1e7,
                Longitude = item.Y / 1e7,
                Altitude = item.Z,
                Command = (WaypointCommand)item.Command,
                Param1 = item.Param1,
                Param2 = item.Param2,
                Param3 = item.Param3,
                Param4 = item.Param4,
                IsCurrent = item.Current == 1
            };

            _downloadedMission.Add(wp);
            _missionItemsReceived++;

            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Received MISSION_ITEM_INT {_missionItemsReceived}/{_missionItemsExpected}");

            if (_missionItemsReceived >= _missionItemsExpected)
            {
                // All items received - send ACK and complete
                var ack = new UasMissionAck
                {
                    TargetSystem = _service.GetTargetSystemId(),
                    TargetComponent = _service.GetTargetComponentId(),
                    Type = MavMissionResult.MavMissionAccepted
                };

                var transport = _service.GetTransport();
                transport?.SendMessage(ack);

                _downloadTcs.TrySetResult(_downloadedMission.ToList());
                System.Diagnostics.Debug.WriteLine("[MissionProtocol] Download complete, sent MISSION_ACK");
                return;
            }

            // Request next item
            var nextSeq = (ushort)_missionItemsReceived;
            var reqNext = new UasMissionRequestInt
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = nextSeq
            };

            _service.GetTransport()?.SendMessage(reqNext);
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Sent MISSION_REQUEST_INT {nextSeq}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionItemInt error: {ex.Message}");
        }
    }

    public void HandleMissionItem(UasMissionItem item)
    {
        try
        {
            if (_downloadedMission == null || _downloadTcs == null)
                return;

            var wp = new WaypointData
            {
                Sequence = item.Seq,
                Latitude = item.X,
                Longitude = item.Y,
                Altitude = item.Z,
                Command = (WaypointCommand)item.Command,
                Param1 = item.Param1,
                Param2 = item.Param2,
                Param3 = item.Param3,
                Param4 = item.Param4,
                IsCurrent = item.Current == 1
            };

            _downloadedMission.Add(wp);
            _missionItemsReceived++;

            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Received MISSION_ITEM {_missionItemsReceived}/{_missionItemsExpected}");

            if (_missionItemsReceived >= _missionItemsExpected)
            {
                var ack = new UasMissionAck
                {
                    TargetSystem = _service.GetTargetSystemId(),
                    TargetComponent = _service.GetTargetComponentId(),
                    Type = MavMissionResult.MavMissionAccepted
                };

                _service.GetTransport()?.SendMessage(ack);
                _downloadTcs.TrySetResult(_downloadedMission.ToList());
                System.Diagnostics.Debug.WriteLine("[MissionProtocol] Download complete (non-int), sent MISSION_ACK");
                return;
            }

            // Request next item (non-int)
            var nextSeq = (ushort)_missionItemsReceived;
            var reqNext = new UasMissionRequest
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = nextSeq
            };

            _service.GetTransport()?.SendMessage(reqNext);
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Sent MISSION_REQUEST {nextSeq}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionItem error: {ex.Message}");
        }
    }
}
