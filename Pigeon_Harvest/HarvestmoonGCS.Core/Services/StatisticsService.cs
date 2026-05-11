using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HarvestmoonGCS.Core.Models;
using HarvestmoonGCS.Models;

namespace HarvestmoonGCS.Core.Services;

/// <summary>
/// Service for managing flight statistics
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly ILogger<StatisticsService> _logger;
    private readonly string _statisticsFilePath;
    private FlightStatistics _currentStatistics;
    private List<FlightStatistics> _allStatistics;
    private GPSData? _lastGpsPosition;
    private DateTime _lastUpdateTime;
    private bool _isArmed;
    private DateTime _armTime;
    private double _totalSpeedSum;
    private int _speedSampleCount;

    public event EventHandler<FlightStatistics>? StatisticsUpdated;

    public StatisticsService(ILogger<StatisticsService> logger)
    {
        _logger = logger;
        _currentStatistics = new FlightStatistics();
        _allStatistics = new List<FlightStatistics>();
        _lastUpdateTime = DateTime.Now;

        // Set statistics file path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pigeonDataPath = Path.Combine(appDataPath, "PigeonUno");
        Directory.CreateDirectory(pigeonDataPath);
        _statisticsFilePath = Path.Combine(pigeonDataPath, "flight_statistics.json");

        _logger.LogInformation("StatisticsService initialized. Data path: {Path}", _statisticsFilePath);
    }

    public FlightStatistics GetCurrentStatistics()
    {
        return _currentStatistics;
    }

    public FlightStatistics GetStatistics(TimePeriod period)
    {
        var now = DateTime.Now;
        var filteredStats = new FlightStatistics();

        var relevantStats = period switch
        {
            TimePeriod.Today => _allStatistics.Where(s => s.Date.Date == now.Date),
            TimePeriod.ThisWeek => _allStatistics.Where(s => GetWeekNumber(s.Date) == GetWeekNumber(now) && s.Date.Year == now.Year),
            TimePeriod.ThisMonth => _allStatistics.Where(s => s.Date.Month == now.Month && s.Date.Year == now.Year),
            TimePeriod.AllTime => _allStatistics,
            _ => _allStatistics
        };

        foreach (var stat in relevantStats)
        {
            filteredStats.Merge(stat);
        }

        // Include current statistics if in the period
        if (ShouldIncludeCurrentStats(period, now))
        {
            filteredStats.Merge(_currentStatistics);
        }

        return filteredStats;
    }

    public void UpdateStatistics(FlightData data)
    {
        try
        {
            var now = DateTime.Now;

            // Check if armed/disarmed
            var wasArmed = _isArmed;
            _isArmed = data.FlightMode != FlightMode.DISARMED;

            // Track arm time
            if (_isArmed && !wasArmed)
            {
                _armTime = now;
                _logger.LogInformation("Vehicle armed, starting flight time tracking");
            }
            else if (!_isArmed && wasArmed)
            {
                // Vehicle disarmed, save current statistics
                _logger.LogInformation("Vehicle disarmed, saving statistics");
                _ = SaveStatisticsAsync();
            }

            // Only update statistics when armed
            if (!_isArmed)
            {
                _lastUpdateTime = now;
                return;
            }

            // Update flight time
            var timeSinceArm = now - _armTime;
            _currentStatistics.FlightTime = timeSinceArm;

            // Update distance if we have GPS data
            if (data.GPS.IsValid)
            {
                if (_lastGpsPosition != null && _lastGpsPosition.IsValid)
                {
                    var distance = CalculateDistance(_lastGpsPosition, data.GPS);
                    _currentStatistics.TotalDistance += distance;
                }
                _lastGpsPosition = data.GPS;
            }

            // Update max altitude (convert from mm to m)
            var altitudeMeters = data.Altitude / 1000.0;
            if (altitudeMeters > _currentStatistics.MaxAltitude)
            {
                _currentStatistics.MaxAltitude = altitudeMeters;
            }

            // Update max speed
            if (data.Speed > _currentStatistics.MaxSpeed)
            {
                _currentStatistics.MaxSpeed = data.Speed;
            }

            // Update average speed
            _totalSpeedSum += data.Speed;
            _speedSampleCount++;
            if (_speedSampleCount > 0)
            {
                _currentStatistics.AverageSpeed = _totalSpeedSum / _speedSampleCount;
            }

            _lastUpdateTime = now;

            // Raise event
            StatisticsUpdated?.Invoke(this, _currentStatistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating statistics");
        }
    }

    public void ResetStatistics()
    {
        _logger.LogInformation("Resetting current statistics");
        _currentStatistics.Reset();
        _lastGpsPosition = null;
        _totalSpeedSum = 0;
        _speedSampleCount = 0;
        _armTime = DateTime.Now;
        StatisticsUpdated?.Invoke(this, _currentStatistics);
    }

    public async Task SaveStatisticsAsync()
    {
        try
        {
            // Only save if there's meaningful data
            if (_currentStatistics.FlightTime.TotalSeconds < 5)
            {
                _logger.LogInformation("Skipping save - flight time too short");
                return;
            }

            // Add current statistics to history
            var statsCopy = new FlightStatistics
            {
                FlightTime = _currentStatistics.FlightTime,
                TotalDistance = _currentStatistics.TotalDistance,
                MaxAltitude = _currentStatistics.MaxAltitude,
                MaxSpeed = _currentStatistics.MaxSpeed,
                AverageSpeed = _currentStatistics.AverageSpeed,
                WaypointsCompleted = _currentStatistics.WaypointsCompleted,
                FlightCount = 1,
                Date = DateTime.Now
            };

            _allStatistics.Add(statsCopy);

            // Save to file
            var json = JsonSerializer.Serialize(_allStatistics, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_statisticsFilePath, json);
            _logger.LogInformation("Statistics saved successfully. Total flights: {Count}", _allStatistics.Count);

            // Reset current statistics after saving
            ResetStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving statistics");
        }
    }

    public async Task LoadStatisticsAsync()
    {
        try
        {
            if (!File.Exists(_statisticsFilePath))
            {
                _logger.LogInformation("No statistics file found, starting fresh");
                return;
            }

            var json = await File.ReadAllTextAsync(_statisticsFilePath);
            _allStatistics = JsonSerializer.Deserialize<List<FlightStatistics>>(json) ?? new List<FlightStatistics>();
            _logger.LogInformation("Loaded {Count} flight statistics records", _allStatistics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading statistics");
            _allStatistics = new List<FlightStatistics>();
        }
    }

    public async Task<List<FlightStatistics>> GetAllStatisticsAsync()
    {
        if (_allStatistics.Count == 0)
        {
            await LoadStatisticsAsync();
        }
        return _allStatistics;
    }

    private double CalculateDistance(GPSData pos1, GPSData pos2)
    {
        // Haversine formula for calculating distance between two GPS coordinates
        const double earthRadius = 6371000; // meters

        var lat1 = pos1.Latitude / 1e7 * Math.PI / 180;
        var lat2 = pos2.Latitude / 1e7 * Math.PI / 180;
        var lon1 = pos1.Longitude / 1e7 * Math.PI / 180;
        var lon2 = pos2.Longitude / 1e7 * Math.PI / 180;

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    private bool ShouldIncludeCurrentStats(TimePeriod period, DateTime now)
    {
        return period switch
        {
            TimePeriod.Today => _currentStatistics.Date.Date == now.Date,
            TimePeriod.ThisWeek => GetWeekNumber(_currentStatistics.Date) == GetWeekNumber(now) && _currentStatistics.Date.Year == now.Year,
            TimePeriod.ThisMonth => _currentStatistics.Date.Month == now.Month && _currentStatistics.Date.Year == now.Year,
            TimePeriod.AllTime => true,
            _ => true
        };
    }

    private int GetWeekNumber(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return culture.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }
}
