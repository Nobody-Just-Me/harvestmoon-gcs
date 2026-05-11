using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Services;

public class SqliteTelemetryStore : ITelemetryStore
{
    private readonly string _dbPath;

    private static readonly (string Name, string TypeSql)[] RequiredColumns =
    {
        ("Latitude", "REAL"),
        ("Longitude", "REAL"),
        ("Altitude", "REAL"),
        ("Heading", "REAL"),
        ("Pitch", "REAL"),
        ("Roll", "REAL"),
        ("Speed", "REAL"),
        ("GroundSpeed", "REAL"),
        ("AirSpeed", "REAL"),
        ("VerticalSpeed", "REAL"),
        ("BatteryVoltage", "REAL"),
        ("BatteryCurrent", "REAL"),
        ("BatteryPercentage", "REAL"),
        ("SatelliteCount", "INTEGER"),
        ("HDOP", "REAL"),
        ("FlightMode", "INTEGER"),
        ("IsArmed", "INTEGER"),
        ("Timestamp", "TEXT")
    };

    public SqliteTelemetryStore()
    {
        _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "pigeon_telemetry.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Telemetry (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Latitude REAL,
                    Longitude REAL,
                    Altitude REAL,
                    Heading REAL,
                    Pitch REAL,
                    Roll REAL,
                    Speed REAL,
                    GroundSpeed REAL,
                    AirSpeed REAL,
                    VerticalSpeed REAL,
                    BatteryVoltage REAL,
                    BatteryCurrent REAL,
                    BatteryPercentage REAL,
                    SatelliteCount INTEGER,
                    HDOP REAL,
                    FlightMode INTEGER,
                    IsArmed INTEGER,
                    Timestamp TEXT
                );

                CREATE INDEX IF NOT EXISTS IX_Telemetry_Timestamp
                ON Telemetry (Timestamp DESC);
            ";
            command.ExecuteNonQuery();

