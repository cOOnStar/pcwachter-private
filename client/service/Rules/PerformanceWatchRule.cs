using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class PerformanceWatchRule : IRule
{
    public const string Id = "rule.performance_watch";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<PerformanceWatchSensorData>(sensorResults, PerformanceWatchSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.performance.failed",
                RuleId,
                "Performance-Daten konnten nicht gelesen werden",
                $"Performance-Sensor fehlgeschlagen: {error}"));
            return findings;
        }

        if (data is null)
        {
            return findings;
        }

        if (data.CpuPercent >= 90 || data.MemoryPercent >= 90)
        {
            findings.Add(new FindingDto
            {
                FindingId = "health.performance.current_spike",
                RuleId = RuleId,
                Category = FindingCategory.Health,
                Severity = FindingSeverity.Warning,
                Title = "Hohe Systemauslastung erkannt",
                Summary = $"CPU {data.CpuPercent}% | RAM {data.MemoryPercent}% | Top-Prozess: {data.TopProcessName} ({data.TopProcessCpuPercent}%).",
                DetailsMarkdown = "Kurzzeitige Spitzen sind normal. Bei wiederholten Spitzen Startup- und Hintergrundprogramme pruefen.",
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data)
            });
        }

        return findings;
    }

    private static Dictionary<string, string> BuildEvidence(PerformanceWatchSensorData data)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu_percent"] = data.CpuPercent.ToString(),
            ["memory_percent"] = data.MemoryPercent.ToString(),
            ["top_process_name"] = data.TopProcessName,
            ["top_process_cpu_percent"] = data.TopProcessCpuPercent.ToString(),
            ["top_process_id"] = data.TopProcessId?.ToString() ?? "-"
        };
    }
}
