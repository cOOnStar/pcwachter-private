using System.Collections.ObjectModel;
using System.Windows;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class AutoFixLogItemViewModel
{
    public string TimestampText { get; set; } = string.Empty;
    public string FindingId { get; set; } = string.Empty;
    public string FindingTitle { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ActionExecutionId { get; set; } = string.Empty;
    public string ResultText { get; set; } = string.Empty;
    public string ResultLevel { get; set; } = "info";
    public string SeverityText { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool CanRollback { get; set; }
    public string RollbackHint { get; set; } = string.Empty;
}

public sealed class TimelineEventItemViewModel
{
    public string TimestampText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
    public string Kind { get; set; } = string.Empty;
    public string FindingId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
}

public sealed class UpdateHistoryItemViewModel
{
    public string TimestampText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

public sealed class UpdateDiagnosticItemViewModel
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string TimestampText { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

public sealed class HistoryViewModel : ReportPageViewModelBase
{
    private string _selectedAuditFilter = "all";

    public HistoryViewModel(
        ReportStore reportStore,
        IpcClientService ipcClient,
        DesktopActionRunner actionRunner,
        ObservableCollection<RemediationQueueItemViewModel> remediationQueue)
        : base("Verlauf / Historie", reportStore, ipcClient, actionRunner)
    {
        RemediationQueue = remediationQueue;
        RemediationQueue.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasRemediationQueue));
        SetAuditFilterCommand = new RelayCommand(param => SetAuditFilter(param?.ToString()));
        RollbackActionCommand = new AsyncRelayCommand(RollbackActionAsync, CanRollbackAction);
        OnReportUpdated(reportStore.CurrentReport);
    }

    public ObservableCollection<FindingCardViewModel> RecentlyResolvedFindings { get; private set; } = new();
    public ObservableCollection<AutoFixLogItemViewModel> AutoFixLogs { get; private set; } = new();
    public ObservableCollection<AutoFixLogItemViewModel> FilteredAutoFixLogs { get; private set; } = new();
    public ObservableCollection<TimelineEventItemViewModel> TimelineEvents { get; private set; } = new();
    public ObservableCollection<UpdateHistoryItemViewModel> UpdateHistory { get; private set; } = new();
    public ObservableCollection<UpdateDiagnosticItemViewModel> UpdateDiagnostics { get; private set; } = new();
    public ObservableCollection<RemediationQueueItemViewModel> RemediationQueue { get; }

    public RelayCommand SetAuditFilterCommand { get; }
    public AsyncRelayCommand RollbackActionCommand { get; }

    public string SelectedAuditFilter
    {
        get => _selectedAuditFilter;
        private set
        {
            if (string.Equals(_selectedAuditFilter, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedAuditFilter = value;
            RaisePropertyChanged(nameof(SelectedAuditFilter));
            RaisePropertyChanged(nameof(IsAuditFilterAllSelected));
            RaisePropertyChanged(nameof(IsAuditFilterSuccessSelected));
            RaisePropertyChanged(nameof(IsAuditFilterErrorSelected));
        }
    }

    public bool IsAuditFilterAllSelected => string.Equals(SelectedAuditFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsAuditFilterSuccessSelected => string.Equals(SelectedAuditFilter, "success", StringComparison.OrdinalIgnoreCase);
    public bool IsAuditFilterErrorSelected => string.Equals(SelectedAuditFilter, "error", StringComparison.OrdinalIgnoreCase);

    public string AuditFilterAllLabel => $"Alle ({AutoFixLogs.Count})";
    public string AuditFilterSuccessLabel => $"Erfolgreich ({AutoFixLogs.Count(x => x.IsSuccess)})";
    public string AuditFilterErrorLabel => $"Fehlgeschlagen ({AutoFixLogs.Count(x => !x.IsSuccess)})";

    public bool HasFilteredAutoFixLogs => FilteredAutoFixLogs.Count > 0;
    public bool HasRemediationQueue => RemediationQueue.Count > 0;
    public bool HasTimelineEvents => TimelineEvents.Count > 0;
    public bool HasUpdateHistory => UpdateHistory.Count > 0;
    public bool HasUpdateDiagnostics => UpdateDiagnostics.Count > 0;
    public int UpdateHistoryCount => UpdateHistory.Count;
    public int UpdateDiagnosticsCount => UpdateDiagnostics.Count;

    public string AuditEmptyText => AutoFixLogs.Count == 0
        ? "Noch keine Auto-Fix Vorgänge protokolliert."
        : "Keine Einträge im gewählten Audit-Filter.";

    public string TimelineEmptyText => "Noch keine Timeline-Einträge verfügbar.";
    public string UpdateHistoryEmptyText => "Noch keine Update-Verlaufseinträge vorhanden.";
    public string UpdateDiagnosticsEmptyText => "Keine aktuellen Fehlerdiagnosen im Update-Bereich.";

    protected override void OnReportUpdated(ScanReportDto report)
    {
        RecentlyResolvedFindings = BuildCards(report.RecentlyResolved.OrderByDescending(f => f.State.ResolvedAtUtc));
        RaisePropertyChanged(nameof(RecentlyResolvedFindings));

        Dictionary<string, FindingDto> findingLookup = report.Findings
            .Concat(report.RecentlyResolved)
            .GroupBy(f => f.FindingId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        AutoFixLogs = new ObservableCollection<AutoFixLogItemViewModel>(report.RecentAutoFixLog
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x =>
            {
                findingLookup.TryGetValue(x.FindingId, out FindingDto? finding);
                FindingSeverity severity = finding?.Severity ?? FindingSeverity.Info;
                return new AutoFixLogItemViewModel
                {
                    TimestampText = x.TimestampUtc.ToLocalTime().ToString("g"),
                    FindingId = x.FindingId,
                    FindingTitle = finding?.Title ?? x.FindingId,
                    ActionId = x.ActionId,
                    ActionExecutionId = x.ActionExecutionId ?? string.Empty,
                    ResultText = x.Success ? "Erfolgreich" : "Fehlgeschlagen",
                    ResultLevel = x.Success ? "success" : "critical",
                    SeverityText = severity switch
                    {
                        FindingSeverity.Critical => "Kritisch",
                        FindingSeverity.Warning => "Warnung",
                        _ => "Info"
                    },
                    Message = x.Message,
                    IsSuccess = x.Success,
                    CanRollback = x.RollbackAvailable && !string.IsNullOrWhiteSpace(x.ActionExecutionId),
                    RollbackHint = x.RollbackHint ?? string.Empty
                };
            }));
        RaisePropertyChanged(nameof(AutoFixLogs));
        RaisePropertyChanged(nameof(AuditFilterAllLabel));
        RaisePropertyChanged(nameof(AuditFilterSuccessLabel));
        RaisePropertyChanged(nameof(AuditFilterErrorLabel));

        TimelineEvents = new ObservableCollection<TimelineEventItemViewModel>(report.Timeline
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new TimelineEventItemViewModel
            {
                TimestampText = x.TimestampUtc.ToLocalTime().ToString("g"),
                Title = x.Title,
                Message = x.Message,
                Level = string.IsNullOrWhiteSpace(x.Level) ? "info" : x.Level,
                Kind = x.Kind,
                FindingId = x.FindingId ?? string.Empty,
                ActionId = x.ActionId ?? string.Empty
            }));
        RaisePropertyChanged(nameof(TimelineEvents));
        RaisePropertyChanged(nameof(HasTimelineEvents));

        BuildUpdateHistory(report);
        BuildUpdateDiagnostics(report);

        ApplyAuditFilter();
    }

    private void SetAuditFilter(string? value)
    {
        string normalized = value?.ToLowerInvariant() switch
        {
            "success" => "success",
            "error" => "error",
            _ => "all"
        };

        SelectedAuditFilter = normalized;
        ApplyAuditFilter();
    }

    private void ApplyAuditFilter()
    {
        IEnumerable<AutoFixLogItemViewModel> filtered = SelectedAuditFilter switch
        {
            "success" => AutoFixLogs.Where(x => x.IsSuccess),
            "error" => AutoFixLogs.Where(x => !x.IsSuccess),
            _ => AutoFixLogs
        };

        FilteredAutoFixLogs = new ObservableCollection<AutoFixLogItemViewModel>(filtered);
        RaisePropertyChanged(nameof(FilteredAutoFixLogs));
        RaisePropertyChanged(nameof(HasFilteredAutoFixLogs));
        RaisePropertyChanged(nameof(AuditEmptyText));
    }

    private async Task RollbackActionAsync(object? parameter)
    {
        string actionExecutionId = parameter?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionExecutionId))
        {
            return;
        }

        ActionExecutionResultDto result = await IpcClient.RollbackActionAsync(actionExecutionId);
        MessageBox.Show(result.Message,
            "Rollback",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        await IpcClient.TriggerScanAsync();
    }

    private static bool CanRollbackAction(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(parameter?.ToString());
    }

    private void BuildUpdateHistory(ScanReportDto report)
    {
        List<UpdateHistoryItemViewModel> entries = report.Timeline
            .Where(IsUpdateTimelineEvent)
            .OrderByDescending(x => x.TimestampUtc)
            .Take(30)
            .Select(x => new UpdateHistoryItemViewModel
            {
                TimestampText = x.TimestampUtc.ToLocalTime().ToString("g"),
                Title = string.IsNullOrWhiteSpace(x.Title) ? "Update-Ereignis" : x.Title,
                Details = x.Message,
                Source = NormalizeUpdateSource(x.Kind, x.ActionId),
                Status = NormalizeUpdateLevel(x.Level),
                IsError = IsUpdateErrorLevel(x.Level)
            })
            .ToList();

        if (entries.Count == 0)
        {
            entries = report.Findings
                .Where(IsUpdateFinding)
                .OrderByDescending(f => f.State.LastSeenUtc ?? f.DetectedAtUtc)
                .Take(20)
                .Select(f => new UpdateHistoryItemViewModel
                {
                    TimestampText = (f.State.LastSeenUtc ?? f.DetectedAtUtc).ToLocalTime().ToString("g"),
                    Title = f.Title,
                    Details = f.Summary,
                    Source = f.FindingId.StartsWith("apps.outdated", StringComparison.OrdinalIgnoreCase)
                        ? "WinGet"
                        : "Windows Update",
                    Status = f.Severity == FindingSeverity.Critical ? "Kritisch" : "Offen",
                    IsError = f.Severity == FindingSeverity.Critical
                })
                .ToList();
        }

        UpdateHistory = new ObservableCollection<UpdateHistoryItemViewModel>(entries);
        RaisePropertyChanged(nameof(UpdateHistory));
        RaisePropertyChanged(nameof(HasUpdateHistory));
        RaisePropertyChanged(nameof(UpdateHistoryCount));
        RaisePropertyChanged(nameof(UpdateHistoryEmptyText));
    }

    private void BuildUpdateDiagnostics(ScanReportDto report)
    {
        var diagnostics = new List<UpdateDiagnosticItemViewModel>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> sensorError in report.SensorErrors)
        {
            if (!IsUpdateSensorKey(sensorError.Key))
            {
                continue;
            }

            string key = $"{sensorError.Key}|{sensorError.Value}";
            if (!dedupe.Add(key))
            {
                continue;
            }

            diagnostics.Add(new UpdateDiagnosticItemViewModel
            {
                Source = sensorError.Key,
                Message = sensorError.Value,
                Hint = "Sensorfehler: Service prüfen und Update-Suche erneut starten.",
                TimestampUtc = report.GeneratedAtUtc,
                TimestampText = report.GeneratedAtUtc.ToLocalTime().ToString("g"),
                IsCritical = true
            });
        }

        foreach (TimelineEventDto evt in report.Timeline
                     .Where(IsUpdateTimelineEvent)
                     .Where(x => IsUpdateErrorLevel(x.Level))
                     .OrderByDescending(x => x.TimestampUtc)
                     .Take(20))
        {
            string key = $"{evt.Title}|{evt.Message}|{evt.TimestampUtc}";
            if (!dedupe.Add(key))
            {
                continue;
            }

            diagnostics.Add(new UpdateDiagnosticItemViewModel
            {
                Source = NormalizeUpdateSource(evt.Kind, evt.ActionId),
                Message = string.IsNullOrWhiteSpace(evt.Message) ? "Unbekannter Update-Fehler." : evt.Message,
                Hint = string.IsNullOrWhiteSpace(evt.Title) ? "Details im Timeline-Bereich prüfen." : evt.Title,
                TimestampUtc = evt.TimestampUtc,
                TimestampText = evt.TimestampUtc.ToLocalTime().ToString("g"),
                IsCritical = true
            });
        }

        foreach (var log in report.RecentAutoFixLog
                     .Where(x => !x.Success && IsUpdateAction(x.ActionId))
                     .OrderByDescending(x => x.TimestampUtc)
                     .Take(20))
        {
            string key = $"{log.ActionId}|{log.Message}|{log.TimestampUtc}";
            if (!dedupe.Add(key))
            {
                continue;
            }

            diagnostics.Add(new UpdateDiagnosticItemViewModel
            {
                Source = string.IsNullOrWhiteSpace(log.ActionId) ? "Update" : log.ActionId,
                Message = string.IsNullOrWhiteSpace(log.Message) ? "Update-Aktion fehlgeschlagen." : log.Message,
                Hint = "Aktion erneut starten oder Service-Verbindung prüfen.",
                TimestampUtc = log.TimestampUtc,
                TimestampText = log.TimestampUtc.ToLocalTime().ToString("g"),
                IsCritical = true
            });
        }

        UpdateDiagnostics = new ObservableCollection<UpdateDiagnosticItemViewModel>(
            diagnostics
                .OrderByDescending(x => x.TimestampUtc)
                .Take(20));

        RaisePropertyChanged(nameof(UpdateDiagnostics));
        RaisePropertyChanged(nameof(HasUpdateDiagnostics));
        RaisePropertyChanged(nameof(UpdateDiagnosticsCount));
        RaisePropertyChanged(nameof(UpdateDiagnosticsEmptyText));
    }

    private static bool IsUpdateTimelineEvent(TimelineEventDto evt)
    {
        string haystack = $"{evt.Kind} {evt.Title} {evt.Message} {evt.ActionId}";
        return haystack.Contains("update", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("winget", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("treiber", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("driver", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("windows", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("kb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpdateFinding(FindingDto finding)
    {
        return finding.FindingId.StartsWith("apps.outdated", StringComparison.OrdinalIgnoreCase)
               || finding.FindingId.StartsWith("updates.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpdateSensorKey(string key)
    {
        return key.Contains("windows_updates", StringComparison.OrdinalIgnoreCase)
               || key.Contains("app_updates", StringComparison.OrdinalIgnoreCase)
               || key.Contains("updates", StringComparison.OrdinalIgnoreCase)
               || key.Contains("winget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpdateAction(string actionId)
    {
        return actionId.Contains("update", StringComparison.OrdinalIgnoreCase)
               || actionId.Contains("apps.", StringComparison.OrdinalIgnoreCase)
               || actionId.Contains("winget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpdateErrorLevel(string? level)
    {
        return string.Equals(level, "error", StringComparison.OrdinalIgnoreCase)
               || string.Equals(level, "critical", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUpdateLevel(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "success" => "Erfolg",
            "warning" => "Warnung",
            "warn" => "Warnung",
            "critical" => "Fehler",
            "error" => "Fehler",
            _ => "Info"
        };
    }

    private static string NormalizeUpdateSource(string? kind, string? actionId)
    {
        string haystack = $"{kind} {actionId}";
        if (haystack.Contains("winget", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("apps.", StringComparison.OrdinalIgnoreCase))
        {
            return "WinGet";
        }

        if (haystack.Contains("driver", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("treiber", StringComparison.OrdinalIgnoreCase))
        {
            return "Treiber";
        }

        if (haystack.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows Update";
        }

        return "Update";
    }
}