            EnsureTelemetryColumns(connection);
        }
        catch (Exception)
        {
            // Ignore init errors for MVP
        }
    }

    public async Task SaveTelemetryAsync(TelemetryData data)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                INSERT INTO Telemetry (
                    Latitude, Longitude, Altitude, Heading, Pitch, Roll,
                    Speed, GroundSpeed, AirSpeed, VerticalSpeed,
                    BatteryVoltage, BatteryCurrent, BatteryPercentage,
                    SatelliteCount, HDOP, FlightMode, IsArmed, Timestamp)
                VALUES (
                    $lat, $lon, $alt, $hdg, $pitch, $roll,
                    $speed, $groundSpeed, $airSpeed, $verticalSpeed,
                    $batteryVoltage, $batteryCurrent, $batteryPercentage,
                    $satelliteCount, $hdop, $flightMode, $isArmed, $time)
            ";

            command.Parameters.AddWithValue("$lat", data.Latitude);
            command.Parameters.AddWithValue("$lon", data.Longitude);
            command.Parameters.AddWithValue("$alt", data.Altitude);
            command.Parameters.AddWithValue("$hdg", data.Heading);
            command.Parameters.AddWithValue("$pitch", data.Pitch);
            command.Parameters.AddWithValue("$roll", data.Roll);
            command.Parameters.AddWithValue("$speed", data.Speed);
            command.Parameters.AddWithValue("$groundSpeed", data.GroundSpeed);
            command.Parameters.AddWithValue("$airSpeed", data.AirSpeed);
            command.Parameters.AddWithValue("$verticalSpeed", data.VerticalSpeed);
            command.Parameters.AddWithValue("$batteryVoltage", data.BatteryVoltage);
            command.Parameters.AddWithValue("$batteryCurrent", data.BatteryCurrent);
            command.Parameters.AddWithValue("$batteryPercentage", data.BatteryPercentage);
            command.Parameters.AddWithValue("$satelliteCount", data.SatelliteCount);
            command.Parameters.AddWithValue("$hdop", data.HDOP);
            command.Parameters.AddWithValue("$flightMode", (int)data.FlightMode);
            command.Parameters.AddWithValue("$isArmed", data.IsArmed ? 1 : 0);
            command.Parameters.AddWithValue("$time", data.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Log error
        }
    }

    public async Task<List<TelemetryData>> GetTelemetryRangeAsync(DateTime start, DateTime end)
    {
        var result = new List<TelemetryData>();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT Latitude, Longitude, Altitude, Heading, Pitch, Roll,
                       Speed, GroundSpeed, AirSpeed, VerticalSpeed,
                       BatteryVoltage, BatteryCurrent, BatteryPercentage,
                       SatelliteCount, HDOP, FlightMode, IsArmed, Timestamp
                FROM Telemetry
                WHERE Timestamp >= $start AND Timestamp <= $end
                ORDER BY Timestamp ASC
            ";
            command.Parameters.AddWithValue("$start", start.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$end", end.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(ReadTelemetryData(reader));
            }
        }
        catch (Exception)
        {
            return new List<TelemetryData>();
        }

        return result;
    }

    public async Task<TelemetryData?> GetLatestTelemetryAsync()
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT Latitude, Longitude, Altitude, Heading, Pitch, Roll,
                       Speed, GroundSpeed, AirSpeed, VerticalSpeed,
                       BatteryVoltage, BatteryCurrent, BatteryPercentage,
                       SatelliteCount, HDOP, FlightMode, IsArmed, Timestamp
                FROM Telemetry
                ORDER BY Timestamp DESC
                LIMIT 1
            ";

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return ReadTelemetryData(reader);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public async Task ClearOldDataAsync(DateTime before)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Telemetry WHERE Timestamp < $before";
            command.Parameters.AddWithValue("$before", before.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
        }
    }

    public async Task ExportToCsvAsync(string filePath, DateTime start, DateTime end)
    {
        var data = await GetTelemetryRangeAsync(start, end);
        await HarvestmoonGCS.Core.Helpers.ReadWriteCsv.WriteCsvAsync(filePath, data);
    }

    private static void EnsureTelemetryColumns(SqliteConnection connection)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Telemetry)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }
        }

        foreach (var (name, typeSql) in RequiredColumns)
        {
            if (existingColumns.Contains(name))
            {
                continue;
            }

            try
            {
                using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = $"ALTER TABLE Telemetry ADD COLUMN {name} {typeSql}";
                alterCommand.ExecuteNonQuery();
            }
            catch
            {
            }
        }
    }

    private static TelemetryData ReadTelemetryData(SqliteDataReader reader)
    {
        var speed = ReadDouble(reader, 6);
        var groundSpeed = ReadDouble(reader, 7, speed);
        var airSpeed = ReadDouble(reader, 8, groundSpeed);
        var verticalSpeed = ReadDouble(reader, 9);

        return new TelemetryData
        {
            Latitude = ReadDouble(reader, 0),
            Longitude = ReadDouble(reader, 1),
            Altitude = ReadDouble(reader, 2),
            Heading = ReadDouble(reader, 3),
            Pitch = ReadDouble(reader, 4),
            Roll = ReadDouble(reader, 5),
            Speed = speed,
            GroundSpeed = groundSpeed,
            AirSpeed = airSpeed,
            VerticalSpeed = verticalSpeed,
            BatteryVoltage = ReadDouble(reader, 10),
            BatteryCurrent = ReadDouble(reader, 11),
            BatteryPercentage = ReadDouble(reader, 12),
            SatelliteCount = ReadInt(reader, 13),
            HDOP = ReadDouble(reader, 14),
            FlightMode = ReadFlightMode(reader, 15),
            IsArmed = ReadBool(reader, 16),
            Timestamp = ParseTimestamp(reader.IsDBNull(17) ? null : reader.GetString(17))
        };
    }

    private static double ReadDouble(SqliteDataReader reader, int ordinal, double fallback = 0)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static int ReadInt(SqliteDataReader reader, int ordinal, int fallback = 0)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool ReadBool(SqliteDataReader reader, int ordinal, bool fallback = false)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) == 1;
        }
        catch
        {
            return fallback;
        }
    }

    private static FlightMode ReadFlightMode(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return FlightMode.MANUAL;
        }

        try
        {
            var raw = reader.GetValue(ordinal);
            if (raw is long asLong)
            {
                return ToFlightMode((int)asLong);
            }

            if (raw is int asInt)
            {
                return ToFlightMode(asInt);
            }

            if (raw is string asString)
            {
                if (Enum.TryParse<FlightMode>(asString, true, out var parsedByName))
                {
                    return parsedByName;
                }

                if (int.TryParse(asString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedByNumber))
                {
                    return ToFlightMode(parsedByNumber);
                }
            }
        }
        catch
        {
        }

        return FlightMode.MANUAL;
    }

    private static FlightMode ToFlightMode(int raw)
    {
        return Enum.IsDefined(typeof(FlightMode), raw)
            ? (FlightMode)raw
            : FlightMode.MANUAL;
    }

    private static DateTime ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;
    }
}
