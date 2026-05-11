using System;
using System.IO;
using System.Diagnostics;
using HarvestmoonGCS.Core.Services;
using HarvestmoonGCS.Core.Exceptions;

namespace HarvestmoonGCS.Core.Helpers;

/// <summary>
/// TLOG file writer for recording MAVLink telemetry data.
/// Writes files in Mission Planner compatible format:
/// 8-byte timestamp (Unix epoch microseconds) + MAVLink packet bytes
/// </summary>
public class TlogWriter : IDisposable
{
    private FileStream? _fileStream;
    private BinaryWriter? _writer;
    private readonly object _writeLock = new object();
    private string? _currentFilePath;
    private readonly ILoggingService? _logger;

    /// <summary>
    /// Indicates whether TLOG recording is currently active
    /// </summary>
    public bool IsRecording { get; private set; }

    /// <summary>
    /// Gets the current TLOG file path
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;

    /// <summary>
    /// Total packets written to current file
    /// </summary>
    public long PacketsWritten { get; private set; }

    public TlogWriter(ILoggingService? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start recording TLOG to a new file
    /// </summary>
    /// <param name="tlogFolder">Folder path where TLOG files will be saved</param>
    /// <returns>True if recording started successfully</returns>
    public bool StartRecording(string tlogFolder)
    {
        if (IsRecording)
        {
            _logger?.LogWarning("Already recording, stopping previous session.", "TlogWriter");
            StopRecording();
        }

        try
        {
            // Create TLogs folder if it doesn't exist
            Directory.CreateDirectory(tlogFolder);

            // Create filename with timestamp (Mission Planner format)
            string filename = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".tlog";
            _currentFilePath = Path.Combine(tlogFolder, filename);

            // Open file for writing
            _fileStream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_fileStream);

            PacketsWritten = 0;
            IsRecording = true;

            _logger?.LogInfo($"Started recording to: {_currentFilePath}", "TlogWriter");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogError($"Access denied to TLOG folder: {tlogFolder}", "TlogWriter", ex);
            CleanupResources();
            throw new FileOperationException($"Access denied to TLOG folder: {tlogFolder}", tlogFolder, "StartRecording", ex);
        }
        catch (IOException ex)
        {
            _logger?.LogError($"I/O error starting recording: {ex.Message}", "TlogWriter", ex);
            CleanupResources();
            throw new FileOperationException($"I/O error starting recording", tlogFolder, "StartRecording", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to start recording: {ex.Message}", "TlogWriter", ex);
            CleanupResources();
            throw new FileOperationException($"Failed to start recording", tlogFolder, "StartRecording", ex);
        }
    }

    /// <summary>
    /// Write a MAVLink packet with timestamp to the TLOG file
    /// </summary>
    /// <param name="packet">Raw MAVLink packet bytes (including STX byte)</param>
    public void WritePacket(byte[] packet)
    {
        if (!IsRecording || packet == null || packet.Length == 0)
            return;

        lock (_writeLock)
        {
            try
            {
                // Get current timestamp as Unix epoch in microseconds
                long timestampUs = GetUnixTimestampMicroseconds();

                // Write 8-byte timestamp (little-endian, as per Mission Planner format)
                _writer?.Write(timestampUs);

                // Write the MAVLink packet
                _writer?.Write(packet);

                PacketsWritten++;

                // Log progress every 1000 packets
                if (PacketsWritten <= 5 || PacketsWritten % 1000 == 0)
                {
                    _logger?.LogDebug($"Written packet #{PacketsWritten}: {packet.Length} bytes", "TlogWriter");
                }
            }
            catch (IOException ex)
            {
                _logger?.LogError($"I/O error writing packet: {ex.Message}", "TlogWriter", ex);
                // Don't throw - continue recording other packets
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error writing packet: {ex.Message}", "TlogWriter", ex);
                // Don't throw - continue recording other packets
            }
        }
    }

    /// <summary>
    /// Stop recording and close the TLOG file
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording)
            return;

        lock (_writeLock)
        {
            try
            {
                _writer?.Flush();
                _logger?.LogInfo($"Stopped recording. Total packets: {PacketsWritten}", "TlogWriter");
            }
            catch (IOException ex)
            {
                _logger?.LogError($"I/O error flushing file: {ex.Message}", "TlogWriter", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error flushing file: {ex.Message}", "TlogWriter", ex);
            }
            finally
            {
                CleanupResources();
                IsRecording = false;
            }
        }
    }

    /// <summary>
    /// Get current Unix timestamp in microseconds
    /// </summary>
    private static long GetUnixTimestampMicroseconds()
    {
        // Get ticks since Unix epoch (Jan 1, 1970)
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long ticksSinceEpoch = DateTime.UtcNow.Ticks - epoch.Ticks;

        // Convert ticks to microseconds (1 tick = 100 nanoseconds = 0.1 microseconds)
        return ticksSinceEpoch / 10;
    }

    /// <summary>
    /// Clean up file resources
    /// </summary>
    private void CleanupResources()
    {
        try
        {
            _writer?.Close();
            _writer?.Dispose();
            _fileStream?.Close();
            _fileStream?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Cleanup error: {ex.Message}", "TlogWriter", ex);
        }
        finally
        {
            _writer = null;
            _fileStream = null;
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        StopRecording();
    }
}
