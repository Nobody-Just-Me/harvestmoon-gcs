using System;
using System.Collections.Generic;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Evaluates anomaly detection outcomes into research metrics (precision/recall/F1).
/// </summary>
public interface IAnomalyEvaluationService
{
    event EventHandler<AnomalyEvaluationMetrics>? MetricsUpdated;

    void ObserveSnapshot(TelemetrySnapshot snapshot, IReadOnlyList<Anomaly>? predictedAnomalies);

    AnomalyEvaluationMetrics GetMetrics();

    void Reset();
}

