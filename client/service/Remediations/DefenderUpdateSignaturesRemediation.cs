using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class DefenderUpdateSignaturesRemediation : IRemediation
{
    public const string Id = "remediation.defender.update_signatures";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 5, "Starte Defender-Signaturupdate");

        if (request.SimulationMode)
        {
            await Task.Delay(300, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Simulation: Signaturupdate erfolgreich"
            };
        }

        string command = "Update-MpSignature";
        Report(progress, 35, "Fuehre Update-MpSignature aus");
        var result = await PowerShellRunner.RunAsync(command, TimeSpan.FromMinutes(2), cancellationToken);

        if (result.TimedOut)
        {
            return new RemediationResult
            {
                Success = false,
                ExitCode = 1,
                Message = "Timeout beim Signaturupdate."
            };
        }

        bool success = result.ExitCode == 0;
        Report(progress, 100, success ? "Signaturupdate abgeschlossen" : "Signaturupdate fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = result.ExitCode,
            Message = success ? "Signaturupdate erfolgreich" : (string.IsNullOrWhiteSpace(result.StdErr) ? "Signaturupdate fehlgeschlagen" : result.StdErr.Trim())
        };
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.DefenderUpdateSignatures,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
