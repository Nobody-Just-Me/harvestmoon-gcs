import asyncio
from Pigeon_Uno.Core.Services import MavLinkService
from Pigeon_Uno.Core.Models import ConnectionType, ConnectionConfig

async def main():
    service = MavLinkService()
    config = ConnectionConfig()
    config.Type = ConnectionType.Serial
    config.SerialPort = "/dev/ttyACM0"
    config.BaudRate = 115200
    ok = await service.ConnectAsync(config)
    print(f"CONNECT_OK={ok}")
    if ok:
        await asyncio.sleep(2)
        await service.DisconnectAsync()
        print("DISCONNECT_OK=True")
    else:
        print("DISCONNECT_OK=False")

if __name__ == "__main__":
    asyncio.run(main())
