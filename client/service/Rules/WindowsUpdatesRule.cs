using AgentService.Remediations;
using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class WindowsUpdatesRule : IRule
{
    public const string Id = "rule.windows_updates";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<WindowsUpdatesSensorData>(sensorResults, WindowsUpdatesSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.windows_updates.failed",
                RuleId,
                "Windows-Update-Status konnte nicht gelesen werden",
                $"Windows-Update-Sensor fehlgeschlagen: {error}"));
            return findings;
        }

        if (data is null)
        {
            return findings;
        }

        if (data.SecurityCount > 0)
        {
            var securityFinding = new FindingDto
            {
                FindingId = "updates.security.missing",
                RuleId = RuleId,
                Category = FindingCategory.Security,
                Severity = data.SecurityCount >= 6 ? FindingSeverity.Critical : FindingSeverity.Warning,
                Title = "Ausstehende Sicherheitsupdates",
                Summary = $"{data.SecurityCount} Sicherheitsupdates sind verfuegbar.",
                DetailsMarkdown = BuildDetails(data.TopTitles),
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data)
            };

            securityFinding.Actions.Add(new ActionDto
            {
                ActionId = ActionIds.WindowsInstallSecurityUpdates,
                Label = "Windows Updates installieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = WindowsInstallSecurityUpdatesRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = true
            });
            findings.Add(securityFinding);
        }

        if (data.OptionalSoftwareCount > 0)
        {
            var optionalFinding = new FindingDto
            {
                FindingId = "updates.optional.available",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Info,
                Title = "Optionale Windows-Updates verfuegbar",
                Summary = $"{data.OptionalSoftwareCount} optionale Software/Funktionsupdates verfuegbar.",
                DetailsMarkdown = BuildDetails(data.TopTitles),
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data)
            };
            optionalFinding.Actions.Add(new ActionDto
            {
                ActionId = ActionIds.WindowsInstallOptionalUpdates,
                Label = "Optionale Updates installieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = WindowsInstallOptionalUpdatesRemediation.Id,
                IsSafeForOneClickMaintenance = false,
                RequiresAdmin = true,
                MayRequireRestart = true
            });
            findings.Add(optionalFinding);
        }

        if (data.DriverCount > 0)
        {
            var driverFinding = new FindingDto
            {
                FindingId = "updates.drivers.available",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Info,
                Title = "Optionale Treiberupdates verfuegbar",
                Summary = $"{data.DriverCount} Treiberupdates verfuegbar (nur manuell empfohlen).",
                DetailsMarkdown = "Treiberupdates sind heikel. Bitte Quelle und Notwendigkeit vor Installation pruefen.",
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data)
            };
            driverFinding.Actions.Add(new ActionDto
            {
                ActionId = ActionIds.WindowsOpenDriverUpdates,
                Label = "Treiberseite oeffnen",
                Kind = ActionKind.OpenExternal,
                ExternalTarget = "ms-settings:windowsupdate-optionalupdates",
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = false,
                MayRequireRestart = false
            });
            findings.Add(driverFinding);
        }

        return findings;
    }

    private static Dictionary<string, string> BuildEvidence(WindowsUpdatesSensorData data)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["security_count"] = data.SecurityCount.ToString(),
            ["optional_count"] = data.OptionalSoftwareCount.ToString(),
            ["driver_count"] = data.DriverCount.ToString(),
            ["top_titles"] = string.Join(" | ", data.TopTitles)
        };
    }

    private static string BuildDetails(IEnumerable<string> titles)
    {
        List<string> collected = titles.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5).ToList();
        if (collected.Count == 0)
        {
            return "Keine Detailtitel verfuegbar.";
        }

        return "Top-Eintraege:" + Environment.NewLine + string.Join(Environment.NewLine, collected.Select(x => $"• {x}"));
    }
}
