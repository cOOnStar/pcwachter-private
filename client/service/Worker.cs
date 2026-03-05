using System.Management;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.ServiceProcess;
using System.Text.Json;
using AgentService.Runtime;
using Microsoft.Win32;

namespace AgentService;

internal sealed class Worker : BackgroundService
{
    private const int PollSeconds = 30;

    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _http;
    private readonly ApiOptions _apiOptions;
    private readonly ScanCoordinator _scanCoordinator;
    private readonly Guid _installId;
    private bool _isRegistered;
    private DateTime _lastInventorySentUtc = DateTime.MinValue;

    public Worker(
        ILogger<Worker> logger,
        IHttpClientFactory httpClientFactory,
        ApiOptions apiOptions,
        ScanCoordinator scanCoordinator)
    {
        _logger = logger;
        _apiOptions = apiOptions;
        _scanCoordinator = scanCoordinator;
        _http = httpClientFactory.CreateClient("PcWaechterApi");
        _installId = GetOrCreateInstallId();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started with device_install_id {installId}", _installId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await _scanCoordinator.RunScanAsync(stoppingToken);
                var snapshot = CollectSecuritySnapshot();
                WriteSnapshot(snapshot);

                await EnsureRegisteredAsync(stoppingToken);
                await SendHeartbeatAsync(stoppingToken);
                await SendTelemetrySnapshotsAsync(snapshot, stoppingToken);

                if (ShouldSendInventory())
                {
                    await SendInventoryAsync(snapshot, stoppingToken);
                    _lastInventorySentUtc = DateTime.UtcNow;
                }

                _logger.LogInformation(
                    "Security snapshot updated at {time}; findings={findingCount}; status={status}",
                    snapshot.CollectedAtUtc,
                    report.Findings.Count,
                    report.OverallStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect, persist, or sync security snapshot.");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollSeconds), stoppingToken);
        }
    }

