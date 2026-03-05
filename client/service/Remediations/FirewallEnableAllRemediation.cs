using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class FirewallEnableAllRemediation : IRemediation
{
    public const string Id = "remediation.firewall.enable_all";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 5, "Starte Firewall-Reparatur");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Simulation: Firewall-Profile aktiviert"
            };
        }

        Report(progress, 35, "Aktiviere Profile via PowerShell");
        var psResult = await PowerShellRunner.RunAsync("Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True", TimeSpan.FromSeconds(30), cancellationToken);
        if (!psResult.TimedOut && psResult.ExitCode == 0)
        {
            Report(progress, 100, "Firewall-Profile aktiviert");
            return new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Firewall-Profile aktiviert"
            };
        }

        Report(progress, 65, "PowerShell fehlgeschlagen, versuche netsh");
        var netshResult = await ProcessRunner.RunAsync("netsh.exe", "advfirewall set allprofiles state on", TimeSpan.FromSeconds(20), cancellationToken);
        bool success = !netshResult.TimedOut && netshResult.ExitCode == 0;
        Report(progress, 100, success ? "Firewall-Profile aktiviert" : "Firewall-Reparatur fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : Math.Max(psResult.ExitCode, netshResult.ExitCode),
            Message = success
                ? "Firewall-Profile aktiviert"
                : BuildError(psResult, netshResult)
        };
    }

    private static string BuildError(ProcessExecutionResult psResult, ProcessExecutionResult netshResult)
    {
        if (psResult.TimedOut)
        {
            return "PowerShell-Timeout bei Firewall-Reparatur";
        }

        if (!string.IsNullOrWhiteSpace(psResult.StdErr))
        {
            return psResult.StdErr.Trim();
        }

        if (netshResult.TimedOut)
        {
            return "netsh-Timeout bei Firewall-Reparatur";
        }

        if (!string.IsNullOrWhiteSpace(netshResult.StdErr))
        {
            return netshResult.StdErr.Trim();
        }

        return "Firewall-Reparatur fehlgeschlagen";
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.FirewallEnableAll,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
