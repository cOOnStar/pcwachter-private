using System.Text.Json;
using Microsoft.Win32;
using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Remediations;

internal sealed class StartupDisableRemediation : IRemediation
{
    public const string Id = "remediation.startup.disable";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UndoKeyPath = @"Software\PCWachter\StartupUndo";

    public string RemediationId => Id;

    public Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken)
    {
        Report(progress, 10, "Pruefe Startup-Eintrag...");

        string location = ReadParam(request, "location");
        string name = ReadParam(request, "name");
        string entryKey = ReadParam(request, "entry_key");

        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(new RemediationResult
            {
                Success = false,
                ExitCode = 10,
                Message = "Startup-Parameter fehlen (location/name)."
            });
        }

        if (request.SimulationMode)
        {
            Report(progress, 100, "Simulation abgeschlossen");
            return Task.FromResult(new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = $"Simulation: Startup-Eintrag '{name}' deaktiviert."
            });
        }

        try
        {
            (RegistryKey root, string normalizedLocation) = ResolveRoot(location);
            using RegistryKey? runKey = root.OpenSubKey(RunKeyPath, writable: true);
            if (runKey is null)
            {
                return Task.FromResult(new RemediationResult
                {
                    Success = false,
                    ExitCode = 11,
                    Message = "Run-Registrypfad nicht verfuegbar."
                });
            }

            string? command = runKey.GetValue(name)?.ToString();
            if (string.IsNullOrWhiteSpace(command))
            {
                return Task.FromResult(new RemediationResult
                {
                    Success = true,
                    ExitCode = 0,
                    Message = $"Startup-Eintrag '{name}' ist bereits deaktiviert oder nicht vorhanden."
                });
            }

            var backup = new StartupUndoEntryDto
            {
                ItemKey = string.IsNullOrWhiteSpace(entryKey) ? BuildEntryKey(normalizedLocation, name) : entryKey,
                Location = normalizedLocation,
                Name = name,
                Command = command,
                DisabledAtUtc = DateTimeOffset.UtcNow
            };

            using RegistryKey undoKey = root.CreateSubKey(UndoKeyPath, writable: true)!;
            undoKey.SetValue(backup.ItemKey, JsonSerializer.Serialize(backup), RegistryValueKind.String);
            runKey.DeleteValue(name, throwOnMissingValue: false);

            Report(progress, 100, "Startup-Eintrag deaktiviert");
            return Task.FromResult(new RemediationResult
            {
                Success = true,
                ExitCode = 0,
                Message = $"Startup-Eintrag '{name}' wurde deaktiviert."
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

    private static (RegistryKey Root, string NormalizedLocation) ResolveRoot(string location)
    {
        if (location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase))
        {
            return (Registry.LocalMachine, "HKLM_RUN");
        }

        return (Registry.CurrentUser, "HKCU_RUN");
    }

    private static string ReadParam(RemediationRequest request, string key)
    {
        return request.Parameters.TryGetValue(key, out string? value) ? value : string.Empty;
    }

    private static string BuildEntryKey(string location, string name)
    {
        string raw = $"{location}:{name}".ToLowerInvariant();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
    }

    private static void Report(IProgress<ActionProgressDto>? progress, int percent, string message)
    {
        progress?.Report(new ActionProgressDto
        {
            ActionId = ActionIds.StartupDisable,
            Percent = percent,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        });
    }
}
