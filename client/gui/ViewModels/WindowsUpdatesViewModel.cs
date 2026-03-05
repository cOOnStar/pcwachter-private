using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;
using PCWachter.Desktop.Views.Windows;

namespace PCWachter.Desktop.ViewModels;

public enum UpdateInstallState
{
    Idle,
    Queued,
    Installing,
    Success,
    Failed
}

public enum UpdateGroupKind
{
    Security,
    Recommended,
    Optional
}

public sealed class UpdateFilterOption
{
    public UpdateFilterOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
}

public sealed class UpdateSortOption
{
    public UpdateSortOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
}

public sealed class AppUpdateSelectionItemViewModel : ObservableObject
{
    private bool _isSelected = true;
    private bool _isDetailsExpanded;
    private UpdateInstallState _installState;
    private int _installProgress;
    private string _installError = string.Empty;

    public string PackageId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ProgramIconGlyph { get; init; } = "\uE71D";
    public ImageSource? ProgramIconImage { get; init; }
    public string InstalledVersion { get; init; } = "-";
    public string AvailableVersion { get; init; } = "-";
    public string Source { get; init; } = "winget";
    public string DownloadSizeText { get; init; } = "unbekannt";
    public long? DownloadSizeBytes { get; init; }
    public bool RequiresRestart { get; init; }
    public string DetailsText { get; init; } = "Keine Details verfügbar.";
    public string ReleaseNotes { get; init; } = "Keine Release Notes verfügbar.";
    public string Publisher { get; init; } = "Unbekannt";
    public DateTimeOffset LastSeenUtc { get; init; }
    public UpdateGroupKind GroupKind { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsDetailsExpanded
    {
        get => _isDetailsExpanded;
        set => SetProperty(ref _isDetailsExpanded, value);
    }

    public UpdateInstallState InstallState
    {
        get => _installState;
        private set
        {
            if (!SetProperty(ref _installState, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsBusy));
            RaisePropertyChanged(nameof(CanInstall));
            RaisePropertyChanged(nameof(ShowProgress));
            RaisePropertyChanged(nameof(ShowOutcome));
            RaisePropertyChanged(nameof(ShowFailureDetails));
            RaisePropertyChanged(nameof(InstallStatusText));
            RaisePropertyChanged(nameof(OutcomeText));
            RaisePropertyChanged(nameof(OutcomeLevel));
        }
    }

    public int InstallProgress
    {
        get => _installProgress;
        private set
        {
            if (!SetProperty(ref _installProgress, Math.Clamp(value, 0, 100)))
            {
                return;
            }

            RaisePropertyChanged(nameof(InstallStatusText));
        }
    }

    public string InstallError
    {
        get => _installError;
        private set
        {
            if (!SetProperty(ref _installError, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ShowFailureDetails));
        }
    }

    public bool IsBusy => InstallState is UpdateInstallState.Queued or UpdateInstallState.Installing;
    public bool CanInstall => !IsBusy;
    public bool ShowProgress => InstallState is UpdateInstallState.Queued or UpdateInstallState.Installing;
    public bool ShowOutcome => InstallState is UpdateInstallState.Success or UpdateInstallState.Failed;
    public bool ShowFailureDetails => InstallState == UpdateInstallState.Failed && !string.IsNullOrWhiteSpace(InstallError);

    public string Subtitle => $"Aktuell: {InstalledVersion} | Neu: {AvailableVersion} | Größe: {DownloadSizeText}";
    public string RestartHint => RequiresRestart ? "Neustart erforderlich" : "Kein Neustart erforderlich";

    public string GroupLabel => GroupKind switch
    {
        UpdateGroupKind.Security => "Kritisches Sicherheitsupdate",
        UpdateGroupKind.Optional => "Optionales Update",
        _ => "Empfohlen"
    };

    public string GroupLevel => GroupKind switch
    {
        UpdateGroupKind.Security => "critical",
        UpdateGroupKind.Optional => "warning",
        _ => "info"
    };

    public bool IsSecurityUpdate => GroupKind == UpdateGroupKind.Security;
    public bool IsLargeDownload => DownloadSizeBytes.HasValue && DownloadSizeBytes.Value >= WindowsUpdatesViewModel.LargeDownloadThresholdBytes;

    public int CriticalitySortValue => GroupKind switch
    {
        UpdateGroupKind.Security => 30,
        UpdateGroupKind.Recommended when RequiresRestart => 20,
        UpdateGroupKind.Recommended => 10,
        UpdateGroupKind.Optional when RequiresRestart => 5,
        _ => 0
    };

    public string InstallStatusText => InstallState switch
    {
        UpdateInstallState.Queued => "Warteschlange...",
        UpdateInstallState.Installing => $"Installiere... {InstallProgress}%",
        UpdateInstallState.Success => "Erfolgreich installiert",
        UpdateInstallState.Failed => "Installation fehlgeschlagen",
        _ => string.Empty
    };

    public string OutcomeText => InstallState switch
    {
        UpdateInstallState.Success => "✔ Erfolgreich installiert",
        UpdateInstallState.Failed => "✖ Fehlgeschlagen",
        _ => string.Empty
    };

    public string OutcomeLevel => InstallState == UpdateInstallState.Success ? "good" : "critical";

    public void SetInstallState(UpdateInstallState state, int progress = 0, string? error = null)
    {
        InstallState = state;
        InstallProgress = progress;
        InstallError = state == UpdateInstallState.Failed
            ? (string.IsNullOrWhiteSpace(error) ? "Unbekannter Fehler." : error.Trim())
            : string.Empty;
    }

    public void ToggleExpandedDetails() => IsDetailsExpanded = !IsDetailsExpanded;
}

public sealed class UpdateHistoryEntryViewModel
{
    public DateTimeOffset TimestampUtc { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public bool IsError { get; init; }

    public string TimeText => TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}

public sealed class WindowsUpdatesViewModel : ReportPageViewModelBase
{
    private const string AppsUpdateSelectedActionId = "action.apps.update_selected";
    private const string InstallWindowsUpdatesActionId = "action.updates.install_security";
    private const string InstallOptionalActionId = "action.updates.install_optional";
    private const string OpenDriverUpdatesActionId = "action.updates.open_driver_updates";
    internal const long LargeDownloadThresholdBytes = 500L * 1024L * 1024L;

    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private static readonly Dictionary<string, ImageSource?> ProgramIconCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ObservableCollection<AppUpdateSelectionItemViewModel> _softwareUpdates = new();
    private readonly ObservableCollection<AppUpdateSelectionItemViewModel> _filteredSoftwareUpdates = new();
    private readonly ObservableCollection<AppUpdateSelectionItemViewModel> _securityUpdates = new();
    private readonly ObservableCollection<AppUpdateSelectionItemViewModel> _recommendedUpdates = new();
    private readonly ObservableCollection<AppUpdateSelectionItemViewModel> _optionalUpdates = new();
    private readonly ICollectionView _softwareUpdatesView;

