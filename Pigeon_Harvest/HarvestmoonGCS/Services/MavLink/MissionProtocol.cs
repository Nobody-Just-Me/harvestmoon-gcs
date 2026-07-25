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
/// Full implementation: handles MISSION_COUNT, MISSION_REQUEST/INT, MISSION_ITEM_INT, MISSION_ACK
/// </summary>
internal class MissionProtocol
{
    private readonly MavLinkService _service;
    private readonly SemaphoreSlim _missionLock = new SemaphoreSlim(1, 1);

    // Upload state
    private TaskCompletionSource<bool>? _uploadTcs;
    private List<WaypointData>? _currentMission;

    // Download state
    private TaskCompletionSource<List<WaypointData>>? _downloadTcs;
    private List<WaypointData>? _downloadBuffer;
    private int _expectedCount;
    private int _receivedCount;

    // Default timeout for mission operations
    private const int MissionOperationTimeoutMs = 15000;

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
            _currentMission = waypointList;

            // Prepare TCS and send MISSION_COUNT
            _uploadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var transport = _service.GetTransport();
            if (transport == null)
            {
                _uploadTcs.TrySetResult(false);
                return false;
            }

            var missionCount = new UasMissionCount
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Count = (ushort)waypointList.Count
            };

            transport.SendMessage(missionCount);
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Sent MISSION_COUNT: {waypointList.Count}");

            // Wait for ack/result or timeout
            var timeoutTask = Task.Delay(MissionOperationTimeoutMs);
            var completed = await Task.WhenAny(_uploadTcs.Task, timeoutTask);
            if (completed == timeoutTask)
            {
                _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, "[MissionProtocol] Upload timed out");
                _uploadTcs = null;
                return false;
            }

            var result = await _uploadTcs.Task;
            _uploadTcs = null;
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Upload failed: {ex.Message}");
            return false;
        }
        finally
        {
            _missionLock.Release();
        }
    }

    public async Task<List<WaypointData>> DownloadMissionAsync()
    {
        await _missionLock.WaitAsync();
        try
        {
            _downloadTcs = new TaskCompletionSource<List<WaypointData>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _downloadBuffer = new List<WaypointData>();
            _expectedCount = 0;
            _receivedCount = 0;

            var transport = _service.GetTransport();
            if (transport == null)
            {
                _downloadTcs.TrySetResult(new List<WaypointData>());
                return new List<WaypointData>();
            }

            var requestList = new UasMissionRequestList
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId()
            };

            transport.SendMessage(requestList);
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, "[MissionProtocol] Sent MISSION_REQUEST_LIST");

            var timeoutTask = Task.Delay(MissionOperationTimeoutMs);
            var completed = await Task.WhenAny(_downloadTcs.Task, timeoutTask);
            if (completed == timeoutTask)
            {
                _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, "[MissionProtocol] Download timed out");
                _downloadTcs = null;
                _downloadBuffer = null;
                return new List<WaypointData>();
            }

            var result = await _downloadTcs.Task;
            _downloadTcs = null;
            _downloadBuffer = null;
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] Download failed: {ex.Message}");
            return new List<WaypointData>();
        }
        finally
        {
            _missionLock.Release();
        }
    }

    // Called by MavLinkService when target requests an item (MISSION_REQUEST / MISSION_REQUEST_INT)
    public void HandleMissionRequest(UasMissionRequest request)
    {
        // Legacy mission request (non-int)
        var seq = request.Seq;
        SendMissionItem(seq);
    }

    public void HandleMissionRequestInt(UasMissionRequestInt request)
    {
        var seq = request.Seq;
        SendMissionItem(seq);
    }

    public void HandleMissionAck(UasMissionAck ack)
    {
        // Called after vehicle finishes receiving mission (ACK)
        try
        {
            var success = ack.Type == MavMissionResult.MavMissionAccepted;
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Received MISSION_ACK: {ack.Type}");

            _uploadTcs?.TrySetResult(success);

            // Clear current mission upload buffer on success/failure
            if (success)
            {
                _currentMission = null;
            }
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
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Received MISSION_COUNT: {count.Count}");

            if (_downloadTcs == null || _downloadBuffer == null)
            {
                // No download in progress
                return;
            }

            _expectedCount = count.Count;
            _receivedCount = 0;
            _downloadBuffer.Clear();

            if (_expectedCount == 0)
            {
                // Empty mission
                _downloadTcs.TrySetResult(new List<WaypointData>());
                return;
            }

            // Request first item (request int preferred)
            var request = new UasMissionRequestInt
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = 0
            };

            var transport = _service.GetTransport();
            transport?.SendMessage(request);
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, "[MissionProtocol] Sent MISSION_REQUEST_INT 0");
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
            if (_downloadBuffer == null || _downloadTcs == null)
            {
                // No download in progress
                return;
            }

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

            _downloadBuffer.Add(wp);
            _receivedCount++;

            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Received MISSION_ITEM_INT {item.Seq} ({_receivedCount}/{_expectedCount})");

            if (_receivedCount >= _expectedCount)
            {
                // All items received — send ACK and complete
                var ack = new UasMissionAck
                {
                    TargetSystem = _service.GetTargetSystemId(),
                    TargetComponent = _service.GetTargetComponentId(),
                    Type = MavMissionResult.MavMissionAccepted
                };

                var transport = _service.GetTransport();
                transport?.SendMessage(ack);
                _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, "[MissionProtocol] Sent MISSION_ACK");

                // Complete the TCS
                _downloadTcs.TrySetResult(_downloadBuffer.ToList());
            }
            else
            {
                // Request next item
                var nextSeq = item.Seq + 1;
                var req = new UasMissionRequestInt
                {
                    TargetSystem = _service.GetTargetSystemId(),
                    TargetComponent = _service.GetTargetComponentId(),
                    Seq = nextSeq
                };
                var transport = _service.GetTransport();
                transport?.SendMessage(req);
                _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Sent MISSION_REQUEST_INT {nextSeq}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] HandleMissionItemInt error: {ex.Message}");
        }
    }

    private void SendMissionItem(int seq)
    {
        try
        {
            if (_currentMission == null) return;
            if (seq < 0 || seq >= _currentMission.Count) return;

            var wp = _currentMission[seq];

            // Build MISSION_ITEM_INT
            var item = new UasMissionItemInt
            {
                TargetSystem = _service.GetTargetSystemId(),
                TargetComponent = _service.GetTargetComponentId(),
                Seq = seq,
                Frame = 0, // MAV_FRAME_GLOBAL_RELATIVE_ALT / may be set by caller; use 0 for protocol compatibility
                Command = (uint)wp.Command,
                Current = (byte)(wp.IsCurrent ? 1 : 0),
                Autocontinue = 1,
                Param1 = (float)wp.Param1,
                Param2 = (float)wp.Param2,
                Param3 = (float)wp.Param3,
                Param4 = (float)wp.Param4,
                X = (int)(wp.Latitude * 1e7),
                Y = (int)(wp.Longitude * 1e7),
                Z = (float)wp.Altitude
            };

            var transport = _service.GetTransport();
            transport?.SendMessage(item);
            _service.GetDiagnosticLogger()?.LogTelemetryEvent(DateTime.Now, $"[MissionProtocol] Sent MISSION_ITEM_INT {seq}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MissionProtocol] SendMissionItem error: {ex.Message}");
        }
    }
}
