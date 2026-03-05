namespace AgentService.Runtime;

internal static class PowerShellRunner
{
    public static Task<ProcessExecutionResult> RunAsync(string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string escaped = command.Replace("\"", "\\\"");
        string args = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{escaped}\"";
        return ProcessRunner.RunAsync("powershell.exe", args, timeout, cancellationToken);
    }
}
