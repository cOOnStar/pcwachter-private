using System.Collections.ObjectModel;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class StartupManagerEntryViewModel : ObservableObject
{
    public string EntryKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string CommandLine { get; init; } = string.Empty;
    public string Impact { get; init; } = "Low";
    public bool IsDisabledByPcwachter { get; init; }

    public string StatusLabel => IsDisabledByPcwachter ? "Deaktiviert" : "Aktiv";
    public bool IsHighImpact => string.Equals(Impact, "High", StringComparison.OrdinalIgnoreCase);
}

public sealed class WindowsViewModel : ReportPageViewModelBase
{
    private const int MaxHealthHistoryEntries = 360;

    private readonly List<HealthScoreHistoryPoint> _healthScoreHistory;

    private int _criticalWindowsCount;
    private int _totalWindowsFindings;
    private int _rebootFindingsCount;
    private int _healthFindingsCount;
    private int _otherFindingsCount;
    private int _startupActiveCount;
    private int _startupHighImpactCount;
    private int _startupDisabledCount;
    private string _selectedFindingFilter = "all";
    private FindingCardViewModel? _selectedFinding;
    private string _healthTrendPoints = "0,26 180,26";
    private string _healthTrendDeltaText = "0";
    private string _healthTrend7AverageText = "0";
    private string _healthTrend30AverageText = "0";
    private string _healthTrend90AverageText = "0";
    private bool _isHealthTrendPositive = true;

    private List<FindingCardViewModel> _allRebootFindings = [];
    private List<FindingCardViewModel> _allHealthFindings = [];
    private List<FindingCardViewModel> _allOtherFindings = [];

