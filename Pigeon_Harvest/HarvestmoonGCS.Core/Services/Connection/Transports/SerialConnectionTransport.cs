using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestmoonGCS.Services.Connection.Transports;

public class SerialConnectionTransport : IConnectionTransport
{
    private readonly string _portName;
    private readonly int _baudRate;
    private SerialPort? _serialPort;

    public SerialConnectionTransport(string portName, int baudRate)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public Task ConnectAsync()
    {
        _serialPort = new SerialPort(_portName, _baudRate);
        _serialPort.Open();
        return Task.CompletedTask;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_serialPort == null || !_serialPort.IsOpen) return 0;
        return await _serialPort.BaseStream.ReadAsync(buffer, offset, count, token);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        if (_serialPort == null || !_serialPort.IsOpen) return;
        await _serialPort.BaseStream.WriteAsync(buffer, offset, count, token);
    }

    public void Dispose()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }
        _serialPort?.Dispose();
    }
}
