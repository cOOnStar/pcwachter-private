using AgentService.Remediations;
using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal sealed class NetworkDiagnosticsRule : IRule
{
    public const string Id = "rule.network_diagnostics";

    public string RuleId => Id;

    public IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context)
    {
        var findings = new List<FindingDto>();

        if (!RuleHelpers.TryGetPayload<NetworkDiagnosticsSensorData>(sensorResults, NetworkDiagnosticsSensor.Id, out var data, out var error))
        {
            findings.Add(RuleHelpers.SensorFailureFinding(
                "health.sensor.network_diagnostics.failed",
                RuleId,
                "Netzwerkdiagnose konnte nicht gelesen werden",
                $"Netzwerk-Sensor fehlgeschlagen: {error}"));
            return findings;
        }

        if (data is null)
        {
            return findings;
        }

        string latencyGateway = data.GatewayLatencyMs.HasValue ? $"{data.GatewayLatencyMs.Value}ms" : "-";
        string latencyDns = data.PublicDnsLatencyMs.HasValue ? $"{data.PublicDnsLatencyMs.Value}ms" : "-";

        var summaryFinding = new FindingDto
        {
            FindingId = "network.diagnostics.summary",
            RuleId = RuleId,
            Category = FindingCategory.System,
            Severity = FindingSeverity.Info,
            Title = "Netzwerk Diagnose",
            Summary = $"Adapter: {data.AdapterSummary} | Gateway: {latencyGateway} | DNS: {latencyDns}",
            DetailsMarkdown = "Quick-Fixes: DNS flush, Proxy deaktivieren, Adapter neu starten.",
            DetectedAtUtc = context.NowUtc,
            Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["has_internet"] = data.HasInternet.ToString(),
                ["gateway_reachable"] = data.GatewayReachable.ToString(),
                ["public_dns_reachable"] = data.PublicDnsReachable.ToString(),
                ["proxy_enabled"] = data.ProxyEnabled.ToString(),
                ["adapter_summary"] = data.AdapterSummary,
                ["gateway_latency_ms"] = data.GatewayLatencyMs?.ToString() ?? "-",
                ["public_dns_latency_ms"] = data.PublicDnsLatencyMs?.ToString() ?? "-"
            }
        };

        summaryFinding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.NetworkFlushDns,
            Label = "DNS Cache leeren",
            Kind = ActionKind.RunRemediation,
            RemediationId = NetworkFlushDnsRemediation.Id,
            IsSafeForOneClickMaintenance = true,
            RequiresAdmin = true,
            MayRequireRestart = false
        });
        summaryFinding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.NetworkDisableProxy,
            Label = "Proxy deaktivieren",
            Kind = ActionKind.RunRemediation,
            RemediationId = NetworkDisableProxyRemediation.Id,
            IsSafeForOneClickMaintenance = false,
            RequiresAdmin = false,
            MayRequireRestart = false
        });
        summaryFinding.Actions.Add(new ActionDto
        {
            ActionId = ActionIds.NetworkResetAdapters,
            Label = "Adapter neu starten",
            Kind = ActionKind.RunRemediation,
            RemediationId = NetworkResetAdaptersRemediation.Id,
            IsSafeForOneClickMaintenance = false,
            RequiresAdmin = true,
            MayRequireRestart = false
        });
        findings.Add(summaryFinding);

        if (!data.HasInternet || !data.PublicDnsReachable)
        {
            findings.Add(new FindingDto
            {
                FindingId = "network.diagnostics.offline",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Critical,
                Title = "Internet instabil oder nicht erreichbar",
                Summary = data.HasInternet
                    ? "Lokale Verbindung vorhanden, aber externer DNS-Endpunkt nicht erreichbar."
                    : "Keine aktive Internetverbindung erkannt.",
                DetailsMarkdown = "Fuehren Sie die Quick-Fixes aus und pruefen Sie Router/Gateway.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["has_internet"] = data.HasInternet.ToString(),
                    ["public_dns_reachable"] = data.PublicDnsReachable.ToString()
                }
            });
        }

        if (data.ProxyEnabled)
        {
            findings.Add(new FindingDto
            {
                FindingId = "network.proxy.enabled",
                RuleId = RuleId,
                Category = FindingCategory.System,
                Severity = FindingSeverity.Warning,
                Title = "Proxy ist aktiviert",
                Summary = "Ein aktiver Proxy kann Verbindungsprobleme verursachen.",
                DetailsMarkdown = "Nur deaktivieren, wenn kein Unternehmens-Proxy benoetigt wird.",
                DetectedAtUtc = context.NowUtc,
                Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["proxy_enabled"] = data.ProxyEnabled.ToString()
                }
            });
        }

        return findings;
    }
}
