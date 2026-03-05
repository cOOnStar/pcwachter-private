using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class PendingRebootRule : IRule
{
    public const string Id = "rule.pending_reboot";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<PendingRebootSensorData>(sensorResults, PendingRebootSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.pending_reboot.failed",
                RuleId,
                "Neustartstatus konnte nicht gelesen werden",
                $"Pending-Reboot-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        if (!data.IsPending)
        {
            return findings;
        }

        var finding = new FindingDto
        {
            FindingId = "system.reboot.pending",
            RuleId = RuleId,
            Category = FindingCategory.System,
            Severity = FindingSeverity.Warning,
            Title = "Neustart ausstehend",
            Summary = "Windows meldet einen ausstehenden Neustart.",
            DetailsMarkdown = "Ein Neustart kann Updates abschliessen und den stabilen Zustand wiederherstellen.",
            DetectedAtUtc = context.NowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signals"] = string.Join(", ", data.TriggeredSignals)
            }
        };

        finding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.SystemRebootNow,
            Label = "Jetzt neu starten",
            Kind = ActionKind.OpenExternal,
            ExternalTarget = "shutdown.exe /r /t 0",
            ConfirmText = "Der Rechner wird sofort neu gestartet. Nicht gespeicherte Daten gehen verloren.",
            DetailsMarkdown = "Fuehrt `shutdown.exe /r /t 0` aus.",
            IsSafeForOneClickMaintenance = false,
            RequiresAdmin = false,
            MayRequireRestart = true
        });

        findings.Add(finding);
        return findings;
    }
}
