using System.Collections.ObjectModel;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class SecurityViewModel : ReportPageViewModelBase
{
    private int _criticalSecurityCount;
    private int _warningSecurityCount;
    private int _totalSecurityFindings;
    private string _selectedFindingFilter = "all";
    private FindingCardViewModel? _selectedFinding;
    private string _smartScreenStatus = "Unbekannt";
    private string _tamperProtectionStatus = "Unbekannt";
    private string _controlledFolderAccessStatus = "Unbekannt";
    private string _exploitProtectionStatus = "Unbekannt";
    private string _lsaProtectionStatus = "Unbekannt";
    private string _credentialGuardStatus = "Unbekannt";
    private string _smartScreenLevel = "info";
    private string _tamperProtectionLevel = "info";
    private string _controlledFolderAccessLevel = "info";
    private string _exploitProtectionLevel = "info";
    private string _lsaProtectionLevel = "info";
    private string _credentialGuardLevel = "info";
    private bool _hasLiveDataSnapshot;

    private List<FindingCardViewModel> _allDefenderFindings = [];
    private List<FindingCardViewModel> _allFirewallFindings = [];
    private List<FindingCardViewModel> _allBitLockerFindings = [];

    public SecurityViewModel(ReportStore reportStore, IpcClientService ipcClient, DesktopActionRunner actionRunner)
        : base("Sicherheit", reportStore, ipcClient, actionRunner)
    {
        SetFindingFilterCommand = new RelayCommand(param => SetFindingFilter(param?.ToString()));
        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<FindingCardViewModel> DefenderFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> FirewallFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> BitLockerFindings { get; private set; } = new();
    public RelayCommand SetFindingFilterCommand { get; }

    public int CriticalSecurityCount
    {
        get => _criticalSecurityCount;
        private set
        {
            if (SetProperty(ref _criticalSecurityCount, value))
            {
                RaisePropertyChanged(nameof(SecurityPostureLabel));
                RaisePropertyChanged(nameof(SecurityPostureHint));
            }
        }
    }

    public int WarningSecurityCount
    {
        get => _warningSecurityCount;
        private set
        {
            if (SetProperty(ref _warningSecurityCount, value))
            {
                RaisePropertyChanged(nameof(SecurityPostureLabel));
                RaisePropertyChanged(nameof(SecurityPostureHint));
            }
        }
    }

    public int TotalSecurityFindings
    {
        get => _totalSecurityFindings;
        private set
        {
            if (SetProperty(ref _totalSecurityFindings, value))
            {
                RaisePropertyChanged(nameof(HasAnySecurityFindings));
            }
        }
    }

    public bool HasAnySecurityFindings => TotalSecurityFindings > 0;
    public bool HasDefenderFindings => DefenderFindings.Count > 0;
    public bool HasFirewallFindings => FirewallFindings.Count > 0;
    public bool HasBitLockerFindings => BitLockerFindings.Count > 0;
    public int VisibleSecurityFindingsCount => DefenderFindings.Count + FirewallFindings.Count + BitLockerFindings.Count;
    public bool ShowSecurityCardSkeletons => !_hasLiveDataSnapshot;

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

    public string SmartScreenStatus
    {
        get => _smartScreenStatus;
        private set => SetProperty(ref _smartScreenStatus, value);
    }

    public string TamperProtectionStatus
    {
        get => _tamperProtectionStatus;
        private set => SetProperty(ref _tamperProtectionStatus, value);
    }

    public string ControlledFolderAccessStatus
    {
        get => _controlledFolderAccessStatus;
        private set => SetProperty(ref _controlledFolderAccessStatus, value);
    }

    public string ExploitProtectionStatus
    {
        get => _exploitProtectionStatus;
        private set => SetProperty(ref _exploitProtectionStatus, value);
    }

    public string LsaProtectionStatus
    {
        get => _lsaProtectionStatus;
        private set => SetProperty(ref _lsaProtectionStatus, value);
    }

    public string CredentialGuardStatus
    {
        get => _credentialGuardStatus;
        private set => SetProperty(ref _credentialGuardStatus, value);
    }

    public string SmartScreenLevel
    {
        get => _smartScreenLevel;
        private set => SetProperty(ref _smartScreenLevel, value);
    }

    public string TamperProtectionLevel
    {
        get => _tamperProtectionLevel;
        private set => SetProperty(ref _tamperProtectionLevel, value);
    }

    public string ControlledFolderAccessLevel
    {
        get => _controlledFolderAccessLevel;
        private set => SetProperty(ref _controlledFolderAccessLevel, value);
    }

    public string ExploitProtectionLevel
    {
        get => _exploitProtectionLevel;
        private set => SetProperty(ref _exploitProtectionLevel, value);
    }

    public string LsaProtectionLevel
    {
        get => _lsaProtectionLevel;
        private set => SetProperty(ref _lsaProtectionLevel, value);
    }

    public string CredentialGuardLevel
    {
        get => _credentialGuardLevel;
        private set => SetProperty(ref _credentialGuardLevel, value);
    }

    public string FindingFilterAllLabel => $"Alle ({TotalSecurityFindings})";
    public string FindingFilterCriticalLabel => $"Kritisch ({CriticalSecurityCount})";
    public string FindingFilterWarningLabel => $"Warnung ({WarningSecurityCount})";

    public string SecurityPostureLabel => CriticalSecurityCount > 0
        ? "Kritisch"
        : WarningSecurityCount > 0
            ? "Achtung"
            : "Stabil";

    public string SecurityPostureHint => CriticalSecurityCount > 0
        ? "Sofort prüfen"
        : WarningSecurityCount > 0
            ? "In den nächsten Schritten beheben"
            : "Keine akuten Sicherheitsfunde";

    protected override void OnReportUpdated(ScanReportDto report)
    {
        if (!_hasLiveDataSnapshot &&
            (IpcClient.IsConnected || report.Findings.Count > 0 || report.SecuritySignals.Count > 0))
        {
            _hasLiveDataSnapshot = true;
            RaisePropertyChanged(nameof(ShowSecurityCardSkeletons));
        }

        List<FindingDto> defender = report.Findings.Where(f =>
            f.FindingId.StartsWith("security.defender", StringComparison.OrdinalIgnoreCase) ||
            (f.Category == FindingCategory.Security &&
             (f.Title.Contains("Defender", StringComparison.OrdinalIgnoreCase) ||
              f.Summary.Contains("Defender", StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allDefenderFindings = [.. BuildCards(defender)];

        List<FindingDto> firewall = report.Findings.Where(f => f.FindingId.StartsWith("security.firewall", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allFirewallFindings = [.. BuildCards(firewall)];

        List<FindingDto> bitLocker = report.Findings.Where(f => f.FindingId.StartsWith("security.bitlocker", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allBitLockerFindings = [.. BuildCards(bitLocker)];

        List<FindingDto> all = [.. defender, .. firewall, .. bitLocker];
        TotalSecurityFindings = all.Count;
        CriticalSecurityCount = all.Count(f => f.Severity == FindingSeverity.Critical);
        WarningSecurityCount = all.Count(f => f.Severity == FindingSeverity.Warning);
        SmartScreenStatus = GetSignalStatus(report.SecuritySignals, "smartscreen");
        TamperProtectionStatus = GetSignalStatus(report.SecuritySignals, "tamper_protection");
        ControlledFolderAccessStatus = GetSignalStatus(report.SecuritySignals, "controlled_folder_access");
        ExploitProtectionStatus = GetSignalStatus(report.SecuritySignals, "exploit_protection");
        LsaProtectionStatus = GetSignalStatus(report.SecuritySignals, "lsa_protection");
        CredentialGuardStatus = GetSignalStatus(report.SecuritySignals, "credential_guard");
        SmartScreenLevel = GetSignalLevel(SmartScreenStatus);
        TamperProtectionLevel = GetSignalLevel(TamperProtectionStatus);
        ControlledFolderAccessLevel = GetSignalLevel(ControlledFolderAccessStatus);
        ExploitProtectionLevel = GetSignalLevel(ExploitProtectionStatus);
        LsaProtectionLevel = GetSignalLevel(LsaProtectionStatus);
        CredentialGuardLevel = GetSignalLevel(CredentialGuardStatus);

        ApplyFindingFilter();
        RaisePropertyChanged(nameof(FindingFilterAllLabel));
        RaisePropertyChanged(nameof(FindingFilterCriticalLabel));
        RaisePropertyChanged(nameof(FindingFilterWarningLabel));
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
        DefenderFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allDefenderFindings, SelectedFindingFilter));
        FirewallFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allFirewallFindings, SelectedFindingFilter));
        BitLockerFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allBitLockerFindings, SelectedFindingFilter));

        RaisePropertyChanged(nameof(DefenderFindings));
        RaisePropertyChanged(nameof(FirewallFindings));
        RaisePropertyChanged(nameof(BitLockerFindings));
        RaisePropertyChanged(nameof(HasDefenderFindings));
        RaisePropertyChanged(nameof(HasFirewallFindings));
        RaisePropertyChanged(nameof(HasBitLockerFindings));
        RaisePropertyChanged(nameof(VisibleSecurityFindingsCount));

        if (SelectedFinding is not null &&
            !DefenderFindings.Contains(SelectedFinding) &&
            !FirewallFindings.Contains(SelectedFinding) &&
            !BitLockerFindings.Contains(SelectedFinding))
        {
            SelectedFinding = null;
        }
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

    private static string GetSignalStatus(IReadOnlyDictionary<string, string>? signals, string key)
    {
        if (signals is null || !signals.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return "Unbekannt";
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "on" => "Aktiv",
            "off" => "Aus",
            "warn" => "Warnung",
            "audit" => "Audit",
            _ => "Unbekannt"
        };
    }

    private static string GetSignalLevel(string status)
    {
        return status switch
        {
            "Aktiv" => "good",
            "Warnung" => "warning",
            "Aus" => "critical",
            "Audit" => "warning",
            _ => "info"
        };
    }
}


