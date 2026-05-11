namespace HarvestmoonGCS.Services;

public enum ConnectionType
{
    Tcp,
    Udp,
    Serial
}

public class ConnectionInfo
{
    public ConnectionType Type { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? PortName { get; set; }
    public int BaudRate { get; set; }
}

public static class ConnectionParser
{
    public static ConnectionInfo Parse(string connectionString)
    {
        var parts = connectionString.Split(':');
        if (parts.Length < 2) throw new System.ArgumentException("Invalid connection string format");

        var typeStr = parts[0].ToLowerInvariant();

        switch (typeStr)
        {
            case "tcp":
                if (parts.Length < 3) throw new System.ArgumentException("Invalid TCP format");
                return new ConnectionInfo
                {
                    Type = ConnectionType.Tcp,
                    Host = parts[1],
                    Port = int.Parse(parts[2])
                };

            case "udp":
                return new ConnectionInfo
                {
                    Type = ConnectionType.Udp,
                    Port = int.Parse(parts[1])
                };

            case "serial":
                if (parts.Length < 3) throw new System.ArgumentException("Invalid Serial format");
                return new ConnectionInfo
                {
                    Type = ConnectionType.Serial,
                    PortName = parts[1],
                    BaudRate = int.Parse(parts[2])
                };

            default:
                throw new System.ArgumentException($"Unknown connection type: {typeStr}");
        }
    }
}