    private int _softwareUpdatesCount;
    private int _selectedSoftwareUpdatesCount;
    private int _securityUpdatesCount;
    private int _optionalUpdatesCount;
    private int _driverUpdatesCount;
    private int _criticalUpdatesCount;

    private bool _isWingetAvailable = true;
    private string _wingetVersion = "-";
    private string _selectedSection = "software";
    private string _softwareSearchText = string.Empty;

    private bool _isSecurityGroupExpanded = true;
    private bool _isRecommendedGroupExpanded = true;
    private bool _isOptionalGroupExpanded;

    private UpdateFilterOption _selectedFilterOption;
    private UpdateSortOption _selectedSortOption;

    private AppUpdateSelectionItemViewModel? _selectedSoftwareUpdate;
    private FindingCardViewModel? _selectedWindowsFinding;
    private FindingCardViewModel? _selectedOptionalFinding;

    private bool _hasSelectedUpdateDetail;
    private string _detailTitle = "Update auswählen";
    private string _detailSummary = "Wähle links einen Eintrag aus, um Details zu sehen.";
    private string _detailReleaseNotes = "-";
    private string _detailRestartHint = "-";
    private string _detailSourceLabel = "-";
    private string _detailSeverityLabel = "-";
    private string _detailRecommendedAction = "-";

    private string _lastUpdateErrorAction = "-";
    private string _lastUpdateErrorCode = "-";
    private string _lastUpdateErrorMessage = string.Empty;
    private string _lastUpdateErrorHint = string.Empty;
    private DateTimeOffset? _lastUpdateErrorAtUtc;

    public WindowsUpdatesViewModel(ReportStore reportStore, IpcClientService ipcClient, DesktopActionRunner actionRunner)
        : base("Updates", reportStore, ipcClient, actionRunner)
    {
        FilterOptions =
        [
            new UpdateFilterOption("all", "Alle"),
            new UpdateFilterOption("security", "Nur Sicherheitsupdates"),
            new UpdateFilterOption("restart", "Neustart erforderlich"),
            new UpdateFilterOption("large", "Große Downloads")
        ];

        SortOptions =
        [
            new UpdateSortOption("name", "Name"),
            new UpdateSortOption("size", "Größe"),
            new UpdateSortOption("criticality", "Kritikalität"),
            new UpdateSortOption("lastseen", "Zuletzt gesehen")
        ];

        _selectedFilterOption = FilterOptions[0];
        _selectedSortOption = SortOptions[0];

        _softwareUpdatesView = CollectionViewSource.GetDefaultView(_softwareUpdates);
        _softwareUpdatesView.Filter = FilterSoftwareView;

        SelectSectionCommand = new RelayCommand(param => SelectSection(param?.ToString()));
        ClearSoftwareSearchCommand = new RelayCommand(() => SoftwareSearchText = string.Empty, () => HasSoftwareSearchText);

        SelectAllCommand = new RelayCommand(_ => SetAllSoftwareSelections(true), _ => FilteredSoftwareUpdatesCount > 0);
        ClearSelectionCommand = new RelayCommand(_ => SetAllSoftwareSelections(false), _ => HasSelectedSoftwareUpdates);

        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync, CanInstallSelected);
        InstallAllCommand = new AsyncRelayCommand(InstallAllAsync, CanInstallAll);
        InstallFromHeaderCommand = new AsyncRelayCommand(InstallFromHeaderAsync, CanInstallFromHeader);
        InstallOneCommand = new AsyncRelayCommand(InstallOneAsync, CanInstallOne);

        InstallSecurityUpdatesCommand = new AsyncRelayCommand(InstallSecurityUpdatesAsync);
        InstallOptionalUpdatesCommand = new AsyncRelayCommand(InstallOptionalUpdatesAsync);
        OpenDriverUpdatesCommand = new AsyncRelayCommand(OpenDriverUpdatesAsync);
        RefreshUpdatesCommand = new AsyncRelayCommand(RefreshUpdatesAsync);

        SelectSoftwareUpdateCommand = new RelayCommand(SelectSoftwareUpdate);
        SelectWindowsFindingCommand = new RelayCommand(SelectWindowsFinding);
        SelectOptionalFindingCommand = new RelayCommand(SelectOptionalFinding);

        ShowDetailsCommand = new RelayCommand(OpenUpdateDetailsDialog);
        ToggleExpandDetailsCommand = new RelayCommand(ToggleExpandedDetails);
        OpenUpdateDetailsDialogCommand = ShowDetailsCommand;
        ClearUpdateErrorCommand = new RelayCommand(ClearLastUpdateError, _ => HasLastUpdateError);

        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<AppUpdateSelectionItemViewModel> SoftwareUpdates => _softwareUpdates;
    public ObservableCollection<AppUpdateSelectionItemViewModel> FilteredSoftwareUpdates => _filteredSoftwareUpdates;
    public ObservableCollection<AppUpdateSelectionItemViewModel> SecurityUpdates => _securityUpdates;
    public ObservableCollection<AppUpdateSelectionItemViewModel> RecommendedUpdates => _recommendedUpdates;
    public ObservableCollection<AppUpdateSelectionItemViewModel> OptionalUpdates => _optionalUpdates;

    public ObservableCollection<FindingCardViewModel> SecurityUpdatesFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> WindowsCombinedFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> OptionalUpdatesFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> DriverUpdatesFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> OptionalCombinedFindings { get; private set; } = new();
    public ObservableCollection<UpdateHistoryEntryViewModel> UpdateHistory { get; } = new();

    public ObservableCollection<UpdateFilterOption> FilterOptions { get; }
    public ObservableCollection<UpdateSortOption> SortOptions { get; }

    public RelayCommand SelectSectionCommand { get; }
    public RelayCommand ClearSoftwareSearchCommand { get; }
    public RelayCommand SelectSoftwareUpdateCommand { get; }
    public RelayCommand SelectWindowsFindingCommand { get; }
    public RelayCommand SelectOptionalFindingCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public RelayCommand ToggleExpandDetailsCommand { get; }
    public RelayCommand OpenUpdateDetailsDialogCommand { get; }
    public RelayCommand ClearUpdateErrorCommand { get; }

    public RelayCommand SelectAllCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }

    public AsyncRelayCommand InstallSelectedCommand { get; }
    public AsyncRelayCommand InstallAllCommand { get; }
    public AsyncRelayCommand InstallFromHeaderCommand { get; }
    public AsyncRelayCommand InstallOneCommand { get; }

