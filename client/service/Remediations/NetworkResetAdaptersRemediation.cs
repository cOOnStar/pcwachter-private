using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class NetworkResetAdaptersRemediation : IRemediation
{
    public const string Id = "remediation.network.reset_adapters";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 15, "Starte Adapter-Reset...");

        if (request.SimulationMode)
        {
            await Task.Delay(200, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: Adapter-Reset ausgefuehrt." };
        }

        string script =
            "$ErrorActionPreference='SilentlyContinue';" +
            "$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.HardwareInterface -eq $true };" +
            "foreach($a in $adapters){ Disable-NetAdapter -Name $a.Name -Confirm:$false -PassThru | Out-Null; Start-Sleep -Milliseconds 700; Enable-NetAdapter -Name $a.Name -Confirm:$false -PassThru | Out-Null };" +
            "Write-Output ('RESET_COUNT=' + ($adapters | Measure-Object).Count);";

        ProcessExecutionResult result = await PowerShellRunner.RunAsync(script, TimeSpan.FromSeconds(40), cancellationToken);
        bool success = !result.TimedOut && result.ExitCode == 0;
        Report(progress, 100, success ? "Adapter neu gestartet" : "Adapter-Reset fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : result.ExitCode,
            Message = success
                ? "Netzwerkadapter wurden neu gestartet."
                : string.IsNullOrWhiteSpace(result.StdErr) ? "Adapter-Reset fehlgeschlagen." : result.StdErr.Trim()
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.NetworkResetAdapters,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
