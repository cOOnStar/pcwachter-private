using System.Management;
using System.Text.Json;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class FirewallSensor : ISensor
{
    public const string Id = "sensor.firewall";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            FirewallSensorData? data = TryCollectViaWmi();
            if (data is null || data.Profiles.Count == 0)
            {
                data = await TryCollectViaPowerShellAsync(cancellationToken);
            }

            if (data is null || data.Profiles.Count == 0)
            {
                return Failure("Unable to read firewall profile status.");
            }

            return Success(data);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private static FirewallSensorData? TryCollectViaWmi()
    {
        try
        {
            var data = new FirewallSensorData();
            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\StandardCimv2",
                "SELECT Name, Enabled FROM MSFT_NetFirewallProfile");

            foreach (ManagementObject row in searcher.Get())
            {
                string rawName = row["Name"]?.ToString() ?? "Unknown";
                bool? enabled = row["Enabled"] is null ? null : Convert.ToBoolean(row["Enabled"]);
                data.Profiles[NormalizeProfileName(rawName)] = enabled;
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<FirewallSensorData?> TryCollectViaPowerShellAsync(CancellationToken cancellationToken)
    {
        string command = "Get-NetFirewallProfile | Select-Object Name,Enabled | ConvertTo-Json -Compress";
        var result = await PowerShellRunner.RunAsync(command, TimeSpan.FromSeconds(8), cancellationToken);
        if (result.TimedOut || result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            var data = new FirewallSensorData();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    AddProfile(element, data);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddProfile(doc.RootElement, data);
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    private static void AddProfile(JsonElement element, FirewallSensorData data)
    {
        string profileName = element.TryGetProperty("Name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? NormalizeProfileName(nameElement.GetString() ?? "Unknown")
            : "Unknown";

        bool? enabled = null;
        if (element.TryGetProperty("Enabled", out JsonElement enabledElement))
        {
            enabled = enabledElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when enabledElement.TryGetInt32(out int number) => number != 0,
                _ => null
            };
        }

        data.Profiles[profileName] = enabled;
    }

    private static string NormalizeProfileName(string name)
    {
        if (name.Equals("1", StringComparison.OrdinalIgnoreCase) || name.Contains("Domain", StringComparison.OrdinalIgnoreCase))
        {
            return "Domain";
        }

        if (name.Equals("2", StringComparison.OrdinalIgnoreCase) || name.Contains("Private", StringComparison.OrdinalIgnoreCase))
        {
            return "Private";
        }

        if (name.Equals("4", StringComparison.OrdinalIgnoreCase) || name.Contains("Public", StringComparison.OrdinalIgnoreCase))
        {
            return "Public";
        }

        return name;
    }

    private static SensorResult Success(FirewallSensorData payload)
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
