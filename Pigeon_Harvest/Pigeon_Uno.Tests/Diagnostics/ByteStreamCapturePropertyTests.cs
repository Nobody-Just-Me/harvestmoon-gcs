using System;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Pigeon_Uno.Core.Diagnostics;
using Xunit;

namespace Pigeon_Uno.Tests.Diagnostics
{
    /// <summary>
    /// Property-based tests for ByteStreamCapture.
    /// Validates byte stream capture and comparison functionality.
    /// </summary>
    public class ByteStreamCapturePropertyTests
    {
        /// <summary>
        /// Property 26: Byte Stream Difference Detection
        /// For any two captured byte streams, the comparison tool should identify 
        /// and highlight all byte positions where the streams differ.
        /// Validates: Requirements 6.5
        /// </summary>
        [Property(MaxTest = 20)]
        public Property ByteStreamDifferenceDetection()
        {
            var gen1 = Gen.ListOf(Gen.Choose(1, 100).SelectMany(len => 
                Gen.ArrayOf(len, Arb.Generate<byte>())));
            var gen2 = Gen.ListOf(Gen.Choose(1, 100).SelectMany(len => 
                Gen.ArrayOf(len, Arb.Generate<byte>())));
            
            return Prop.ForAll(
                Arb.From(gen1),
                Arb.From(gen2),
                (stream1, stream2) =>
                {
                    if (stream1.Count() == 0 && stream2.Count() == 0) return true;

                    var capture = new ByteStreamCapture();
                    var file1 = Path.GetTempFileName();
                    var file2 = Path.GetTempFileName();

                    try
                    {
                        // Capture first stream
                        capture.StartCapture(file1);
                        foreach (var bytes in stream1)
                        {
                            capture.CaptureBytes(bytes, bytes.Length);
                        }
                        capture.StopCapture();

                        // Capture second stream
                        capture.StartCapture(file2);
                        foreach (var bytes in stream2)
                        {
                            capture.CaptureBytes(bytes, bytes.Length);
                        }
                        capture.StopCapture();

                        // Compare streams
                        var differences = capture.CompareStreams(file1, file2);

                        // Verify differences are detected correctly
                        if (stream1.Count() != stream2.Count())
                        {
                            // Should detect length difference
                            return differences.Count > 0;
                        }

                        // Check if any bytes differ
                        bool hasDifferences = false;
                        var list1 = stream1.ToList();
                        var list2 = stream2.ToList();
                        for (int i = 0; i < Math.Min(list1.Count, list2.Count); i++)
                        {
                            if (list1[i].Length != list2[i].Length ||
                                !list1[i].SequenceEqual(list2[i]))
                            {
                                hasDifferences = true;
                                break;
                            }
                        }

                        // If streams differ, differences should be detected
                        if (hasDifferences)
                        {
                            return differences.Count > 0;
                        }

                        // If streams are identical, no differences should be detected
                        return differences.Count == 0;
                    }
                    finally
                    {
                        capture.Dispose();
                        if (File.Exists(file1)) File.Delete(file1);
                        if (File.Exists(file2)) File.Delete(file2);
                    }
                });
        }

        /// <summary>
        /// Property: Capture Completeness
        /// For any byte array captured, the capture file should contain 
        /// the complete byte data in hexadecimal format.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property CaptureCompleteness()
        {
            var gen = Gen.Choose(1, 256).SelectMany(len => 
                Gen.ArrayOf(len, Arb.Generate<byte>()));
            
            return Prop.ForAll(
                Arb.From(gen),
                bytes =>
                {
                    var capture = new ByteStreamCapture();
                    var file = Path.GetTempFileName();

                    try
                    {
                        capture.StartCapture(file);
                        capture.CaptureBytes(bytes, bytes.Length);
                        capture.StopCapture();

                        var lines = File.ReadAllLines(file);
                        var dataLines = lines.Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l)).ToList();

                        // Should have exactly one data line
                        if (dataLines.Count != 1) return false;

                        var line = dataLines[0];
                        var parts = line.Split('|');
                        if (parts.Length < 3) return false;

                        // Verify length is recorded
                        var lengthStr = parts[1].Trim();
                        if (!int.TryParse(lengthStr, out int recordedLength)) return false;
                        if (recordedLength != bytes.Length) return false;

                        // Verify hex data is present
                        var hexData = parts[2].Trim();
                        return !string.IsNullOrWhiteSpace(hexData);
                    }
                    finally
                    {
                        capture.Dispose();
                        if (File.Exists(file)) File.Delete(file);
                    }
                });
        }

        /// <summary>
        /// Property: Capture State Management
        /// For any capture operation, starting a new capture should stop the previous one.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property CaptureStateManagement()
        {
            var gen = Gen.ArrayOf(10, Arb.Generate<byte>());
            return Prop.ForAll(
                Arb.From(gen),
                bytes =>
                {
                    var capture = new ByteStreamCapture();
                    var file1 = Path.GetTempFileName();
                    var file2 = Path.GetTempFileName();

                    try
                    {
                        // Start first capture
                        capture.StartCapture(file1);
                        var isCapturing1 = capture.IsCapturing;

                        // Start second capture (should stop first)
                        capture.StartCapture(file2);
                        var isCapturing2 = capture.IsCapturing;

                        // Stop capture
                        capture.StopCapture();
                        var isCapturing3 = capture.IsCapturing;

                        return isCapturing1 && isCapturing2 && !isCapturing3;
                    }
                    finally
                    {
                        capture.Dispose();
                        if (File.Exists(file1)) File.Delete(file1);
                        if (File.Exists(file2)) File.Delete(file2);
                    }
                });
        }

        /// <summary>
        /// Property: Comparison Symmetry
        /// For any two streams, comparing (A, B) should find the same differences 
        /// as comparing (B, A), just with reversed file indicators.
        /// </summary>
        [Property(MaxTest = 10)]
        public Property ComparisonSymmetry()
        {
            var gen1 = Gen.ListOf(Gen.Choose(1, 50).SelectMany(len => 
                Gen.ArrayOf(len, Arb.Generate<byte>())));
            var gen2 = Gen.ListOf(Gen.Choose(1, 50).SelectMany(len => 
                Gen.ArrayOf(len, Arb.Generate<byte>())));
            
            return Prop.ForAll(
                Arb.From(gen1),
                Arb.From(gen2),
                (stream1, stream2) =>
                {
                    if (stream1.Count() == 0 && stream2.Count() == 0) return true;

                    var capture = new ByteStreamCapture();
                    var file1 = Path.GetTempFileName();
                    var file2 = Path.GetTempFileName();

                    try
                    {
                        // Capture streams
                        capture.StartCapture(file1);
                        foreach (var bytes in stream1) capture.CaptureBytes(bytes, bytes.Length);
                        capture.StopCapture();

                        capture.StartCapture(file2);
                        foreach (var bytes in stream2) capture.CaptureBytes(bytes, bytes.Length);
                        capture.StopCapture();

                        // Compare both directions
                        var diff12 = capture.CompareStreams(file1, file2);
                        var diff21 = capture.CompareStreams(file2, file1);

                        // Should find same number of differences
                        return diff12.Count == diff21.Count;
                    }
                    finally
                    {
                        capture.Dispose();
                        if (File.Exists(file1)) File.Delete(file1);
                        if (File.Exists(file2)) File.Delete(file2);
                    }
                });
        }
    }
}
