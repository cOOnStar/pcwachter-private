using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class EventLogRule : IRule
{
    public const string Id = "rule.eventlog";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<EventLogHealthSensorData>(sensorResults, EventLogSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.eventlog.failed",
                RuleId,
                "Eventlog-Status konnte nicht gelesen werden",
                $"Eventlog-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        int warningThreshold = Math.Clamp(context.Thresholds.EventLogWarningCount24h, 1, 500);

        if (data.SystemErrorCount24h > warningThreshold)
        {
            findings.Add(new FindingDto
            {
                FindingId = "health.eventlog.system_errors",
                RuleId = RuleId,
                Category = FindingCategory.Health,
                Severity = FindingSeverity.Warning,
                Title = "Viele kritische System-Events",
                Summary = $"System-Log: {data.SystemErrorCount24h} Error/Critical Events in 24h.",
                DetailsMarkdown = $"Prufen Sie die System-Ereignisse im Event Viewer. Aktiver Warnwert: > {warningThreshold} Events in 24h.",
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data, "system", warningThreshold),
                Actions =
                {
                    new ActionDto
                    {
                        ActionId = ActionIds.EventLogOpenViewer,
                        Label = "Event Viewer offnen",
                        Kind = ActionKind.OpenExternal,
                        ExternalTarget = "eventvwr.msc",
                        IsSafeForOneClickMaintenance = true,
                        RequiresAdmin = false,
                        MayRequireRestart = false
                    }
                }
            });
        }

        if (data.ApplicationErrorCount24h > warningThreshold)
        {
            findings.Add(new FindingDto
            {
                FindingId = "health.eventlog.app_errors",
                RuleId = RuleId,
                Category = FindingCategory.Health,
                Severity = FindingSeverity.Warning,
                Title = "Viele kritische Application-Events",
                Summary = $"Application-Log: {data.ApplicationErrorCount24h} Error/Critical Events in 24h.",
                DetailsMarkdown = $"Prufen Sie die Application-Ereignisse im Event Viewer. Aktiver Warnwert: > {warningThreshold} Events in 24h.",
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data, "application", warningThreshold),
                Actions =
                {
                    new ActionDto
                    {
                        ActionId = ActionIds.EventLogOpenViewer,
                        Label = "Event Viewer offnen",
                        Kind = ActionKind.OpenExternal,
                        ExternalTarget = "eventvwr.msc",
                        IsSafeForOneClickMaintenance = true,
                        RequiresAdmin = false,
                        MayRequireRestart = false
                    }
                }
            });
        }

        return findings;
    }

    private static Dictionary<string, string> BuildEvidence(EventLogHealthSensorData data, string scope, int warningThreshold)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["scope"] = scope,
            ["window_start_utc"] = data.WindowStartUtc.ToString("O"),
            ["window_end_utc"] = data.WindowEndUtc.ToString("O"),
            ["system_error_count_24h"] = data.SystemErrorCount24h.ToString(),
            ["app_error_count_24h"] = data.ApplicationErrorCount24h.ToString(),
            ["warning_threshold_24h"] = warningThreshold.ToString(),
            ["top_system_sources"] = string.Join(", ", data.TopSystemSources),
            ["top_app_sources"] = string.Join(", ", data.TopApplicationSources)
        };
    }
}
