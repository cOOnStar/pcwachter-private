using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class StartupRule : IRule
{
    public const string Id = "rule.startup";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<StartupAppsSensorData>(sensorResults, StartupAppsSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.startup.failed",
                RuleId,
                "Autostartliste konnte nicht gelesen werden",
                $"Startup-Sensor fehlgeschlagen: {error}"));
            return findings;
        }

        if (data is null || data.Entries.Count == 0)
        {
            return findings;
        }

        int highImpact = data.Entries.Count(x => string.Equals(x.Impact, "High", StringComparison.OrdinalIgnoreCase) && !x.IsDisabledByPcwachter);
        int activeCount = data.Entries.Count(x => !x.IsDisabledByPcwachter);
        int disabledCount = data.Entries.Count(x => x.IsDisabledByPcwachter);

        FindingSeverity summarySeverity = highImpact >= 3
            ? FindingSeverity.Warning
            : highImpact > 0
                ? FindingSeverity.Info
                : FindingSeverity.Info;

        findings.Add(new FindingDto
        {
            FindingId = "system.startup.summary",
            RuleId = RuleId,
            Category = FindingCategory.System,
            Severity = summarySeverity,
            Title = "Autostart Manager",
            Summary = $"{activeCount} aktive Eintraege ({highImpact} High Impact), {disabledCount} durch PCWachter deaktiviert.",
            DetailsMarkdown = "Deaktivieren Sie High-Impact-Eintraege und nutzen Sie Undo bei Bedarf.",
            DetectedAtUtc = context.NowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["active_count"] = activeCount.ToString(),
                ["high_impact_count"] = highImpact.ToString(),
                ["disabled_by_pcwachter_count"] = disabledCount.ToString()
            }
        });

        foreach (StartupEntryData entry in data.Entries.Take(80))
        {
            FindingSeverity severity = entry.IsDisabledByPcwachter
                ? FindingSeverity.Info
                : string.Equals(entry.Impact, "High", StringComparison.OrdinalIgnoreCase)
                    ? FindingSeverity.Warning
                    : FindingSeverity.Info;

            findings.Add(new FindingDto
            {
                FindingId = $"system.startup.app.{entry.EntryKey}",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = severity,
                Title = entry.Name,
                Summary = entry.IsDisabledByPcwachter
                    ? $"Deaktiviert durch PCWachter ({entry.Impact} Impact)."
                    : $"Aktiv im Autostart ({entry.Impact} Impact).",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entry_key"] = entry.EntryKey,
                    ["name"] = entry.Name,
                    ["command"] = entry.Command,
                    ["location"] = entry.Location,
                    ["impact"] = entry.Impact,
                    ["disabled_by_pcwachter"] = entry.IsDisabledByPcwachter.ToString()
                }
            });
        }

        return findings;
    }
}
