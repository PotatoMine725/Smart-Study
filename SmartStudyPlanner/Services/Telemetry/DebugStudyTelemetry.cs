using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartStudyPlanner.Services.Telemetry;

public sealed class DebugStudyTelemetry : IStudyTelemetry
{
    public void Track(string eventName, IDictionary<string, string>? properties = null)
    {
        var payload = properties == null || properties.Count == 0
            ? string.Empty
            : string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}"));
        Debug.WriteLine($"[UX-TELEMETRY] {DateTime.UtcNow:O} {eventName} {payload}");
    }
}

