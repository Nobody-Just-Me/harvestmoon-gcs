using System;

namespace HarvestmoonGCS.Core.Diagnostics
{
    /// <summary>
    /// Configuration for diagnostic features.
    /// Controls logging, tracing, performance monitoring, and debugging options.
    /// </summary>
    public class DiagnosticConfig
    {
        public bool EnableLogging { get; set; } = true;
        public bool EnableEventTracing { get; set; } = true;
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool BypassThrottling { get; set; } = false;
        public bool UseFallbackParser { get; set; } = false;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;
        public int MaxLogEntries { get; set; } = 10000;

        /// <summary>
        /// Gets the default diagnostic configuration.
        /// All features enabled for debugging.
        /// </summary>
        public static DiagnosticConfig Default => new DiagnosticConfig
        {
            EnableLogging = true,
            EnableEventTracing = true,
            EnablePerformanceMonitoring = true,
            BypassThrottling = false,
            UseFallbackParser = false,
            MinimumLogLevel = LogLevel.Debug,
            MaxLogEntries = 10000
        };

        /// <summary>
        /// Gets a production configuration.
        /// Minimal logging for performance.
        /// </summary>
        public static DiagnosticConfig Production => new DiagnosticConfig
        {
            EnableLogging = false,
            EnableEventTracing = false,
            EnablePerformanceMonitoring = false,
            BypassThrottling = false,
            UseFallbackParser = false,
            MinimumLogLevel = LogLevel.Warning,
            MaxLogEntries = 1000
        };
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
