using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Telemetry;

public interface IStudyTelemetry
{
    void Track(string eventName, IDictionary<string, string>? properties = null);
}

