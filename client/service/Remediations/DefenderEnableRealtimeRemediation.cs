using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class DefenderEnableRealtimeRemediation : IRemediation
{
    public const string Id = "remediation.defender.enable_realtime";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 5, "Pruefe Echtzeitschutz");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Simulation: Echtzeitschutz aktiviert"
            };
        }

        string command = "Set-MpPreference -DisableRealtimeMonitoring $false";
        Report(progress, 40, "Aktiviere Echtzeitschutz");
        var result = await PowerShellRunner.RunAsync(command, TimeSpan.FromMinutes(1), cancellationToken);

        if (result.TimedOut)
        {
            return new RemediationResult
            {
                Success = false,
                ExitCode = 1,
                Message = "Timeout beim Aktivieren des Echtzeitschutzes."
            };
        }

        bool success = result.ExitCode == 0;
        Report(progress, 100, success ? "Echtzeitschutz aktiviert" : "Aktivierung fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = result.ExitCode,
            Message = success ? "Echtzeitschutz aktiviert" : (string.IsNullOrWhiteSpace(result.StdErr) ? "Aktivierung fehlgeschlagen" : result.StdErr.Trim())
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.DefenderEnableRealtime,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
