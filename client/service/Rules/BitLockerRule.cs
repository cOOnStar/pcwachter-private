using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class BitLockerRule : IRule
{
    public const string Id = "rule.bitlocker";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<BitLockerSensorData>(sensorResults, BitLockerSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.bitlocker.failed",
                RuleId,
                "BitLocker-Status konnte nicht gelesen werden",
                $"BitLocker-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        if (data.IsProtectionOn != false)
        {
            return findings;
        }

        FindingSeverity severity = data.IsWindowsHomeEdition ? FindingSeverity.Info : FindingSeverity.Warning;

        var finding = new FindingDto
        {
            FindingId = "security.bitlocker.off",
            RuleId = RuleId,
            Category = FindingCategory.Security,
            Severity = severity,
            Title = "BitLocker-Schutz ist nicht aktiv",
            Summary = data.IsWindowsHomeEdition
                ? "Laufwerksschutz ist aus (Windows Home kann eingeschrankte Optionen haben)."
                : "Systemlaufwerk ist nicht mit BitLocker geschutzt.",
            DetailsMarkdown = BuildDetailsMarkdown(data.SystemDrive),
            DetectedAtUtc = context.NowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["system_drive"] = data.SystemDrive,
                ["protection_status"] = data.ProtectionStatusRaw ?? "unknown",
                ["encryption_method"] = data.EncryptionMethod ?? "unknown",
                ["has_key_protector"] = data.HasKeyProtector?.ToString() ?? "unknown",
                ["source"] = data.Source,
                ["windows_home"] = data.IsWindowsHomeEdition.ToString()
            }
        };

        finding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.BitLockerHowTo,
            Label = "Anleitung anzeigen",
            Kind = ActionKind.OpenDetails,
            DetailsMarkdown = BuildDetailsMarkdown(data.SystemDrive),
            IsSafeForOneClickMaintenance = true,
            RequiresAdmin = false,
            MayRequireRestart = false
        });

        findings.Add(finding);
        return findings;
    }

    private static string BuildDetailsMarkdown(string drive)
    {
        return string.Join('\n',
            "1. Offnen Sie die Systemsteuerung: `control /name Microsoft.BitLockerDriveEncryption`.",
            $"2. Aktivieren Sie BitLocker fur Laufwerk `{drive}`.",
            "3. Hinterlegen Sie den Wiederherstellungsschlussel sicher (z.B. Microsoft-Konto/USB/Datei).",
            "4. Starten Sie den Rechner bei Bedarf neu.");
    }
}