    private async Task EnsureRegisteredAsync(CancellationToken cancellationToken)
    {
        if (_isRegistered)
        {
            return;
        }

        var os = Environment.OSVersion;
        string agentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var (primaryIp, macs) = GetNetworkData();

        var payload = new
        {
            device_install_id = _installId,
            hostname = Environment.MachineName,
            os = new
            {
                name = "Windows",
                version = os.VersionString,
                build = os.Version.Build.ToString()
            },
            agent = new
            {
                version = agentVersion,
                channel = "system-service"
            },
            network = new
            {
                primary_ip = primaryIp,
                macs
            }
        };

        var response = await _http.PostAsJsonAsync("agent/register", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        _isRegistered = true;
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            device_install_id = _installId,
            at = DateTime.UtcNow,
            status = new
            {
                source = "system-service",
                uptime_seconds = Environment.TickCount64 / 1000
            }
        };

        var response = await _http.PostAsJsonAsync("agent/heartbeat", payload, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _isRegistered = false;
            await EnsureRegisteredAsync(cancellationToken);
            response = await _http.PostAsJsonAsync("agent/heartbeat", payload, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task SendInventoryAsync(SecuritySnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = new
        {
            device_install_id = _installId,
            collected_at = DateTime.UtcNow,
            inventory = new
            {
                security = new
                {
                    antivirus_name = snapshot.AntivirusName,
                    antivirus_enabled = snapshot.AntivirusEnabled,
                    firewall_name = snapshot.FirewallName,
                    firewall_enabled = snapshot.FirewallEnabled,
                    firewall_profiles = snapshot.FirewallProfiles,
                    defender_realtime = snapshot.DefenderRealtime,
                    defender_service_running = snapshot.DefenderServiceRunning,
                    security_center_service_running = snapshot.SecurityCenterServiceRunning,
                    defender_signature_version = snapshot.DefenderSignatureVersion,
                    defender_engine_version = snapshot.DefenderEngineVersion,
                    last_scan_utc = snapshot.LastScanUtc,
                    definition_last_updated_utc = snapshot.DefinitionLastUpdatedUtc
                },
                security_products = new
                {
                    antivirus = snapshot.AntivirusProducts,
                    antispyware = snapshot.AntispywareProducts,
                    installed_security_software = snapshot.InstalledSecuritySoftware
                },
                source = snapshot.Source
            }
        };

        var response = await _http.PostAsJsonAsync("agent/inventory", payload, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _isRegistered = false;
            await EnsureRegisteredAsync(cancellationToken);
            response = await _http.PostAsJsonAsync("agent/inventory", payload, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
    }

    private bool ShouldSendInventory()
    {
        int intervalSeconds = Math.Max(_apiOptions.InventoryIntervalSeconds, PollSeconds);
        return DateTime.UtcNow - _lastInventorySentUtc >= TimeSpan.FromSeconds(intervalSeconds);
    }

    private async Task SendTelemetrySnapshotsAsync(SecuritySnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var memoryPayload = new
            {
                total_gb = snapshot.MemoryTotalGb,
                used_gb = snapshot.MemoryUsedGb,
                usage_percent = snapshot.MemoryUsagePercent,
                collected_at_utc = snapshot.CollectedAtUtc
            };

            var ssdPayload = new
            {
                system_drive_total_gb = snapshot.SystemDriveTotalGb,
                system_drive_free_gb = snapshot.SystemDriveFreeGb,
                system_drive_used_percent = snapshot.SystemDriveUsedPercent,
                collected_at_utc = snapshot.CollectedAtUtc
            };

            var antivirusPayload = new
            {
                antivirus_name = snapshot.AntivirusName,
                antivirus_enabled = snapshot.AntivirusEnabled,
                firewall_name = snapshot.FirewallName,
                firewall_enabled = snapshot.FirewallEnabled,
                defender_realtime = snapshot.DefenderRealtime,
                collected_at_utc = snapshot.CollectedAtUtc
            };

            await _http.PostAsJsonAsync(
                "telemetry/snapshot",
                new
                {
                    device_install_id = _installId.ToString(),
                    host_name = Environment.MachineName,
                    category = "memory",
                    payload = memoryPayload,
                    summary = BuildMemorySummary(snapshot),
                    source = "agent"
                },
                cancellationToken);

            await _http.PostAsJsonAsync(
                "telemetry/snapshot",
                new
                {
                    device_install_id = _installId.ToString(),
                    host_name = Environment.MachineName,
                    category = "ssd",
                    payload = ssdPayload,
                    summary = BuildStorageSummary(snapshot),
                    source = "agent"
                },
                cancellationToken);

            await _http.PostAsJsonAsync(
                "telemetry/snapshot",
                new
                {
                    device_install_id = _installId.ToString(),
                    host_name = Environment.MachineName,
                    category = "antivirus",
                    payload = antivirusPayload,
                    summary = BuildSecuritySummary(snapshot),
                    source = "agent"
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telemetry snapshot transfer failed.");
        }
    }

    private static Guid GetOrCreateInstallId()
    {
        string? directory = Path.GetDirectoryName(RuntimePaths.InstallIdPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Install ID path is invalid.");
        }

        Directory.CreateDirectory(directory);

        if (File.Exists(RuntimePaths.InstallIdPath))
        {
            string existing = File.ReadAllText(RuntimePaths.InstallIdPath).Trim();
            if (Guid.TryParse(existing, out Guid parsed))
            {
                return parsed;
            }
        }

        Guid created = Guid.NewGuid();
        File.WriteAllText(RuntimePaths.InstallIdPath, created.ToString());
        return created;
    }

    private static (string? PrimaryIp, string[] Macs) GetNetworkData()
    {
        string? primaryIp = null;
        var macs = new List<string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var mac = networkInterface.GetPhysicalAddress()?.ToString();
            if (!string.IsNullOrWhiteSpace(mac))
            {
                macs.Add(mac);
            }

            if (!string.IsNullOrWhiteSpace(primaryIp))
            {
                continue;
            }

            var unicast = networkInterface.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            primaryIp = unicast?.Address.ToString();
        }

        return (primaryIp, macs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static SecuritySnapshot CollectSecuritySnapshot()
    {
        string antivirusName = "Nicht erkannt";
        bool? antivirusEnabled = null;
        bool? defenderRealtime = null;
        DateTime? lastScanUtc = null;
        DateTime? definitionLastUpdatedUtc = null;
        var antivirusCandidates = new List<AntivirusCandidate>();
        var antispywareCandidates = new List<AntivirusCandidate>();
        string? defenderSignatureVersion = null;
        string? defenderEngineVersion = null;

        try
        {
            using var avSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\SecurityCenter2",
                "SELECT displayName, productState FROM AntiVirusProduct");

            foreach (ManagementObject product in avSearcher.Get())
            {
                string name = product["displayName"]?.ToString() ?? "Unbekannt";
                int state = Convert.ToInt32(product["productState"] ?? 0);

                antivirusCandidates.Add(new AntivirusCandidate
                {
                    Name = name,
                    Enabled = IsSecurityCenterProductEnabled(state),
                    IsDefender = name.Contains("Defender", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (antivirusCandidates.Count > 0)
            {
                var primary = SelectPrimaryAntivirus(antivirusCandidates);
                antivirusName = primary.Name;
                antivirusEnabled = primary.Enabled;
            }
        }
        catch
        {
        }

        try
        {
            using var antispywareSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\SecurityCenter2",
                "SELECT displayName, productState FROM AntiSpywareProduct");

            foreach (ManagementObject product in antispywareSearcher.Get())
            {
                string name = product["displayName"]?.ToString() ?? "Unbekannt";
                int state = Convert.ToInt32(product["productState"] ?? 0);

                antispywareCandidates.Add(new AntivirusCandidate
                {
                    Name = name,
                    Enabled = IsSecurityCenterProductEnabled(state),
                    IsDefender = name.Contains("Defender", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch
        {
        }

        try
        {
            using var defenderSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\Microsoft\Windows\Defender",
                "SELECT AntivirusEnabled, RealTimeProtectionEnabled, QuickScanEndTime, FullScanEndTime, AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus");

            foreach (ManagementObject status in defenderSearcher.Get())
            {
                bool avEnabled = Convert.ToBoolean(status["AntivirusEnabled"] ?? false);
                bool rtEnabled = Convert.ToBoolean(status["RealTimeProtectionEnabled"] ?? false);
                defenderRealtime = avEnabled && rtEnabled;

                if (status["QuickScanEndTime"] is string quickScanRaw && !string.IsNullOrWhiteSpace(quickScanRaw))
                {
                    lastScanUtc = ManagementDateTimeConverter.ToDateTime(quickScanRaw).ToUniversalTime();
                }
                else if (status["FullScanEndTime"] is string fullScanRaw && !string.IsNullOrWhiteSpace(fullScanRaw))
                {
                    lastScanUtc = ManagementDateTimeConverter.ToDateTime(fullScanRaw).ToUniversalTime();
                }

                definitionLastUpdatedUtc = ParseWmiDateTime(status["AntivirusSignatureLastUpdated"])?.ToUniversalTime();
                defenderSignatureVersion = status["AntivirusSignatureVersion"]?.ToString();
                defenderEngineVersion = status["AMEngineVersion"]?.ToString();

                break;
            }
        }
        catch
        {
        }

        bool? defenderServiceRunning = TryGetServiceRunning("WinDefend");
        bool? securityCenterServiceRunning = TryGetServiceRunning("wscsvc");
        bool? defenderRealtimeRegistry = TryGetDefenderRealtimeFromRegistry();

        bool isDefenderOrUnknown = IsDefenderOrUnknown(antivirusName);

        if (isDefenderOrUnknown && defenderRealtime == true)
        {
            antivirusEnabled = true;
            if (antivirusName == "Nicht erkannt" || antivirusName == "Unbekannt")
            {
                antivirusName = "Microsoft Defender Antivirus";
            }
        }
        else if (isDefenderOrUnknown && antivirusEnabled != true && defenderServiceRunning == true && defenderRealtimeRegistry == true)
        {
            antivirusEnabled = true;
            if (antivirusName == "Nicht erkannt" || antivirusName == "Unbekannt")
            {
                antivirusName = "Microsoft Defender Antivirus";
            }
        }

        bool? firewallEnabled = null;
        string firewallName = "Nicht erkannt";
        try
        {
            using var firewallSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\SecurityCenter2",
                "SELECT displayName, productState FROM FirewallProduct");

            var firewallCandidates = new List<AntivirusCandidate>();
            foreach (ManagementObject product in firewallSearcher.Get())
            {
                string name = product["displayName"]?.ToString() ?? "Unbekannt";
                int productState = Convert.ToInt32(product["productState"] ?? 0);

                firewallCandidates.Add(new AntivirusCandidate
                {
                    Name = name,
                    Enabled = IsSecurityCenterProductEnabled(productState),
                    IsDefender = name.Contains("Defender", StringComparison.OrdinalIgnoreCase)
                                || name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (firewallCandidates.Count > 0)
            {
                var primaryFirewall = SelectPrimaryAntivirus(firewallCandidates);
                firewallEnabled = primaryFirewall.Enabled;
                firewallName = primaryFirewall.Name;
            }
        }
        catch
        {
        }

        if (!firewallEnabled.HasValue)
        {
            try
            {
                bool allEnabled = true;
                using var firewallSearcher = new ManagementObjectSearcher(
                    @"\\localhost\root\StandardCimv2",
                    "SELECT Enabled FROM MSFT_NetFirewallProfile");

                foreach (ManagementObject profile in firewallSearcher.Get())
                {
                    bool enabled = Convert.ToBoolean(profile["Enabled"] ?? false);
                    if (!enabled)
                    {
                        allEnabled = false;
                        break;
                    }
                }

                firewallEnabled = allEnabled;
                firewallName = "Windows-Firewall";
            }
            catch
            {
                firewallEnabled = TryGetServiceRunning("mpssvc");
                firewallName = "Windows-Firewall";
            }
        }

        var firewallProfiles = GetFirewallProfiles();
        var installedSecuritySoftware = antivirusCandidates
            .Select(candidate => candidate.Name)
            .Concat(antispywareCandidates.Select(candidate => candidate.Name))
            .Concat(new[] { firewallName })
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name.Equals("Nicht erkannt", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        (double? memoryTotalGb, double? memoryUsedGb, int? memoryUsagePercent) = GetMemoryStatus();
        (double? systemDriveTotalGb, double? systemDriveFreeGb, int? systemDriveUsedPercent) = GetSystemDriveStatus();

        return new SecuritySnapshot
        {
            CollectedAtUtc = DateTime.UtcNow,
            AntivirusName = antivirusName,
            AntivirusEnabled = antivirusEnabled,
            FirewallEnabled = firewallEnabled,
            FirewallName = firewallName,
            FirewallProfiles = firewallProfiles,
            DefenderRealtime = defenderRealtime,
            DefenderServiceRunning = defenderServiceRunning,
            SecurityCenterServiceRunning = securityCenterServiceRunning,
            DefenderSignatureVersion = defenderSignatureVersion,
            DefenderEngineVersion = defenderEngineVersion,
            LastScanUtc = lastScanUtc,
            DefinitionLastUpdatedUtc = definitionLastUpdatedUtc,
            AntivirusProducts = antivirusCandidates.Select(ToSecurityProductStatus).ToList(),
            AntispywareProducts = antispywareCandidates.Select(ToSecurityProductStatus).ToList(),
            InstalledSecuritySoftware = installedSecuritySoftware,
            MemoryTotalGb = memoryTotalGb,
            MemoryUsedGb = memoryUsedGb,
            MemoryUsagePercent = memoryUsagePercent,
            SystemDriveTotalGb = systemDriveTotalGb,
            SystemDriveFreeGb = systemDriveFreeGb,
            SystemDriveUsedPercent = systemDriveUsedPercent,
            Source = "system-service"
        };
    }

    private static (double? TotalGb, double? UsedGb, int? UsagePercent) GetMemoryStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\localhost\root\CIMV2",
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (ManagementObject os in searcher.Get())
            {
                double totalKb = Convert.ToDouble(os["TotalVisibleMemorySize"] ?? 0);
                double freeKb = Convert.ToDouble(os["FreePhysicalMemory"] ?? 0);
                if (totalKb <= 0)
                {
                    return (null, null, null);
                }

                double usedKb = Math.Max(0, totalKb - freeKb);
                double totalGb = Math.Round(totalKb / 1024d / 1024d, 1);
                double usedGb = Math.Round(usedKb / 1024d / 1024d, 1);
                int usage = (int)Math.Round((usedKb / totalKb) * 100d);
                return (totalGb, usedGb, usage);
            }
        }
        catch
        {
        }

        return (null, null, null);
    }

    private static (double? TotalGb, double? FreeGb, int? UsedPercent) GetSystemDriveStatus()
    {
        try
        {
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed && d.Name.Equals(systemRoot, StringComparison.OrdinalIgnoreCase));

            if (drive == null || drive.TotalSize <= 0)
            {
                return (null, null, null);
            }

            double totalGb = Math.Round(drive.TotalSize / 1024d / 1024d / 1024d, 1);
            double freeGb = Math.Round(drive.TotalFreeSpace / 1024d / 1024d / 1024d, 1);
            int usedPercent = (int)Math.Round((1d - (drive.TotalFreeSpace / (double)drive.TotalSize)) * 100d);
            return (totalGb, freeGb, usedPercent);
        }
        catch
        {
        }

        return (null, null, null);
    }

    private static string BuildMemorySummary(SecuritySnapshot snapshot)
    {
        if (!snapshot.MemoryUsagePercent.HasValue || !snapshot.MemoryUsedGb.HasValue || !snapshot.MemoryTotalGb.HasValue)
        {
            return "Memory: nicht verfÃ¼gbar";
        }

        return $"RAM {snapshot.MemoryUsedGb:0.0}/{snapshot.MemoryTotalGb:0.0} GB ({snapshot.MemoryUsagePercent.Value}%)";
    }

    private static string BuildStorageSummary(SecuritySnapshot snapshot)
    {
        if (!snapshot.SystemDriveUsedPercent.HasValue || !snapshot.SystemDriveTotalGb.HasValue || !snapshot.SystemDriveFreeGb.HasValue)
        {
            return "SSD: nicht verfÃ¼gbar";
        }

        return $"SSD frei {snapshot.SystemDriveFreeGb:0.0}/{snapshot.SystemDriveTotalGb:0.0} GB ({snapshot.SystemDriveUsedPercent.Value}% belegt)";
    }

    private static string BuildSecuritySummary(SecuritySnapshot snapshot)
    {
        string av = snapshot.AntivirusEnabled == true ? "AV OK" : "AV Warnung";
        string fw = snapshot.FirewallEnabled == true ? "Firewall OK" : "Firewall Warnung";
        return $"{av}, {fw}";
    }

    private static List<FirewallProfileStatus> GetFirewallProfiles()
    {
        var profiles = new List<FirewallProfileStatus>();

        try
        {
            using var profileSearcher = new ManagementObjectSearcher(
                @"\\localhost\root\StandardCimv2",
                "SELECT Name, Enabled FROM MSFT_NetFirewallProfile");

            foreach (ManagementObject profile in profileSearcher.Get())
            {
                string name = profile["Name"]?.ToString() ?? "Unbekannt";
                bool? enabled = profile["Enabled"] is null ? null : Convert.ToBoolean(profile["Enabled"]);

                profiles.Add(new FirewallProfileStatus
                {
                    Name = name,
                    Enabled = enabled
                });
            }
        }
        catch
        {
        }

        return profiles;
    }

    private static SecurityProductStatus ToSecurityProductStatus(AntivirusCandidate candidate)
    {
        return new SecurityProductStatus
        {
            Name = candidate.Name,
            Enabled = candidate.Enabled,
            IsDefender = candidate.IsDefender
        };
    }

    private static DateTime? ParseWmiDateTime(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        if (value is string raw && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                return ManagementDateTimeConverter.ToDateTime(raw);
            }
            catch
            {
                if (DateTime.TryParse(raw, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static bool? TryGetServiceRunning(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return null;
        }
    }

    private static bool? TryGetDefenderRealtimeFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
            if (key == null)
            {
                return null;
            }

            object? value = key.GetValue("DisableRealtimeMonitoring");
            if (value == null)
            {
                return null;
            }

            int disabled = Convert.ToInt32(value);
            return disabled == 0;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSecurityCenterProductEnabled(int productState)
    {
        int stateNibble = (productState >> 12) & 0xF;
        return stateNibble == 1;
    }

    private static AntivirusCandidate SelectPrimaryAntivirus(List<AntivirusCandidate> candidates)
    {
        return candidates
            .OrderBy(c => c.IsDefender)
            .ThenByDescending(c => c.Enabled)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static bool IsDefenderOrUnknown(string antivirusName)
    {
        if (string.IsNullOrWhiteSpace(antivirusName))
        {
            return true;
        }

        return antivirusName.Equals("Nicht erkannt", StringComparison.OrdinalIgnoreCase)
               || antivirusName.Equals("Unbekannt", StringComparison.OrdinalIgnoreCase)
               || antivirusName.Contains("Defender", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSnapshot(SecuritySnapshot snapshot)
    {
        string? directory = Path.GetDirectoryName(RuntimePaths.SnapshotPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(RuntimePaths.SnapshotPath, json);
    }

    private sealed class SecuritySnapshot
    {
        public DateTime CollectedAtUtc { get; set; }
        public string AntivirusName { get; set; } = "Nicht erkannt";
        public bool? AntivirusEnabled { get; set; }
        public bool? FirewallEnabled { get; set; }
        public string FirewallName { get; set; } = "Nicht erkannt";
        public List<FirewallProfileStatus> FirewallProfiles { get; set; } = new();
        public bool? DefenderRealtime { get; set; }
        public bool? DefenderServiceRunning { get; set; }
        public bool? SecurityCenterServiceRunning { get; set; }
        public string? DefenderSignatureVersion { get; set; }
        public string? DefenderEngineVersion { get; set; }
        public DateTime? LastScanUtc { get; set; }
        public DateTime? DefinitionLastUpdatedUtc { get; set; }
        public List<SecurityProductStatus> AntivirusProducts { get; set; } = new();
        public List<SecurityProductStatus> AntispywareProducts { get; set; } = new();
        public List<string> InstalledSecuritySoftware { get; set; } = new();
        public double? MemoryTotalGb { get; set; }
        public double? MemoryUsedGb { get; set; }
        public int? MemoryUsagePercent { get; set; }
        public double? SystemDriveTotalGb { get; set; }
        public double? SystemDriveFreeGb { get; set; }
        public int? SystemDriveUsedPercent { get; set; }
        public string Source { get; set; } = "system-service";
    }

    private sealed class SecurityProductStatus
    {
        public string Name { get; set; } = "Unbekannt";
        public bool Enabled { get; set; }
        public bool IsDefender { get; set; }
    }

    private sealed class FirewallProfileStatus
    {
        public string Name { get; set; } = "Unbekannt";
        public bool? Enabled { get; set; }
    }

    private sealed class AntivirusCandidate
    {
        public string Name { get; set; } = "Unbekannt";
        public bool Enabled { get; set; }
        public bool IsDefender { get; set; }
    }
}

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = "https://api.xn--pcwchter-2za.de";
    public int InventoryIntervalSeconds { get; set; } = 300;
    public string AgentApiKey { get; set; } = string.Empty;
}

