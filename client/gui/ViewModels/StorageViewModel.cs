using System.Collections.ObjectModel;
using System.IO;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class StorageDriveViewModel : ObservableObject
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public double UsedPercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLevel { get; set; } = "good";
    public bool SmartAvailable { get; set; }
    public bool PredictFailure { get; set; }
    public int? TemperatureC { get; set; }
    public string LastCheckedText { get; set; } = string.Empty;
    public IReadOnlyList<string> HealthDetails { get; set; } = [];
    public bool HasHealthDetails => HealthDetails.Count > 0;
    public IReadOnlyList<string> Recommendations { get; set; } = [];
    public bool HasRecommendations => Recommendations.Count > 0;
    public string TemperatureText => TemperatureC is int value ? $"{value} °C" : "Nicht verfügbar";
    public bool HasTemperature => TemperatureC is not null;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class StorageConsumerViewModel : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public long Bytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
}

public sealed class StorageViewModel : ReportPageViewModelBase
{
    private int _totalDriveCount;
    private int _criticalDriveCount;
    private int _warningDriveCount;
    private int _totalStorageFindings;
    private int _criticalStorageFindings;
    private double _totalDiskSpaceGb;
    private double _totalFreeSpaceGb;
    private string _selectedFindingFilter = "all";
    private FindingCardViewModel? _selectedFinding;
    private StorageDriveViewModel? _selectedDrive;
    private List<FindingCardViewModel> _allStorageFindings = [];
    private long _downloadsBytes;
    private long _tempBytes;
    private long _updateCacheBytes;
    private long _recycleBinBytes;
    private long _storageAnalysisTotalBytes;

    public StorageViewModel(ReportStore reportStore, IpcClientService ipcClient, DesktopActionRunner actionRunner)
        : base("Speicher", reportStore, ipcClient, actionRunner)
    {
        OpenCleanupCommand = new RelayCommand(() => DesktopActionRunner.OpenExternal("ms-settings:storagesense"));
        SetFindingFilterCommand = new RelayCommand(param => SetFindingFilter(param?.ToString()));
        SelectDriveCommand = new RelayCommand(param => SelectDrive(param as StorageDriveViewModel));
        RefreshDriveCards(reportStore.CurrentReport);
        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<StorageDriveViewModel> Drives { get; } = new();
    public ObservableCollection<StorageConsumerViewModel> TopConsumers { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> StorageFindings { get; private set; } = new();
    public RelayCommand OpenCleanupCommand { get; }
    public RelayCommand SetFindingFilterCommand { get; }
    public RelayCommand SelectDriveCommand { get; }

    public int TotalDriveCount
    {
        get => _totalDriveCount;
        private set => SetProperty(ref _totalDriveCount, value);
    }

    public int CriticalDriveCount
    {
        get => _criticalDriveCount;
        private set
        {
            if (SetProperty(ref _criticalDriveCount, value))
            {
                RaisePropertyChanged(nameof(StorageHealthLabel));
                RaisePropertyChanged(nameof(StorageHealthHint));
            }
        }
    }

    public int WarningDriveCount
    {
        get => _warningDriveCount;
        private set
        {
            if (SetProperty(ref _warningDriveCount, value))
            {
                RaisePropertyChanged(nameof(StorageHealthLabel));
                RaisePropertyChanged(nameof(StorageHealthHint));
            }
        }
    }

    public int TotalStorageFindings
    {
        get => _totalStorageFindings;
        private set => SetProperty(ref _totalStorageFindings, value);
    }

    public int CriticalStorageFindings
    {
        get => _criticalStorageFindings;
        private set => SetProperty(ref _criticalStorageFindings, value);
    }

    public double TotalDiskSpaceGb
    {
        get => _totalDiskSpaceGb;
        private set => SetProperty(ref _totalDiskSpaceGb, value);
    }

    public double TotalFreeSpaceGb
    {
        get => _totalFreeSpaceGb;
        private set => SetProperty(ref _totalFreeSpaceGb, value);
    }

    public long DownloadsBytes
    {
        get => _downloadsBytes;
        private set => SetProperty(ref _downloadsBytes, value);
    }

    public long TempBytes
    {
        get => _tempBytes;
        private set => SetProperty(ref _tempBytes, value);
    }

    public long UpdateCacheBytes
    {
        get => _updateCacheBytes;
        private set => SetProperty(ref _updateCacheBytes, value);
    }

    public long RecycleBinBytes
    {
        get => _recycleBinBytes;
        private set => SetProperty(ref _recycleBinBytes, value);
    }

    public long StorageAnalysisTotalBytes
    {
        get => _storageAnalysisTotalBytes;
        private set
        {
            if (!SetProperty(ref _storageAnalysisTotalBytes, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasStorageBreakdown));
        }
    }

    public string TotalDiskSpaceText => $"{TotalDiskSpaceGb:0.0} GB";
    public string TotalFreeSpaceText => $"{TotalFreeSpaceGb:0.0} GB";
    public string DownloadsSizeText => FormatBytes(DownloadsBytes);
    public string TempSizeText => FormatBytes(TempBytes);
    public string UpdateCacheSizeText => FormatBytes(UpdateCacheBytes);
    public string RecycleBinSizeText => FormatBytes(RecycleBinBytes);
    public string StorageAnalysisTotalText => FormatBytes(StorageAnalysisTotalBytes);

    public bool HasDrives => Drives.Count > 0;
    public bool HasStorageFindings => StorageFindings.Count > 0;
    public bool HasTopConsumers => TopConsumers.Count > 0;
    public bool HasStorageBreakdown => StorageAnalysisTotalBytes > 0;
    public bool HasSelectedDrive => SelectedDrive is not null;

    public StorageDriveViewModel? SelectedDrive
    {
        get => _selectedDrive;
        private set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                RaisePropertyChanged(nameof(HasSelectedDrive));
            }
        }
    }

