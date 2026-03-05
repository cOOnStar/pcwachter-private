using AgentService.Runtime;
using AgentService.Remediations;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class DefenderRule : IRule
{
    public const string Id = "rule.defender";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<DefenderSensorData>(sensorResults, DefenderSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.defender.failed",
                RuleId,
                "Defender-Status konnte nicht gelesen werden",
                $"Defender-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        if (data.RealtimeProtectionEnabled == false)
        {
            var finding = new FindingDto
            {
                FindingId = "security.defender.realtime_off",
                RuleId = RuleId,
                Category = FindingCategory.Security,
                Severity = FindingSeverity.Critical,
                Title = "Defender Echtzeitschutz ist deaktiviert",
                Summary = "Microsoft Defender Echtzeitschutz ist aus.",
                DetailsMarkdown = "Aktivieren Sie den Echtzeitschutz oder lassen Sie den Service eine Reparatur versuchen.",
                DetectedAtUtc = context.NowUtc,
                Evidence = BuildEvidence(data)
            };

            finding.Actions.Add(new ActionDto
            {
                ActionId = ActionIds.DefenderUpdateSignatures,
                Label = "Signaturen aktualisieren",
                Kind = ActionKind.RunRemediation,
                RemediationId = DefenderUpdateSignaturesRemediation.Id,
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = true,
                MayRequireRestart = false
            });

            if (data.CanAttemptEnableRealtime)
            {
                finding.Actions.Add(new ActionDto
                {
                    ActionId = ActionIds.DefenderEnableRealtime,
                    Label = "Echtzeitschutz aktivieren",
                    Kind = ActionKind.RunRemediation,
                    RemediationId = DefenderEnableRealtimeRemediation.Id,
                    IsSafeForOneClickMaintenance = true,
                    RequiresAdmin = true,
                    MayRequireRestart = false
                });
            }

            findings.Add(finding);
        }

        if (data.DaysSinceLastUpdate.HasValue)
        {
            int warningDays = Math.Clamp(context.Thresholds.DefenderSignatureWarningDays, 1, 90);
            int criticalDays = Math.Clamp(context.Thresholds.DefenderSignatureCriticalDays, warningDays + 1, 180);
            int ageDays = data.DaysSinceLastUpdate.Value;

            FindingSeverity? severity;
            if (ageDays >= criticalDays)
            {
                severity = FindingSeverity.Critical;
            }
            else if (ageDays >= warningDays)
            {
                severity = FindingSeverity.Warning;
            }
            else
            {
                severity = null;
            }

            if (severity.HasValue)
            {
                var finding = new FindingDto
                {
                    FindingId = "security.defender.signatures_old",
                    RuleId = RuleId,
                    Category = FindingCategory.Security,
                    Severity = severity.Value,
                    Title = "Defender-Signaturen sind veraltet",
                    Summary = $"Signatur-Stand ist {data.DaysSinceLastUpdate.Value} Tage alt.",
                    DetailsMarkdown = $"Aktualisieren Sie die Signaturen, damit neue Bedrohungen erkannt werden.\n\n" +
                                      $"Aktive Schwellwerte: Warnung ab {warningDays} Tagen, kritisch ab {criticalDays} Tagen.",
                    DetectedAtUtc = context.NowUtc,
                    Evidence = BuildEvidence(data)
                };
                finding.Evidence["warning_threshold_days"] = warningDays.ToString();
                finding.Evidence["critical_threshold_days"] = criticalDays.ToString();

                finding.Actions.Add(new ActionDto
                {
                    ActionId = ActionIds.DefenderUpdateSignatures,
                    Label = "Signaturen aktualisieren",
                    Kind = ActionKind.RunRemediation,
                    RemediationId = DefenderUpdateSignaturesRemediation.Id,
                    IsSafeForOneClickMaintenance = true,
                    RequiresAdmin = true,
                    MayRequireRestart = false
                });

                findings.Add(finding);
            }
        }

        return findings;
    }

    private static Dictionary<string, string> BuildEvidence(DefenderSensorData data)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["realtime_enabled"] = data.RealtimeProtectionEnabled?.ToString() ?? "unknown",
            ["signature_last_updated_utc"] = data.SignatureLastUpdatedUtc?.ToString("O") ?? "unknown",
            ["days_since_signature_update"] = data.DaysSinceLastUpdate?.ToString() ?? "unknown",
            ["tamper_protection"] = data.TamperProtectionEnabled?.ToString() ?? "unknown",
            ["engine_version"] = data.EngineVersion ?? "unknown",
            ["platform_version"] = data.PlatformVersion ?? "unknown",
            ["signature_version"] = data.SignatureVersion ?? "unknown",
            ["source"] = data.Source
        };
    }
}
