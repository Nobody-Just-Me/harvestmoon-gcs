using System;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;

class SerialConnectProbe
{
    static async Task<int> Main(string[] args)
    {
        string port = args.Length > 0 ? args[0] : "/dev/ttyACM0";
        int baud = args.Length > 1 && int.TryParse(args[1], out var b) ? b : 115200;

        var service = new MavLinkService();
        var config = new ConnectionConfig
        {
            Type = ConnectionType.Serial,
            SerialPort = port,
            BaudRate = baud
        };

        bool ok = await service.ConnectAsync(config);
        Console.WriteLine($"CONNECT_OK={ok}");
        if (!ok)
        {
            return 1;
        }

        await Task.Delay(2000);
        await service.DisconnectAsync();
        Console.WriteLine("DISCONNECT_OK=True");
        return 0;
    }
}
