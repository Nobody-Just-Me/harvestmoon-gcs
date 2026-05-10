using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.Services.Optimization;

namespace Pigeon_Uno.Services
{
    /// <summary>
    /// Optimized telemetry handler with throttling and batching for emulator/low-end devices
    /// </summary>
    public class OptimizedTelemetryHandler : IOptimizedTelemetryHandler
    {
        private readonly object _statsLock = new();
        private readonly object _messageRateLock = new();
        private TelemetryProcessingStats _stats;
        private readonly List<double> _processingTimes = new();
        private readonly Queue<byte[]> _telemetryQueue = new();
        private readonly Dictionary<int, DateTime> _lastMessageProcessedUtc = new();
        private readonly ILoggingService _logger;
        
        public TelemetryProcessingMode ProcessingMode { get; set; } = TelemetryProcessingMode.Full;
        public int PollingRateMs { get; set; } = 100;
        public int BatchSize { get; set; } = 10;
        
        public TelemetryProcessingStats ProcessingStats
        {
            get
            {
                lock (_statsLock)
                {
                    return new TelemetryProcessingStats
                    {
                        TotalPacketsProcessed = _stats.TotalPacketsProcessed,
                        TotalPacketsFiltered = _stats.TotalPacketsFiltered,
                        AverageProcessingTimeMs = _processingTimes.Count > 0 ? _processingTimes.Average() : 0,
                        LastProcessingTime = _stats.LastProcessingTime,
                        CurrentBatchSize = BatchSize,
                        CurrentPollingRateMs = PollingRateMs
                    };
                }
            }
        }
        
        public event EventHandler<TelemetryProcessingStats>? StatisticsUpdated;
        
        public OptimizedTelemetryHandler(ILoggingService logger)
        {
            _logger = logger;
            _stats = new TelemetryProcessingStats();
        }
        
        /// <summary>
        /// Processes telemetry data with optimizations
        /// </summary>
        public async Task<ProcessedTelemetryData> ProcessTelemetryAsync(byte[] telemetryData)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var messageId = ExtractMessageId(telemetryData);
                if (!ShouldProcessMessage(messageId, DateTime.UtcNow))
                {
                    return new ProcessedTelemetryData
                    {
                        Data = Array.Empty<byte>(),
                        ProcessedAt = DateTime.UtcNow,
                        Priority = TelemetryPriority.Low,
                        OriginalSize = telemetryData.Length,
                        ProcessedSize = 0
                    };
                }
                
                // Determine priority based on MAVLink message id.
                var priority = DetermineTelemetryPriority(messageId);
                
                // Process based on mode
                var processed = ProcessingMode switch
                {
                    TelemetryProcessingMode.CriticalOnly => ProcessCriticalOnly(telemetryData, priority),
                    TelemetryProcessingMode.Optimized => await ProcessOptimizedAsync(telemetryData, priority),
                    _ => await ProcessFullAsync(telemetryData, priority)
                };
                
                // Update stats
                stopwatch.Stop();
                UpdateProcessingStats(stopwatch.ElapsedMilliseconds, telemetryData.Length, processed.ProcessedSize);
                
