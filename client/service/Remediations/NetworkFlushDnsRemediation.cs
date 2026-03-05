using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class NetworkFlushDnsRemediation : IRemediation
{
    public const string Id = "remediation.network.flush_dns";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 20, "Leere DNS-Cache...");

        if (request.SimulationMode)
        {
            await Task.Delay(200, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: DNS-Cache geleert." };
        }

        ProcessExecutionResult result = await ProcessRunner.RunAsync("ipconfig.exe", "/flushdns", TimeSpan.FromSeconds(20), cancellationToken);
        bool success = !result.TimedOut && result.ExitCode == 0;
        Report(progress, 100, success ? "DNS-Cache geleert" : "DNS-Flush fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : result.ExitCode,
            Message = success
                ? "DNS-Cache wurde geleert."
                : string.IsNullOrWhiteSpace(result.StdErr) ? "DNS-Flush fehlgeschlagen." : result.StdErr.Trim()
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.NetworkFlushDns,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
