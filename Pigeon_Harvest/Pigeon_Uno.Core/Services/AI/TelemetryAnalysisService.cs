using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI
{
    /// <summary>
    /// Lightweight interface to provide telemetry data snapshots from the buffer layer.
    /// </summary>
    public interface ITelemetryBufferProvider
    {
        /// <summary>
        /// Returns recent telemetry snapshots within the last N seconds.
        /// </summary>
        /// <param name="lastSeconds">Number of seconds to look back</param>
        /// <returns>Collection of telemetry snapshots</returns>
        IEnumerable<TelemetrySnapshot> GetSnapshots(int lastSeconds);
    }

    /// <summary>
    /// Telemetry analysis service that periodically analyzes flight telemetry using an LLM.
    /// Aggregates buffered snapshots and sends them to the configured LLM for analysis,
    /// firing the AnalysisCompleted event when results are available.
    /// </summary>
    public class TelemetryAnalysisService
    {
        private readonly AISettings _settings;
        private readonly ITelemetryBufferProvider _buffer;
        private readonly Func<ILLMService> _llmProviderFactory;
        private Timer? _timer;
        private int _isAnalyzing = 0; // 0 = not running, 1 = analyzing
        private readonly object _timerLock = new();

        /// <summary>
        /// Creates a new TelemetryAnalysisService.
        /// </summary>
        /// <param name="settings">AI settings configuration</param>
        /// <param name="buffer">Telemetry buffer provider for retrieving snapshots</param>
        /// <param name="llmProviderFactory">Factory function to create LLM service instances</param>
        public TelemetryAnalysisService(AISettings settings, ITelemetryBufferProvider buffer, Func<ILLMService> llmProviderFactory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        }

        /// <summary>
        /// Fired when telemetry analysis completes with the analysis results.
        /// </summary>
        public event EventHandler<TelemetryAnalysis>? AnalysisCompleted;

        /// <summary>
        /// Starts periodic telemetry analysis at the configured interval.
        /// </summary>
        public void Start()
        {
            lock (_timerLock)
            {
                if (_timer != null) return;
                var intervalSec = Math.Max(1, _settings.Analysis.IntervalSeconds);
                var intervalMs = intervalSec * 1000;
                _timer = new Timer(TimerElapsed, null, 0, intervalMs);
            }
        }

        /// <summary>
        /// Stops periodic telemetry analysis and disposes the timer.
        /// </summary>
        public void Stop()
        {
            lock (_timerLock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void TimerElapsed(object? state)
        {
            // Fire-and-forget to avoid blocking the timer thread
            _ = AnalyzeAsync();
        }

        private async Task AnalyzeAsync()
        {
            // Ensure only one analysis runs at a time
            if (Interlocked.Exchange(ref _isAnalyzing, 1) == 1)
                return;

            try
            {
                var bufferSeconds = Math.Max(0, _settings.Analysis.BufferSeconds);
                var snapshots = _buffer.GetSnapshots(bufferSeconds).ToList();

                if (snapshots.Count == 0)
                    return;

                // Aggregate snapshots to a summary string (via TelemetryAggregator)
                var summary = TelemetryAggregator.Summarize(snapshots);
                var prompt = BuildPrompt(summary, snapshots);

                var llm = _llmProviderFactory?.Invoke();
                if (llm == null)
                {
                    return; // Graceful: no provider available
                }

                // Request structured TelemetryAnalysis from LLM
                var analysis = await llm.GenerateStructuredAsync<TelemetryAnalysis>(prompt, LLMRole.TelemetryAnalysis);
                if (analysis != null)
                {
                    AnalysisCompleted?.Invoke(this, analysis);
                }
            }
            catch
            {
                // Do not crash the host; swallow errors gracefully
            }
            finally
            {
                Interlocked.Exchange(ref _isAnalyzing, 0);
            }
        }

        private string BuildPrompt(TelemetrySummary summary, List<TelemetrySnapshot> snapshots)
        {
            return $@"
Anda adalah AI analis telemetri UAV untuk Ground Control Station.
Lakukan analisis ringkas dan presisi untuk keselamatan penerbangan.

Ringkasan telemetri ({snapshots.Count} samples):
- WindowStart: {summary.WindowStart:O}
- WindowEnd: {summary.WindowEnd:O}
- Battery: min={summary.BatteryMin:F2}, max={summary.BatteryMax:F2}, avg={summary.BatteryAvg:F2}, std={summary.BatteryStdDev:F2}, drainPeak={summary.BatteryDrainRate:F3}%/min, drainAvg={summary.BatteryDrainRateAvg:F3}%/min
- BatteryElectrical: voltageAvg={summary.BatteryVoltageAvg:F2}V, currentAvg={summary.BatteryCurrentAvg:F2}A, tempAvg={summary.BatteryTempAvg:F2}C
- Altitude: min={summary.AltitudeMin:F2}, max={summary.AltitudeMax:F2}, avg={summary.AltitudeAvg:F2}, std={summary.AltitudeStdDev:F2}, trend={summary.AltitudeTrend}
- VerticalProfile: vSpeedAvg={summary.VerticalSpeedAvg:F2}, climbAvg={summary.ClimbRateAvg:F2}, descentAvg={summary.DescentRateAvg:F2}
- Speed: min={summary.SpeedMin:F2}, max={summary.SpeedMax:F2}, avg={summary.SpeedAvg:F2}, std={summary.SpeedStdDev:F2}, groundAvg={summary.GroundSpeedAvg:F2}, airAvg={summary.AirSpeedAvg:F2}
- Attitude: rollAvg={summary.RollAvg:F2}, pitchAvg={summary.PitchAvg:F2}, yawAvg={summary.YawAvg:F2}, headingErrAvg={summary.HeadingErrorAvg:F2}
- Vibration: magAvg={summary.VibrationMagnitudeAvg:F2}, magMax={summary.VibrationMagnitudeMax:F2}
- Wind: avg={summary.WindSpeedAvg:F2}, gustMax={summary.WindGustMax:F2}
- GPS: hdopAvg={summary.GpsHdopAvg:F2}, vdopAvg={summary.GpsVdopAvg:F2}, dropout={summary.DropoutCount}, gpsFixLost={summary.GpsFixLostCount}
- Link: qualityAvg={summary.LinkQualityAvg:F2}, packetLossAvg={summary.PacketLossAvg:F2}, degradedCount={summary.LinkDegradedCount}
- Mission: modeChanges={summary.ModeChanges}, waypointChanges={summary.MissionWaypointChanges}
- Distance: fromHomeMax={summary.DistanceFromHomeMax:F1}m, travelled={summary.DistanceTravelledMeters:F1}m
- EnvironmentEvents: highVibration={summary.HighVibrationCount}, highWind={summary.HighWindCount}
- StabilityScore(0-100): {summary.StabilityScore:F1}

Kembalikan HANYA JSON valid dalam bentuk object dengan field:
overallStatus (GOOD|WARNING|CRITICAL),
keyInsights (string),
warnings (string[]),
recommendations (string[]),
predictedIssues (string[]),
confidence (0.0-1.0),
timestamp (ISO 8601 UTC).
";
        }
    }
}
