using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class StartupAppsSensor : ISensor
{
    public const string Id = "sensor.startup_apps";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string PcwachterUndoPath = @"Software\PCWachter\StartupUndo";

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = new Dictionary<string, StartupEntryData>(StringComparer.OrdinalIgnoreCase);

            ReadRunEntries(Registry.CurrentUser, "HKCU_RUN", items);
            ReadRunEntries(Registry.LocalMachine, "HKLM_RUN", items);
            ReadStartupFolderEntries(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "STARTUP_USER",
                items);
            ReadStartupFolderEntries(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                "STARTUP_COMMON",
                items);

            foreach (StartupEntryData disabled in ReadDisabledEntries(Registry.CurrentUser, "HKCU_RUN"))
            {
                items[disabled.EntryKey] = disabled;
            }

            foreach (StartupEntryData disabled in ReadDisabledEntries(Registry.LocalMachine, "HKLM_RUN"))
            {
                items[disabled.EntryKey] = disabled;
            }

            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = new StartupAppsSensorData
                {
                    Entries = items.Values
                        .OrderByDescending(x => ImpactRank(x.Impact))
                        .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = false,
                Error = ex.Message
            });
        }
    }

    private static void ReadRunEntries(RegistryKey root, string location, Dictionary<string, StartupEntryData> result)
    {
        using RegistryKey? key = root.OpenSubKey(RunKeyPath, false);
        if (key is null)
        {
            return;
        }

        foreach (string valueName in key.GetValueNames())
        {
            string command = key.GetValue(valueName)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            string entryKey = ComputeEntryKey(location, valueName);
            result[entryKey] = new StartupEntryData
            {
                EntryKey = entryKey,
                Name = valueName,
                Command = command,
                Location = location,
                Impact = EstimateImpact(valueName, command),
                IsDisabledByPcwachter = false
            };
        }
    }

    private static void ReadStartupFolderEntries(string folderPath, string location, Dictionary<string, StartupEntryData> result)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(folderPath))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string command = file;
            string entryKey = ComputeEntryKey(location, name);

            result[entryKey] = new StartupEntryData
            {
                EntryKey = entryKey,
                Name = name,
                Command = command,
                Location = location,
                Impact = EstimateImpact(name, command),
                IsDisabledByPcwachter = false
            };
        }
    }

    private static IEnumerable<StartupEntryData> ReadDisabledEntries(RegistryKey root, string expectedLocation)
    {
        using RegistryKey? undoKey = root.OpenSubKey(PcwachterUndoPath, false);
        if (undoKey is null)
        {
            yield break;
        }

        foreach (string valueName in undoKey.GetValueNames())
        {
            string? raw = undoKey.GetValue(valueName)?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            StartupUndoEntryDto? backup = TryParseBackup(raw);
            if (backup is null)
            {
                continue;
            }

            if (!string.Equals(backup.Location, expectedLocation, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new StartupEntryData
            {
                EntryKey = backup.ItemKey,
                Name = backup.Name,
                Command = backup.Command,
                Location = backup.Location,
                Impact = EstimateImpact(backup.Name, backup.Command),
                IsDisabledByPcwachter = true
            };
        }
    }

    private static StartupUndoEntryDto? TryParseBackup(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StartupUndoEntryDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string EstimateImpact(string name, string command)
    {
        string combined = (name + " " + command).ToLowerInvariant();
        if (combined.Contains("steam") ||
            combined.Contains("discord") ||
            combined.Contains("teams") ||
            combined.Contains("adobe") ||
            combined.Contains("onedrive") ||
            combined.Contains("spotify") ||
            combined.Contains("epic") ||
            combined.Contains("riot"))
        {
            return "High";
        }

        if (combined.Contains("update") ||
            combined.Contains("helper") ||
            combined.Contains("agent") ||
            combined.Contains("launcher"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static int ImpactRank(string impact)
    {
        return impact.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }

    private static string ComputeEntryKey(string location, string name)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{location}:{name}".ToLowerInvariant()));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
