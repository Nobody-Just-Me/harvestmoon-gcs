using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Models;
using Serilog;

namespace Pigeon_Uno.Services;

/// <summary>
/// Implementation of diagnostics service
/// </summary>
public class DiagnosticsService : IDiagnosticsService
{
    private readonly List<ErrorLogEntry> _errorLog = new();
    private readonly object _errorLogLock = new();
    private readonly DateTime _startTime = DateTime.Now;
    private readonly string _logsFolder;
    private DateTime _lastCpuCheck = DateTime.Now;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private double _cpuUsagePercent = 0.0;

    public DiagnosticsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logsFolder = Path.Combine(appDataPath, "Pigeon_Uno", "logs");
        Directory.CreateDirectory(_logsFolder);
        
        // Initialize CPU tracking
        var process = Process.GetCurrentProcess();
        _lastTotalProcessorTime = process.TotalProcessorTime;
    }

    public SystemHealthStatus GetSystemHealth()
    {
        var recentErrors = GetRecentErrors(10);
        var criticalErrors = recentErrors.Count(e => e.Level == "Critical");
        var errors = recentErrors.Count(e => e.Level == "Error");

        HealthLevel health;
        if (criticalErrors > 0)
            health = HealthLevel.Critical;
        else if (errors > 5)
            health = HealthLevel.Error;
        else if (errors > 0)
            health = HealthLevel.Warning;
        else
            health = HealthLevel.Good;

        return new SystemHealthStatus
        {
            OverallHealth = health,
            LastCheck = DateTime.Now,
            Message = $"{errors} errors, {criticalErrors} critical"
        };
    }

    public DiagnosticInfo GetDiagnosticInfo()
    {
        var process = Process.GetCurrentProcess();
        UpdateCpuUsage(process);
        
        return new DiagnosticInfo
        {
            AppVersion = GetAppVersion(),
            Platform = GetPlatform(),
            OsVersion = Environment.OSVersion.ToString(),
            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
            CpuUsagePercent = _cpuUsagePercent,
            ThreadCount = process.Threads.Count,
            Uptime = DateTime.Now - _startTime
        };
    }

    private void UpdateCpuUsage(Process process)
    {
        try
        {
            var currentTime = DateTime.Now;
            var currentTotalProcessorTime = process.TotalProcessorTime;
            
            var timeDiff = (currentTime - _lastCpuCheck).TotalMilliseconds;
            if (timeDiff > 500) // Update every 500ms minimum
            {
                var cpuTimeDiff = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                var cpuUsage = (cpuTimeDiff / (Environment.ProcessorCount * timeDiff)) * 100.0;
                
                _cpuUsagePercent = Math.Min(100.0, Math.Max(0.0, cpuUsage));
                _lastCpuCheck = currentTime;
                _lastTotalProcessorTime = currentTotalProcessorTime;
            }
        }
        catch
        {
            // If CPU tracking fails, keep last known value
        }
    }

    public PerformanceMetrics GetPerformanceMetrics()
    {
        return new PerformanceMetrics
        {
            AverageFps = 60.0,
            ActiveConnections = 0,
            TelemetryMessagesPerSecond = 0,
            NetworkBytesReceived = 0,
            NetworkBytesSent = 0
        };
    }

    public List<ErrorLogEntry> GetRecentErrors(int count)
    {
        lock (_errorLogLock)
        {
            return _errorLog.TakeLast(count).ToList();
        }
    }

    public async Task<string> ExportLogsAsync()
    {
        try
        {
            var exportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"Pigeon_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            );

            var sb = new StringBuilder();
            sb.AppendLine("=== Pigeon GCS Diagnostic Report ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var diagnosticInfo = GetDiagnosticInfo();
            sb.AppendLine("=== System Information ===");
            sb.AppendLine($"App Version: {diagnosticInfo.AppVersion}");
            sb.AppendLine($"Platform: {diagnosticInfo.Platform}");
            sb.AppendLine($"OS Version: {diagnosticInfo.OsVersion}");
            sb.AppendLine($"Memory Usage: {diagnosticInfo.MemoryUsageMB} MB");
            sb.AppendLine($"Thread Count: {diagnosticInfo.ThreadCount}");
            sb.AppendLine($"Uptime: {diagnosticInfo.Uptime}");
            sb.AppendLine();

            var health = GetSystemHealth();
            sb.AppendLine("=== System Health ===");
            sb.AppendLine($"Overall Health: {health.OverallHealth}");
            sb.AppendLine($"Last Check: {health.LastCheck:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var recentErrors = GetRecentErrors(50);
            if (recentErrors.Any())
            {
                sb.AppendLine("=== Recent Errors ===");
                foreach (var error in recentErrors)
                {
                    sb.AppendLine($"[{error.Timestamp:yyyy-MM-dd HH:mm:ss}] [{error.Level}] {error.Source ?? "Unknown"}");
                    sb.AppendLine($"  Message: {error.Message}");
                    if (!string.IsNullOrEmpty(error.StackTrace))
                    {
                        sb.AppendLine($"  Stack Trace: {error.StackTrace}");
                    }
                    sb.AppendLine();
                }
            }

            await File.WriteAllTextAsync(exportPath, sb.ToString());
            
            Log.Information("Diagnostic report exported to: {ExportPath}", exportPath);
            return exportPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export diagnostic logs");
            throw;
        }
    }

    public async Task ClearOldLogsAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var logFiles = Directory.GetFiles(_logsFolder, "*.log");

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(logFile);
                    Log.Information("Deleted old log file: {LogFile}", logFile);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear old logs");
            throw;
        }
    }

    private string GetAppVersion()
    {
        return typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private string GetPlatform()
    {
#if __ANDROID__
        return "Android";
#elif __IOS__
        return "iOS";
#elif __WASM__
        return "WebAssembly";
#else
        return "Desktop";
#endif
    }
}
