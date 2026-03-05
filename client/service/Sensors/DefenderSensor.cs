using System.Management;
using System.Text.Json;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class DefenderSensor : ISensor
{
    public const string Id = "sensor.defender";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            DefenderSensorData? data = TryCollectViaWmi();
            if (data is not null)
            {
                PopulateDerived(data);
                return Success(data);
            }

            data = await TryCollectViaPowerShellAsync(cancellationToken);
            if (data is not null)
            {
                PopulateDerived(data);
                return Success(data);
            }

            return Failure("Unable to read Defender status via WMI or PowerShell.");
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private static DefenderSensorData? TryCollectViaWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\Microsoft\Windows\Defender",
                "SELECT RealTimeProtectionEnabled, AntivirusSignatureLastUpdated, IsTamperProtected, AMProductVersion, AMEngineVersion, AntivirusSignatureVersion FROM MSFT_MpComputerStatus");

            foreach (ManagementObject row in searcher.Get())
            {
                return new DefenderSensorData
                {
                    RealtimeProtectionEnabled = ConvertToNullableBool(row["RealTimeProtectionEnabled"]),
                    SignatureLastUpdatedUtc = ParseAnyDateTime(row["AntivirusSignatureLastUpdated"]),
                    TamperProtectionEnabled = ConvertToNullableBool(row["IsTamperProtected"]),
                    PlatformVersion = row["AMProductVersion"]?.ToString(),
                    EngineVersion = row["AMEngineVersion"]?.ToString(),
                    SignatureVersion = row["AntivirusSignatureVersion"]?.ToString(),
                    CanAttemptEnableRealtime = true,
                    Source = "wmi"
                };
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<DefenderSensorData?> TryCollectViaPowerShellAsync(CancellationToken cancellationToken)
    {
        string command = "Get-MpComputerStatus | Select-Object RealTimeProtectionEnabled,AntivirusSignatureLastUpdated,IsTamperProtected,AMProductVersion,AMEngineVersion,AntivirusSignatureVersion | ConvertTo-Json -Compress";
        var result = await PowerShellRunner.RunAsync(command, TimeSpan.FromSeconds(8), cancellationToken);

        if (result.TimedOut || result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            JsonElement root = doc.RootElement;

            return new DefenderSensorData
            {
                RealtimeProtectionEnabled = ReadNullableBool(root, "RealTimeProtectionEnabled"),
                SignatureLastUpdatedUtc = ReadNullableDate(root, "AntivirusSignatureLastUpdated"),
                TamperProtectionEnabled = ReadNullableBool(root, "IsTamperProtected"),
                PlatformVersion = ReadString(root, "AMProductVersion"),
                EngineVersion = ReadString(root, "AMEngineVersion"),
                SignatureVersion = ReadString(root, "AntivirusSignatureVersion"),
                CanAttemptEnableRealtime = true,
                Source = "powershell"
            };
        }
        catch
        {
            return null;
        }
    }

    private static void PopulateDerived(DefenderSensorData data)
    {
        if (!data.SignatureLastUpdatedUtc.HasValue)
        {
            return;
        }

        data.DaysSinceLastUpdate = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - data.SignatureLastUpdatedUtc.Value).TotalDays));
    }

    private static SensorResult Success(DefenderSensorData data)
    {
        return new SensorResult
        {
            SensorId = Id,
            Success = true,
            Payload = data
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

    private static bool? ConvertToNullableBool(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ParseAnyDateTime(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTime asDate)
        {
            return asDate.ToUniversalTime();
        }

        string raw = value.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            if (raw.Length >= 14 && raw[14] == '.')
            {
                return ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime();
            }
        }
        catch
        {
        }

        return DateTime.TryParse(raw, out DateTime parsed) ? parsed.ToUniversalTime() : null;
    }

    private static bool? ReadNullableBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out int number) => number != 0,
            JsonValueKind.String when bool.TryParse(value.GetString(), out bool parsed) => parsed,
            _ => null
        };
    }

    private static DateTime? ReadNullableDate(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out DateTime parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
