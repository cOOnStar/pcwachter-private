using System.Text.RegularExpressions;
using AgentService.Remediations;
using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class AppUpdatesRule : IRule
{
    public const string Id = "rule.app_updates";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<AppUpdatesSensorData>(sensorResults, AppUpdatesSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.app_updates.failed",
                RuleId,
                "App-Update-Status konnte nicht gelesen werden",
                $"WinGet-Sensor fehlgeschlagen: {error}"));
            return findings;
        }

        if (data is null || !data.WingetAvailable)
        {
            findings.Add(new FindingDto
            {
                FindingId = "apps.winget.unavailable",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Info,
                Title = "WinGet nicht verfuegbar",
                Summary = "Software-Updates per WinGet konnten auf diesem System nicht aktiviert werden.",
                DetailsMarkdown = "Pruefen Sie, ob `winget` installiert und im Dienstkontext erreichbar ist.",
                DetectedAtUtc = context.NowUtc
            });
            return findings;
        }

        int updateCount = data.Updates.Count;
        if (updateCount <= 0)
        {
            return findings;
        }

        FindingSeverity overallSeverity = updateCount >= 20
            ? FindingSeverity.Critical
            : updateCount >= 5
                ? FindingSeverity.Warning
                : FindingSeverity.Info;

        var summaryFinding = new FindingDto
        {
            FindingId = "apps.outdated.count",
            RuleId = RuleId,
            Category = FindingCategory.System,
            Severity = overallSeverity,
            Title = "Veraltete Programme gefunden",
            Summary = $"{updateCount} installierte Programme haben verfuegbare Updates.",
            DetailsMarkdown = "Waehlen Sie die gewuenschten Programme aus und starten Sie die Aktualisierung.",
            DetectedAtUtc = context.NowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["count"] = updateCount.ToString(),
                ["winget_version"] = data.WingetVersion,
                ["package_ids"] = string.Join("|", data.Updates.Select(x => x.PackageId))
            }
        };

        summaryFinding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.AppsUpdateSelected,
            Label = "Ausgewaehlte Apps aktualisieren",
            Kind = ActionKind.RunRemediation,
            RemediationId = AppsUpdateSelectedRemediation.Id,
            ConfirmText = "Ausgewaehlte Programme werden jetzt ueber WinGet aktualisiert. Fortfahren?",
            IsSafeForOneClickMaintenance = false,
            RequiresAdmin = true,
            MayRequireRestart = true
        });
        findings.Add(summaryFinding);

        foreach (AppUpdateItemData update in data.Updates.Take(60))
        {
            string findingId = $"apps.outdated.{SanitizeId(update.PackageId)}";
            findings.Add(new FindingDto
            {
                FindingId = findingId,
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Warning,
                Title = update.Name,
                Summary = $"Installiert: {Normalize(update.InstalledVersion)} | Verfuegbar: {Normalize(update.AvailableVersion)}",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["package_id"] = update.PackageId,
                    ["name"] = update.Name,
                    ["installed_version"] = Normalize(update.InstalledVersion),
                    ["available_version"] = Normalize(update.AvailableVersion),
                    ["source"] = update.Source
                }
            });
        }

        return findings;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static string SanitizeId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknown";
        }

        string normalized = raw.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9._-]", "_");
        if (normalized.Length > 80)
        {
            normalized = normalized[..80];
        }

        return normalized;
    }
}
