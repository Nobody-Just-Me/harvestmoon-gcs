using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Services;

namespace HarvestmoonGCS.Services;

/// <summary>
/// Camera service using Python OpenCV wrapper
/// Works around OpenCvSharp dependency issues on Ubuntu 24.04
/// </summary>
public class PythonCameraService : ICameraService
{
    private static readonly JsonSerializerOptions CameraJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Process? _streamProcess;
    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private Task? _stderrTask;
    private string? _currentSource;
    private readonly string _outputDirectory;
    private readonly string? _pythonScript;
    private readonly string? _pythonCommand;
    private readonly string _pythonLauncherArgs;
    private readonly bool _pythonScriptExists;

    public bool IsStreaming { get; private set; }
    public bool IsRecording { get; private set; }

    public event EventHandler<byte[]>? FrameReceived;
    public event EventHandler<bool>? StreamingStatusChanged;
    public event EventHandler<bool>? RecordingStatusChanged;
    public event EventHandler<string>? ConnectionError;

    public PythonCameraService()
    {
        // Create output directory for screenshots and videos
        _outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HarvestmoonGCS",
            "Camera"
        );
        Directory.CreateDirectory(_outputDirectory);

        (_pythonCommand, _pythonLauncherArgs) = ResolvePythonCommand();
        _pythonScript = ResolvePythonScriptPath();
        _pythonScriptExists = !string.IsNullOrWhiteSpace(_pythonScript) && File.Exists(_pythonScript);

        Serilog.Log.Information($"[PythonCameraService] Output directory: {_outputDirectory}");
        Serilog.Log.Information($"[PythonCameraService] Python command: {_pythonCommand ?? "NOT_FOUND"}");
        Serilog.Log.Information($"[PythonCameraService] Python script: {_pythonScript ?? "NOT_FOUND"}");

        if (string.IsNullOrWhiteSpace(_pythonCommand))
        {
            Serilog.Log.Warning("[PythonCameraService] Python runtime not found. Install python3 and ensure it is in PATH.");
        }

