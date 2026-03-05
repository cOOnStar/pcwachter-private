using System.Diagnostics.Eventing.Reader;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class EventLogSensor : ISensor
{
    public const string Id = "sensor.eventlog";
    private const int WindowHours = 24;

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime fromUtc = nowUtc.AddHours(-WindowHours);

            (int systemCount, List<string> systemTop) = Collect("System", fromUtc);
            (int appCount, List<string> appTop) = Collect("Application", fromUtc);

            var payload = new EventLogHealthSensorData
            {
                WindowStartUtc = fromUtc,
                WindowEndUtc = nowUtc,
                SystemErrorCount24h = systemCount,
                ApplicationErrorCount24h = appCount,
                TopSystemSources = systemTop,
                TopApplicationSources = appTop
            };

            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = payload
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = false,
                Error = ex.Message
            });
        }
    }

    private static (int Count, List<string> TopSources) Collect(string logName, DateTime fromUtc)
    {
        string query = "*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 86400000]]]";
        var sourceCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        var eventQuery = new EventLogQuery(logName, PathType.LogName, query)
        {
            ReverseDirection = true
        };

        using var reader = new EventLogReader(eventQuery);
        for (EventRecord? record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                DateTime? time = record.TimeCreated?.ToUniversalTime();
                if (!time.HasValue || time.Value < fromUtc)
                {
                    continue;
                }

                count++;
                string source = record.ProviderName ?? "Unknown";
                sourceCounter[source] = sourceCounter.TryGetValue(source, out int current) ? current + 1 : 1;
            }

            if (count >= 20000)
            {
                break;
            }
        }

        List<string> top = sourceCounter
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(x => $"{x.Key} ({x.Value})")
            .ToList();

        return (count, top);
    }
}