    public FindingCardViewModel? SelectedFinding
    {
        get => _selectedFinding;
        set
        {
            if (SetProperty(ref _selectedFinding, value))
            {
                RaisePropertyChanged(nameof(HasSelectedFinding));
            }
        }
    }

    public bool HasSelectedFinding => SelectedFinding is not null;

    public string SelectedFindingFilter
    {
        get => _selectedFindingFilter;
        private set
        {
            if (string.Equals(_selectedFindingFilter, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedFindingFilter = value;
            RaisePropertyChanged(nameof(SelectedFindingFilter));
            RaisePropertyChanged(nameof(IsFindingFilterAllSelected));
            RaisePropertyChanged(nameof(IsFindingFilterCriticalSelected));
            RaisePropertyChanged(nameof(IsFindingFilterWarningSelected));
        }
    }

    public bool IsFindingFilterAllSelected => string.Equals(SelectedFindingFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsFindingFilterCriticalSelected => string.Equals(SelectedFindingFilter, "critical", StringComparison.OrdinalIgnoreCase);
    public bool IsFindingFilterWarningSelected => string.Equals(SelectedFindingFilter, "warning", StringComparison.OrdinalIgnoreCase);

    public string FindingFilterAllLabel => $"Alle ({TotalStorageFindings})";
    public string FindingFilterCriticalLabel => $"Kritisch ({CriticalStorageFindings})";
    public string FindingFilterWarningLabel => $"Warnung ({Math.Max(0, TotalStorageFindings - CriticalStorageFindings)})";

    public string StorageHealthLabel => CriticalDriveCount > 0
        ? "Kritisch"
        : WarningDriveCount > 0
            ? "Achtung"
            : "Stabil";

    public string StorageHealthHint => CriticalDriveCount > 0
        ? "Mindestens ein Laufwerk ist fast voll"
        : WarningDriveCount > 0
            ? "Speicherbelegung beobachten"
            : "Laufwerke mit Reserve";

    protected override void OnReportUpdated(ScanReportDto report)
    {
        List<FindingDto> findings = report.Findings
            .Where(f => f.FindingId.StartsWith("storage.", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allStorageFindings = [.. BuildCards(findings)];
        TotalStorageFindings = findings.Count;
        CriticalStorageFindings = findings.Count(f => f.Severity == FindingSeverity.Critical);
        ApplyFindingFilter();
        RaisePropertyChanged(nameof(FindingFilterAllLabel));
        RaisePropertyChanged(nameof(FindingFilterCriticalLabel));
        RaisePropertyChanged(nameof(FindingFilterWarningLabel));

        RefreshStorageBreakdown(findings);
        RefreshDriveCards(report);
    }

    private void SetFindingFilter(string? value)
    {
        string normalized = value?.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "warning" => "warning",
            _ => "all"
        };

        SelectedFindingFilter = normalized;
        ApplyFindingFilter();
    }

    private void ApplyFindingFilter()
    {
        StorageFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allStorageFindings, SelectedFindingFilter));
        RaisePropertyChanged(nameof(StorageFindings));
        RaisePropertyChanged(nameof(HasStorageFindings));

        if (SelectedFinding is not null && !StorageFindings.Contains(SelectedFinding))
        {
            SelectedFinding = null;
        }
    }

    private void RefreshStorageBreakdown(IEnumerable<FindingDto> findings)
    {
        FindingDto? breakdown = findings.FirstOrDefault(f =>
            f.FindingId.Equals("storage.analysis.breakdown", StringComparison.OrdinalIgnoreCase));

        DownloadsBytes = ParseLongEvidence(breakdown, "downloads_bytes");
        TempBytes = ParseLongEvidence(breakdown, "temp_bytes");
        UpdateCacheBytes = ParseLongEvidence(breakdown, "windows_update_cache_bytes");
        RecycleBinBytes = ParseLongEvidence(breakdown, "recycle_bin_bytes");
        StorageAnalysisTotalBytes = DownloadsBytes + TempBytes + UpdateCacheBytes + RecycleBinBytes;
        RaisePropertyChanged(nameof(DownloadsSizeText));
        RaisePropertyChanged(nameof(TempSizeText));
        RaisePropertyChanged(nameof(UpdateCacheSizeText));
        RaisePropertyChanged(nameof(RecycleBinSizeText));
        RaisePropertyChanged(nameof(StorageAnalysisTotalText));

        string topConsumersRaw = ReadEvidence(breakdown, "top_consumers", string.Empty);
        List<StorageConsumerViewModel> parsed = ParseTopConsumers(topConsumersRaw);
        TopConsumers = new ObservableCollection<StorageConsumerViewModel>(parsed);
        RaisePropertyChanged(nameof(TopConsumers));
        RaisePropertyChanged(nameof(HasTopConsumers));
    }

    private static IEnumerable<FindingCardViewModel> FilterBySeverity(IEnumerable<FindingCardViewModel> cards, string filter)
    {
        return filter switch
        {
            "critical" => cards.Where(c => c.Severity == FindingSeverity.Critical),
            "warning" => cards.Where(c => c.Severity == FindingSeverity.Warning),
            _ => cards
        };
    }

    private void SelectDrive(StorageDriveViewModel? drive)
    {
        foreach (StorageDriveViewModel item in Drives)
        {
            item.IsSelected = ReferenceEquals(item, drive);
        }

        SelectedDrive = drive;
    }

    private void RefreshDriveCards(ScanReportDto report)
    {
        string? selectedDriveName = SelectedDrive?.Name;
        SelectDrive(null);
        Drives.Clear();
        int critical = 0;
        int warning = 0;
        double totalSpace = 0;
        double totalFree = 0;

        if (report.Drives.Count > 0)
        {
            foreach (DriveStatusDto drive in report.Drives.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                double usedPercent = drive.TotalBytes <= 0
                    ? 0
                    : 100d - ((drive.FreeBytes / (double)drive.TotalBytes) * 100d);
                string statusLevel = ToStatusLevel(drive.HealthState);

                if (drive.HealthState == DriveHealthState.Critical)
                {
                    critical++;
                }
                else if (drive.HealthState == DriveHealthState.Warning)
                {
                    warning++;
                }

                totalSpace += drive.TotalBytes / 1024d / 1024d / 1024d;
                totalFree += drive.FreeBytes / 1024d / 1024d / 1024d;

                IReadOnlyList<string> details = drive.HealthDetails
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
                IReadOnlyList<string> recommendations = BuildDriveRecommendations(
                    drive.HealthState,
                    drive.PredictFailure,
                    usedPercent,
                    drive.SmartAvailable,
                    drive.TemperatureC);

                Drives.Add(new StorageDriveViewModel
                {
                    Name = drive.Name,
                    Summary = $"{FormatGb(drive.FreeBytes)} frei / {FormatGb(drive.TotalBytes)} gesamt",
                    UsedPercent = Math.Round(usedPercent, 1),
                    Status = string.IsNullOrWhiteSpace(drive.HealthBadgeText) ? ToStatusText(drive.HealthState) : drive.HealthBadgeText,
                    StatusLevel = statusLevel,
                    SmartAvailable = drive.SmartAvailable,
                    PredictFailure = drive.PredictFailure,
                    TemperatureC = drive.TemperatureC,
                    LastCheckedText = FormatLastChecked(drive.LastCheckedUtc),
                    HealthDetails = details,
                    Recommendations = recommendations
                });
            }
        }
        else
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                double usedPercent = 100d - ((drive.AvailableFreeSpace / (double)drive.TotalSize) * 100d);
                string status = usedPercent switch
                {
                    >= 95 => "Kritisch",
                    >= 85 => "Achtung",
                    _ => "Zustand gut"
                };
                string statusLevel = usedPercent switch
                {
                    >= 95 => "critical",
                    >= 85 => "warning",
                    _ => "good"
                };

                if (statusLevel == "critical")
                {
                    critical++;
                }
                else if (statusLevel == "warning")
                {
                    warning++;
                }

                totalSpace += drive.TotalSize / 1024d / 1024d / 1024d;
                totalFree += drive.AvailableFreeSpace / 1024d / 1024d / 1024d;

                Drives.Add(new StorageDriveViewModel
                {
                    Name = drive.Name.TrimEnd('\\'),
                    Summary = $"{FormatGb(drive.AvailableFreeSpace)} frei / {FormatGb(drive.TotalSize)} gesamt",
                    UsedPercent = Math.Round(usedPercent, 1),
                    Status = status,
                    StatusLevel = statusLevel,
                    LastCheckedText = "Geprüft: lokal",
                    Recommendations = BuildDriveRecommendations(DriveHealthState.Unknown, false, usedPercent, false, null)
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedDriveName))
        {
            StorageDriveViewModel? restored = Drives.FirstOrDefault(d => string.Equals(d.Name, selectedDriveName, StringComparison.OrdinalIgnoreCase));
            if (restored is not null)
            {
                SelectDrive(restored);
            }
        }

        TotalDriveCount = Drives.Count;
        CriticalDriveCount = critical;
        WarningDriveCount = warning;
        TotalDiskSpaceGb = Math.Round(totalSpace, 1);
        TotalFreeSpaceGb = Math.Round(totalFree, 1);
        RaisePropertyChanged(nameof(TotalDiskSpaceText));
        RaisePropertyChanged(nameof(TotalFreeSpaceText));
        RaisePropertyChanged(nameof(HasDrives));
    }

    private static string ToStatusLevel(DriveHealthState state)
    {
        return state switch
        {
            DriveHealthState.Warning => "warning",
            DriveHealthState.Critical => "critical",
            DriveHealthState.Unknown => "unknown",
            _ => "good"
        };
    }

    private static string ToStatusText(DriveHealthState state)
    {
        return state switch
        {
            DriveHealthState.Warning => "Achtung",
            DriveHealthState.Critical => "Kritisch",
            DriveHealthState.Unknown => "Unbekannt",
            _ => "Zustand gut"
        };
    }

    private static string FormatLastChecked(DateTimeOffset timestamp)
    {
        if (timestamp == default)
        {
            return "Geprüft: -";
        }

        DateTime local = timestamp.ToLocalTime().DateTime;
        return $"Geprüft: {local:dd.MM.yyyy HH:mm}";
    }

    private static IReadOnlyList<string> BuildDriveRecommendations(
        DriveHealthState healthState,
        bool predictFailure,
        double usedPercent,
        bool smartAvailable,
        int? temperatureC)
    {
        var recommendations = new List<string>();

        if (predictFailure)
        {
            recommendations.Add("Sofort ein Backup erstellen und Laufwerk zeitnah austauschen.");
        }

        if (temperatureC is >= 65)
        {
            recommendations.Add("Temperatur kritisch: Luftstrom/Kühlung prüfen und Last reduzieren.");
        }
        else if (temperatureC is >= 55)
        {
            recommendations.Add("Temperatur erhöht: Gehäusebelüftung und Staubfilter kontrollieren.");
        }

        if (usedPercent >= 95)
        {
            recommendations.Add("Dringend Speicher freigeben oder Daten auf anderes Laufwerk verschieben.");
        }
        else if (usedPercent >= 85)
        {
            recommendations.Add("Speicher bereinigen, damit kritische Schwelle vermieden wird.");
        }

        if (!smartAvailable)
        {
            recommendations.Add("SMART-Daten nicht verfügbar (z.B. RAID/USB). Gesundheitszustand manuell beobachten.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(healthState == DriveHealthState.Good
                ? "Kein akuter Handlungsbedarf."
                : "Aktuell keine automatische Empfehlung verfügbar.");
        }

        return recommendations;
    }

    private static string FormatGb(long bytes)
    {
        return $"{Math.Round(bytes / 1024d / 1024d / 1024d, 1):0.0} GB";
    }

    private static long ParseLongEvidence(FindingDto? finding, string key)
    {
        if (finding is null || !finding.Evidence.TryGetValue(key, out string? raw))
        {
            return 0;
        }

        return long.TryParse(raw, out long value) ? Math.Max(0, value) : 0;
    }

    private static string ReadEvidence(FindingDto? finding, string key, string fallback)
    {
        if (finding is null)
        {
            return fallback;
        }

        return finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static List<StorageConsumerViewModel> ParseTopConsumers(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var results = new List<StorageConsumerViewModel>();
        string[] entries = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string entry in entries)
        {
            int separator = entry.LastIndexOf(':');
            if (separator <= 0 || separator >= entry.Length - 1)
            {
                continue;
            }

            string name = entry[..separator].Trim();
            string bytesRaw = entry[(separator + 1)..].Trim();
            if (!long.TryParse(bytesRaw, out long bytes))
            {
                continue;
            }

            results.Add(new StorageConsumerViewModel
            {
                Name = name,
                Bytes = Math.Max(0, bytes),
                SizeText = FormatBytes(bytes)
            });
        }

        return results
            .OrderByDescending(x => x.Bytes)
            .Take(8)
            .ToList();
    }

    private static string FormatBytes(long bytes)
    {
        double value = Math.Max(0, bytes);
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit <= 1 ? $"{Math.Round(value):0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }
}



