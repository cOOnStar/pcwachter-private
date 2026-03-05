namespace AgentService.Runtime;

internal sealed class DefenderSensorData
{
    public bool? RealtimeProtectionEnabled { get; set; }
    public DateTime? SignatureLastUpdatedUtc { get; set; }
    public int? DaysSinceLastUpdate { get; set; }
    public bool? TamperProtectionEnabled { get; set; }
    public string? PlatformVersion { get; set; }
    public string? EngineVersion { get; set; }
    public string? SignatureVersion { get; set; }
    public bool CanAttemptEnableRealtime { get; set; }
    public string Source { get; set; } = "unknown";
}

internal sealed class StorageSensorData
{
    public string SystemDrive { get; set; } = "C:";
    public long FreeBytes { get; set; }
    public long TotalBytes { get; set; }
    public double PercentFree { get; set; }
    public long DownloadsBytes { get; set; }
    public long TempBytes { get; set; }
    public long WindowsUpdateCacheBytes { get; set; }
    public long RecycleBinBytes { get; set; }
    public List<StorageConsumerItem> TopConsumers { get; set; } = new();
    public List<StorageDriveHealthData> Drives { get; set; } = new();
}

internal sealed class StorageDriveHealthData
{
    public string Name { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public PCWachter.Contracts.DriveHealthState HealthState { get; set; } = PCWachter.Contracts.DriveHealthState.Unknown;
    public string HealthBadgeText { get; set; } = "Unbekannt";
    public bool SmartAvailable { get; set; }
    public bool PredictFailure { get; set; }
    public int? TemperatureC { get; set; }
    public List<string> HealthDetails { get; set; } = new();
    public DateTimeOffset LastCheckedUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class StorageConsumerItem
{
    public string Name { get; set; } = string.Empty;
    public long Bytes { get; set; }
}

internal sealed class BitLockerSensorData
{
    public string SystemDrive { get; set; } = "C:";
    public bool? IsProtectionOn { get; set; }
    public string? ProtectionStatusRaw { get; set; }
    public string? EncryptionMethod { get; set; }
    public bool? HasKeyProtector { get; set; }
    public bool IsWindowsHomeEdition { get; set; }
    public string Source { get; set; } = "unknown";
}

internal sealed class PendingRebootSensorData
{
    public bool IsPending { get; set; }
    public List<string> TriggeredSignals { get; set; } = new();
}

internal sealed class FirewallSensorData
{
    public Dictionary<string, bool?> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> DisabledProfiles => Profiles
        .Where(x => x.Value == false)
        .Select(x => x.Key)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

internal sealed class EventLogHealthSensorData
{
    public int SystemErrorCount24h { get; set; }
    public int ApplicationErrorCount24h { get; set; }
    public List<string> TopSystemSources { get; set; } = new();
    public List<string> TopApplicationSources { get; set; } = new();
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
}

internal sealed class SecurityHardeningSensorData
{
    public bool? SmartScreenEnabled { get; set; }
    public string? SmartScreenMode { get; set; }
    public bool? ControlledFolderAccessEnabled { get; set; }
    public string? ControlledFolderAccessMode { get; set; }
    public bool? ExploitProtectionEnabled { get; set; }
    public bool? LsaProtectionEnabled { get; set; }
    public bool? CredentialGuardEnabled { get; set; }
    public string Source { get; set; } = "registry";
}

internal sealed class AppUpdateItemData
{
    public string PackageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public string AvailableVersion { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

internal sealed class AppUpdatesSensorData
{
    public bool WingetAvailable { get; set; }
    public string WingetVersion { get; set; } = string.Empty;
    public List<AppUpdateItemData> Updates { get; set; } = new();
    public string Source { get; set; } = "winget";
}

internal sealed class StartupEntryData
{
    public string EntryKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Impact { get; set; } = "Medium";
    public bool IsDisabledByPcwachter { get; set; }
}

internal sealed class StartupAppsSensorData
{
    public List<StartupEntryData> Entries { get; set; } = new();
}

internal sealed class NetworkDiagnosticsSensorData
{
    public bool HasInternet { get; set; }
    public bool GatewayReachable { get; set; }
    public bool PublicDnsReachable { get; set; }
    public bool ProxyEnabled { get; set; }
    public string AdapterSummary { get; set; } = string.Empty;
    public int? GatewayLatencyMs { get; set; }
    public int? PublicDnsLatencyMs { get; set; }
}

internal sealed class WindowsUpdatesSensorData
{
    public int SecurityCount { get; set; }
    public int OptionalSoftwareCount { get; set; }
    public int DriverCount { get; set; }
    public List<string> TopTitles { get; set; } = new();
}

internal sealed class PerformanceWatchSensorData
{
    public int CpuPercent { get; set; }
    public int MemoryPercent { get; set; }
    public string TopProcessName { get; set; } = string.Empty;
    public int TopProcessCpuPercent { get; set; }
    public int? TopProcessId { get; set; }
}
