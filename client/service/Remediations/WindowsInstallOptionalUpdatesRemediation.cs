using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class WindowsInstallOptionalUpdatesRemediation : IRemediation
{
    public const string Id = "remediation.updates.install_optional";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 15, "Starte optionalen Update-Scan...");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: Optionale Updates angestossen." };
        }

        ProcessExecutionResult interactive = await ProcessRunner.RunAsync("UsoClient.exe", "StartInteractiveScan", TimeSpan.FromSeconds(30), cancellationToken);
        bool success = !interactive.TimedOut;
        Report(progress, 100, success ? "Optionale Updates angestossen" : "Optionale Updates fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : 1,
            Message = success
                ? "Optionale Updates wurden angestossen. Pruefen Sie Windows Update fuer Details."
                : $"Optionaler Update-Scan fehlgeschlagen (ExitCode={interactive.ExitCode})."
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.WindowsInstallOptionalUpdates,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
