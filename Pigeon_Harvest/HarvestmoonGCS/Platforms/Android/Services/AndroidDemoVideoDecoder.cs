#if __ANDROID__
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Media;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Platforms.Android.Services;

/// <summary>
/// Decodes the bundled YDXJ_fused_only_detected.mp4 asset frame-by-frame using
/// MediaMetadataRetriever, converts each frame to JPEG, and fires the
/// FrameDecoded event so DashboardPage can feed it into VideoStreamControl.
/// Loops the video continuously until StopAsync() is called.
/// </summary>
public sealed class AndroidDemoVideoDecoder : IDisposable
{
    private const string AssetPath = "demo_videos/YDXJ_fused_only_detected.mp4";
    // Push the dashboard demo a bit faster so playback feels smoother on the tablet.
    private const int TargetFps = 18;
    private const int FrameIntervalMs = 1000 / TargetFps;
    private const int JpegQuality = 58;
    private const int MaxWidth = 480;

    private readonly Context _context;
    private CancellationTokenSource? _cts;
    private Task? _decodeTask;
    private string? _tempVideoPath;

    public bool IsRunning => _decodeTask != null && !_decodeTask.IsCompleted;

    /// <summary>Fired on a background thread for each decoded JPEG frame.</summary>
    public event EventHandler<byte[]>? FrameDecoded;

    public AndroidDemoVideoDecoder(Context context)
    {
        _context = context;
    }

    /// <summary>
    /// Copy the asset to a temp file (needed because MediaExtractor requires a seekable file
    /// descriptor, which AssetFileDescriptor provides but we open it safely via CreateFd).
    /// </summary>
    private async Task<string?> ExtractAssetAsync(CancellationToken ct)
    {
        try
        {
            var dir = _context.CacheDir?.AbsolutePath ?? System.IO.Path.GetTempPath();
            var dest = System.IO.Path.Combine(dir, "ydxj_demo.mp4");

            // Only re-extract if the cached copy is missing or zero-length
            if (!File.Exists(dest) || new FileInfo(dest).Length < 1000)
            {
                using var assetStream = _context.Assets?.Open(AssetPath);
                if (assetStream == null)
                {
                    Serilog.Log.Warning("[DemoDecoder] Asset not found: {Path}", AssetPath);
                    return null;
                }
                using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write);
                await assetStream.CopyToAsync(fs, ct);
            }
            return dest;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[DemoDecoder] Failed to extract asset");
            return null;
        }
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _tempVideoPath = await ExtractAssetAsync(ct);
        if (_tempVideoPath == null) return;

        _decodeTask = Task.Run(() => DecodeLoop(_tempVideoPath, ct), ct);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_decodeTask != null)
        {
            try { await _decodeTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
        }
        _decodeTask = null;
        _cts = null;
    }

    private void DecodeLoop(string videoPath, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                DecodeOnce(videoPath, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[DemoDecoder] Decode error, retrying");
                Thread.Sleep(500);
            }
        }
    }

    private void DecodeOnce(string videoPath, CancellationToken ct)
    {
        using var retriever = new MediaMetadataRetriever();
        retriever.SetDataSource(videoPath);
        var durationMsText = retriever.ExtractMetadata(MetadataKey.Duration);
        if (!long.TryParse(durationMsText, out var durationMs) || durationMs <= 0)
        {
            Serilog.Log.Warning("[DemoDecoder] Invalid duration for {Path}", videoPath);
            return;
        }

        long frameIntervalUs = 1_000_000L / TargetFps;
        long durationUs = durationMs * 1000L;
        long frameTimeUs = 0;

        try
        {
            while (!ct.IsCancellationRequested && frameTimeUs < durationUs)
            {
                var frameStopwatch = Stopwatch.StartNew();
                using var bmp = retriever.GetFrameAtTime(frameTimeUs, global::Android.Media.Option.Closest);
                if (bmp != null)
                {
                    var jpeg = BitmapToJpeg(bmp);
                    if (jpeg != null && jpeg.Length > 0)
                    {
                        FrameDecoded?.Invoke(this, jpeg);
                    }
                }

                frameTimeUs += frameIntervalUs;
                frameStopwatch.Stop();
                var remainingDelay = FrameIntervalMs - (int)frameStopwatch.ElapsedMilliseconds;
                if (remainingDelay > 0)
                {
                    Thread.Sleep(remainingDelay);
                }
            }
        }
        finally
        {
            retriever.Release();
        }
    }

    private static byte[]? BitmapToJpeg(Bitmap bmp)
    {
        try
        {
            int width  = bmp.Width;
            int height = bmp.Height;

            // Scale down if too large
            int outW = width, outH = height;
            if (width > MaxWidth)
            {
                outH = height * MaxWidth / width;
                outW = MaxWidth;
            }

            // Resize if needed
            if (outW != width || outH != height)
            {
                var scaled = Bitmap.CreateScaledBitmap(bmp, outW, outH, true)!;
                using var msScaled = new MemoryStream();
                scaled.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, msScaled);
                scaled.Recycle();
                return msScaled.ToArray();
            }

            using var ms = new MemoryStream();
            bmp.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[DemoDecoder] BitmapToJpeg failed");
            return null;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
#endif
