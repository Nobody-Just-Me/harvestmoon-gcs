using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Core.Models.AI;
using HarvestmoonGCS.Core.Services.AI;

namespace HarvestmoonGCS.Core.Services.AI
{
    /// <summary>
    /// AI-powered anomaly detector that delegates analysis to a configured LLM service.
    /// </summary>
    public class AIAnomalyDetector : IAnomalyDetector
    {
        private readonly ILLMService _llmService;
        private readonly AISettings _settings;

        public string Name => "AI";

        public AIAnomalyDetector(ILLMService llmService, AISettings settings)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Analyzes the provided telemetry snapshot using an LLM and returns detected anomalies.
        /// If the LLM fails or returns null, returns an empty list (failover).
        /// </summary>
        /// <param name="snapshot">Telemetry snapshot to analyze</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of anomalies detected by the LLM, or empty list on failure</returns>
        public async Task<List<Anomaly>> DetectAsync(TelemetrySnapshot snapshot, CancellationToken ct = default)
        {
            if (snapshot == null) return new List<Anomaly>();

            try
            {
                string prompt = BuildPrompt(snapshot);
                var result = await _llmService.GenerateStructuredAsync<List<Anomaly>>(prompt, LLMRole.AnomalyDetection, ct);
                return result ?? new List<Anomaly>();
            }
            catch
            {
                // Fallback: do not crash the system on LLM failures
                return new List<Anomaly>();
            }
        }

        private string BuildPrompt(TelemetrySnapshot snapshot)
        {
            var payload = new
            {
                Snapshot = snapshot,
                Timestamp = DateTime.UtcNow
            };
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
            return $"Analyze the following telemetry snapshot for anomalies. Payload: {json}";
        }

        public IList<Anomaly> Evaluate(TelemetrySnapshot snapshot)
        {
            return DetectAsync(snapshot).GetAwaiter().GetResult();
        }

        public async Task<IList<Anomaly>> EvaluateBatchAsync(IList<TelemetrySnapshot> snapshots)
        {
            var allAnomalies = new List<Anomaly>();
            foreach (var snapshot in snapshots)
            {
                var anomalies = await DetectAsync(snapshot);
                allAnomalies.AddRange(anomalies);
            }
            return allAnomalies;
        }
    }
}