    public WindowsViewModel(
        ReportStore reportStore,
        IpcClientService ipcClient,
        DesktopActionRunner actionRunner,
        List<HealthScoreHistoryPoint> healthScoreHistory)
        : base("Windows", reportStore, ipcClient, actionRunner)
    {
        _healthScoreHistory = healthScoreHistory;
        SetFindingFilterCommand = new RelayCommand(param => SetFindingFilter(param?.ToString()));
        DisableStartupEntryCommand = new AsyncRelayCommand(DisableStartupEntryAsync, CanDisableStartupEntry);
        UndoStartupEntryCommand = new AsyncRelayCommand(UndoStartupEntryAsync, CanUndoStartupEntry);
        RefreshHealthTrendDerivedProperties(DateTimeOffset.UtcNow);
        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<FindingCardViewModel> RebootFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> HealthFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> OtherFindings { get; private set; } = new();
    public ObservableCollection<StartupManagerEntryViewModel> StartupEntries { get; private set; } = new();
    public RelayCommand SetFindingFilterCommand { get; }
    public AsyncRelayCommand DisableStartupEntryCommand { get; }
    public AsyncRelayCommand UndoStartupEntryCommand { get; }

    public int CriticalWindowsCount
    {
        get => _criticalWindowsCount;
        private set
        {
            if (SetProperty(ref _criticalWindowsCount, value))
            {
                RaisePropertyChanged(nameof(SystemStateLabel));
                RaisePropertyChanged(nameof(SystemStateHint));
            }
        }
    }

    public int TotalWindowsFindings
    {
        get => _totalWindowsFindings;
        private set
        {
            if (SetProperty(ref _totalWindowsFindings, value))
            {
                RaisePropertyChanged(nameof(HasAnyWindowsFindings));
            }
        }
    }

    public int RebootFindingsCount
    {
        get => _rebootFindingsCount;
        private set => SetProperty(ref _rebootFindingsCount, value);
    }

    public int HealthFindingsCount
    {
        get => _healthFindingsCount;
        private set => SetProperty(ref _healthFindingsCount, value);
    }

    public int OtherFindingsCount
    {
        get => _otherFindingsCount;
        private set => SetProperty(ref _otherFindingsCount, value);
    }

    public int StartupActiveCount
    {
        get => _startupActiveCount;
        private set => SetProperty(ref _startupActiveCount, value);
    }

    public int StartupHighImpactCount
    {
        get => _startupHighImpactCount;
        private set => SetProperty(ref _startupHighImpactCount, value);
    }

    public int StartupDisabledCount
    {
        get => _startupDisabledCount;
        private set => SetProperty(ref _startupDisabledCount, value);
    }

    public bool HasAnyWindowsFindings => TotalWindowsFindings > 0;
    public bool HasRebootFindings => RebootFindings.Count > 0;
    public bool HasHealthFindings => HealthFindings.Count > 0;
    public bool HasOtherFindings => OtherFindings.Count > 0;
    public bool HasStartupEntries => StartupEntries.Count > 0;
    public int VisibleWindowsFindingsCount => RebootFindings.Count + HealthFindings.Count + OtherFindings.Count;

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

    public string FindingFilterAllLabel => $"Alle ({TotalWindowsFindings})";
    public string FindingFilterCriticalLabel => $"Kritisch ({CriticalWindowsCount})";
    public string FindingFilterWarningLabel => $"Warnung ({Math.Max(0, TotalWindowsFindings - CriticalWindowsCount)})";

    public string HealthTrendPoints
    {
        get => _healthTrendPoints;
        private set => SetProperty(ref _healthTrendPoints, value);
    }

    public string HealthTrendDeltaText
    {
        get => _healthTrendDeltaText;
        private set => SetProperty(ref _healthTrendDeltaText, value);
    }

    public string HealthTrend7AverageText
    {
        get => _healthTrend7AverageText;
        private set => SetProperty(ref _healthTrend7AverageText, value);
    }

    public string HealthTrend30AverageText
    {
        get => _healthTrend30AverageText;
        private set => SetProperty(ref _healthTrend30AverageText, value);
    }

    public string HealthTrend90AverageText
    {
        get => _healthTrend90AverageText;
        private set => SetProperty(ref _healthTrend90AverageText, value);
    }

    public bool IsHealthTrendPositive
    {
        get => _isHealthTrendPositive;
        private set => SetProperty(ref _isHealthTrendPositive, value);
    }

    public string SystemStateLabel => CriticalWindowsCount > 0
        ? "Kritisch"
        : TotalWindowsFindings > 0
            ? "Achtung"
            : "Stabil";

    public string SystemStateHint => CriticalWindowsCount > 0
        ? "Relevante Systemprobleme offen"
        : TotalWindowsFindings > 0
            ? "Einige Hinweise zur Prüfung"
            : "Keine offenen Windows-Hinweise";

    public string StartupManagerHint => StartupHighImpactCount > 0
        ? $"{StartupHighImpactCount} High-Impact Einträge können den Start verlangsamen."
        : "Keine High-Impact Autostart-Einträge gefunden.";

    protected override void OnReportUpdated(ScanReportDto report)
    {
        List<FindingDto> reboot = report.Findings.Where(f => f.FindingId.StartsWith("system.reboot", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allRebootFindings = [.. BuildCards(reboot)];
        RebootFindingsCount = reboot.Count;

        List<FindingDto> health = report.Findings.Where(f =>
            f.FindingId.StartsWith("health.eventlog", StringComparison.OrdinalIgnoreCase) ||
            f.Category == FindingCategory.Health)
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allHealthFindings = [.. BuildCards(health)];
        HealthFindingsCount = health.Count;

        List<FindingDto> other = report.Findings.Where(f =>
            f.FindingId.StartsWith("system.", StringComparison.OrdinalIgnoreCase) &&
            !f.FindingId.StartsWith("system.reboot", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        _allOtherFindings = [.. BuildCards(other)];
        OtherFindingsCount = other.Count;

        BuildStartupEntries(report.Findings);

        List<FindingDto> all = [.. reboot, .. health, .. other];
        TotalWindowsFindings = all.Count;
        CriticalWindowsCount = all.Count(f => f.Severity == FindingSeverity.Critical);

        ApplyFindingFilter();
        RaisePropertyChanged(nameof(FindingFilterAllLabel));
        RaisePropertyChanged(nameof(FindingFilterCriticalLabel));
        RaisePropertyChanged(nameof(FindingFilterWarningLabel));

        UpdateHealthTrend(report.GeneratedAtUtc, report.HealthScore);
    }

    private void BuildStartupEntries(IEnumerable<FindingDto> findings)
    {
        List<StartupManagerEntryViewModel> entries = findings
            .Where(f => f.FindingId.StartsWith("system.startup.app.", StringComparison.OrdinalIgnoreCase))
            .Select(f => new StartupManagerEntryViewModel
            {
                EntryKey = ReadEvidence(f, "entry_key", f.FindingId),
                Name = ReadEvidence(f, "name", f.Title),
                CommandLine = ReadEvidence(f, "command", string.Empty),
                Location = ReadEvidence(f, "location", "HKCU_RUN"),
                Impact = ReadEvidence(f, "impact", "Low"),
                IsDisabledByPcwachter = bool.TryParse(ReadEvidence(f, "disabled_by_pcwachter", "false"), out bool disabled) && disabled
            })
            .OrderBy(x => x.IsDisabledByPcwachter)
            .ThenByDescending(x => ImpactRank(x.Impact))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StartupEntries = new ObservableCollection<StartupManagerEntryViewModel>(entries);
        RaisePropertyChanged(nameof(StartupEntries));
        RaisePropertyChanged(nameof(HasStartupEntries));

        StartupActiveCount = entries.Count(x => !x.IsDisabledByPcwachter);
        StartupHighImpactCount = entries.Count(x => !x.IsDisabledByPcwachter && x.IsHighImpact);
        StartupDisabledCount = entries.Count(x => x.IsDisabledByPcwachter);
        RaisePropertyChanged(nameof(StartupManagerHint));
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
        RebootFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allRebootFindings, SelectedFindingFilter));
        HealthFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allHealthFindings, SelectedFindingFilter));
        OtherFindings = new ObservableCollection<FindingCardViewModel>(FilterBySeverity(_allOtherFindings, SelectedFindingFilter));

        RaisePropertyChanged(nameof(RebootFindings));
        RaisePropertyChanged(nameof(HealthFindings));
        RaisePropertyChanged(nameof(OtherFindings));
        RaisePropertyChanged(nameof(HasRebootFindings));
        RaisePropertyChanged(nameof(HasHealthFindings));
        RaisePropertyChanged(nameof(HasOtherFindings));
        RaisePropertyChanged(nameof(VisibleWindowsFindingsCount));

        if (SelectedFinding is not null &&
            !RebootFindings.Contains(SelectedFinding) &&
            !HealthFindings.Contains(SelectedFinding) &&
            !OtherFindings.Contains(SelectedFinding))
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

    private async Task DisableStartupEntryAsync(object? parameter)
    {
        if (parameter is not StartupManagerEntryViewModel entry || entry.IsDisabledByPcwachter)
        {
            return;
        }

        ActionExecutionResultDto result = await IpcClient.RunActionAsync(
            "action.startup.disable",
            parameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry_key"] = entry.EntryKey,
                ["location"] = entry.Location,
                ["name"] = entry.Name
            });
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(
                result.Message,
                "Autostart konnte nicht deaktiviert werden",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        await IpcClient.TriggerScanAsync();
    }

    private async Task UndoStartupEntryAsync(object? parameter)
    {
        if (parameter is not StartupManagerEntryViewModel entry || !entry.IsDisabledByPcwachter)
        {
            return;
        }

        ActionExecutionResultDto result = await IpcClient.RunActionAsync(
            "action.startup.undo",
            parameters: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry_key"] = entry.EntryKey,
                ["location"] = entry.Location
            });
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(
                result.Message,
                "Undo fehlgeschlagen",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        await IpcClient.TriggerScanAsync();
    }

    private static bool CanDisableStartupEntry(object? parameter)
    {
        return parameter is StartupManagerEntryViewModel entry && !entry.IsDisabledByPcwachter;
    }

    private static bool CanUndoStartupEntry(object? parameter)
    {
        return parameter is StartupManagerEntryViewModel entry && entry.IsDisabledByPcwachter;
    }

    private static int ImpactRank(string impact)
    {
        return impact.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
    }

    private static string ReadEvidence(FindingDto finding, string key, string fallback)
    {
        return finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private void UpdateHealthTrend(DateTimeOffset generatedAtUtc, int score)
    {
        HealthScoreHistoryPoint? latest = _healthScoreHistory.Count == 0 ? null : _healthScoreHistory[^1];
        if (latest is null || latest.TimestampUtc != generatedAtUtc || latest.Score != score)
        {
            _healthScoreHistory.Add(new HealthScoreHistoryPoint
            {
                TimestampUtc = generatedAtUtc,
                Score = score
            });
        }

        if (_healthScoreHistory.Count > MaxHealthHistoryEntries)
        {
            _healthScoreHistory.RemoveRange(0, _healthScoreHistory.Count - MaxHealthHistoryEntries);
        }

        RefreshHealthTrendDerivedProperties(generatedAtUtc);
    }

    private void RefreshHealthTrendDerivedProperties(DateTimeOffset nowUtc)
    {
        List<HealthScoreHistoryPoint> recent90 = _healthScoreHistory
            .Where(p => p.TimestampUtc >= nowUtc.AddDays(-90))
            .OrderBy(p => p.TimestampUtc)
            .ToList();

        HealthTrendPoints = BuildSparklinePoints(recent90.TakeLast(40).ToList());

        List<HealthScoreHistoryPoint> points7 = _healthScoreHistory
            .Where(p => p.TimestampUtc >= nowUtc.AddDays(-7))
            .OrderBy(p => p.TimestampUtc)
            .ToList();
        List<HealthScoreHistoryPoint> points30 = _healthScoreHistory
            .Where(p => p.TimestampUtc >= nowUtc.AddDays(-30))
            .OrderBy(p => p.TimestampUtc)
            .ToList();

        HealthTrend7AverageText = $"{Math.Round(points7.Count > 0 ? points7.Average(p => p.Score) : 0):0}";
        HealthTrend30AverageText = $"{Math.Round(points30.Count > 0 ? points30.Average(p => p.Score) : 0):0}";
        HealthTrend90AverageText = $"{Math.Round(recent90.Count > 0 ? recent90.Average(p => p.Score) : 0):0}";

        if (points30.Count >= 2)
        {
            int delta = points30[^1].Score - points30[0].Score;
            HealthTrendDeltaText = delta > 0 ? $"+{delta}" : delta.ToString();
            IsHealthTrendPositive = delta >= 0;
            return;
        }

        HealthTrendDeltaText = "0";
        IsHealthTrendPositive = true;
    }

    private static string BuildSparklinePoints(IReadOnlyList<HealthScoreHistoryPoint> points)
    {
        if (points.Count == 0)
        {
            return "0,26 180,26";
        }

        if (points.Count == 1)
        {
            int y = 52 - (int)Math.Round((Math.Clamp(points[0].Score, 0, 100) / 100.0) * 52);
            return $"0,{y} 180,{y}";
        }

        const int width = 180;
        const int height = 52;
        int minScore = Math.Max(0, points.Min(p => p.Score) - 5);
        int maxScore = Math.Min(100, points.Max(p => p.Score) + 5);
        if (maxScore <= minScore)
        {
            maxScore = minScore + 1;
        }

        var entries = new List<string>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            int x = (int)Math.Round(i * (width / (double)(points.Count - 1)));
            double normalized = (points[i].Score - minScore) / (double)(maxScore - minScore);
            int y = height - (int)Math.Round(Math.Clamp(normalized, 0, 1) * height);
            entries.Add($"{x},{y}");
        }

        return string.Join(" ", entries);
    }
}

