using System.Management;
using AgentService.Runtime;
using Microsoft.Win32;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class BitLockerSensor : ISensor
{
    public const string Id = "sensor.bitlocker";

    public string SensorId => Id;

    public async Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            BitLockerSensorData? data = TryCollectViaWmi();
            if (data is null)
            {
                data = await TryCollectViaManageBdeAsync(cancellationToken);
            }

            if (data is null)
            {
                return Failure("Unable to determine BitLocker status.");
            }

            data.IsWindowsHomeEdition = IsHomeEdition();
            return Success(data);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    private static BitLockerSensorData? TryCollectViaWmi()
    {
        try
        {
            string drive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\');
            string query = $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = '{drive}'";

            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2\Security\MicrosoftVolumeEncryption",
                query);

            foreach (ManagementObject volume in searcher.Get())
            {
                uint? protectionStatus = InvokeUInt(volume, "GetProtectionStatus", "ProtectionStatus");
                uint? encryptionMethod = InvokeUInt(volume, "GetEncryptionMethod", "EncryptionMethod");

                bool? hasKeyProtector = null;
                try
                {
                    using var inParams = volume.GetMethodParameters("GetKeyProtectors");
                    inParams["KeyProtectorType"] = 0;
                    using var outParams = volume.InvokeMethod("GetKeyProtectors", inParams, null);
                    var ids = outParams?["VolumeKeyProtectorID"] as string[];
                    hasKeyProtector = ids is { Length: > 0 };
                }
                catch
                {
                }

                return new BitLockerSensorData
                {
                    SystemDrive = drive,
                    IsProtectionOn = protectionStatus == 1,
                    ProtectionStatusRaw = protectionStatus?.ToString(),
                    EncryptionMethod = MapEncryptionMethod(encryptionMethod),
                    HasKeyProtector = hasKeyProtector,
                    Source = "wmi"
                };
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<BitLockerSensorData?> TryCollectViaManageBdeAsync(CancellationToken cancellationToken)
    {
        string drive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\');
        var result = await ProcessRunner.RunAsync("manage-bde.exe", $"-status {drive}", TimeSpan.FromSeconds(10), cancellationToken);
        if (result.TimedOut || result.ExitCode != 0)
        {
            return null;
        }

        string text = string.Concat(result.StdOut, Environment.NewLine, result.StdErr);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        bool? protectionOn = null;
        string? encryptionMethod = null;
        bool? hasKeyProtector = null;

        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Contains("Protection Status", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Schutzstatus", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains("On", StringComparison.OrdinalIgnoreCase) || line.Contains("Ein", StringComparison.OrdinalIgnoreCase) || line.Contains("Aktiv", StringComparison.OrdinalIgnoreCase))
                {
                    protectionOn = true;
                }
                else if (line.Contains("Off", StringComparison.OrdinalIgnoreCase) || line.Contains("Aus", StringComparison.OrdinalIgnoreCase) || line.Contains("Deaktiv", StringComparison.OrdinalIgnoreCase))
                {
                    protectionOn = false;
                }
            }
            else if (line.Contains("Encryption Method", StringComparison.OrdinalIgnoreCase) || line.Contains("Verschluesselungsmethode", StringComparison.OrdinalIgnoreCase))
            {
                int idx = line.IndexOf(':');
                encryptionMethod = idx >= 0 ? line[(idx + 1)..].Trim() : line;
            }
            else if (line.Contains("Key Protectors", StringComparison.OrdinalIgnoreCase) || line.Contains("Schluetzerschluessel", StringComparison.OrdinalIgnoreCase))
            {
                hasKeyProtector = true;
            }
        }

        return new BitLockerSensorData
        {
            SystemDrive = drive,
            IsProtectionOn = protectionOn,
            EncryptionMethod = encryptionMethod,
            HasKeyProtector = hasKeyProtector,
            ProtectionStatusRaw = protectionOn.HasValue ? (protectionOn.Value ? "1" : "0") : null,
            Source = "manage-bde"
        };
    }

    private static bool IsHomeEdition()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string edition = key?.GetValue("EditionID")?.ToString() ?? string.Empty;
            return edition.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || edition.Contains("Home", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static uint? InvokeUInt(ManagementObject volume, string methodName, string outField)
    {
        try
        {
            using var outParams = volume.InvokeMethod(methodName, null, null);
            if (outParams is null)
            {
                return null;
            }

            object? value = outParams[outField];
            return value is null ? null : Convert.ToUInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? MapEncryptionMethod(uint? method)
    {
        return method switch
        {
            0 => "None",
            1 => "AES_128_DIFFUSER",
            2 => "AES_256_DIFFUSER",
            3 => "AES_128",
            4 => "AES_256",
            5 => "HARDWARE",
            6 => "XTS_AES_128",
            7 => "XTS_AES_256",
            _ => method?.ToString()
        };
    }

    private static SensorResult Success(BitLockerSensorData payload)
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