    public AsyncRelayCommand UpdateSelectedAppsCommand => InstallSelectedCommand;
    public RelayCommand SelectAllAppsCommand => SelectAllCommand;
    public RelayCommand DeselectAllAppsCommand => ClearSelectionCommand;
    public AsyncRelayCommand InstallSingleAppCommand => InstallOneCommand;

    public AsyncRelayCommand InstallSecurityUpdatesCommand { get; }
    public AsyncRelayCommand InstallOptionalUpdatesCommand { get; }
    public AsyncRelayCommand OpenDriverUpdatesCommand { get; }
    public AsyncRelayCommand RefreshUpdatesCommand { get; }

    public int SoftwareUpdatesCount { get => _softwareUpdatesCount; private set => SetProperty(ref _softwareUpdatesCount, value); }
    public int SelectedSoftwareUpdatesCount { get => _selectedSoftwareUpdatesCount; private set => SetProperty(ref _selectedSoftwareUpdatesCount, value); }
    public int SecurityUpdatesCount { get => _securityUpdatesCount; private set => SetProperty(ref _securityUpdatesCount, value); }
    public int OptionalUpdatesCount { get => _optionalUpdatesCount; private set => SetProperty(ref _optionalUpdatesCount, value); }
    public int DriverUpdatesCount { get => _driverUpdatesCount; private set => SetProperty(ref _driverUpdatesCount, value); }
    public int CriticalUpdatesCount { get => _criticalUpdatesCount; private set => SetProperty(ref _criticalUpdatesCount, value); }

    public bool IsWingetAvailable { get => _isWingetAvailable; private set => SetProperty(ref _isWingetAvailable, value); }
    public string WingetVersion { get => _wingetVersion; private set => SetProperty(ref _wingetVersion, value); }

    public string SoftwareSearchText
    {
        get => _softwareSearchText;
        set
        {
            if (!SetProperty(ref _softwareSearchText, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasSoftwareSearchText));
            ApplySoftwareView();
        }
    }

    public UpdateFilterOption SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            if (value is null || !SetProperty(ref _selectedFilterOption, value))
            {
                return;
            }

