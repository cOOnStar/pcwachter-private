using AgentService.Runtime;
using Microsoft.Win32;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class SecurityHardeningSensor : ISensor
{
    public const string Id = "sensor.security_hardening";

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            (bool? smartScreenEnabled, string smartScreenMode) = ReadSmartScreen();
            (bool? cfaEnabled, string cfaMode) = ReadControlledFolderAccess();

            var payload = new SecurityHardeningSensorData
            {
                SmartScreenEnabled = smartScreenEnabled,
                SmartScreenMode = smartScreenMode,
                ControlledFolderAccessEnabled = cfaEnabled,
                ControlledFolderAccessMode = cfaMode,
                ExploitProtectionEnabled = ReadExploitProtection(),
                LsaProtectionEnabled = ReadLsaProtection(),
                CredentialGuardEnabled = ReadCredentialGuard(),
                Source = "registry"
            };

            return Task.FromResult(new SensorResult
            {
                SensorId = Id,
                Success = true,
                Payload = payload
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

    private static (bool? Enabled, string Mode) ReadSmartScreen()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
        string mode = key?.GetValue("SmartScreenEnabled")?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(mode))
        {
            return (null, "Unknown");
        }

        bool enabled = !mode.Equals("Off", StringComparison.OrdinalIgnoreCase);
        return (enabled, mode);
    }

    private static (bool? Enabled, string Mode) ReadControlledFolderAccess()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access");
        object? raw = key?.GetValue("EnableControlledFolderAccess");
        if (raw is null)
        {
            return (null, "Unknown");
        }

        int mode = Convert.ToInt32(raw);
        return mode switch
        {
            1 => (true, "Enabled"),
            2 => (true, "Audit"),
            _ => (false, "Disabled")
        };
    }

    private static bool? ReadExploitProtection()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Kernel");
        object? mitigationOptions = key?.GetValue("MitigationOptions");
        object? mitigationAuditOptions = key?.GetValue("MitigationAuditOptions");
        if (mitigationOptions is null && mitigationAuditOptions is null)
        {
            return null;
        }

        if (mitigationOptions is byte[] bytes)
        {
            return bytes.Any(b => b != 0);
        }

        return true;
    }

    private static bool? ReadLsaProtection()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Lsa");
        object? raw = key?.GetValue("RunAsPPL");
        if (raw is null)
        {
            return null;
        }

        return Convert.ToInt32(raw) > 0;
    }

    private static bool? ReadCredentialGuard()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\CredentialGuard");
        object? raw = key?.GetValue("Enabled");
        if (raw is null)
        {
            return null;
        }

        return Convert.ToInt32(raw) > 0;
    }
}
