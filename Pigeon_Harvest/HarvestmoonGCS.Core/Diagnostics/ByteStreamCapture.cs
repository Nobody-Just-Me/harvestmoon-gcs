using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Captures raw byte streams for offline analysis and comparison.
    /// Useful for comparing Avalonia vs Uno implementations.
    /// </summary>
    public interface IByteStreamCapture
    {
        void StartCapture(string filePath);
        void StopCapture();
        void CaptureBytes(byte[] data, int length);
        bool IsCapturing { get; }
        List<ByteStreamDifference> CompareStreams(string file1, string file2);
    }

    public class ByteStreamCapture : IByteStreamCapture, IDisposable
    {
        private StreamWriter? _writer;
        private readonly object _lock = new();
        private bool _isCapturing = false;
        private string _currentFilePath = "";

        public bool IsCapturing => _isCapturing;

        public void StartCapture(string filePath)
        {
            lock (_lock)
            {
                if (_isCapturing)
                {
                    StopCapture();
                }

                try
                {
                    _currentFilePath = filePath;
                    _writer = new StreamWriter(filePath, false, Encoding.UTF8);
                    _writer.WriteLine($"# Byte Stream Capture Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    _writer.WriteLine("# Format: Timestamp | Length | Hex Data");
                    _writer.WriteLine();
                    _isCapturing = true;

                    System.Diagnostics.Debug.WriteLine($"[ByteStreamCapture] Started capturing to {filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ByteStreamCapture] Failed to start capture: {ex.Message}");
                    _isCapturing = false;
                }
            }
        }

        public void StopCapture()
        {
            lock (_lock)
            {
                if (_writer != null)
                {
                    _writer.WriteLine();
                    _writer.WriteLine($"# Byte Stream Capture Stopped: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }

                _isCapturing = false;
                System.Diagnostics.Debug.WriteLine("[ByteStreamCapture] Stopped capturing");
            }
        }

        public void CaptureBytes(byte[] data, int length)
        {
            if (!_isCapturing || _writer == null) return;

            lock (_lock)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var hex = BitConverter.ToString(data, 0, Math.Min(length, data.Length)).Replace("-", " ");
                    _writer.WriteLine($"{timestamp} | {length} | {hex}");
                    _writer.Flush();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ByteStreamCapture] Failed to capture bytes: {ex.Message}");
                }
            }
        }

        public List<ByteStreamDifference> CompareStreams(string file1, string file2)
        {
            var differences = new List<ByteStreamDifference>();

            try
            {
                var lines1 = File.ReadAllLines(file1).Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l)).ToList();
                var lines2 = File.ReadAllLines(file2).Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l)).ToList();

                int maxLines = Math.Max(lines1.Count, lines2.Count);

                for (int i = 0; i < maxLines; i++)
                {
                    var line1 = i < lines1.Count ? lines1[i] : null;
                    var line2 = i < lines2.Count ? lines2[i] : null;

                    if (line1 == null)
                    {
                        differences.Add(new ByteStreamDifference
                        {
                            LineNumber = i + 1,
                            Type = DifferenceType.MissingInFile1,
                            File1Data = "",
                            File2Data = line2 ?? ""
                        });
                    }
                    else if (line2 == null)
                    {
                        differences.Add(new ByteStreamDifference
                        {
                            LineNumber = i + 1,
                            Type = DifferenceType.MissingInFile2,
                            File1Data = line1,
                            File2Data = ""
                        });
                    }
                    else if (line1 != line2)
                    {
                        var bytes1 = ExtractBytesFromLine(line1);
                        var bytes2 = ExtractBytesFromLine(line2);

                        differences.Add(new ByteStreamDifference
                        {
                            LineNumber = i + 1,
                            Type = DifferenceType.ByteMismatch,
                            File1Data = line1,
                            File2Data = line2,
                            DifferingPositions = FindDifferingPositions(bytes1, bytes2)
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ByteStreamCapture] Comparison found {differences.Count} differences");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ByteStreamCapture] Failed to compare streams: {ex.Message}");
            }

            return differences;
        }

        private byte[] ExtractBytesFromLine(string line)
        {
            try
            {
                // Format: "timestamp | length | hex data"
                var parts = line.Split('|');
                if (parts.Length < 3) return Array.Empty<byte>();

                var hexData = parts[2].Trim();
                var hexBytes = hexData.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return hexBytes.Select(h => Convert.ToByte(h, 16)).ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private List<int> FindDifferingPositions(byte[] bytes1, byte[] bytes2)
        {
            var positions = new List<int>();
            int maxLength = Math.Max(bytes1.Length, bytes2.Length);

            for (int i = 0; i < maxLength; i++)
            {
                byte b1 = i < bytes1.Length ? bytes1[i] : (byte)0;
                byte b2 = i < bytes2.Length ? bytes2[i] : (byte)0;

                if (b1 != b2)
                {
                    positions.Add(i);
                }
            }

            return positions;
        }

        public void Dispose()
        {
            StopCapture();
        }
    }

    public class ByteStreamDifference
    {
        public int LineNumber { get; set; }
        public DifferenceType Type { get; set; }
        public string File1Data { get; set; } = "";
        public string File2Data { get; set; } = "";
        public List<int> DifferingPositions { get; set; } = new();

        public override string ToString()
        {
            return Type switch
            {
                DifferenceType.MissingInFile1 => $"Line {LineNumber}: Missing in file 1",
                DifferenceType.MissingInFile2 => $"Line {LineNumber}: Missing in file 2",
                DifferenceType.ByteMismatch => $"Line {LineNumber}: Bytes differ at positions {string.Join(", ", DifferingPositions)}",
                _ => $"Line {LineNumber}: Unknown difference"
            };
        }
    }

    public enum DifferenceType
    {
        MissingInFile1,
        MissingInFile2,
        ByteMismatch
    }
}