                return processed;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing telemetry: {ex.Message}", nameof(OptimizedTelemetryHandler));
                throw;
            }
        }
        
        /// <summary>
        /// Process telemetry async (interface implementation)
        /// </summary>
        Task IOptimizedTelemetryHandler.ProcessTelemetryAsync(byte[] data)
        {
            return ProcessTelemetryAsync(data);
        }

        public bool ShouldProcessMessage(int messageId, DateTime utcNow)
        {
            var priority = DetermineTelemetryPriority(messageId);

            if (!IsPriorityEnabled(priority))
            {
                UpdateFilteredStats();
                return false;
            }

            var minIntervalMs = GetMinMessageIntervalMs(priority);
            if (minIntervalMs <= 0)
            {
                return true;
            }

            lock (_messageRateLock)
            {
                if (_lastMessageProcessedUtc.TryGetValue(messageId, out var previousAt))
                {
                    var elapsedMs = (utcNow - previousAt).TotalMilliseconds;
                    if (elapsedMs < minIntervalMs)
                    {
                        UpdateFilteredStats();
                        return false;
                    }
                }

                _lastMessageProcessedUtc[messageId] = utcNow;

                // Keep map bounded and avoid unbounded growth on long sessions.
                if (_lastMessageProcessedUtc.Count > 512)
                {
                    _lastMessageProcessedUtc.Clear();
                }
            }

            lock (_statsLock)
            {
                _stats.TotalPacketsProcessed++;
                _stats.LastProcessingTime = 0;
            }

            return true;
        }

        public int GetRecommendedDispatchIntervalMs()
        {
            return Math.Clamp(PollingRateMs, 20, 500);
        }
        
        /// <summary>
        /// Set update rate
        /// </summary>
        public void SetUpdateRate(int updatesPerSecond)
        {
            if (updatesPerSecond > 0)
            {
                PollingRateMs = Math.Clamp(1000 / updatesPerSecond, 20, 1000);
            }
        }
        
        /// <summary>
        /// Enable batching
        /// </summary>
        public void EnableBatching(bool enable)
        {
            if (enable)
            {
                BatchSize = 20;
            }
            else
            {
                BatchSize = 1;
            }
        }
        
        /// <summary>
        /// Get stats (interface implementation)
        /// </summary>
        TelemetryProcessingStats IOptimizedTelemetryHandler.GetStats()
        {
            lock (_statsLock)
            {
                return new TelemetryProcessingStats
                {
                    PacketsProcessed = (int)_stats.TotalPacketsProcessed,
                    AverageProcessingTime = _processingTimes.Count > 0 ? _processingTimes.Average() : 0,
                    QueuedPackets = _telemetryQueue.Count,
                    TotalPacketsProcessed = _stats.TotalPacketsProcessed,
                    TotalPacketsFiltered = _stats.TotalPacketsFiltered,
                    AverageProcessingTimeMs = _processingTimes.Count > 0 ? _processingTimes.Average() : 0,
                    LastProcessingTime = _stats.LastProcessingTime,
                    CurrentBatchSize = BatchSize,
                    CurrentPollingRateMs = PollingRateMs
                };
            }
        }
        
        /// <summary>
        /// Sets processing mode based on device capabilities
        /// </summary>
        public void SetProcessingMode(TelemetryProcessingMode mode)
        {
            ProcessingMode = mode;
            
            // Adjust settings based on mode
            switch (mode)
            {
                case TelemetryProcessingMode.CriticalOnly:
                    PollingRateMs = 450;
                    BatchSize = 50;
                    break;
                case TelemetryProcessingMode.Minimal:
                    PollingRateMs = 380;
                    BatchSize = 40;
                    break;
                case TelemetryProcessingMode.EmulatorOptimized:
                    PollingRateMs = 240;
                    BatchSize = 30;
                    break;
                case TelemetryProcessingMode.BatterySaver:
                    PollingRateMs = 520;
                    BatchSize = 64;
                    break;
                case TelemetryProcessingMode.Optimized:
                    PollingRateMs = 160;
                    BatchSize = 20;
                    break;
                case TelemetryProcessingMode.Full:
                    PollingRateMs = 80;
                    BatchSize = 10;
                    break;
            }
            
            _logger.LogInfo($"Telemetry processing mode set to: {mode}", nameof(OptimizedTelemetryHandler));
        }
        
        private static int ExtractMessageId(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return -1;
            }

            // MAVLink v2 packet format: STX(0xFD), LEN, INC, COMPAT, SEQ, SYSID, COMPID, MSGID(3 bytes)
            if (data.Length >= 10 && data[0] == 0xFD)
            {
                return data[7] | (data[8] << 8) | (data[9] << 16);
            }

            // MAVLink v1 packet format: STX(0xFE), LEN, SEQ, SYSID, COMPID, MSGID
            if (data.Length >= 6 && data[0] == 0xFE)
            {
                return data[5];
            }

            // Fallback for compact signatures.
            if (data.Length >= 6)
            {
                return data[3] | (data[4] << 8) | (data[5] << 16);
            }

            return -1;
        }

        private static TelemetryPriority DetermineTelemetryPriority(int msgId)
        {
            return msgId switch
            {
                0 or 1 or 2 => TelemetryPriority.Critical, // HEARTBEAT, SYS_STATUS
                22 or 24 or 29 or 30 or 33 => TelemetryPriority.High, // PARAM_VALUE, GPS/ATTITUDE/PRESSURE/POSITION
                39 or 40 or 44 or 47 or 65 or 73 or 74 or 77 or 125 or 147 or 191 => TelemetryPriority.High,
                253 => TelemetryPriority.High, // STATUSTEXT should still surface quickly
                _ => TelemetryPriority.Normal
            };
        }

        private bool IsPriorityEnabled(TelemetryPriority priority)
        {
            return ProcessingMode switch
            {
                TelemetryProcessingMode.CriticalOnly => priority == TelemetryPriority.Critical,
                TelemetryProcessingMode.Minimal => priority is TelemetryPriority.Critical or TelemetryPriority.High,
                TelemetryProcessingMode.BatterySaver => priority is TelemetryPriority.Critical or TelemetryPriority.High,
                TelemetryProcessingMode.EmulatorOptimized => priority != TelemetryPriority.Low,
                _ => true
            };
        }

        private int GetMinMessageIntervalMs(TelemetryPriority priority)
        {
            return ProcessingMode switch
            {
                TelemetryProcessingMode.Full => priority switch
                {
                    TelemetryPriority.Critical => 0,
                    TelemetryPriority.High => 10,
                    TelemetryPriority.Normal => 20,
                    _ => 35
                },
                TelemetryProcessingMode.Optimized => priority switch
                {
                    TelemetryPriority.Critical => 0,
                    TelemetryPriority.High => 20,
                    TelemetryPriority.Normal => Math.Max(30, PollingRateMs / 2),
                    _ => Math.Max(60, PollingRateMs)
                },
                TelemetryProcessingMode.EmulatorOptimized => priority switch
                {
                    TelemetryPriority.Critical => 15,
                    TelemetryPriority.High => Math.Max(35, PollingRateMs / 2),
                    TelemetryPriority.Normal => Math.Max(70, PollingRateMs),
                    _ => int.MaxValue
                },
                TelemetryProcessingMode.CriticalOnly => Math.Max(50, PollingRateMs),
                TelemetryProcessingMode.Minimal => priority switch
                {
                    TelemetryPriority.Critical => Math.Max(40, PollingRateMs / 2),
                    TelemetryPriority.High => Math.Max(80, PollingRateMs),
                    _ => int.MaxValue
                },
                TelemetryProcessingMode.BatterySaver => priority switch
                {
                    TelemetryPriority.Critical => Math.Max(60, PollingRateMs / 2),
                    TelemetryPriority.High => Math.Max(120, PollingRateMs),
                    _ => int.MaxValue
                },
                _ => Math.Max(40, PollingRateMs)
            };
        }
        
        private ProcessedTelemetryData ProcessCriticalOnly(byte[] data, TelemetryPriority priority)
        {
            // Minimal processing for critical-only mode
            return new ProcessedTelemetryData
            {
                Data = priority == TelemetryPriority.Critical ? data : Array.Empty<byte>(),
                ProcessedAt = DateTime.UtcNow,
                Priority = priority,
                OriginalSize = data.Length,
                ProcessedSize = priority == TelemetryPriority.Critical ? data.Length : 0
            };
        }
        
        private Task<ProcessedTelemetryData> ProcessOptimizedAsync(byte[] data, TelemetryPriority priority)
        {
            // Optimized processing keeps payload intact while skipping simulated delays.
            return Task.FromResult(new ProcessedTelemetryData
            {
                Data = data,
                ProcessedAt = DateTime.UtcNow,
                Priority = priority,
                OriginalSize = data.Length,
                ProcessedSize = data.Length
            });
        }
        
        private Task<ProcessedTelemetryData> ProcessFullAsync(byte[] data, TelemetryPriority priority)
        {
            // Full processing path without synthetic delay.
            return Task.FromResult(new ProcessedTelemetryData
            {
                Data = data,
                ProcessedAt = DateTime.UtcNow,
                Priority = priority,
                OriginalSize = data.Length,
                ProcessedSize = data.Length
            });
        }
        
        private void UpdateFilteredStats()
        {
            lock (_statsLock)
            {
                _stats.TotalPacketsFiltered++;
            }
        }
        
        private void UpdateProcessingStats(long elapsedMs, int originalSize, int processedSize)
        {
            lock (_statsLock)
            {
                _stats.TotalPacketsProcessed++;
                _stats.LastProcessingTime = elapsedMs;
                
                _processingTimes.Add(elapsedMs);
                if (_processingTimes.Count > 100)
                    _processingTimes.RemoveAt(0);
            }
            
            StatisticsUpdated?.Invoke(this, ProcessingStats);
        }
    }
    
    public class ProcessedTelemetryData
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime ProcessedAt { get; set; }
        public TelemetryPriority Priority { get; set; }
        public int OriginalSize { get; set; }
        public int ProcessedSize { get; set; }
    }
    
    public enum TelemetryPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}
