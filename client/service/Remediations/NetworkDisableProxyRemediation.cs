using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class NetworkDisableProxyRemediation : IRemediation
{
    public const string Id = "remediation.network.disable_proxy";

    public string RemediationId => Id;

    public async Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 20, "Deaktiviere Proxy...");

        if (request.SimulationMode)
        {
            await Task.Delay(200, cancellationToken);
            Report(progress, 100, "Simulation abgeschlossen");
            return new RemediationResult { Success = true, ExitCode = 0, Message = "Simulation: Proxy deaktiviert." };
        }

        ProcessExecutionResult regResult = await ProcessRunner.RunAsync(
            "reg.exe",
            "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f",
            TimeSpan.FromSeconds(20),
            cancellationToken);

        ProcessExecutionResult winHttpResult = await ProcessRunner.RunAsync(
            "netsh.exe",
            "winhttp reset proxy",
            TimeSpan.FromSeconds(20),
            cancellationToken);

        bool success = !regResult.TimedOut && regResult.ExitCode == 0 && !winHttpResult.TimedOut && winHttpResult.ExitCode == 0;
        Report(progress, 100, success ? "Proxy deaktiviert" : "Proxy-Deaktivierung fehlgeschlagen");

        return new RemediationResult
        {
            Success = success,
            ExitCode = success ? 0 : 1,
            Message = success
                ? "Proxy wurde deaktiviert."
                : BuildError(regResult, winHttpResult)
        };
    }

    private static string BuildError(ProcessExecutionResult regResult, ProcessExecutionResult winHttpResult)
    {
        if (!string.IsNullOrWhiteSpace(regResult.StdErr))
        {
            return regResult.StdErr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(winHttpResult.StdErr))
        {
            return winHttpResult.StdErr.Trim();
        }

        if (regResult.TimedOut || winHttpResult.TimedOut)
        {
            return "Proxy-Deaktivierung Timeout.";
        }

        return "Proxy-Deaktivierung fehlgeschlagen.";
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.NetworkDisableProxy,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
