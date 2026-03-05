using System.Text.Json;
using AgentService.Runtime;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class WindowsUpdatesSensor : ISensor
{
    public const string Id = "sensor.windows_updates";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            string script =
                "$ErrorActionPreference='Stop';" +
                "$session = New-Object -ComObject Microsoft.Update.Session;" +
                "$searcher = $session.CreateUpdateSearcher();" +
                "$result = $searcher.Search(\"IsInstalled=0 and IsHidden=0\");" +
                "$security=0; $optional=0; $drivers=0; $titles = New-Object System.Collections.Generic.List[string];" +
                "foreach($u in $result.Updates){" +
                "  $isSecurity=$false; $isDriver=$false;" +
                "  foreach($c in $u.Categories){" +
                "    if($c.Name -match 'Security'){ $isSecurity=$true }" +
                "    if($c.Name -match 'Driver'){ $isDriver=$true }" +
                "  }" +
                "  if($isSecurity){ $security++ } elseif($isDriver){ $drivers++ } else { $optional++ }" +
                "  if($titles.Count -lt 6){ [void]$titles.Add($u.Title) }" +
                "}" +
                "[pscustomobject]@{SecurityCount=$security; OptionalSoftwareCount=$optional; DriverCount=$drivers; TopTitles=$titles} | ConvertTo-Json -Compress;";

            ProcessExecutionResult result = await PowerShellRunner.RunAsync(script, TimeSpan.FromSeconds(35), cancellationToken);
            if (result.TimedOut)
            {
                return Failure("Windows-Update-Abfrage Timeout.");
            }

            if (result.ExitCode != 0)
            {
                return Failure(string.IsNullOrWhiteSpace(result.StdErr) ? "Windows-Update-Abfrage fehlgeschlagen." : result.StdErr.Trim());
            }

            WindowsUpdatesSensorData parsed = ParsePayload(result.StdOut);
            return new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = parsed
            };
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private static WindowsUpdatesSensorData ParsePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WindowsUpdatesSensorData();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            var data = new WindowsUpdatesSensorData
            {
                SecurityCount = ReadInt(root, "SecurityCount"),
                OptionalSoftwareCount = ReadInt(root, "OptionalSoftwareCount"),
                DriverCount = ReadInt(root, "DriverCount")
            };

            if (root.TryGetProperty("TopTitles", out JsonElement titles) && titles.ValueKind == JsonValueKind.Array)
            {
                data.TopTitles = titles.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(6)
                    .ToList();
            }

            return data;
        }
        catch
        {
            return new WindowsUpdatesSensorData();
        }
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
        {
            return parsed;
        }

        return 0;
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
