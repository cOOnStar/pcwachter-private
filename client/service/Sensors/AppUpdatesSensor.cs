using System.Text.Json;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class AppUpdatesSensor : ISensor
{
    public const string Id = "sensor.app_updates";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProcessExecutionResult versionResult = await ProcessRunner.RunAsync(
                "winget.exe",
                "--version",
                TimeSpan.FromSeconds(8),
                cancellationToken);

            bool wingetAvailable = !versionResult.TimedOut && versionResult.ExitCode == 0;
            string wingetVersion = ExtractFirstNonEmptyLine(versionResult.StdOut);

            var payload = new AppUpdatesSensorData
            {
                WingetAvailable = wingetAvailable,
                WingetVersion = wingetVersion
            };

            if (!wingetAvailable)
            {
                return Success(payload);
            }

            ProcessExecutionResult upgradesResult = await ProcessRunner.RunAsync(
                "winget.exe",
                "upgrade --include-unknown --accept-source-agreements --accept-package-agreements --output json",
                TimeSpan.FromSeconds(60),
                cancellationToken);

            if (upgradesResult.TimedOut)
            {
                return Failure("winget upgrade timed out.");
            }

            if (string.IsNullOrWhiteSpace(upgradesResult.StdOut))
            {
                // No data means no update candidates.
                return Success(payload);
            }

            List<AppUpdateItemData> updates = ParseUpdates(upgradesResult.StdOut);
            payload.Updates = updates
                .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
                .GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Success(payload);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private static List<AppUpdateItemData> ParseUpdates(string json)
    {
        var items = new List<AppUpdateItemData>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            CollectItems(doc.RootElement, items);
        }
        catch
        {
            // Keep sensor resilient on output format changes.
        }

        return items;
    }

    private static void CollectItems(JsonElement element, List<AppUpdateItemData> items)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                {
                    CollectItems(child, items);
                }

                return;
            case JsonValueKind.Object:
                TryCollectFromObject(element, items);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectItems(property.Value, items);
                }

                return;
            default:
                return;
        }
    }

    private static void TryCollectFromObject(JsonElement obj, List<AppUpdateItemData> items)
    {
        string packageId = ReadString(obj, "PackageIdentifier")
                           ?? ReadString(obj, "PackageId")
                           ?? ReadString(obj, "Id")
                           ?? string.Empty;
        string availableVersion = ReadString(obj, "AvailableVersion")
                                  ?? ReadString(obj, "Available")
                                  ?? string.Empty;

        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(availableVersion))
        {
            return;
        }

        items.Add(new AppUpdateItemData
        {
            PackageId = packageId.Trim(),
            Name = ReadString(obj, "PackageName") ?? ReadString(obj, "Name") ?? packageId.Trim(),
            InstalledVersion = ReadString(obj, "InstalledVersion") ?? ReadString(obj, "Version") ?? "-",
            AvailableVersion = availableVersion.Trim(),
            Source = ReadString(obj, "Source") ?? "winget"
        });
    }

    private static string? ReadString(JsonElement obj, string name)
    {
        return obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string ExtractFirstNonEmptyLine(string text)
    {
        foreach (string line in text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }

    private static SensorResult Success(AppUpdatesSensorData payload)
    {
        return new SensorResult
        {
            SensorId = Id,
            Success = true,
            Payload = payload
        };
    }

    private static SensorResult Failure(string error)
    {
        return new SensorResult
        {
            SensorId = Id,
            Success = false,
            Error = error
        };
    }
}
