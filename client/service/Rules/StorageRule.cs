using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class StorageRule : IRule
{
    public const string Id = "rule.storage";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<StorageSensorData>(sensorResults, StorageSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.storage.failed",
                RuleId,
                "Speicherstatus konnte nicht gelesen werden",
                $"Storage-Sensor fehlgeschlagen: {error}"));
            return findings;
        }
        if (data is null)
        {
            return findings;
        }

        foreach (StorageDriveHealthData drive in data.Drives.Where(d => d.PredictFailure))
        {
            string suffix = SanitizeIdPart(drive.Name);
            findings.Add(new FindingDto
            {
                FindingId = $"storage.disk.health.critical.{suffix}",
                RuleId = RuleId,
                Category = FindingCategory.Storage,
                Severity = FindingSeverity.Critical,
                Title = $"SMART-Alarm auf Laufwerk {drive.Name}",
                Summary = "Das Laufwerk meldet uber SMART einen moglichen Ausfall.",
                DetailsMarkdown = "Erstellen Sie sofort ein Backup und prufen Sie einen Austausch des Laufwerks.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["drive"] = drive.Name,
                    ["smart_available"] = drive.SmartAvailable.ToString(),
                    ["predict_failure"] = drive.PredictFailure.ToString(),
                    ["health_state"] = drive.HealthState.ToString(),
                    ["health_details"] = string.Join(" | ", drive.HealthDetails),
                    ["last_checked_utc"] = drive.LastCheckedUtc.ToString("O")
                }
            });
        }

        int criticalThreshold = Math.Clamp(context.Thresholds.StorageCriticalPercentFree, 1, 50);
        int warningThreshold = Math.Clamp(context.Thresholds.StorageWarningPercentFree, criticalThreshold + 1, 80);

        FindingSeverity? severity;
        if (data.PercentFree < criticalThreshold)
        {
            severity = FindingSeverity.Critical;
        }
        else if (data.PercentFree < warningThreshold)
        {
            severity = FindingSeverity.Warning;
        }
        else
        {
            severity = null;
        }

        if (!severity.HasValue)
        {
            // Keep low-space finding suppressed when enough free storage,
            // but still provide space-consumer analysis below.
        }
        else
        {
            double freeGb = Math.Round(data.FreeBytes / 1024d / 1024d / 1024d, 1);
            double totalGb = Math.Round(data.TotalBytes / 1024d / 1024d / 1024d, 1);

            var finding = new FindingDto
            {
                FindingId = "storage.system.low_space",
                RuleId = RuleId,
                Category = FindingCategory.Storage,
                Severity = severity.Value,
                Title = "Wenig freier Speicher auf dem Systemlaufwerk",
                Summary = $"{data.SystemDrive} frei={freeGb:0.0}GB ({data.PercentFree:0.0}%).",
                DetailsMarkdown =
                    $"Bereinigen Sie temporare Dateien oder verschieben Sie Daten auf ein anderes Laufwerk.\n\n" +
                    $"Aktive Schwellwerte: kritisch < {criticalThreshold}% frei, warnung < {warningThreshold}% frei.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["system_drive"] = data.SystemDrive,
                    ["free_bytes"] = data.FreeBytes.ToString(),
                    ["total_bytes"] = data.TotalBytes.ToString(),
                    ["percent_free"] = data.PercentFree.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["critical_threshold_percent"] = criticalThreshold.ToString(),
                    ["warning_threshold_percent"] = warningThreshold.ToString()
                }
            };

            finding.Actions.Add(new ActionDto
            {
                ActionId = ActionIds.StorageOpenCleanup,
                Label = "Speicherbereinigung offnen",
                Kind = ActionKind.OpenExternal,
                ExternalTarget = "ms-settings:storagesense",
                DetailsMarkdown = "Offnet die Windows-Speichereinstellungen (Storage Sense).",
                IsSafeForOneClickMaintenance = true,
                RequiresAdmin = false,
                MayRequireRestart = false
            });

            findings.Add(finding);
        }

        long totalConsumers = data.DownloadsBytes + data.TempBytes + data.WindowsUpdateCacheBytes + data.RecycleBinBytes;
        if (totalConsumers > 0)
        {
            findings.Add(new FindingDto
            {
                FindingId = "storage.analysis.breakdown",
                RuleId = RuleId,
                Category = FindingCategory.Storage,
                Severity = FindingSeverity.Info,
                Title = "Was frisst Platz",
                Summary = "Breakdown fuer Downloads, Temp, Update-Cache und Papierkorb verfuegbar.",
                DetailsMarkdown = "Nutzen Sie die Storage-Analyse, um grosse Verzeichnisse gezielt zu bereinigen.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["downloads_bytes"] = data.DownloadsBytes.ToString(),
                    ["temp_bytes"] = data.TempBytes.ToString(),
                    ["windows_update_cache_bytes"] = data.WindowsUpdateCacheBytes.ToString(),
                    ["recycle_bin_bytes"] = data.RecycleBinBytes.ToString(),
                    ["top_consumers"] = string.Join("|", data.TopConsumers.Select(x => $"{x.Name}:{x.Bytes}"))
                }
            });
        }

        return findings;
    }

    private static string SanitizeIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        Span<char> buffer = stackalloc char[value.Length];
        int idx = 0;
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[idx++] = char.ToLowerInvariant(ch);
            }
            else if (idx == 0 || buffer[idx - 1] != '_')
            {
                buffer[idx++] = '_';
            }
        }

        string sanitized = new string(buffer[..idx]).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
