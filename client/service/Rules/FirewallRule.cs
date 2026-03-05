using AgentService.Runtime;
using AgentService.Remediations;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class FirewallRule : IRule
{
    public const string Id = "rule.firewall";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<FirewallSensorData>(sensorResults, FirewallSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.firewall.failed",
                RuleId,
                "Firewall-Status konnte nicht gelesen werden",
                $"Firewall-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        var disabled = data.DisabledProfiles;
        if (disabled.Count == 0)
        {
            return findings;
        }

        var finding = new FindingDto
        {
            FindingId = "security.firewall.disabled",
            RuleId = RuleId,
            Category = FindingCategory.Security,
            Severity = FindingSeverity.Critical,
            Title = "Windows-Firewall ist teilweise deaktiviert",
            Summary = $"Deaktivierte Profile: {string.Join(", ", disabled)}",
            DetailsMarkdown = "Aktivieren Sie alle Firewall-Profile (Domain/Private/Public).",
            DetectedAtUtc = context.NowUtc,
            Evidence = data.Profiles.ToDictionary(
                x => $"profile_{x.Key.ToLowerInvariant()}",
                x => x.Value?.ToString() ?? "unknown",
                StringComparer.OrdinalIgnoreCase)
        };

        finding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.FirewallEnableAll,
            Label = "Alle Profile aktivieren",
            Kind = ActionKind.RunRemediation,
            RemediationId = FirewallEnableAllRemediation.Id,
            IsSafeForOneClickMaintenance = true,
            RequiresAdmin = true,
            MayRequireRestart = false
        });

        findings.Add(finding);
        return findings;
    }
}
