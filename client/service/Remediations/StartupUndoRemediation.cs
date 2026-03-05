using System.Text.Json;
using Microsoft.Win32;
using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class StartupUndoRemediation : IRemediation
{
    public const string Id = "remediation.startup.undo";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UndoKeyPath = @"Software\PCWachter\StartupUndo";

    public string RemediationId => Id;

    public Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 10, "Suche Undo-Daten...");

        string entryKey = ReadParam(request, "entry_key");
        string location = ReadParam(request, "location");
        if (string.IsNullOrWhiteSpace(entryKey))
        {
            return Task.FromResult(new RemediationResult
            {
                Success = false,
                ExitCode = 10,
                Message = "Undo-Key fehlt."
            });
        }

        if (request.SimulationMode)
        {
            Report(progress, 100, "Simulation abgeschlossen");
            return Task.FromResult(new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = "Simulation: Startup-Eintrag wiederhergestellt."
            });
        }

        try
        {
            RegistryKey[] roots = ResolveRoots(location);
            foreach (RegistryKey root in roots)
            {
                using RegistryKey? undoKey = root.OpenSubKey(UndoKeyPath, writable: true);
                string? raw = undoKey?.GetValue(entryKey)?.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                StartupUndoEntryDto? backup = JsonSerializer.Deserialize<StartupUndoEntryDto>(raw);
                if (backup is null || string.IsNullOrWhiteSpace(backup.Name))
                {
                    continue;
                }

                using RegistryKey runKey = root.CreateSubKey(RunKeyPath, writable: true)!;
                runKey.SetValue(backup.Name, backup.Command, RegistryValueKind.String);
                undoKey!.DeleteValue(entryKey, throwOnMissingValue: false);

                Report(progress, 100, "Undo abgeschlossen");
                return Task.FromResult(new RemediationResult
                {
                    Success = true,
                    ExitCode = 0,
                    Message = $"Startup-Eintrag '{backup.Name}' wurde wiederhergestellt."
                });
            }

            return Task.FromResult(new RemediationResult
            {
                Success = false,
                ExitCode = 11,
                Message = "Kein passender Undo-Eintrag gefunden."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RemediationResult
            {
                Success = false,
                ExitCode = 12,
                Message = ex.Message
            });
        }
    }

    private static RegistryKey[] ResolveRoots(string location)
    {
        if (location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase))
        {
            return [Registry.LocalMachine];
        }

        if (location.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            return [Registry.CurrentUser];
        }

        return [Registry.CurrentUser, Registry.LocalMachine];
    }

    private static string ReadParam(RemediationRequest request, string key)
    {
        return request.Parameters.TryGetValue(key, out string? value) ? value : string.Empty;
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.StartupUndo,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
