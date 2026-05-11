#if !__WASM__
using System;
using System.Collections.Generic;
using System.Linq;
using HarvestmoonGCS.Core.Models;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Adapter that bridges TelemetryBuffer to ITelemetryBufferProvider interface
/// </summary>
public class TelemetryBufferAdapter : ITelemetryBufferProvider
{
    private readonly TelemetryBuffer _buffer;

    public TelemetryBufferAdapter(TelemetryBuffer buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public IEnumerable<TelemetrySnapshot> GetSnapshots(int lastSeconds)
    {
        var seconds = Math.Max(1, lastSeconds);
        return _buffer.GetWindow(TimeSpan.FromSeconds(seconds));
    }
}
#endif