        if (!_pythonScriptExists)
        {
            Serilog.Log.Warning("[PythonCameraService] camera_service.py not found. Set PIGEON_CAMERA_SCRIPT or ensure camera_service.py exists in app/repo root.");
        }
    }

    public async Task InitializeAsync()
    {
        Serilog.Log.Information("[PythonCameraService] Initialized");
        await Task.CompletedTask;
    }

    public async Task<List<CameraSource>> GetAvailableSourcesAsync()
    {
        var sources = new List<CameraSource>();

        if (!EnsureRuntimeReady(notifyUser: false))
        {
            return sources;
        }

        try
        {
            Serilog.Log.Information("[PythonCameraService] ========== DETECTING CAMERAS ==========");

            // Call Python script to list cameras
            var process = new Process
            {
                StartInfo = BuildPythonStartInfo("list")
            };

            Serilog.Log.Information("[PythonCameraService] Calling Python script...");
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(error))
            {
                Serilog.Log.Information($"[PythonCameraService] Python stderr: {error}");
            }

            Serilog.Log.Information($"[PythonCameraService] Python output: {output}");

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                Serilog.Log.Warning("[PythonCameraService] Camera listing returned no usable JSON output (exitCode={ExitCode})", process.ExitCode);
                return sources;
            }

            // Parse JSON output
            try
            {
                sources = JsonSerializer.Deserialize<List<CameraSource>>(output, CameraJsonOptions) ?? new List<CameraSource>();
                NormalizeSources(sources);
            }
            catch (JsonException ex)
            {
                Serilog.Log.Warning(ex, "[PythonCameraService] Failed to parse camera list JSON output");
                return new List<CameraSource>();
            }
            
            Serilog.Log.Information($"[PythonCameraService] ========== FOUND {sources.Count} CAMERA SOURCES ==========");
            
            foreach (var source in sources)
            {
                if (source.Type == CameraSourceType.LocalCamera)
                {
                    Serilog.Log.Information($"[PythonCameraService] ✓ {source.Name}: {source.Description}");
                }
            }

            if (sources.Count(s => s.Type == CameraSourceType.LocalCamera) == 0)
            {
                Serilog.Log.Warning("[PythonCameraService] ⚠️  WARNING: No local cameras detected!");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[PythonCameraService] ========== ERROR DETECTING CAMERAS ==========");
            ConnectionError?.Invoke(this, $"Camera detection error: {ex.Message}");
        }
        
        return sources;
    }

    private static void NormalizeSources(List<CameraSource> sources)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!string.IsNullOrWhiteSpace(source.Id))
            {
                usedIds.Add(source.Id);
            }
        }

        var nextLocalIndex = 0;
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Id))
            {
                if (source.Type == CameraSourceType.NetworkStream)
                {
                    var candidate = "network";
                    var suffix = 1;
                    while (usedIds.Contains(candidate))
                    {
                        candidate = $"network_{suffix++}";
                    }

                    source.Id = candidate;
                }
                else
                {
                    while (usedIds.Contains(nextLocalIndex.ToString()))
                    {
                        nextLocalIndex++;
                    }

                    source.Id = nextLocalIndex.ToString();
                }

                usedIds.Add(source.Id);
            }

            if (string.IsNullOrWhiteSpace(source.Name))
            {
                source.Name = source.Type == CameraSourceType.NetworkStream
                    ? "Network Stream (RTSP/HTTP)"
                    : $"Camera {source.Id}";
            }

            if (string.IsNullOrWhiteSpace(source.Description))
            {
                source.Description = source.Type == CameraSourceType.NetworkStream
                    ? "Enter custom network stream URL"
                    : $"Local Camera {source.Id}";
            }
        }
    }

    public async Task<bool> StartCameraAsync(string source)
    {
        if (!EnsureRuntimeReady(notifyUser: true))
        {
            return false;
        }

        try
        {
            Serilog.Log.Information($"[PythonCameraService] ========== STARTING CAMERA ==========");
            Serilog.Log.Information($"[PythonCameraService] Source: {source}");
            
            await StopCameraAsync();
            
            _currentSource = source;

            if (source == "network")
            {
                Serilog.Log.Information("[PythonCameraService] Network stream selected - user needs to provide URL");
                ConnectionError?.Invoke(this, "Please enter network stream URL");
                return false;
            }

            // Start Python process to stream camera
            _streamProcess = new Process
            {
                StartInfo = BuildPythonStartInfo($"stream {QuoteArg(source)}")
            };

            Serilog.Log.Information("[PythonCameraService] Starting Python stream process...");
            _streamProcess.Start();

            IsStreaming = true;
            StreamingStatusChanged?.Invoke(this, true);

            // Start reading frames
            _streamCts = new CancellationTokenSource();
            _streamTask = Task.Run(() => ReadFramesLoop(_streamCts.Token));
            _stderrTask = Task.Run(() => ReadStdErrLoop(_streamCts.Token));

            Serilog.Log.Information("[PythonCameraService] ✓ Camera streaming started");
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[PythonCameraService] ✗ Error starting camera");
            ConnectionError?.Invoke(this, $"Failed to start camera: {ex.Message}");
            return false;
        }
    }

    public async Task StopCameraAsync()
    {
        Serilog.Log.Information("[PythonCameraService] Stopping camera");
        
        _streamCts?.Cancel();

        if (_streamTask != null)
        {
            await _streamTask;
        }

        if (_stderrTask != null)
        {
            await _stderrTask;
        }

        if (_streamProcess != null && !_streamProcess.HasExited)
        {
            try
            {
                _streamProcess.Kill();
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "[PythonCameraService] Stream process kill failed");
            }
        }

        _streamProcess?.Dispose();
        _streamProcess = null;
        _streamTask = null;
        _stderrTask = null;
        _streamCts?.Dispose();
        _streamCts = null;

        IsStreaming = false;
        StreamingStatusChanged?.Invoke(this, false);

        Serilog.Log.Information("[PythonCameraService] Camera stopped");
    }

    private void ReadFramesLoop(CancellationToken ct)
    {
        Serilog.Log.Information("[PythonCameraService] Frame reading loop started");
        
        int frameCount = 0;
        
        try
        {
            while (!ct.IsCancellationRequested && _streamProcess != null && !_streamProcess.HasExited)
            {
                var line = _streamProcess.StandardOutput.ReadLine();
                
                if (string.IsNullOrEmpty(line))
                    continue;
                
                try
                {
                    // Parse JSON frame data
                    var frameData = JsonSerializer.Deserialize<JsonElement>(line);
                    
                    if (frameData.TryGetProperty("type", out var type) && type.GetString() == "frame")
                    {
                        if (frameData.TryGetProperty("data", out var data))
                        {
                            var base64Data = data.GetString();
                            if (!string.IsNullOrEmpty(base64Data))
                            {
                                var frameBytes = Convert.FromBase64String(base64Data);
                                FrameReceived?.Invoke(this, frameBytes);
                                
                                frameCount++;
                                
                                if (frameCount % 30 == 0)
                                {
                                    Serilog.Log.Information($"[PythonCameraService] Streamed {frameCount} frames");
                                }
                            }
                        }
                    }
                    else if (frameData.TryGetProperty("error", out var error))
                    {
                        Serilog.Log.Error($"[PythonCameraService] Python error: {error.GetString()}");
                        break;
                    }
                }
                catch (JsonException ex)
                {
                    Serilog.Log.Warning($"[PythonCameraService] Failed to parse frame: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[PythonCameraService] Frame reading error");
        }

        Serilog.Log.Information("[PythonCameraService] Frame reading loop ended");
    }

    private void ReadStdErrLoop(CancellationToken ct)
    {
        try
        {
            if (_streamProcess == null)
            {
                return;
            }

            while (!ct.IsCancellationRequested && !_streamProcess.HasExited)
            {
                var line = _streamProcess.StandardError.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Serilog.Log.Debug("[PythonCameraService] python stderr: {Line}", line);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[PythonCameraService] stderr reader stopped");
        }
    }

    public async Task<bool> TakePictureAsync(string? filename = null)
    {
        if (!EnsureRuntimeReady(notifyUser: false))
        {
            return false;
        }

        if (!IsStreaming || string.IsNullOrEmpty(_currentSource))
        {
            Serilog.Log.Warning("[PythonCameraService] Cannot take picture - not streaming");
            return false;
        }

        try
        {
            filename ??= Path.Combine(_outputDirectory, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");

            // Call Python script to take picture
            var process = new Process
            {
                StartInfo = BuildPythonStartInfo($"picture {QuoteArg(_currentSource!)} {QuoteArg(filename)}")
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return false;
            }
            
            JsonElement result;
            try
            {
                result = JsonSerializer.Deserialize<JsonElement>(output);
            }
            catch (JsonException)
            {
                return false;
            }

            if (result.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                Serilog.Log.Information($"[PythonCameraService] Picture saved: {filename}");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[PythonCameraService] Error taking picture");
            return false;
        }
    }

    private bool EnsureRuntimeReady(bool notifyUser)
    {
        if (!_pythonScriptExists || string.IsNullOrWhiteSpace(_pythonScript))
        {
            if (notifyUser)
            {
                ConnectionError?.Invoke(this, "camera_service.py not found. Set PIGEON_CAMERA_SCRIPT or place camera_service.py in app directory.");
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(_pythonCommand))
        {
            if (notifyUser)
            {
                ConnectionError?.Invoke(this, "Python runtime not found. Install python3 and retry.");
            }

            return false;
        }

        return true;
    }

    private ProcessStartInfo BuildPythonStartInfo(string scriptArguments)
    {
        var launcher = string.IsNullOrWhiteSpace(_pythonLauncherArgs) ? "" : $"{_pythonLauncherArgs} ";
        var args = $"{launcher}{QuoteArg(_pythonScript!)}";
        if (!string.IsNullOrWhiteSpace(scriptArguments))
        {
            args = $"{args} {scriptArguments}";
        }

        return new ProcessStartInfo
        {
            FileName = _pythonCommand!,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static string? ResolvePythonScriptPath()
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var envPath = Environment.GetEnvironmentVariable("PIGEON_CAMERA_SCRIPT");
        AddCandidate(candidates, seen, envPath);

        AddCandidate(candidates, seen, Path.Combine(AppContext.BaseDirectory, "camera_service.py"));
        AddCandidate(candidates, seen, Path.Combine(Directory.GetCurrentDirectory(), "camera_service.py"));

        var probeRoots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct();

        foreach (var root in probeRoots)
        {
            var current = root;
            for (var depth = 0; depth < 8; depth++)
            {
                AddCandidate(candidates, seen, Path.Combine(current, "camera_service.py"));
                AddCandidate(candidates, seen, Path.Combine(current, "HarvestmoonGCS", "camera_service.py"));
                AddCandidate(candidates, seen, Path.Combine(current, "Pigeon_Harvest", "HarvestmoonGCS", "camera_service.py"));
                AddCandidate(candidates, seen, Path.Combine(current, "Pigeon_Harvest", "camera_service.py"));

                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void AddCandidate(List<string> candidates, HashSet<string> seen, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
            {
                candidates.Add(fullPath);
            }
        }
        catch
        {
            // Ignore invalid path candidates
        }
    }

    private static (string? Command, string LauncherArgs) ResolvePythonCommand()
    {
        var probes = new List<(string Command, string ProbeArgs, string LauncherArgs)>();

        // Environment override takes priority so users can point to their own venv.
        var envOverride = Environment.GetEnvironmentVariable("PIGEON_CAMERA_PYTHON");
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            probes.Add((envOverride!, "--version", string.Empty));
        }

        var probedDirs = new List<string>
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            // Walk up from the executable until we find a .venv-camera directory. This covers
            // the common case where the app is run from the repo root via `dotnet run` while
            // the venv lives a few directories deeper.
        };

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                probedDirs.Add(dir.FullName);
            }
        }
        catch
        {
        }

        try
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
            {
                probedDirs.Add(dir.FullName);
            }
        }
        catch
        {
        }

        foreach (var root in probedDirs.Distinct())
        {
            probes.Add((Path.Combine(root, ".venv-camera", "bin", "python"), "--version", string.Empty));
            probes.Add((Path.Combine(root, ".venv-camera", "bin", "python3"), "--version", string.Empty));
            probes.Add((Path.Combine(root, "Pigeon_Harvest", ".venv-camera", "bin", "python"), "--version", string.Empty));
            probes.Add((Path.Combine(root, "Pigeon_Harvest", ".venv-camera", "bin", "python3"), "--version", string.Empty));
        }

        probes.Add(("python3", "--version", string.Empty));
        probes.Add(("python", "--version", string.Empty));

        if (OperatingSystem.IsWindows())
        {
            probes.Insert(0, ("py", "-3 --version", "-3"));
        }

        foreach (var probe in probes)
        {
            // Prefer pythons that also have cv2 available; fall back to any working python.
            if (CanExecute(probe.Command, probe.ProbeArgs) && HasCv2Module(probe.Command))
            {
                Serilog.Log.Information("[PythonCameraService] Selected python with cv2: {Cmd}", probe.Command);
                return (probe.Command, probe.LauncherArgs);
            }
        }

        foreach (var probe in probes)
        {
            if (CanExecute(probe.Command, probe.ProbeArgs))
            {
                Serilog.Log.Warning("[PythonCameraService] Falling back to python without verified cv2: {Cmd}", probe.Command);
                return (probe.Command, probe.LauncherArgs);
            }
        }

        return (null, string.Empty);
    }

    private static bool HasCv2Module(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "-c \"import cv2\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanExecute(string command, string args)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            if (!process.WaitForExit(2000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StartRecordingAsync(string? filename = null)
    {
        // TODO: Implement video recording
        Serilog.Log.Warning("[PythonCameraService] Video recording not yet implemented");
        return await Task.FromResult(false);
    }

    public async Task<bool> StopRecordingAsync()
    {
        // TODO: Implement video recording
        return await Task.FromResult(false);
    }

    public async Task SendCameraControlAsync(CameraControlCommand command, float value)
    {
        Serilog.Log.Information($"[PythonCameraService] Camera control: {command} = {value}");
        // TODO: Implement camera controls if needed
        await Task.CompletedTask;
    }
}
