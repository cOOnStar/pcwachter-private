using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class WindowsInstallSecurityUpdatesRemediation : IRemediation
{
    public const string Id = "remediation.updates.install_security";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 10, "Starte Windows Update Scan...");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: Sicherheitsupdates angestossen." };
        }

        ProcessExecutionResult scan = await ProcessRunner.RunAsync("UsoClient.exe", "StartScan", TimeSpan.FromSeconds(30), cancellationToken);
        ProcessExecutionResult download = await ProcessRunner.RunAsync("UsoClient.exe", "StartDownload", TimeSpan.FromSeconds(30), cancellationToken);
        ProcessExecutionResult install = await ProcessRunner.RunAsync("UsoClient.exe", "StartInstall", TimeSpan.FromSeconds(30), cancellationToken);

        bool success = !scan.TimedOut && !download.TimedOut && !install.TimedOut;
        Report(progress, 100, success ? "Windows Update angestossen" : "Windows Update teilweise fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : 1,
            Message = success
                ? "Windows Sicherheitsupdates wurden angestossen."
                : $"Update-Lauf unvollstaendig: Scan={scan.ExitCode}, Download={download.ExitCode}, Install={install.ExitCode}."
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.WindowsInstallSecurityUpdates,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
