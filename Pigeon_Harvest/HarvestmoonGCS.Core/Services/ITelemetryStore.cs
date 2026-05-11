using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Services;

public interface ITelemetryStore
{
    Task SaveTelemetryAsync(TelemetryData data);
    Task<List<TelemetryData>> GetTelemetryRangeAsync(DateTime start, DateTime end);
    Task<TelemetryData?> GetLatestTelemetryAsync();
    Task ClearOldDataAsync(DateTime before);
    Task ExportToCsvAsync(string filePath, DateTime start, DateTime end);
}
