using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pigeon_Uno.Core.Helpers;

/// <summary>
/// TLOG file player for playing back recorded MAVLink telemetry.
/// Supports both Mission Planner format (8-byte timestamp + packet) and raw MAVLink packets.
/// </summary>
public class TlogPlayer : IDisposable
{
    private FileStream? _fileStream;
    private BinaryReader? _reader;
    private long _fileLength;

    public DateTime StartTime { get; private set; }
    public TimeSpan TotalDuration { get; private set; }
    public int TotalPackets { get; private set; }
    public bool IsLoaded { get; private set; }

    // Store packet positions, sizes, and timestamps
    private List<long> _packetPositions = new List<long>();
    private List<int> _packetSizes = new List<int>();
    private List<long> _packetTimestamps = new List<long>(); // Unix epoch microseconds
    
    // Current packet index for sequential reading
    private int _currentPacketIndex = 0;

    // Whether the file has Mission Planner timestamps
    private bool _hasTimestamps = false;

    private const byte STX_V1 = 0xFE;
    private const byte STX_V2 = 0xFD;

    // Unix epoch for timestamp conversion
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public bool LoadTlogFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            Dispose();

            _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_fileStream);
            _fileLength = _fileStream.Length;

            _packetPositions.Clear();
            _packetSizes.Clear();
            _packetTimestamps.Clear();
            TotalPackets = 0;
            _hasTimestamps = false;

            long pos = 0;

            // Detect TLOG header (A3 95 xx) - skip if present
            if (_fileLength >= 3)
            {
                _fileStream.Seek(0, SeekOrigin.Begin);
                byte b1 = _reader.ReadByte();
                byte b2 = _reader.ReadByte();
                if (b1 == 0xA3 && b2 == 0x95)
                {
                    _reader.ReadByte(); // version
                    pos = 3;
                }
                else
                {
                    _fileStream.Seek(0, SeekOrigin.Begin);
                }
            }

            // Try to detect if file has Mission Planner timestamp format
            // Format: 8-byte timestamp (long) + MAVLink packet
            _hasTimestamps = DetectTimestampFormat(pos);
            System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Timestamp format detected: {_hasTimestamps}");

            // Scan all MAVLink packets
            while (pos < _fileLength)
            {
                long timestampUs = 0;
                long packetStartPos = pos;

                if (_hasTimestamps)
                {
                    // Read 8-byte timestamp first
                    if (pos + 8 > _fileLength) break;
                    _fileStream.Seek(pos, SeekOrigin.Begin);
                    timestampUs = _reader.ReadInt64();
                    pos += 8;
                }

                if (pos + 2 > _fileLength) break;

                _fileStream.Seek(pos, SeekOrigin.Begin);
                byte possibleStx = _reader.ReadByte();

                // Skip non-MAVLink bytes
                if (possibleStx != STX_V1 && possibleStx != STX_V2)
                {
                    pos++;
                    continue;
                }

                byte len = _reader.ReadByte();
                if (len > 255) { pos++; continue; }

                int packetSize;
                if (possibleStx == STX_V1)
                {
                    // MAVLink v1: STX + LEN + SEQ + SYSID + COMPID + MSGID + PAYLOAD + CRC(2)
                    packetSize = 1 + 1 + 4 + len + 2;  // 8 + len
                }
                else
                {
                    // MAVLink v2: STX + LEN + INCOMPAT + COMPAT + SEQ + SYSID + COMPID + MSGID(3) + PAYLOAD + CRC(2)
                    packetSize = 1 + 1 + 8 + len + 2;  // 12 + len
                    
                    // Check for signature (incompat flag bit 0)
                    if (pos + 2 < _fileLength)
                    {
                        _fileStream.Seek(pos + 2, SeekOrigin.Begin);
                        byte incompatFlags = _reader.ReadByte();
                        if ((incompatFlags & 0x01) != 0)
                        {
                            packetSize += 13; // Signature size
                        }
                    }
                }

                if (pos + packetSize > _fileLength) break;

                _packetPositions.Add(pos);  // Store MAVLink packet position (after timestamp)
                _packetSizes.Add(packetSize);
                _packetTimestamps.Add(timestampUs);
                TotalPackets++;
                pos += packetSize;
            }

            if (TotalPackets == 0)
            {
                System.Diagnostics.Debug.WriteLine("[TlogPlayer] No packets found!");
                return false;
            }

            // Calculate duration from actual timestamps if available
            if (_hasTimestamps && _packetTimestamps.Count >= 2)
            {
                long firstTs = _packetTimestamps[0];
                long lastTs = _packetTimestamps[_packetTimestamps.Count - 1];
                double durationSeconds = (lastTs - firstTs) / 1000000.0; // microseconds to seconds
                
                if (durationSeconds > 0 && durationSeconds < 36000) // Sanity check: max 10 hours
                {
                    TotalDuration = TimeSpan.FromSeconds(durationSeconds);
                    StartTime = UnixEpoch.AddTicks(firstTs * 10).ToLocalTime(); // Convert microseconds to ticks
                    System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Using actual timestamps. Duration: {TotalDuration:hh\\:mm\\:ss}");
                }
                else
                {
                    // Fall back to estimation
                    EstimateDuration();
                }
            }
            else
            {
                // Estimate duration based on packet rate
                EstimateDuration();
            }

            IsLoaded = true;
            _currentPacketIndex = 0;

            System.Diagnostics.Debug.WriteLine($"[TlogPlayer] SUCCESS! {TotalPackets} packets → Duration {TotalDuration:mm\\:ss}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Failed to load: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Detect if file uses Mission Planner timestamp format
    /// </summary>
    private bool DetectTimestampFormat(long startPos)
    {
        if (_fileLength < startPos + 16) return false;

        try
        {
            _fileStream!.Seek(startPos, SeekOrigin.Begin);
            
            // Read potential timestamp (8 bytes)
            long potentialTs = _reader!.ReadInt64();
            byte stx = _reader.ReadByte();

            // Check if next byte is a valid MAVLink STX
            if (stx == STX_V1 || stx == STX_V2)
            {
                // Validate timestamp is reasonable (year 2000-2100)
                DateTime minDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime maxDate = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                long minTimestamp = (minDate.Ticks - UnixEpoch.Ticks) / 10;
                long maxTimestamp = (maxDate.Ticks - UnixEpoch.Ticks) / 10;

                if (potentialTs >= minTimestamp && potentialTs <= maxTimestamp)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Estimate duration based on typical MAVLink packet rates
    /// </summary>
    private void EstimateDuration()
    {
        double seconds = TotalPackets / 75.0; // Typical 75 Hz
        if (seconds < 10) seconds = TotalPackets / 40.0;
        if (seconds > 3600) seconds = 3600;

        TotalDuration = TimeSpan.FromSeconds(seconds);
        StartTime = DateTime.Now.AddSeconds(-seconds);
    }

    public Tuple<DateTime, byte[]>? GetNextPacket()
    {
        if (!IsLoaded || _fileStream == null)
        {
            return null;
        }

        // Check if we've reached the end
        if (_currentPacketIndex >= TotalPackets)
        {
            System.Diagnostics.Debug.WriteLine("[TlogPlayer] End of packets reached");
            return null;
        }

        try
        {
            long pos = _packetPositions[_currentPacketIndex];
            int size = _packetSizes[_currentPacketIndex];

            _fileStream.Seek(pos, SeekOrigin.Begin);
            byte[] packet = _reader!.ReadBytes(size);

            DateTime packetTime;
            if (_hasTimestamps && _currentPacketIndex < _packetTimestamps.Count)
            {
                // Use actual timestamp
                long timestampUs = _packetTimestamps[_currentPacketIndex];
                packetTime = UnixEpoch.AddTicks(timestampUs * 10).ToLocalTime();
            }
            else
            {
                // Calculate time based on packet index
                double ratio = (double)_currentPacketIndex / Math.Max(1, TotalPackets - 1);
                TimeSpan offset = TimeSpan.FromSeconds(ratio * TotalDuration.TotalSeconds);
                packetTime = StartTime + offset;
            }

            int idx = _currentPacketIndex;
            _currentPacketIndex++;

            // Log only first few and every 100th packet
            if (idx < 5 || idx % 100 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Packet {idx}: {size} bytes at pos {pos}");
            }

            return new Tuple<DateTime, byte[]>(packetTime, packet);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Error reading packet {_currentPacketIndex}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get time offset for a specific packet index
    /// </summary>
    public TimeSpan GetPacketTimeOffset(int packetIndex)
    {
        if (!IsLoaded || packetIndex < 0 || packetIndex >= TotalPackets)
            return TimeSpan.Zero;

        if (_hasTimestamps && _packetTimestamps.Count > packetIndex)
        {
            long firstTs = _packetTimestamps[0];
            long packetTs = _packetTimestamps[packetIndex];
            return TimeSpan.FromTicks((packetTs - firstTs) * 10);
        }
        else
        {
            double ratio = (double)packetIndex / Math.Max(1, TotalPackets - 1);
            return TimeSpan.FromSeconds(ratio * TotalDuration.TotalSeconds);
        }
    }

    public int CurrentPacketIndex => _currentPacketIndex;

    public void Reset()
    {
        _currentPacketIndex = 0;
        System.Diagnostics.Debug.WriteLine("[TlogPlayer] Reset to beginning");
    }

    public bool SeekToTime(TimeSpan desiredOffset)
    {
        if (!IsLoaded || TotalPackets == 0) return false;

        int targetIndex;

        if (_hasTimestamps && _packetTimestamps.Count > 1)
        {
            // Binary search for the closest timestamp
            long firstTs = _packetTimestamps[0];
            long targetTs = firstTs + (long)(desiredOffset.TotalSeconds * 1000000);

            targetIndex = BinarySearchTimestamp(targetTs);
        }
        else
        {
            // Use ratio-based seeking
            double ratio = desiredOffset.TotalSeconds / TotalDuration.TotalSeconds;
            ratio = Math.Max(0, Math.Min(1, ratio));
            targetIndex = (int)(ratio * (TotalPackets - 1));
        }

        targetIndex = Math.Max(0, Math.Min(targetIndex, TotalPackets - 1));
        _currentPacketIndex = targetIndex;
        
        System.Diagnostics.Debug.WriteLine($"[TlogPlayer] Seek to index {targetIndex} (time {desiredOffset:mm\\:ss})");
        return true;
    }

    /// <summary>
    /// Binary search to find packet index closest to target timestamp
    /// </summary>
    private int BinarySearchTimestamp(long targetTs)
    {
        int left = 0;
        int right = _packetTimestamps.Count - 1;

        while (left < right)
        {
            int mid = (left + right) / 2;
            if (_packetTimestamps[mid] < targetTs)
                left = mid + 1;
            else
                right = mid;
        }

        return left;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _fileStream?.Dispose();
        _reader = null;
        _fileStream = null;
        _packetPositions.Clear();
        _packetSizes.Clear();
        _packetTimestamps.Clear();
        IsLoaded = false;
        _currentPacketIndex = 0;
    }
}