            ApplySoftwareView();
        }
    }

    public UpdateSortOption SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (value is null || !SetProperty(ref _selectedSortOption, value))
            {
                return;
            }

            ApplySoftwareView();
        }
    }

    public string SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (!SetProperty(ref _selectedSection, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsSoftwareSectionSelected));
            RaisePropertyChanged(nameof(IsWindowsSectionSelected));
            RaisePropertyChanged(nameof(IsOptionalSectionSelected));
        }
    }

    public bool IsSecurityGroupExpanded { get => _isSecurityGroupExpanded; set => SetProperty(ref _isSecurityGroupExpanded, value); }
    public bool IsRecommendedGroupExpanded { get => _isRecommendedGroupExpanded; set => SetProperty(ref _isRecommendedGroupExpanded, value); }
    public bool IsOptionalGroupExpanded { get => _isOptionalGroupExpanded; set => SetProperty(ref _isOptionalGroupExpanded, value); }

    public bool IsSoftwareSectionSelected => string.Equals(SelectedSection, "software", StringComparison.OrdinalIgnoreCase);
    public bool IsWindowsSectionSelected => string.Equals(SelectedSection, "windows", StringComparison.OrdinalIgnoreCase);
    public bool IsOptionalSectionSelected => string.Equals(SelectedSection, "optional", StringComparison.OrdinalIgnoreCase);

    public bool HasSoftwareUpdates => SoftwareUpdatesCount > 0;
    public bool HasFilteredSoftwareUpdates => FilteredSoftwareUpdatesCount > 0;
    public bool HasSelectedSoftwareUpdates => SelectedSoftwareUpdatesCount > 0;
    public bool HasSoftwareSearchText => !string.IsNullOrWhiteSpace(SoftwareSearchText);

    public int SoftwareSectionBadgeCount => SoftwareUpdatesCount;
    public int WindowsSectionBadgeCount => SecurityUpdatesCount;
    public int OptionalSectionBadgeCount => OptionalUpdatesCount + DriverUpdatesCount;

    public int FilteredSoftwareUpdatesCount => FilteredSoftwareUpdates.Count;
    public int SecurityGroupCount => SecurityUpdates.Count;
    public int RecommendedGroupCount => RecommendedUpdates.Count;
    public int OptionalGroupCount => OptionalUpdates.Count;

    public string SecurityGroupTitle => $"Sicherheitsupdates ({SecurityGroupCount})";
    public string RecommendedGroupTitle => $"Empfohlene Updates ({RecommendedGroupCount})";
    public string OptionalGroupTitle => $"Optionale Updates ({OptionalGroupCount})";

    public long TotalDownloadSizeBytes => FilteredSoftwareUpdates.Where(x => x.DownloadSizeBytes.HasValue).Sum(x => x.DownloadSizeBytes ?? 0);
    public string TotalDownloadSizeText => TotalDownloadSizeBytes > 0 ? FormatBytes(TotalDownloadSizeBytes) : "-";

    public int HeaderTotalCount => FilteredSoftwareUpdatesCount;
    public int HeaderSecurityCount => SecurityGroupCount;
    public bool HasCriticalSecurityHint => HeaderSecurityCount > 0;
    public string CriticalSecurityHintText => HeaderSecurityCount > 0 ? $"⚠ {HeaderSecurityCount} Sicherheitsupdates sind offen" : "Keine kritischen Sicherheitsupdates offen";
    public string HeaderInstallButtonText => HasSelectedSoftwareUpdates ? $"{SelectedSoftwareUpdatesCount} ausgewählt – Installieren" : "Alle installieren";

    public string SoftwareStatusHint => IsWingetAvailable ? $"WinGet verfügbar ({WingetVersion})" : "WinGet fehlt oder ist im Service-Kontext nicht verfügbar";

    public string LastUpdateErrorAction { get => _lastUpdateErrorAction; private set => SetProperty(ref _lastUpdateErrorAction, value); }
    public string LastUpdateErrorCode { get => _lastUpdateErrorCode; private set => SetProperty(ref _lastUpdateErrorCode, value); }
    public string LastUpdateErrorMessage { get => _lastUpdateErrorMessage; private set => SetProperty(ref _lastUpdateErrorMessage, value); }
    public string LastUpdateErrorHint { get => _lastUpdateErrorHint; private set => SetProperty(ref _lastUpdateErrorHint, value); }
    public string LastUpdateErrorTimeText => _lastUpdateErrorAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "-";
    public bool HasLastUpdateError => !string.IsNullOrWhiteSpace(LastUpdateErrorMessage);

    public bool HasSelectedUpdateDetail
    {
        get => _hasSelectedUpdateDetail;
        private set => SetProperty(ref _hasSelectedUpdateDetail, value);
    }

    public string DetailTitle
    {
        get => _detailTitle;
        private set => SetProperty(ref _detailTitle, value);
    }

    public string DetailSummary
    {
        get => _detailSummary;
        private set => SetProperty(ref _detailSummary, value);
    }

    public string DetailReleaseNotes
    {
        get => _detailReleaseNotes;
        private set => SetProperty(ref _detailReleaseNotes, value);
    }

    public string DetailRestartHint
    {
        get => _detailRestartHint;
        private set => SetProperty(ref _detailRestartHint, value);
    }

    public string DetailSourceLabel
    {
        get => _detailSourceLabel;
        private set => SetProperty(ref _detailSourceLabel, value);
    }

    public string DetailSeverityLabel
    {
        get => _detailSeverityLabel;
        private set => SetProperty(ref _detailSeverityLabel, value);
    }

    public string DetailRecommendedAction
    {
        get => _detailRecommendedAction;
        private set => SetProperty(ref _detailRecommendedAction, value);
    }

    protected override void OnReportUpdated(ScanReportDto report)
    {
        string? selectedSoftwarePackage = _selectedSoftwareUpdate?.PackageId;

        List<AppUpdateSelectionItemViewModel> softwareItems = BuildSoftwareItems(report);
        RebuildSoftwareCollection(softwareItems);

        List<FindingDto> security = report.Findings.Where(f =>
                f.FindingId.StartsWith("updates.security", StringComparison.OrdinalIgnoreCase) ||
                f.FindingId.Equals("updates.security.missing", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        SecurityUpdatesFindings = BuildCards(security);
        SecurityUpdatesCount = security.Count;

        List<FindingDto> optional = report.Findings
            .Where(f => f.FindingId.StartsWith("updates.optional", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        OptionalUpdatesFindings = BuildCards(optional);
        OptionalUpdatesCount = optional.Count;

        List<FindingDto> driver = report.Findings
            .Where(f => f.FindingId.StartsWith("updates.drivers", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        DriverUpdatesFindings = BuildCards(driver);
        DriverUpdatesCount = driver.Count;

        WindowsCombinedFindings = BuildCards(security);
        OptionalCombinedFindings = BuildCards(optional.Concat(driver));

        CriticalUpdatesCount = security.Concat(optional).Concat(driver).Count(f => f.Severity == FindingSeverity.Critical);

        if (!string.IsNullOrWhiteSpace(selectedSoftwarePackage))
        {
            _selectedSoftwareUpdate = SoftwareUpdates.FirstOrDefault(x => string.Equals(x.PackageId, selectedSoftwarePackage, StringComparison.OrdinalIgnoreCase));
        }

        RefreshDetailPanelForActiveSelection();
    }

    private void SelectSection(string? section)
    {
        SelectedSection = section?.Trim().ToLowerInvariant() switch
        {
            "windows" => "windows",
            "optional" => "optional",
            _ => "software"
        };

        RefreshDetailPanelForActiveSelection();
    }

    private bool FilterSoftwareView(object obj)
    {
        if (obj is not AppUpdateSelectionItemViewModel item)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SoftwareSearchText))
        {
            string needle = SoftwareSearchText.Trim();
            bool matchesSearch = item.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                                 item.Subtitle.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                                 item.Source.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                                 item.Publisher.Contains(needle, StringComparison.OrdinalIgnoreCase);
            if (!matchesSearch)
            {
                return false;
            }
        }

        return SelectedFilterOption.Key switch
        {
            "security" => item.IsSecurityUpdate,
            "restart" => item.RequiresRestart,
            "large" => item.IsLargeDownload,
            _ => true
        };
    }

    private void ApplySoftwareView()
    {
        IEnumerable<AppUpdateSelectionItemViewModel> filtered = _softwareUpdates.Where(item => FilterSoftwareView(item));

        filtered = SelectedSortOption.Key switch
        {
            "size" => filtered.OrderByDescending(x => x.DownloadSizeBytes ?? 0L).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "criticality" => filtered.OrderByDescending(x => x.CriticalitySortValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "lastseen" => filtered.OrderByDescending(x => x.LastSeenUtc).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        };

        ReplaceCollection(_filteredSoftwareUpdates, filtered);
        ReplaceCollection(_securityUpdates, _filteredSoftwareUpdates.Where(x => x.GroupKind == UpdateGroupKind.Security));
        ReplaceCollection(_recommendedUpdates, _filteredSoftwareUpdates.Where(x => x.GroupKind == UpdateGroupKind.Recommended));
        ReplaceCollection(_optionalUpdates, _filteredSoftwareUpdates.Where(x => x.GroupKind == UpdateGroupKind.Optional));

        SelectedSoftwareUpdatesCount = _softwareUpdates.Count(x => x.IsSelected);

        RaisePropertyChanged(nameof(FilteredSoftwareUpdates));
        RaisePropertyChanged(nameof(FilteredSoftwareUpdatesCount));
        RaisePropertyChanged(nameof(HasFilteredSoftwareUpdates));
        RaisePropertyChanged(nameof(SecurityGroupCount));
        RaisePropertyChanged(nameof(RecommendedGroupCount));
        RaisePropertyChanged(nameof(OptionalGroupCount));
        RaisePropertyChanged(nameof(SecurityGroupTitle));
        RaisePropertyChanged(nameof(RecommendedGroupTitle));
        RaisePropertyChanged(nameof(OptionalGroupTitle));
        RaisePropertyChanged(nameof(HeaderTotalCount));
        RaisePropertyChanged(nameof(HeaderSecurityCount));
        RaisePropertyChanged(nameof(TotalDownloadSizeBytes));
        RaisePropertyChanged(nameof(TotalDownloadSizeText));
        RaisePropertyChanged(nameof(HasCriticalSecurityHint));
        RaisePropertyChanged(nameof(CriticalSecurityHintText));
        RaisePropertyChanged(nameof(HeaderInstallButtonText));

        SelectAllCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
        InstallSelectedCommand.RaiseCanExecuteChanged();
        InstallAllCommand.RaiseCanExecuteChanged();
        InstallFromHeaderCommand.RaiseCanExecuteChanged();
    }

    private void SetAllSoftwareSelections(bool value)
    {
        IEnumerable<AppUpdateSelectionItemViewModel> source = _filteredSoftwareUpdates.Count > 0 ? _filteredSoftwareUpdates : _softwareUpdates;
        foreach (AppUpdateSelectionItemViewModel item in source)
        {
            if (!item.IsBusy)
            {
                item.IsSelected = value;
            }
        }

        SelectedSoftwareUpdatesCount = _softwareUpdates.Count(x => x.IsSelected);
        RaisePropertyChanged(nameof(HeaderInstallButtonText));
    }

    private void OnSoftwareItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppUpdateSelectionItemViewModel.IsSelected) ||
            e.PropertyName == nameof(AppUpdateSelectionItemViewModel.InstallState))
        {
            SelectedSoftwareUpdatesCount = _softwareUpdates.Count(x => x.IsSelected);
            RaisePropertyChanged(nameof(HeaderInstallButtonText));
            InstallSelectedCommand.RaiseCanExecuteChanged();
            InstallAllCommand.RaiseCanExecuteChanged();
            InstallFromHeaderCommand.RaiseCanExecuteChanged();
            InstallOneCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task InstallFromHeaderAsync()
    {
        if (HasSelectedSoftwareUpdates)
        {
            await InstallSelectedAsync();
            return;
        }

        await InstallAllAsync();
    }

    private async Task InstallAllAsync()
    {
        List<AppUpdateSelectionItemViewModel> queue = _filteredSoftwareUpdates.Where(x => x.CanInstall && !string.IsNullOrWhiteSpace(x.PackageId)).ToList();
        if (queue.Count == 0)
        {
            return;
        }

        foreach (AppUpdateSelectionItemViewModel item in queue)
        {
            item.IsSelected = true;
        }

        await ExecuteInstallQueueAsync(queue);
    }

    private async Task InstallSelectedAsync()
    {
        List<AppUpdateSelectionItemViewModel> queue = _softwareUpdates
            .Where(x => x.IsSelected && x.CanInstall && !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();
        if (queue.Count == 0)
        {
            return;
        }

        await ExecuteInstallQueueAsync(queue);
    }

    private async Task InstallOneAsync(object? parameter)
    {
        if (parameter is not AppUpdateSelectionItemViewModel item || !CanInstallOne(parameter))
        {
            return;
        }

        await ExecuteInstallQueueAsync([item]);
    }

    private async Task ExecuteInstallQueueAsync(List<AppUpdateSelectionItemViewModel> queue)
    {
        if (!IsWingetAvailable || queue.Count == 0)
        {
            return;
        }

        ClearLastUpdateError();
        foreach (AppUpdateSelectionItemViewModel item in queue)
        {
            item.SetInstallState(UpdateInstallState.Queued, 5);
        }

        int success = 0;
        int failed = 0;
        foreach (AppUpdateSelectionItemViewModel item in queue)
        {
            bool ok = await InstallOneInternalAsync(item);
            if (ok)
            {
                success++;
            }
            else
            {
                failed++;
            }
        }

        AppendHistory("Installationslauf abgeschlossen", failed > 0 ? "Warnung" : "Erfolg", "WinGet", $"{success} erfolgreich, {failed} fehlgeschlagen", failed > 0);

        try
        {
            await IpcClient.TriggerScanAsync();
        }
        catch
        {
        }
    }

    private async Task<bool> InstallOneInternalAsync(AppUpdateSelectionItemViewModel item)
    {
        try
        {
            item.SetInstallState(UpdateInstallState.Installing, 15);
            await Task.Delay(90);
            item.SetInstallState(UpdateInstallState.Installing, 42);
            await Task.Delay(90);
            item.SetInstallState(UpdateInstallState.Installing, 68);

            ActionExecutionResultDto result = await IpcClient.RunActionAsync(
                AppsUpdateSelectedActionId,
                parameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["package_ids"] = item.PackageId
                });

            if (result.Success)
            {
                item.SetInstallState(UpdateInstallState.Success, 100);
                item.IsSelected = false;
                AppendHistory($"{item.Name} aktualisiert", "Erfolg", "WinGet", result.Message, isError: false);
                return true;
            }

            item.SetInstallState(UpdateInstallState.Failed, 0, result.Message);
            string hint = string.IsNullOrWhiteSpace(result.RollbackHint)
                ? "Bitte erneut versuchen oder das Paket manuell über WinGet aktualisieren."
                : result.RollbackHint;
            SetLastUpdateError(item.Name, result.Message, result.ExitCode, hint);
            AppendHistory($"{item.Name} fehlgeschlagen", "Fehler", "WinGet", result.Message, isError: true);
            return false;
        }
        catch (Exception ex)
        {
            item.SetInstallState(UpdateInstallState.Failed, 0, ex.Message);
            SetLastUpdateError(item.Name, ex.Message, -1, "Bitte Verbindung zum Service prüfen und erneut versuchen.");
            AppendHistory($"{item.Name} fehlgeschlagen", "Fehler", "WinGet", ex.Message, isError: true);
            return false;
        }
    }

    private async Task InstallSecurityUpdatesAsync()
    {
        try
        {
            ActionExecutionResultDto result = await IpcClient.RunActionAsync(InstallWindowsUpdatesActionId);
            HandleActionResult("Windows Updates", result, source: "Windows Update");
            await IpcClient.TriggerScanAsync();
        }
        catch (Exception ex)
        {
            SetLastUpdateError("Windows Updates", ex.Message, -1, "Bitte später erneut versuchen oder manuell über Windows Update starten.");
            AppendHistory("Windows Updates fehlgeschlagen", "Fehler", "Windows Update", ex.Message, isError: true);
        }
    }

    private async Task InstallOptionalUpdatesAsync()
    {
        try
        {
            FindingDto? finding = ReportStore.CurrentReport.Findings.FirstOrDefault(f =>
                f.FindingId.StartsWith("updates.optional", StringComparison.OrdinalIgnoreCase));

            if (finding is null)
            {
                ActionExecutionResultDto result = await IpcClient.RunActionAsync(InstallOptionalActionId);
                HandleActionResult("Optionale Updates", result, source: "Windows Update");
                await IpcClient.TriggerScanAsync();
                return;
            }

            await ActionRunner.RunBestFixAsync(finding);
            ClearLastUpdateError();
            AppendHistory("Optionale Updates gestartet", "Gestartet", "Windows Update", finding.Title, isError: false);
        }
        catch (Exception ex)
        {
            SetLastUpdateError("Optionale Updates", ex.Message, -1, "Bitte später erneut versuchen oder manuell über Windows Update starten.");
            AppendHistory("Optionale Updates fehlgeschlagen", "Fehler", "Windows Update", ex.Message, isError: true);
        }
    }

    private Task OpenDriverUpdatesAsync()
    {
        try
        {
            FindingDto? finding = ReportStore.CurrentReport.Findings.FirstOrDefault(f =>
                f.FindingId.StartsWith("updates.drivers", StringComparison.OrdinalIgnoreCase));
            ActionDto? action = finding?.Actions.FirstOrDefault(a => string.Equals(a.ActionId, OpenDriverUpdatesActionId, StringComparison.OrdinalIgnoreCase));
            string target = action?.ExternalTarget ?? "ms-settings:windowsupdate-optionalupdates";
            DesktopActionRunner.OpenExternal(target);

            ClearLastUpdateError();
            AppendHistory("Treiberseite geöffnet", "Info", "Windows Update", target, isError: false);
        }
        catch (Exception ex)
        {
            SetLastUpdateError("Treiberseite", ex.Message, -1, "Windows Einstellungen konnten nicht geöffnet werden.");
            AppendHistory("Treiberseite fehlgeschlagen", "Fehler", "Windows Update", ex.Message, isError: true);
        }

        return Task.CompletedTask;
    }

    private async Task RefreshUpdatesAsync()
    {
        try
        {
            ClearLastUpdateError();
            AppendHistory("Update-Suche gestartet", "Info", "Service", "Suche nach neuen Updates wurde gestartet.", isError: false);
            await IpcClient.TriggerScanAsync();
            AppendHistory("Update-Suche abgeschlossen", "Erfolg", "Service", "Aktueller Update-Stand wurde neu geladen.", isError: false);
        }
        catch (Exception ex)
        {
            SetLastUpdateError("Update-Suche", ex.Message, -1, "Bitte Service-Verbindung prüfen und erneut versuchen.");
            AppendHistory("Update-Suche fehlgeschlagen", "Fehler", "Service", ex.Message, isError: true);
        }
    }

    private bool CanInstallSelected()
    {
        return IsWingetAvailable && _softwareUpdates.Any(x => x.IsSelected && x.CanInstall && !string.IsNullOrWhiteSpace(x.PackageId));
    }

    private bool CanInstallAll()
    {
        return IsWingetAvailable && _filteredSoftwareUpdates.Any(x => x.CanInstall && !string.IsNullOrWhiteSpace(x.PackageId));
    }

    private bool CanInstallFromHeader()
    {
        return IsWingetAvailable && (CanInstallSelected() || CanInstallAll());
    }

    private bool CanInstallOne(object? parameter)
    {
        return IsWingetAvailable &&
               parameter is AppUpdateSelectionItemViewModel item &&
               item.CanInstall &&
               !string.IsNullOrWhiteSpace(item.PackageId);
    }

    private void SelectSoftwareUpdate(object? parameter)
    {
        if (parameter is not AppUpdateSelectionItemViewModel item)
        {
            return;
        }

        _selectedSoftwareUpdate = item;
        SetDetailFromSoftware(item);
    }

    private void SelectWindowsFinding(object? parameter)
    {
        if (parameter is not FindingCardViewModel item)
        {
            return;
        }

        _selectedWindowsFinding = item;
        SetDetailFromFinding(item, "Windows Update");
    }

    private void SelectOptionalFinding(object? parameter)
    {
        if (parameter is not FindingCardViewModel item)
        {
            return;
        }

        _selectedOptionalFinding = item;
        SetDetailFromFinding(item, "Optional/Treiber");
    }

    private void ToggleExpandedDetails(object? parameter)
    {
        if (parameter is AppUpdateSelectionItemViewModel item)
        {
            item.ToggleExpandedDetails();
        }
    }

    private void OpenUpdateDetailsDialog(object? parameter)
    {
        if (parameter is AppUpdateSelectionItemViewModel softwareItem)
        {
            _selectedSoftwareUpdate = softwareItem;
            SetDetailFromSoftware(softwareItem);
            ShowDetailsPopup();
            return;
        }

        if (parameter is FindingCardViewModel findingItem)
        {
            SetDetailFromFinding(findingItem, "Windows Update");
            ShowDetailsPopup();
            return;
        }

        if (HasSelectedUpdateDetail)
        {
            ShowDetailsPopup();
        }
    }

    private void ShowDetailsPopup()
    {
        var model = new UpdateDetailsDialogModel
        {
            Title = DetailTitle,
            SourceLabel = DetailSourceLabel,
            SeverityLabel = DetailSeverityLabel,
            RestartHint = DetailRestartHint,
            Summary = DetailSummary,
            RecommendedAction = DetailRecommendedAction,
            ReleaseNotes = DetailReleaseNotes
        };

        var window = new UpdateDetailsWindow(model);
        if (Application.Current?.MainWindow is Window owner)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    private void SetDetailFromSoftware(AppUpdateSelectionItemViewModel item)
    {
        SetDetailPanel(
            title: item.Name,
            summary: item.DetailsText,
            sourceLabel: $"Quelle: {item.Source} | Publisher: {item.Publisher}",
            severityLabel: item.GroupLabel,
            restartHint: item.RestartHint,
            releaseNotes: item.ReleaseNotes,
            recommendedAction: "Ausgewähltes Paket installieren");
    }

    private void SetDetailFromFinding(FindingCardViewModel card, string source)
    {
        SetDetailPanel(
            title: card.Title,
            summary: string.IsNullOrWhiteSpace(card.Summary) ? card.WhatIsThisText : card.Summary,
            sourceLabel: source,
            severityLabel: card.SeverityText,
            restartHint: card.RestartHintText,
            releaseNotes: card.ReleaseNotesText,
            recommendedAction: card.RecommendedActionText);
    }

    private void SetDetailPanel(
        string title,
        string summary,
        string sourceLabel,
        string severityLabel,
        string restartHint,
        string releaseNotes,
        string recommendedAction)
    {
        DetailTitle = string.IsNullOrWhiteSpace(title) ? "Update" : title.Trim();
        DetailSummary = string.IsNullOrWhiteSpace(summary) ? "Keine Detailbeschreibung verfügbar." : summary.Trim();
        DetailSourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "-" : sourceLabel.Trim();
        DetailSeverityLabel = string.IsNullOrWhiteSpace(severityLabel) ? "-" : severityLabel.Trim();
        DetailRestartHint = string.IsNullOrWhiteSpace(restartHint) ? "-" : restartHint.Trim();
        DetailReleaseNotes = string.IsNullOrWhiteSpace(releaseNotes) ? "Keine Release Notes verfügbar." : releaseNotes.Trim();
        DetailRecommendedAction = string.IsNullOrWhiteSpace(recommendedAction) ? "Manuell prüfen" : recommendedAction.Trim();
        HasSelectedUpdateDetail = true;
    }

    private void RefreshDetailPanelForActiveSelection()
    {
        if (_selectedSoftwareUpdate is not null)
        {
            SetDetailFromSoftware(_selectedSoftwareUpdate);
            return;
        }

        if (_selectedWindowsFinding is not null)
        {
            SetDetailFromFinding(_selectedWindowsFinding, "Windows Update");
            return;
        }

        if (_selectedOptionalFinding is not null)
        {
            SetDetailFromFinding(_selectedOptionalFinding, "Optional/Treiber");
            return;
        }

        if (!HasSelectedUpdateDetail)
        {
            DetailTitle = "Update auswählen";
            DetailSummary = "Wähle links einen Eintrag aus, um Details zu sehen.";
            DetailSourceLabel = "-";
            DetailSeverityLabel = "-";
            DetailRestartHint = "-";
            DetailReleaseNotes = "-";
            DetailRecommendedAction = "-";
        }
    }

    private void HandleActionResult(string actionName, ActionExecutionResultDto result, string source)
    {
        if (result.Success)
        {
            ClearLastUpdateError();
            AppendHistory($"{actionName} erfolgreich", "Erfolg", source, result.Message, isError: false);
            return;
        }

        string hint = string.IsNullOrWhiteSpace(result.RollbackHint)
            ? "Bitte erneut versuchen oder manuell in Windows prüfen."
            : result.RollbackHint;

        SetLastUpdateError(actionName, result.Message, result.ExitCode, hint);
        AppendHistory($"{actionName} fehlgeschlagen", "Fehler", source, result.Message, isError: true);
    }

    private void SetLastUpdateError(string actionName, string message, int exitCode, string hint)
    {
        LastUpdateErrorAction = string.IsNullOrWhiteSpace(actionName) ? "Update" : actionName;
        LastUpdateErrorCode = exitCode >= 0 ? exitCode.ToString() : "-";
        LastUpdateErrorMessage = string.IsNullOrWhiteSpace(message) ? "Unbekannter Fehler." : message.Trim();
        LastUpdateErrorHint = string.IsNullOrWhiteSpace(hint) ? "Bitte erneut versuchen." : hint.Trim();
        _lastUpdateErrorAtUtc = DateTimeOffset.UtcNow;

        RaisePropertyChanged(nameof(HasLastUpdateError));
        RaisePropertyChanged(nameof(LastUpdateErrorTimeText));
        ClearUpdateErrorCommand.RaiseCanExecuteChanged();
    }

    private void ClearLastUpdateError(object? _ = null)
    {
        LastUpdateErrorAction = "-";
        LastUpdateErrorCode = "-";
        LastUpdateErrorMessage = string.Empty;
        LastUpdateErrorHint = string.Empty;
        _lastUpdateErrorAtUtc = null;

        RaisePropertyChanged(nameof(HasLastUpdateError));
        RaisePropertyChanged(nameof(LastUpdateErrorTimeText));
        ClearUpdateErrorCommand.RaiseCanExecuteChanged();
    }

    private void AppendHistory(string title, string status, string source, string details, bool isError)
    {
        UpdateHistory.Insert(0, new UpdateHistoryEntryViewModel
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Title = title,
            Status = status,
            Source = source,
            Details = details,
            IsError = isError
        });

        while (UpdateHistory.Count > 30)
        {
            UpdateHistory.RemoveAt(UpdateHistory.Count - 1);
        }
    }

    private List<AppUpdateSelectionItemViewModel> BuildSoftwareItems(ScanReportDto report)
    {
        Dictionary<string, bool> selectedByPackage = _softwareUpdates
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToDictionary(x => x.PackageId, x => x.IsSelected, StringComparer.OrdinalIgnoreCase);

        List<FindingDto> appFindings = report.Findings
            .Where(f => f.FindingId.StartsWith("apps.outdated.", StringComparison.OrdinalIgnoreCase) &&
                        !f.FindingId.Equals("apps.outdated.count", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IsWingetAvailable = report.Findings.All(f => !f.FindingId.Equals("apps.winget.unavailable", StringComparison.OrdinalIgnoreCase));

        FindingDto? summary = report.Findings.FirstOrDefault(f => f.FindingId.Equals("apps.outdated.count", StringComparison.OrdinalIgnoreCase));
        WingetVersion = summary?.Evidence.TryGetValue("winget_version", out string? version) == true && !string.IsNullOrWhiteSpace(version)
            ? version
            : (IsWingetAvailable ? "unbekannt" : "-");

        var list = new List<AppUpdateSelectionItemViewModel>();
        foreach (FindingDto finding in appFindings)
        {
            string packageId = ReadEvidence(finding, "package_id", finding.FindingId);
            string appName = ReadEvidence(finding, "name", finding.Title);
            long? downloadSizeBytes = ReadDownloadSizeBytes(finding);

            UpdateGroupKind group = finding.Severity == FindingSeverity.Critical
                ? UpdateGroupKind.Security
                : (downloadSizeBytes.HasValue && downloadSizeBytes.Value < 60L * 1024L * 1024L ? UpdateGroupKind.Optional : UpdateGroupKind.Recommended);

            list.Add(new AppUpdateSelectionItemViewModel
            {
                PackageId = packageId,
                Name = appName,
                ProgramIconImage = ResolveInstalledProgramIcon(packageId, appName),
                ProgramIconGlyph = ResolveAppIconGlyph(packageId, appName),
                InstalledVersion = ReadEvidence(finding, "installed_version", "-"),
                AvailableVersion = ReadEvidence(finding, "available_version", "-"),
                Source = ReadEvidence(finding, "source", "winget"),
                DownloadSizeText = ReadDownloadSizeText(finding),
                DownloadSizeBytes = downloadSizeBytes,
                RequiresRestart = HasRestartHint(finding),
                ReleaseNotes = ReadReleaseNotesText(finding),
                DetailsText = ReadDetailsText(finding),
                Publisher = ReadEvidence(finding, "publisher", "Unbekannt"),
                LastSeenUtc = finding.State.LastSeenUtc ?? finding.DetectedAtUtc,
                GroupKind = group,
                IsSelected = !selectedByPackage.TryGetValue(packageId, out bool selected) || selected
            });
        }

        return list;
    }

    private void RebuildSoftwareCollection(List<AppUpdateSelectionItemViewModel> items)
    {
        foreach (AppUpdateSelectionItemViewModel item in _softwareUpdates)
        {
            item.PropertyChanged -= OnSoftwareItemPropertyChanged;
        }

        _softwareUpdates.Clear();
        foreach (AppUpdateSelectionItemViewModel item in items)
        {
            item.PropertyChanged += OnSoftwareItemPropertyChanged;
            _softwareUpdates.Add(item);
        }

        SoftwareUpdatesCount = _softwareUpdates.Count;
        ApplySoftwareView();

        RaisePropertyChanged(nameof(SoftwareUpdates));
        RaisePropertyChanged(nameof(SoftwareSectionBadgeCount));
        RaisePropertyChanged(nameof(WindowsSectionBadgeCount));
        RaisePropertyChanged(nameof(OptionalSectionBadgeCount));
        RaisePropertyChanged(nameof(SoftwareStatusHint));
        RaisePropertyChanged(nameof(HeaderInstallButtonText));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }

    private static string ReadEvidence(FindingDto finding, string key, string fallback)
    {
        return finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static long? ReadDownloadSizeBytes(FindingDto finding)
    {
        if (finding.Evidence.TryGetValue("download_size_bytes", out string? bytesRaw) &&
            long.TryParse(bytesRaw, out long bytes) &&
            bytes > 0)
        {
            return bytes;
        }

        if (finding.Evidence.TryGetValue("download_size_mb", out string? mbRaw) &&
            double.TryParse(mbRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double mb) &&
            mb > 0)
        {
            return (long)(mb * 1024d * 1024d);
        }

        return null;
    }

    private static string ReadDownloadSizeText(FindingDto finding)
    {
        if (finding.Evidence.TryGetValue("download_size", out string? direct) && !string.IsNullOrWhiteSpace(direct))
        {
            return direct.Trim();
        }

        long? bytes = ReadDownloadSizeBytes(finding);
        return bytes.HasValue ? FormatBytes(bytes.Value) : "unbekannt";
    }

    private static string ReadReleaseNotesText(FindingDto finding)
    {
        foreach (string key in new[] { "release_notes", "release_notes_url", "changelog", "kb_article", "kb_url" })
        {
            if (finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return !string.IsNullOrWhiteSpace(finding.DetailsMarkdown)
            ? finding.DetailsMarkdown
            : "Keine Release Notes verfügbar.";
    }

    private static string ReadDetailsText(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.WhatIsThis))
        {
            return finding.WhatIsThis;
        }

        if (!string.IsNullOrWhiteSpace(finding.WhyImportant))
        {
            return finding.WhyImportant;
        }

        if (!string.IsNullOrWhiteSpace(finding.Summary))
        {
            return finding.Summary;
        }

        if (!string.IsNullOrWhiteSpace(finding.DetailsMarkdown))
        {
            return finding.DetailsMarkdown;
        }

        return "Keine Detailbeschreibung verfügbar.";
    }

    private static bool HasRestartHint(FindingDto finding)
    {
        if (finding.Actions.Any(a => a.MayRequireRestart))
        {
            return true;
        }

        foreach (string key in new[] { "restart_required", "requires_restart", "may_require_restart", "reboot_required", "requires_reboot", "needs_restart" })
        {
            if (finding.Evidence.TryGetValue(key, out string? raw) && IsTrueFlag(raw))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrueFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string value = raw.Trim().ToLowerInvariant();
        return value is "1" or "true" or "yes" or "ja" or "required" or "possible" ||
               value.Contains("neustart", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("reboot", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        double value = bytes;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static string ResolveAppIconGlyph(string packageId, string name)
    {
        string haystack = $"{packageId} {name}".ToLowerInvariant();

        if (haystack.Contains("chrome") || haystack.Contains("edge") || haystack.Contains("firefox") || haystack.Contains("brave") || haystack.Contains("opera"))
        {
            return "\uE774";
        }

        if (haystack.Contains("defender") || haystack.Contains("security") || haystack.Contains("antivirus"))
        {
            return "\uE72E";
        }

        if (haystack.Contains("7zip") || haystack.Contains("zip") || haystack.Contains("rar"))
        {
            return "\uE8B7";
        }

        if (haystack.Contains("vlc") || haystack.Contains("media") || haystack.Contains("player"))
        {
            return "\uE768";
        }

        return "\uE71D";
    }

    private static ImageSource? ResolveInstalledProgramIcon(string packageId, string name)
    {
        string cacheKey = $"{packageId}|{name}";
        if (ProgramIconCache.TryGetValue(cacheKey, out ImageSource? cached))
        {
            return cached;
        }

        string normalizedName = NormalizeForMatch(name);
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (string root in UninstallRoots)
            {
                using RegistryKey? rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64).OpenSubKey(root);
                if (rootKey is null)
                {
                    continue;
                }

                foreach (string subName in rootKey.GetSubKeyNames())
                {
                    using RegistryKey? appKey = rootKey.OpenSubKey(subName);
                    string? displayName = appKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    string normalizedDisplay = NormalizeForMatch(displayName);
                    if (!normalizedDisplay.Contains(normalizedName, StringComparison.Ordinal) &&
                        !normalizedName.Contains(normalizedDisplay, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string? displayIcon = appKey?.GetValue("DisplayIcon") as string;
                    if (TryLoadProgramIcon(displayIcon, out ImageSource? icon))
                    {
                        ProgramIconCache[cacheKey] = icon;
                        return icon;
                    }
                }
            }
        }

        ProgramIconCache[cacheKey] = null;
        return null;
    }

    private static bool TryLoadProgramIcon(string? iconReference, out ImageSource? source)
    {
        source = null;
        if (string.IsNullOrWhiteSpace(iconReference))
        {
            return false;
        }

        string trimmed = iconReference.Trim().Trim('"');
        int comma = trimmed.LastIndexOf(',');
        int iconIndex = 0;
        if (comma > 1 && int.TryParse(trimmed[(comma + 1)..].Trim(), out int parsedIndex))
        {
            iconIndex = parsedIndex;
            trimmed = trimmed[..comma].Trim().Trim('"');
        }

        if (!File.Exists(trimmed))
        {
            return false;
        }

        IntPtr[] large = new IntPtr[1];
        IntPtr[] small = new IntPtr[1];
        uint extracted = ExtractIconEx(trimmed, iconIndex, large, small, 1);
        IntPtr handle = large[0] != IntPtr.Zero ? large[0] : small[0];
        if (extracted == 0 || handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(16, 16));
            bitmap.Freeze();
            source = bitmap;
            return true;
        }
        finally
        {
            if (large[0] != IntPtr.Zero)
            {
                _ = DestroyIcon(large[0]);
            }

            if (small[0] != IntPtr.Zero && small[0] != large[0])
            {
                _ = DestroyIcon(small[0]);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static string NormalizeForMatch(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[raw.Length];
        int index = 0;
        foreach (char ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[index++] = char.ToLowerInvariant(ch);
            }
        }

        return index == 0 ? string.Empty : new string(buffer[..index]);
    }
}
