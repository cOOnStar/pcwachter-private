using System.Management;
using AgentService.Runtime;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Sensors;

internal sealed class StorageSensor : ISensor
{
    public const string Id = "sensor.storage";

    public string SensorId => Id;

    public Task<SensorResult> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<DriveInfo> fixedDrives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fixedDrives.Count == 0)
            {
                return Task.FromResult(Failure("No fixed drives available."));
            }

            string driveRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            DriveInfo? drive = fixedDrives
                .FirstOrDefault(d => d.Name.Equals(driveRoot, StringComparison.OrdinalIgnoreCase))
                ?? fixedDrives.FirstOrDefault();

            if (drive is null || drive.TotalSize <= 0)
            {
                return Task.FromResult(Failure("System drive not available."));
            }

            var data = new StorageSensorData
            {
                SystemDrive = drive.Name.TrimEnd('\\'),
                FreeBytes = drive.TotalFreeSpace,
                TotalBytes = drive.TotalSize,
                PercentFree = Math.Round((drive.TotalFreeSpace / (double)drive.TotalSize) * 100d, 2)
            };

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloadsPath = Path.Combine(userProfile, "Downloads");
            string tempPath = Path.GetTempPath();
            string windowsUpdateCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
            string recyclePath = Path.Combine(drive.Name, "$Recycle.Bin");

            data.DownloadsBytes = GetDirectorySize(downloadsPath, maxDepth: 2);
            data.TempBytes = GetDirectorySize(tempPath, maxDepth: 2);
            data.WindowsUpdateCacheBytes = GetDirectorySize(windowsUpdateCache, maxDepth: 2);
            data.RecycleBinBytes = GetDirectorySize(recyclePath, maxDepth: 2);

            data.TopConsumers = new List<StorageConsumerItem>
            {
                new() { Name = "Downloads", Bytes = data.DownloadsBytes },
                new() { Name = "Temp", Bytes = data.TempBytes },
                new() { Name = "Windows Update Cache", Bytes = data.WindowsUpdateCacheBytes },
                new() { Name = "Papierkorb", Bytes = data.RecycleBinBytes }
            }
            .OrderByDescending(x => x.Bytes)
            .ToList();

            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            Dictionary<string, bool> smartPredictByPnp = ReadSmartPredictStatusByPnpDeviceId();
            Dictionary<string, int> smartTemperatureByPnp = ReadSmartTemperatureByPnpDeviceId();
            Dictionary<string, List<string>> driveToPnp = ResolveLogicalDriveToPnpIds();
            data.Drives = fixedDrives
                .Select(localDrive =>
                {
                    string driveName = localDrive.Name.TrimEnd('\\');
                    driveToPnp.TryGetValue(driveName, out List<string>? pnpCandidates);
                    return BuildDriveHealth(localDrive, pnpCandidates ?? [], smartPredictByPnp, smartTemperatureByPnp, nowUtc);
                })
                .ToList();

            return Task.FromResult(Success(data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure(ex.Message));
        }
    }

    private static SensorResult Success(StorageSensorData payload)
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

    private static StorageDriveHealthData BuildDriveHealth(
        DriveInfo drive,
        IReadOnlyCollection<string> pnpCandidates,
        IReadOnlyDictionary<string, bool> smartPredictByPnp,
        IReadOnlyDictionary<string, int> smartTemperatureByPnp,
        DateTimeOffset nowUtc)
    {
        const int tempWarningC = 55;
        const int tempCriticalC = 65;

        string driveName = drive.Name.TrimEnd('\\');
        double percentFree = GetPercentFree(drive);
        bool smartAvailable = TryResolvePredictFailure(pnpCandidates, smartPredictByPnp, out bool predictFailure);
        bool hasTemperature = TryResolveTemperatureC(pnpCandidates, smartTemperatureByPnp, out int temperatureC);
        int? temperature = hasTemperature ? temperatureC : null;
        var details = new List<string>();

        if (predictFailure)
        {
            details.Add("SMART meldet ein mogliches Laufwerksproblem.");
        }

        if (temperature is not null)
        {
            if (temperature.Value >= tempCriticalC)
            {
                details.Add($"SMART-Temperatur kritisch ({temperature.Value}°C).");
            }
            else if (temperature.Value >= tempWarningC)
            {
                details.Add($"SMART-Temperatur erhoeht ({temperature.Value}°C).");
            }
        }

        if (percentFree < 5d)
        {
            details.Add($"Sehr wenig freier Speicher ({percentFree:0.0}% frei).");
        }
        else if (percentFree < 15d)
        {
            details.Add($"Speicher wird knapp ({percentFree:0.0}% frei).");
        }

        if (!smartAvailable)
        {
            details.Add("SMART ist fur dieses Laufwerk nicht verfugbar.");
        }

        bool tempCritical = temperature is not null && temperature.Value >= tempCriticalC;
        bool tempWarning = temperature is not null && temperature.Value >= tempWarningC;

        DriveHealthState state = predictFailure || percentFree < 5d || tempCritical
            ? DriveHealthState.Critical
            : percentFree < 15d || tempWarning
                ? DriveHealthState.Warning
                : smartAvailable || temperature is not null
                    ? DriveHealthState.Good
                    : DriveHealthState.Unknown;

        if (state == DriveHealthState.Good)
        {
            details.Clear();
        }

        return new StorageDriveHealthData
        {
            Name = driveName,
            TotalBytes = drive.TotalSize,
            FreeBytes = drive.AvailableFreeSpace,
            SmartAvailable = smartAvailable,
            PredictFailure = predictFailure,
            TemperatureC = temperature,
            HealthState = state,
            HealthBadgeText = ToBadgeText(state),
            HealthDetails = details,
            LastCheckedUtc = nowUtc
        };
    }

    private static bool TryResolvePredictFailure(
        IReadOnlyCollection<string> pnpCandidates,
        IReadOnlyDictionary<string, bool> smartPredictByPnp,
        out bool predictFailure)
    {
        predictFailure = false;
        if (pnpCandidates.Count == 0 || smartPredictByPnp.Count == 0)
        {
            return false;
        }

        bool found = false;
        foreach (string candidateRaw in pnpCandidates)
        {
            string candidate = NormalizePnpId(candidateRaw);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (smartPredictByPnp.TryGetValue(candidate, out bool direct))
            {
                found = true;
                predictFailure |= direct;
                continue;
            }

            KeyValuePair<string, bool>? fuzzyMatch = null;
            foreach (KeyValuePair<string, bool> entry in smartPredictByPnp)
            {
                if (entry.Key.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    fuzzyMatch = entry;
                    break;
                }
            }

            if (fuzzyMatch is not null)
            {
                found = true;
                predictFailure |= fuzzyMatch.Value.Value;
            }
        }

        return found;
    }

    private static bool TryResolveTemperatureC(
        IReadOnlyCollection<string> pnpCandidates,
        IReadOnlyDictionary<string, int> smartTemperatureByPnp,
        out int temperatureC)
    {
        temperatureC = 0;
        if (pnpCandidates.Count == 0 || smartTemperatureByPnp.Count == 0)
        {
            return false;
        }

        bool found = false;
        foreach (string candidateRaw in pnpCandidates)
        {
            string candidate = NormalizePnpId(candidateRaw);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (smartTemperatureByPnp.TryGetValue(candidate, out int directTemp))
            {
                temperatureC = directTemp;
                return true;
            }

            foreach (KeyValuePair<string, int> entry in smartTemperatureByPnp)
            {
                if (entry.Key.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                    || candidate.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    temperatureC = entry.Value;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                break;
            }
        }

        return found;
    }

    private static Dictionary<string, bool> ReadSmartPredictStatusByPnpDeviceId()
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\wmi",
                "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus");

            foreach (ManagementObject item in searcher.Get())
            {
                string instanceName = item["InstanceName"]?.ToString() ?? string.Empty;
                string normalized = NormalizePnpFromInstanceName(instanceName);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                bool predictFailure = false;
                object? rawPredict = item["PredictFailure"];
                if (rawPredict is bool b)
                {
                    predictFailure = b;
                }
                else if (rawPredict is not null && bool.TryParse(rawPredict.ToString(), out bool parsed))
                {
                    predictFailure = parsed;
                }

                map[normalized] = predictFailure;
            }
        }
        catch
        {
        }

        return map;
    }

    private static Dictionary<string, int> ReadSmartTemperatureByPnpDeviceId()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\wmi",
                "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData");

            foreach (ManagementObject item in searcher.Get())
            {
                string instanceName = item["InstanceName"]?.ToString() ?? string.Empty;
                string normalized = NormalizePnpFromInstanceName(instanceName);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (item["VendorSpecific"] is byte[] vendorSpecific
                    && TryExtractTemperatureC(vendorSpecific, out int tempC))
                {
                    map[normalized] = tempC;
                }
            }
        }
        catch
        {
        }

        return map;
    }

    private static bool TryExtractTemperatureC(byte[] vendorSpecific, out int tempC)
    {
        tempC = 0;
        if (vendorSpecific.Length < 12)
        {
            return false;
        }

        // SMART vendor blob contains 30 attributes in 12-byte entries, starting at offset 2.
        for (int offset = 2; offset + 11 < vendorSpecific.Length; offset += 12)
        {
            byte attributeId = vendorSpecific[offset];
            if (attributeId == 0)
            {
                continue;
            }

            if (attributeId is not (190 or 194 or 231))
            {
                continue;
            }

            for (int rawOffset = 5; rawOffset <= 10; rawOffset++)
            {
                int candidate = vendorSpecific[offset + rawOffset];
                if (candidate is > 10 and < 120)
                {
                    tempC = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<string, List<string>> ResolveLogicalDriveToPnpIds()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            string driveName = drive.Name.TrimEnd('\\');
            var pnpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string logicalDiskQuery = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{EscapeWmiLiteral(driveName)}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";
                using var partitionSearcher = new ManagementObjectSearcher(@"\\localhost\root\cimv2", logicalDiskQuery);
                foreach (ManagementObject partition in partitionSearcher.Get())
                {
                    string? partitionId = partition["DeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(partitionId))
                    {
                        continue;
                    }

                    string diskQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{EscapeWmiLiteral(partitionId)}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                    using var diskSearcher = new ManagementObjectSearcher(@"\\localhost\root\cimv2", diskQuery);
                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        string? pnpRaw = disk["PNPDeviceID"]?.ToString();
                        string normalized = NormalizePnpId(pnpRaw);
                        if (!string.IsNullOrWhiteSpace(normalized))
                        {
                            pnpIds.Add(normalized);
                        }
                    }
                }
            }
            catch
            {
            }

            result[driveName] = pnpIds.ToList();
        }

        return result;
    }

    private static string EscapeWmiLiteral(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string NormalizePnpFromInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return string.Empty;
        }

        string normalized = instanceName.Trim().ToUpperInvariant().Replace("/", "\\", StringComparison.Ordinal);
        int suffixIndex = normalized.LastIndexOf("_0", StringComparison.Ordinal);
        if (suffixIndex > 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return normalized;
    }

    private static string NormalizePnpId(string? pnpDeviceId)
    {
        return (pnpDeviceId ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace("/", "\\", StringComparison.Ordinal);
    }

    private static string ToBadgeText(DriveHealthState state)
    {
        return state switch
        {
            DriveHealthState.Good => "Zustand gut",
            DriveHealthState.Warning => "Achtung",
            DriveHealthState.Critical => "Kritisch",
            _ => "Unbekannt"
        };
    }

    private static double GetPercentFree(DriveInfo drive)
    {
        if (drive.TotalSize <= 0)
        {
            return 0;
        }

        return Math.Round((drive.AvailableFreeSpace / (double)drive.TotalSize) * 100d, 2);
    }

    private static long GetDirectorySize(string path, int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return 0;
        }

        try
        {
            return GetDirectorySizeInternal(path, 0, maxDepth);
        }
        catch
        {
            return 0;
        }
    }

    private static long GetDirectorySizeInternal(string path, int depth, int maxDepth)
    {
        long total = 0;

        try
        {
            foreach (string file in Directory.EnumerateFiles(path))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        if (depth >= maxDepth)
        {
            return total;
        }

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(path))
            {
                try
                {
                    total += GetDirectorySizeInternal(dir, depth + 1, maxDepth);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return total;
    }
}
