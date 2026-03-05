using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class WindowsInstallAllUpdatesRemediation : IRemediation
{
    public const string Id = "remediation.updates.install_all";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 10, "Starte Windows Update Interaktiv-Scan...");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: kompletter Windows-Update-Lauf angestossen." };
        }

        ProcessExecutionResult interactiveScan = await ProcessRunner.RunAsync("UsoClient.exe", "StartInteractiveScan", TimeSpan.FromSeconds(30), cancellationToken);
        ProcessExecutionResult scan = await ProcessRunner.RunAsync("UsoClient.exe", "StartScan", TimeSpan.FromSeconds(30), cancellationToken);
        ProcessExecutionResult download = await ProcessRunner.RunAsync("UsoClient.exe", "StartDownload", TimeSpan.FromSeconds(30), cancellationToken);
        ProcessExecutionResult install = await ProcessRunner.RunAsync("UsoClient.exe", "StartInstall", TimeSpan.FromSeconds(30), cancellationToken);

        bool success = !interactiveScan.TimedOut && !scan.TimedOut && !download.TimedOut && !install.TimedOut;
        Report(progress, 100, success ? "Windows Update komplett angestossen" : "Windows Update nur teilweise angestossen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : 1,
            Message = success
                ? "Kompletter Windows-Update-Lauf wurde angestossen (wie in Windows Einstellungen)."
                : $"Update-Lauf unvollstaendig: Interactive={interactiveScan.ExitCode}, Scan={scan.ExitCode}, Download={download.ExitCode}, Install={install.ExitCode}."
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.WindowsInstallAllUpdates,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
