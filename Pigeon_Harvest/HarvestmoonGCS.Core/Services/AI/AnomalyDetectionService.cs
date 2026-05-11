using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services;
using Timer = System.Timers.Timer;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Coordinates multi-layer anomaly detection:
/// Layer 1: RuleBasedDetector (real-time, every snapshot)
/// Layer 2: StatisticalDetector (z-score, 30-second interval)
/// Layer 3: AIAnomalyDetector (LLM-powered, 30-second interval)
/// </summary>
public class AnomalyDetectionService : IAnomalyDetectionService, IDisposable
{
    private readonly IAlertManager _alertManager;
    private readonly IAnomalyDetector _ruleBasedDetector;
    private readonly IAnomalyDetector? _statisticalDetector;
    private readonly IAnomalyDetector? _aiDetector;
    private readonly AnomalyDetectionConfig _config;

    private Timer? _layer2Timer;
    private Timer? _layer3Timer;
    private readonly ConcurrentQueue<TelemetrySnapshot> _snapshotBuffer;
    private readonly ConcurrentDictionary<AnomalyType, DateTime> _lastAlertTime;
    private readonly List<Anomaly> _lastDetectedAnomalies;
    private readonly ReaderWriterLockSlim _anomaliesLock;

    private const int DefaultBufferSize = 1000;
    private const int Layer2IntervalMs = 30000;
    private const int Layer3IntervalMs = 30000;
    private readonly TimeSpan _criticalDedupWindow = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _warningDedupWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets whether the anomaly detection service is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the list of anomalies detected in the last analysis cycle.
    /// </summary>
    public IReadOnlyList<Anomaly> LastDetectedAnomalies
    {
        get
        {
            _anomaliesLock.EnterReadLock();
            try
            {
                return _lastDetectedAnomalies.ToList().AsReadOnly();
            }
            finally
            {
                _anomaliesLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Fired when anomalies are detected by any layer.
    /// </summary>
    public event EventHandler<AnomaliesDetectedEventArgs>? AnomaliesDetected;

    /// <summary>
    /// Creates a new AnomalyDetectionService coordinating multiple detection layers.
    /// </summary>
    /// <param name="alertManager">Alert manager for sending notifications</param>
    /// <param name="ruleBasedDetector">Layer 1: Real-time rule-based detector</param>
    /// <param name="statisticalDetector">Layer 2: Statistical z-score detector (optional)</param>
    /// <param name="aiDetector">Layer 3: AI/LLM-powered detector (optional)</param>
    /// <param name="config">Detection configuration settings (optional)</param>
    public AnomalyDetectionService(
        IAlertManager alertManager,
        IAnomalyDetector ruleBasedDetector,
        IAnomalyDetector? statisticalDetector = null,
        IAnomalyDetector? aiDetector = null,
        AnomalyDetectionConfig? config = null)
    {
        _alertManager = alertManager ?? throw new ArgumentNullException(nameof(alertManager));
        _ruleBasedDetector = ruleBasedDetector ?? throw new ArgumentNullException(nameof(ruleBasedDetector));
        _statisticalDetector = statisticalDetector;
        _aiDetector = aiDetector;
        _config = config ?? new AnomalyDetectionConfig();

        _snapshotBuffer = new ConcurrentQueue<TelemetrySnapshot>();
        _lastAlertTime = new ConcurrentDictionary<AnomalyType, DateTime>();
        _lastDetectedAnomalies = new List<Anomaly>();
        _anomaliesLock = new ReaderWriterLockSlim();
    }

    public Task StartAsync()
    {
        if (IsRunning)
            return Task.CompletedTask;

        IsRunning = true;

        if (_config.StatisticalEnabled && _statisticalDetector != null)
        {
            _layer2Timer = new Timer(Layer2IntervalMs);
            _layer2Timer.Elapsed += async (_, _) => await RunLayer2DetectionAsync();
            _layer2Timer.AutoReset = true;
            _layer2Timer.Start();
        }

        if (_config.AIEnabled && _aiDetector != null)
        {
            _layer3Timer = new Timer(Layer3IntervalMs);
            _layer3Timer.Elapsed += async (_, _) => await RunLayer3DetectionAsync();
            _layer3Timer.AutoReset = true;
            _layer3Timer.Start();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        IsRunning = false;

        _layer2Timer?.Stop();
        _layer2Timer?.Dispose();
        _layer2Timer = null;

        _layer3Timer?.Stop();
        _layer3Timer?.Dispose();
        _layer3Timer = null;

        _snapshotBuffer.Clear();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a single telemetry snapshot through Layer 1 (rule-based) detection.
    /// Also buffers the snapshot for Layer 2/3 batch analysis.
    /// </summary>
    /// <param name="snapshot">Telemetry snapshot to process</param>
    public async Task ProcessSnapshotAsync(TelemetrySnapshot snapshot)
    {
        if (!IsRunning || snapshot == null)
            return;

        _snapshotBuffer.Enqueue(snapshot);
        while (_snapshotBuffer.Count > DefaultBufferSize)
        {
            _snapshotBuffer.TryDequeue(out _);
        }

        if (_config.RuleBasedEnabled)
        {
            var anomalies = _ruleBasedDetector.Evaluate(snapshot);
            await ProcessDetectedAnomaliesAsync(anomalies, "Layer1-RuleBased");
        }
    }

    private async Task RunLayer2DetectionAsync()
    {
        if (_statisticalDetector == null || !IsRunning)
            return;

        var snapshots = _snapshotBuffer.ToList();
        if (snapshots.Count < 2)
            return;

        try
        {
            var anomalies = await _statisticalDetector.EvaluateBatchAsync(snapshots);
            await ProcessDetectedAnomaliesAsync(anomalies, "Layer2-Statistical");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnomalyDetectionService] Layer 2 error: {ex.Message}");
        }
    }

    private async Task RunLayer3DetectionAsync()
    {
        if (_aiDetector == null || !IsRunning)
            return;

        var snapshots = _snapshotBuffer.ToList();
        if (snapshots.Count < 2)
            return;

        try
        {
            var anomalies = await _aiDetector.EvaluateBatchAsync(snapshots);
            await ProcessDetectedAnomaliesAsync(anomalies, "Layer3-AI");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnomalyDetectionService] Layer 3 error: {ex.Message}");
        }
    }

    private async Task ProcessDetectedAnomaliesAsync(IList<Anomaly> anomalies, string sourceLayer)
    {
        if (anomalies == null || anomalies.Count == 0)
            return;

        var filteredAnomalies = new List<Anomaly>();

        foreach (var anomaly in anomalies)
        {
            if (ShouldEmitAnomaly(anomaly))
            {
                filteredAnomalies.Add(anomaly);
                _lastAlertTime[anomaly.Type] = DateTime.UtcNow;
            }
        }

        if (filteredAnomalies.Count == 0)
            return;

        _anomaliesLock.EnterWriteLock();
        try
        {
            _lastDetectedAnomalies.Clear();
            _lastDetectedAnomalies.AddRange(filteredAnomalies);
        }
        finally
        {
            _anomaliesLock.ExitWriteLock();
        }

        foreach (var anomaly in filteredAnomalies)
        {
            var priority = MapSeverityToPriority(anomaly.Severity);
            await _alertManager.QueueCustomAlertAsync(anomaly.Message, priority);
        }

        AnomaliesDetected?.Invoke(this, new AnomaliesDetectedEventArgs(filteredAnomalies, sourceLayer));
    }

    private bool ShouldEmitAnomaly(Anomaly anomaly)
    {
        var now = DateTime.UtcNow;

        if (anomaly.Severity == AnomalySeverity.Critical)
        {
            if (_lastAlertTime.TryGetValue(anomaly.Type, out var lastTime))
            {
                return (now - lastTime) >= _criticalDedupWindow;
            }
            return true;
        }

        if (_lastAlertTime.TryGetValue(anomaly.Type, out var lastWarningTime))
        {
            return (now - lastWarningTime) >= _warningDedupWindow;
        }

        return true;
    }

    private static AlertPriority MapSeverityToPriority(AnomalySeverity severity)
    {
        return severity switch
        {
            AnomalySeverity.Critical => AlertPriority.Critical,
            AnomalySeverity.Warning => AlertPriority.High,
            AnomalySeverity.Info => AlertPriority.Normal,
            _ => AlertPriority.Normal
        };
    }

    /// <summary>
    /// Disposes the service and stops all timers.
    /// </summary>
    public void Dispose()
    {
        StopAsync().Wait();
        _anomaliesLock?.Dispose();
    }
}
