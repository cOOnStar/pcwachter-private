using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class AppsUpdateSelectedRemediation : IRemediation
{
    public const string Id = "remediation.apps.update_selected";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 5, "Pruefe ausgewaehlte Programme...");

        List<string> selected = ExtractPackageIds(request);
        if (selected.Count == 0)
        {
            return new RemediationResult
            {
                Success = false,
                ExitCode = 10,
                Message = "Keine Programme fuer das Update ausgewaehlt."
            };
        }

        if (request.SimulationMode)
        {
            await Task.Delay(250, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = $"Simulation: {selected.Count} App-Updates geplant."
            };
        }

        int success = 0;
        var failed = new List<string>();

        for (int i = 0; i < selected.Count; i++)
        {
            string packageId = selected[i];
            int percent = 10 + (int)Math.Round((i / (double)Math.Max(1, selected.Count)) * 80d);
            Report(progress, percent, $"Aktualisiere {packageId}...");

            string args = $"upgrade --id \"{packageId}\" --accept-source-agreements --accept-package-agreements --silent";
            ProcessExecutionResult result = await ProcessRunner.RunAsync("winget.exe", args, TimeSpan.FromMinutes(4), cancellationToken);
            if (!result.TimedOut && result.ExitCode == 0)
            {
                success++;
            }
            else
            {
                failed.Add(packageId);
            }
        }

        bool overallSuccess = failed.Count == 0;
        Report(progress, 100, overallSuccess
            ? $"App-Updates abgeschlossen ({success}/{selected.Count})."
            : $"App-Updates teilweise abgeschlossen ({success}/{selected.Count}).");

        return new RemediationResult
        {
            Success = overallSuccess,
            ExitCode = overallSuccess ? 0 : 1,
            Message = overallSuccess
                ? $"Alle {success} ausgewaehlten Programme wurden aktualisiert."
                : $"{success} von {selected.Count} Programmen aktualisiert. Fehlgeschlagen: {string.Join(", ", failed)}"
        };
    }

    private static List<string> ExtractPackageIds(RemediationRequest request)
    {
        if (request.Parameters.TryGetValue("package_ids", out string? raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            return raw.Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalTarget))
        {
            return [request.ExternalTarget.Trim()];
        }

        return [];
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.AppsUpdateSelected,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
