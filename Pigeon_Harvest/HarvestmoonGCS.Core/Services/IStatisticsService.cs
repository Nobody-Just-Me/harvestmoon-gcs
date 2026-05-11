using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for managing flight statistics
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Get current statistics
    /// </summary>
    FlightStatistics GetCurrentStatistics();

    /// <summary>
    /// Get statistics for a specific time period
    /// </summary>
    FlightStatistics GetStatistics(TimePeriod period);

    /// <summary>
    /// Update statistics with new telemetry data
    /// </summary>
    void UpdateStatistics(FlightData data);

    /// <summary>
    /// Reset current statistics
    /// </summary>
    void ResetStatistics();

    /// <summary>
    /// Save statistics to persistent storage
    /// </summary>
    Task SaveStatisticsAsync();

    /// <summary>
    /// Load statistics from persistent storage
    /// </summary>
    Task LoadStatisticsAsync();

    /// <summary>
    /// Get all saved statistics
    /// </summary>
    Task<List<FlightStatistics>> GetAllStatisticsAsync();

    /// <summary>
    /// Event raised when statistics are updated
    /// </summary>
    event EventHandler<FlightStatistics> StatisticsUpdated;
}
