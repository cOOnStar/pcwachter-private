using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class DashboardViewModel : ReportPageViewModelBase
{
    private const int MaxHealthHistoryEntries = 240;

    private readonly Func<bool> _isDemoModeEnabled;
    private readonly List<HealthScoreHistoryPoint> _healthScoreHistory;

    private int _healthScore = 100;
    private string _healthScoreLabel = "Sehr gut";
    private string _deviceSummary = "-";
    private string _lastScanText = "-";
    private string _scanFeedbackText = string.Empty;
    private bool _scanFeedbackIsError;
    private bool _isServiceConnected;
    private string _healthTrendPoints = "0,26 180,26";
    private string _healthTrendDeltaText = "0";
    private string _healthTrend7AverageText = "0";
    private string _healthTrend30AverageText = "0";
    private bool _isHealthTrendPositive = true;
    private bool _hasAutoFixResult;
    private string _autoFixResultTitle = "Noch kein Auto-Fix Ergebnis";
    private string _autoFixResultFinding = "Noch keine abgeschlossene Aktion";
    private string _autoFixResultOutcome = "Vorher/Nachher wird nach der ersten Behebung angezeigt.";
    private string _autoFixResultMessage = "Sobald ein Fix ausgeführt wird, erscheint hier das Ergebnis.";
    private string _autoFixResultTimeText = "-";
    private bool _autoFixResultSuccess;
    private string _selectedFindingFilter = "all";
    private bool _hasLiveDataSnapshot;
    private string _monthlySummaryText = "Monatsbericht wird vorbereitet.";
    private string _monthlyScoreDeltaText = "0";
    private string _monthlyResolvedText = "0";
    private string _monthlyOpenText = "0";
    private string _monthlyTrendPoints = "0,26 180,26";
    private bool _isMonthlyTrendPositive = true;
    private int _performanceSpikesToday;
    private string _performanceLastCause = "-";
    private string _performanceLastValue = "-";

    public DashboardViewModel(
        ReportStore reportStore,
        IpcClientService ipcClient,
        DesktopActionRunner actionRunner,
        Action navigateToOptions,
        Func<bool> isDemoModeEnabled,
        List<HealthScoreHistoryPoint> healthScoreHistory)
        : base("Dashboard", reportStore, ipcClient, actionRunner)
    {
        _isDemoModeEnabled = isDemoModeEnabled;
        _healthScoreHistory = healthScoreHistory;
        _isServiceConnected = ipcClient.IsConnected;
        TriggerScanCommand = new AsyncRelayCommand(TriggerScanAsync);
        RunMaintenanceCommand = new AsyncRelayCommand(RunMaintenanceAsync);
        TriggerScanCommand.PropertyChanged += TriggerScanCommandOnPropertyChanged;
        RunMaintenanceCommand.PropertyChanged += RunMaintenanceCommandOnPropertyChanged;
        OpenOptionsCommand = new RelayCommand(() => navigateToOptions(), () => !TriggerScanCommand.IsRunning && !RunMaintenanceCommand.IsRunning);
        SetFindingFilterCommand = new RelayCommand(param => SetFindingFilter(param?.ToString()));
        IpcClient.ConnectionChanged += IpcClientOnConnectionChanged;

        RefreshHealthTrendDerivedProperties(DateTimeOffset.UtcNow);
        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<FindingCardViewModel> TopFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> AllFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> FilteredAllFindings { get; private set; } = new();
    public ObservableCollection<FindingCardViewModel> RecentlyResolved { get; private set; } = new();

    public int HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    public string HealthScoreLabel
    {
        get => _healthScoreLabel;
        set => SetProperty(ref _healthScoreLabel, value);
    }

    public string DeviceSummary
    {
        get => _deviceSummary;
        set => SetProperty(ref _deviceSummary, value);
    }

    public string LastScanText
    {
        get => _lastScanText;
        set => SetProperty(ref _lastScanText, value);
    }

    public string ScanFeedbackText
    {
        get => _scanFeedbackText;
        set
        {
            if (SetProperty(ref _scanFeedbackText, value))
            {
                RaisePropertyChanged(nameof(HasScanFeedback));
            }
        }
    }

    public bool ScanFeedbackIsError
    {
        get => _scanFeedbackIsError;
        set => SetProperty(ref _scanFeedbackIsError, value);
    }

    public bool HasScanFeedback => !string.IsNullOrWhiteSpace(ScanFeedbackText);

    public string ScanActionButtonText => TriggerScanCommand.IsRunning ? "Scan läuft..." : "Jetzt scannen";
    public string MaintenanceActionButtonText => RunMaintenanceCommand.IsRunning ? "Optimierung läuft..." : "Jetzt optimieren";

    public bool IsDemoMode => _isDemoModeEnabled();
    public bool IsServiceDisconnected => !IsDemoMode && !_isServiceConnected;

    public string DashboardErrorText =>
        IsDemoMode
            ? "Demo-Modus aktiv. Live-Service wird aktuell nicht verwendet."
            : string.IsNullOrWhiteSpace(IpcClient.LastError)
                ? "Service nicht erreichbar. Bitte Verbindung prüfen."
                : $"Service nicht erreichbar: {IpcClient.LastError}";

    public bool IsDashboardLoading => TriggerScanCommand.IsRunning || RunMaintenanceCommand.IsRunning;

    public bool HasTopFindings => TopFindings.Count > 0;
    public bool HasAnyFindings => AllFindings.Count > 0;
    public bool HasAllFindings => FilteredAllFindings.Count > 0;
    public bool HasRecentlyResolved => RecentlyResolved.Count > 0;

    public bool ShowDashboardLoadingState => IsDashboardLoading;
    public bool ShowDashboardErrorState => !IsDashboardLoading && IsServiceDisconnected;
    public bool ShowDashboardEmptyState => !IsDashboardLoading && !IsServiceDisconnected && !HasAnyFindings;
    public bool ShowDashboardCardSkeletons =>
        !_hasLiveDataSnapshot
        && !IsDemoMode
        && string.IsNullOrWhiteSpace(IpcClient.LastError);

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

    public bool IsHealthTrendPositive
    {
        get => _isHealthTrendPositive;
        private set => SetProperty(ref _isHealthTrendPositive, value);
    }

    public bool HasAutoFixResult
    {
        get => _hasAutoFixResult;
        private set => SetProperty(ref _hasAutoFixResult, value);
    }

    public string AutoFixResultTitle
    {
        get => _autoFixResultTitle;
        private set => SetProperty(ref _autoFixResultTitle, value);
    }

    public string AutoFixResultFinding
    {
        get => _autoFixResultFinding;
        private set => SetProperty(ref _autoFixResultFinding, value);
    }

    public string AutoFixResultOutcome
    {
        get => _autoFixResultOutcome;
        private set => SetProperty(ref _autoFixResultOutcome, value);
    }

    public string AutoFixResultMessage
    {
        get => _autoFixResultMessage;
        private set => SetProperty(ref _autoFixResultMessage, value);
    }

    public string AutoFixResultTimeText
    {
        get => _autoFixResultTimeText;
        private set => SetProperty(ref _autoFixResultTimeText, value);
    }

    public bool AutoFixResultSuccess
    {
        get => _autoFixResultSuccess;
        private set => SetProperty(ref _autoFixResultSuccess, value);
    }

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
            RaisePropertyChanged(nameof(IsFindingFilterInfoSelected));
        }
    }

    public bool IsFindingFilterAllSelected => string.Equals(SelectedFindingFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsFindingFilterCriticalSelected => string.Equals(SelectedFindingFilter, "critical", StringComparison.OrdinalIgnoreCase);
    public bool IsFindingFilterWarningSelected => string.Equals(SelectedFindingFilter, "warning", StringComparison.OrdinalIgnoreCase);
    public bool IsFindingFilterInfoSelected => string.Equals(SelectedFindingFilter, "info", StringComparison.OrdinalIgnoreCase);

    public string FindingFilterAllLabel => $"Alle ({AllFindings.Count})";
    public string FindingFilterCriticalLabel => $"Kritisch ({AllFindings.Count(x => x.Severity == FindingSeverity.Critical)})";
    public string FindingFilterWarningLabel => $"Warnung ({AllFindings.Count(x => x.Severity == FindingSeverity.Warning)})";
    public string FindingFilterInfoLabel => $"Info ({AllFindings.Count(x => x.Severity == FindingSeverity.Info)})";

    public string AllFindingsEmptyText => HasAnyFindings
        ? "Keine Einträge im gewählten Filter."
        : "Keine Probleme gefunden. Der nächste Scan aktualisiert diese Liste automatisch.";

    public string MonthlySummaryText
    {
        get => _monthlySummaryText;
        private set => SetProperty(ref _monthlySummaryText, value);
    }

    public string MonthlyScoreDeltaText
    {
        get => _monthlyScoreDeltaText;
        private set => SetProperty(ref _monthlyScoreDeltaText, value);
    }

    public string MonthlyResolvedText
    {
        get => _monthlyResolvedText;
        private set => SetProperty(ref _monthlyResolvedText, value);
    }

    public string MonthlyOpenText
    {
        get => _monthlyOpenText;
        private set => SetProperty(ref _monthlyOpenText, value);
    }

    public string MonthlyTrendPoints
    {
        get => _monthlyTrendPoints;
        private set => SetProperty(ref _monthlyTrendPoints, value);
    }

    public bool IsMonthlyTrendPositive
    {
        get => _isMonthlyTrendPositive;
        private set => SetProperty(ref _isMonthlyTrendPositive, value);
    }

    public int PerformanceSpikesToday
    {
        get => _performanceSpikesToday;
        private set
        {
            if (!SetProperty(ref _performanceSpikesToday, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasPerformanceSpikesToday));
        }
    }

    public string PerformanceLastCause
    {
        get => _performanceLastCause;
        private set => SetProperty(ref _performanceLastCause, value);
    }

    public string PerformanceLastValue
    {
        get => _performanceLastValue;
        private set => SetProperty(ref _performanceLastValue, value);
    }

    public bool HasPerformanceSpikesToday => PerformanceSpikesToday > 0;

    public AsyncRelayCommand TriggerScanCommand { get; }
    public AsyncRelayCommand RunMaintenanceCommand { get; }
    public RelayCommand OpenOptionsCommand { get; }
    public RelayCommand SetFindingFilterCommand { get; }

    private async Task TriggerScanAsync()
    {
        ScanFeedbackIsError = false;
        ScanFeedbackText = "Scan wird gestartet...";

        try
        {
            await IpcClient.TriggerScanAsync();
            ScanFeedbackText = "Scan wurde erfolgreich gestartet.";
        }
        catch
        {
            ScanFeedbackIsError = true;
            ScanFeedbackText = "Scan konnte nicht gestartet werden.";
        }
    }

    private async Task RunMaintenanceAsync()
    {
        ScanFeedbackIsError = false;
        ScanFeedbackText = "Sichere Wartung wird gestartet...";

        try
        {
            MaintenanceRunResultDto result = await IpcClient.RunSafeMaintenanceAsync();
            int successCount = result.Steps.Count(x => x.Success);
            int failedCount = result.Steps.Count(x => !x.Success && !x.Skipped);
            int skippedCount = result.Steps.Count(x => x.Skipped);
            ScanFeedbackIsError = failedCount > 0;

            string summary = string.IsNullOrWhiteSpace(result.Summary)
                ? $"Wartung abgeschlossen: {successCount} erfolgreich, {failedCount} fehlgeschlagen, {skippedCount} übersprungen."
                : result.Summary;

            if (result.RestartRecommended &&
                !summary.Contains("Neustart", StringComparison.OrdinalIgnoreCase))
            {
                summary += " Neustart wird empfohlen.";
            }

            ScanFeedbackText = summary;
            await IpcClient.GetLatestReportAsync();
        }
        catch
        {
            ScanFeedbackIsError = true;
            ScanFeedbackText = "Sichere Wartung konnte nicht abgeschlossen werden.";
        }
    }

    private void TriggerScanCommandOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AsyncRelayCommand.IsRunning), StringComparison.Ordinal))
        {
            return;
        }

        RaisePropertyChanged(nameof(ScanActionButtonText));
        RunMaintenanceCommand.RaiseCanExecuteChanged();
        OpenOptionsCommand.RaiseCanExecuteChanged();
        RaiseDashboardStateProperties();
    }

    private void RunMaintenanceCommandOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AsyncRelayCommand.IsRunning), StringComparison.Ordinal))
        {
            return;
        }

        RaisePropertyChanged(nameof(MaintenanceActionButtonText));
        TriggerScanCommand.RaiseCanExecuteChanged();
        OpenOptionsCommand.RaiseCanExecuteChanged();
        RaiseDashboardStateProperties();
    }

    protected override void OnReportUpdated(ScanReportDto report)
    {
        if (!_hasLiveDataSnapshot &&
            (IpcClient.IsConnected || report.Findings.Count > 0 || report.RecentlyResolved.Count > 0))
        {
            _hasLiveDataSnapshot = true;
            RaisePropertyChanged(nameof(ShowDashboardCardSkeletons));
        }

        HealthScore = report.HealthScore;
        HealthScoreLabel = report.HealthScore switch
        {
            >= 90 => "Sehr gut",
            >= 70 => "Gut",
            >= 40 => "Achtung",
            _ => "Kritisch"
        };

        LastScanText = report.GeneratedAtUtc.ToLocalTime().ToString("g");
        UpdateHealthTrend(report.GeneratedAtUtc, report.HealthScore);

        string deviceType = report.DeviceContext.IsLaptop ? "Laptop" : report.DeviceContext.IsDesktop ? "Desktop" : report.DeviceContext.IsServer ? "Server" : "Unbekannt";
        DeviceSummary = $"{deviceType} | {report.DeviceContext.Manufacturer ?? "-"} {report.DeviceContext.Model ?? "-"}";

        IEnumerable<FindingDto> top = report.TopFindings.Count > 0
            ? report.TopFindings
            : report.Findings.OrderByDescending(f => f.Priority).Take(3);

        TopFindings = BuildCards(top);
        RaisePropertyChanged(nameof(TopFindings));

        AllFindings = BuildCards(report.Findings.OrderByDescending(f => f.Priority));
        RaisePropertyChanged(nameof(AllFindings));
        ApplyFindingFilter();

        RecentlyResolved = BuildCards(report.RecentlyResolved
            .OrderByDescending(f => f.State.ResolvedAtUtc)
            .Take(3));
        RaisePropertyChanged(nameof(RecentlyResolved));

        UpdateAutoFixResult(report);
        UpdateMonthlyAndPerformanceSignals(report);
        RaiseCollectionStateProperties();
        RaiseDashboardStateProperties();
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
        List<HealthScoreHistoryPoint> recent = _healthScoreHistory
            .OrderBy(p => p.TimestampUtc)
            .TakeLast(24)
            .ToList();

        HealthTrendPoints = BuildSparklinePoints(recent);

        List<HealthScoreHistoryPoint> points7 = _healthScoreHistory
            .Where(p => p.TimestampUtc >= nowUtc.AddDays(-7))
            .OrderBy(p => p.TimestampUtc)
            .ToList();
        List<HealthScoreHistoryPoint> points30 = _healthScoreHistory
            .Where(p => p.TimestampUtc >= nowUtc.AddDays(-30))
            .OrderBy(p => p.TimestampUtc)
            .ToList();

        double avg7 = points7.Count > 0 ? points7.Average(p => p.Score) : 0;
        double avg30 = points30.Count > 0 ? points30.Average(p => p.Score) : 0;
        HealthTrend7AverageText = $"{Math.Round(avg7):0}";
        HealthTrend30AverageText = $"{Math.Round(avg30):0}";

        if (points7.Count >= 2)
        {
            int delta = points7[^1].Score - points7[0].Score;
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

        int width = 180;
        int height = 52;
        int minScore = Math.Max(0, points.Min(p => p.Score) - 5);
        int maxScore = Math.Min(100, points.Max(p => p.Score) + 5);
        if (maxScore <= minScore)
        {
            maxScore = minScore + 1;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < points.Count; i++)
        {
            int x = (int)Math.Round(i * (width / (double)(points.Count - 1)));
            double normalized = (points[i].Score - minScore) / (double)(maxScore - minScore);
            int y = height - (int)Math.Round(Math.Clamp(normalized, 0, 1) * height);
            builder.Append(x).Append(',').Append(y);
            if (i < points.Count - 1)
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    private void UpdateAutoFixResult(ScanReportDto report)
    {
        AutoFixLogItemDto? latest = report.RecentAutoFixLog
            .OrderByDescending(l => l.TimestampUtc)
            .FirstOrDefault();

        if (latest is null)
        {
            HasAutoFixResult = false;
            AutoFixResultSuccess = false;
            AutoFixResultTitle = "Noch kein Auto-Fix Ergebnis";
            AutoFixResultFinding = "Noch keine abgeschlossene Aktion";
            AutoFixResultOutcome = "Vorher/Nachher wird nach der ersten Behebung angezeigt.";
            AutoFixResultMessage = "Sobald ein Fix ausgeführt wird, erscheint hier das Ergebnis.";
            AutoFixResultTimeText = "-";
            return;
        }

        HasAutoFixResult = true;
        AutoFixResultSuccess = latest.Success;
        AutoFixResultTitle = latest.Success ? "Auto-Fix erfolgreich" : "Auto-Fix fehlgeschlagen";
        AutoFixResultTimeText = latest.TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        FindingDto? matchingFinding = report.Findings
            .Concat(report.RecentlyResolved)
            .FirstOrDefault(f => string.Equals(f.FindingId, latest.FindingId, StringComparison.OrdinalIgnoreCase));
        string findingTitle = matchingFinding?.Title ?? latest.FindingId;
        AutoFixResultFinding = findingTitle;

        bool stillOpen = report.Findings.Any(f => string.Equals(f.FindingId, latest.FindingId, StringComparison.OrdinalIgnoreCase));
        if (latest.Success)
        {
            AutoFixResultOutcome = stillOpen
                ? "Vorher: offen | Nachher: weiterhin offen"
                : "Vorher: offen | Nachher: behoben";
        }
        else
        {
            AutoFixResultOutcome = "Vorher: offen | Nachher: fehlgeschlagen";
        }

        AutoFixResultMessage = string.IsNullOrWhiteSpace(latest.Message)
            ? "Keine Detailmeldung vorhanden."
            : latest.Message;
    }

    private void UpdateMonthlyAndPerformanceSignals(ScanReportDto report)
    {
        string monthlySummary = ReadSignal(report, "monthly_summary", "Monatsdaten werden nach mehreren Scans aufgebaut.");
        string scoreDeltaRaw = ReadSignal(report, "monthly_score_delta", "0");
        string resolvedRaw = ReadSignal(report, "monthly_resolved_count", "0");
        string openRaw = ReadSignal(report, "monthly_open_count", report.Findings.Count.ToString());
        string trendRaw = ReadSignal(report, "monthly_trend_points", string.Empty);

        int scoreDelta = TryParseInt(scoreDeltaRaw);
        IsMonthlyTrendPositive = scoreDelta >= 0;
        MonthlyScoreDeltaText = scoreDelta > 0 ? $"+{scoreDelta}" : scoreDelta.ToString();
        MonthlyResolvedText = TryParseInt(resolvedRaw).ToString();
        MonthlyOpenText = TryParseInt(openRaw).ToString();
        MonthlySummaryText = monthlySummary;
        MonthlyTrendPoints = BuildSignalSparklinePoints(trendRaw);

        FindingDto? spikeFinding = report.Findings.FirstOrDefault(f =>
            f.FindingId.Equals("health.performance.spikes_today", StringComparison.OrdinalIgnoreCase));
        if (spikeFinding is null)
        {
            PerformanceSpikesToday = 0;
            PerformanceLastCause = "-";
            PerformanceLastValue = "-";
            return;
        }

        PerformanceSpikesToday = TryParseInt(ReadEvidence(spikeFinding, "spikes_today", "0"));
        PerformanceLastCause = ReadEvidence(spikeFinding, "latest_process", "-");
        string value = ReadEvidence(spikeFinding, "latest_value", "-");
        PerformanceLastValue = value == "-" ? "-" : $"{value}%";
    }

    private void IpcClientOnConnectionChanged(object? sender, bool connected)
    {
        if (_isServiceConnected != connected)
        {
            _isServiceConnected = connected;
            RaisePropertyChanged(nameof(IsServiceDisconnected));
        }

        RaisePropertyChanged(nameof(IsDemoMode));
        RaisePropertyChanged(nameof(DashboardErrorText));
        RaiseDashboardStateProperties();
    }

    private void SetFindingFilter(string? value)
    {
        string normalized = value?.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "warning" => "warning",
            "info" => "info",
            _ => "all"
        };

        SelectedFindingFilter = normalized;
        ApplyFindingFilter();
    }

    private void ApplyFindingFilter()
    {
        IEnumerable<FindingCardViewModel> filtered = SelectedFindingFilter switch
        {
            "critical" => AllFindings.Where(f => f.Severity == FindingSeverity.Critical),
            "warning" => AllFindings.Where(f => f.Severity == FindingSeverity.Warning),
            "info" => AllFindings.Where(f => f.Severity == FindingSeverity.Info),
            _ => AllFindings
        };

        FilteredAllFindings = new ObservableCollection<FindingCardViewModel>(filtered);
        RaisePropertyChanged(nameof(FilteredAllFindings));
        RaisePropertyChanged(nameof(HasAllFindings));
        RaisePropertyChanged(nameof(AllFindingsEmptyText));
    }

    private void RaiseCollectionStateProperties()
    {
        RaisePropertyChanged(nameof(HasTopFindings));
        RaisePropertyChanged(nameof(HasAnyFindings));
        RaisePropertyChanged(nameof(HasAllFindings));
        RaisePropertyChanged(nameof(HasRecentlyResolved));
        RaisePropertyChanged(nameof(FindingFilterAllLabel));
        RaisePropertyChanged(nameof(FindingFilterCriticalLabel));
        RaisePropertyChanged(nameof(FindingFilterWarningLabel));
        RaisePropertyChanged(nameof(FindingFilterInfoLabel));
        RaisePropertyChanged(nameof(AllFindingsEmptyText));
    }

    private void RaiseDashboardStateProperties()
    {
        RaisePropertyChanged(nameof(IsDashboardLoading));
        RaisePropertyChanged(nameof(ShowDashboardLoadingState));
        RaisePropertyChanged(nameof(ShowDashboardErrorState));
        RaisePropertyChanged(nameof(ShowDashboardEmptyState));
        RaisePropertyChanged(nameof(ShowDashboardCardSkeletons));
    }

    private static string ReadSignal(ScanReportDto report, string key, string fallback)
    {
        return report.SecuritySignals.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static string ReadEvidence(FindingDto finding, string key, string fallback)
    {
        return finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int TryParseInt(string raw)
    {
        return int.TryParse(raw, out int value) ? value : 0;
    }

    private static string BuildSignalSparklinePoints(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "0,26 180,26";
        }

        List<int> values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TryParseInt)
            .ToList();
        if (values.Count == 0)
        {
            return "0,26 180,26";
        }

        if (values.Count == 1)
        {
            int y = 52 - (int)Math.Round((Math.Clamp(values[0], 0, 100) / 100.0) * 52);
            return $"0,{y} 180,{y}";
        }

        const int width = 180;
        const int height = 52;
        int minScore = Math.Max(0, values.Min() - 5);
        int maxScore = Math.Min(100, values.Max() + 5);
        if (maxScore <= minScore)
        {
            maxScore = minScore + 1;
        }

        var points = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            int x = (int)Math.Round(i * (width / (double)(values.Count - 1)));
            double normalized = (values[i] - minScore) / (double)(maxScore - minScore);
            int y = height - (int)Math.Round(Math.Clamp(normalized, 0, 1) * height);
            points.Add($"{x},{y}");
        }

        return string.Join(" ", points);
    }
}


