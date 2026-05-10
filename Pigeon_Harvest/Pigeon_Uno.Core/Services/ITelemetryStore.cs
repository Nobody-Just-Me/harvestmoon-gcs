using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;

namespace Pigeon_Uno.Services;

public interface ITelemetryStore
{
    Task SaveTelemetryAsync(TelemetryData data);
    Task<List<TelemetryData>> GetTelemetryRangeAsync(DateTime start, DateTime end);
    Task<TelemetryData?> GetLatestTelemetryAsync();
    Task ClearOldDataAsync(DateTime before);
    Task ExportToCsvAsync(string filePath, DateTime start, DateTime end);
}
