using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class OptionsViewModel : ReportPageViewModelBase
{
    private readonly Func<bool> _getDemoMode;
    private readonly Func<bool, Task> _setDemoModeAsync;
    private readonly Func<bool> _getAutomaticUpdatesEnabled;
    private readonly Action<bool> _setAutomaticUpdatesEnabled;

    private AutoFixMode _selectedMode = AutoFixMode.RecommendOnly;
    private bool _requireNetwork = true;
    private bool _requireAcPower;
    private int _maxFixesPerDay = 10;
    private int _cooldownHours = 2;
    private int _scanIntervalMinutes = 30;
    private int _storageCriticalPercentFree = 5;
    private int _storageWarningPercentFree = 15;
    private int _eventLogWarningCount24h = 10;
    private int _defenderSignatureWarningDays = 7;
    private int _defenderSignatureCriticalDays = 14;
    private string _baselineStatusText = "Keine Baseline vorhanden.";
    private string _baselineDriftText = "Noch kein Vergleich vorhanden.";
    private string _saveStatus = string.Empty;
    private bool _saveStatusIsError;
    private bool _isDemoMode;
    private bool _isAutomaticUpdatesEnabled;

    public OptionsViewModel(
        ReportStore reportStore,
        IpcClientService ipcClient,
        DesktopActionRunner actionRunner,
        Func<bool> getDemoMode,
        Func<bool, Task> setDemoModeAsync,
        Func<bool> getAutomaticUpdatesEnabled,
        Action<bool> setAutomaticUpdatesEnabled)
        : base("Optionen", reportStore, ipcClient, actionRunner)
    {
        _getDemoMode = getDemoMode;
        _setDemoModeAsync = setDemoModeAsync;
        _getAutomaticUpdatesEnabled = getAutomaticUpdatesEnabled;
        _setAutomaticUpdatesEnabled = setAutomaticUpdatesEnabled;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CreateBaselineCommand = new AsyncRelayCommand(CreateBaselineAsync);
        IsDemoMode = _getDemoMode();
        IsAutomaticUpdatesEnabled = _getAutomaticUpdatesEnabled();
        OnReportUpdated(reportStore.CurrentReport);
        _ = RefreshFeatureConfigAsync();
    }

    public Array Modes => Enum.GetValues(typeof(AutoFixMode));

    public AutoFixMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public bool RequireNetwork
    {
        get => _requireNetwork;
        set
        {
            if (SetProperty(ref _requireNetwork, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public bool RequireAcPower
    {
        get => _requireAcPower;
        set
        {
            if (SetProperty(ref _requireAcPower, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public int MaxFixesPerDay
    {
        get => _maxFixesPerDay;
        set
        {
            if (SetProperty(ref _maxFixesPerDay, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public int CooldownHours
    {
        get => _cooldownHours;
        set
        {
            if (SetProperty(ref _cooldownHours, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public int ScanIntervalMinutes
    {
        get => _scanIntervalMinutes;
        set
        {
            if (SetProperty(ref _scanIntervalMinutes, value))
            {
                RaisePolicySummaryProperties();
            }
        }
    }

    public int StorageCriticalPercentFree
    {
        get => _storageCriticalPercentFree;
        set => SetProperty(ref _storageCriticalPercentFree, value);
    }

    public int StorageWarningPercentFree
    {
        get => _storageWarningPercentFree;
        set => SetProperty(ref _storageWarningPercentFree, value);
    }

    public int EventLogWarningCount24h
    {
        get => _eventLogWarningCount24h;
        set => SetProperty(ref _eventLogWarningCount24h, value);
    }

    public int DefenderSignatureWarningDays
    {
        get => _defenderSignatureWarningDays;
        set => SetProperty(ref _defenderSignatureWarningDays, value);
    }

    public int DefenderSignatureCriticalDays
    {
        get => _defenderSignatureCriticalDays;
        set => SetProperty(ref _defenderSignatureCriticalDays, value);
    }

    public string BaselineStatusText
    {
        get => _baselineStatusText;
        set => SetProperty(ref _baselineStatusText, value);
    }

    public string BaselineDriftText
    {
        get => _baselineDriftText;
        set => SetProperty(ref _baselineDriftText, value);
    }

    public bool IsDemoMode
    {
        get => _isDemoMode;
        set
        {
            if (SetProperty(ref _isDemoMode, value))
            {
                RaisePropertyChanged(nameof(DemoModeHint));
                RaisePropertyChanged(nameof(DataSourceLabel));
            }
        }
    }

    public string DemoModeHint => IsDemoMode
        ? "Demo-Modus aktiv: Seiten zeigen lokale Testdaten."
        : "Live-Modus aktiv: Daten kommen vom lokalen Service.";

    public bool IsAutomaticUpdatesEnabled
    {
        get => _isAutomaticUpdatesEnabled;
        set
        {
            if (SetProperty(ref _isAutomaticUpdatesEnabled, value))
            {
                RaisePropertyChanged(nameof(AutomaticUpdateHint));
            }
        }
    }

    public string AutomaticUpdateHint => IsAutomaticUpdatesEnabled
        ? "Neue Versionen werden vor dem nächsten Programmstart automatisch installiert."
        : "Neue Versionen werden nur nach manueller Freigabe installiert.";

    public string AutoFixModeLabel => SelectedMode switch
    {
        AutoFixMode.Off => "Aus",
        AutoFixMode.RecommendOnly => "Empfehlung",
        AutoFixMode.AutoSafe => "Sicher Auto",
        _ => SelectedMode.ToString()
    };

    public string DataSourceLabel => IsDemoMode ? "Demo" : "Live";

    public string ExecutionGateLabel
    {
        get
        {
            if (RequireNetwork && RequireAcPower)
            {
                return "Netzwerk + Netzstrom";
            }

            if (RequireNetwork)
            {
                return "Nur Netzwerk";
            }

            if (RequireAcPower)
            {
                return "Nur Netzstrom";
            }

            return "Keine Einschraenkungen";
        }
    }

    public string SaveStatus
    {
        get => _saveStatus;
        set
        {
            if (SetProperty(ref _saveStatus, value))
            {
                RaisePropertyChanged(nameof(HasSaveStatus));
            }
        }
    }

    public bool SaveStatusIsError
    {
        get => _saveStatusIsError;
        private set => SetProperty(ref _saveStatusIsError, value);
    }

    public bool HasSaveStatus => !string.IsNullOrWhiteSpace(SaveStatus);

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CreateBaselineCommand { get; }

    protected override void OnReportUpdated(ScanReportDto report)
    {
        AutoFixPolicyDto policy = report.AutoFixPolicy ?? new AutoFixPolicyDto();

        SelectedMode = policy.Mode;
        RequireNetwork = policy.RequireNetwork;
        RequireAcPower = policy.RequireAcPower;
        MaxFixesPerDay = policy.MaxFixesPerDay;
        CooldownHours = policy.CooldownHours;
        ScanIntervalMinutes = policy.ScanIntervalMinutes;

        RuleThresholdsDto thresholds = report.RuleThresholds ?? new RuleThresholdsDto();
        thresholds.Normalize();
        StorageCriticalPercentFree = thresholds.StorageCriticalPercentFree;
        StorageWarningPercentFree = thresholds.StorageWarningPercentFree;
        EventLogWarningCount24h = thresholds.EventLogWarningCount24h;
        DefenderSignatureWarningDays = thresholds.DefenderSignatureWarningDays;
        DefenderSignatureCriticalDays = thresholds.DefenderSignatureCriticalDays;

        BaselineDriftSummaryDto baseline = report.BaselineDrift ?? new BaselineDriftSummaryDto();
        if (baseline.HasBaseline)
        {
            string baselineDate = baseline.BaselineCreatedAtUtc?.ToLocalTime().ToString("g") ?? "-";
            BaselineStatusText = $"Baseline: {baseline.BaselineLabel} ({baselineDate})";
            BaselineDriftText = $"Neu: {baseline.NewFindings}, Geaendert: {baseline.ChangedFindings}, Behoben: {baseline.ResolvedFindings}";
        }
        else
        {
            BaselineStatusText = "Keine Baseline vorhanden.";
            BaselineDriftText = "Noch kein Vergleich vorhanden.";
        }

        IsDemoMode = _getDemoMode();
        IsAutomaticUpdatesEnabled = _getAutomaticUpdatesEnabled();
        RaisePropertyChanged(nameof(DemoModeHint));
        RaisePolicySummaryProperties();
    }

    private async Task SaveAsync()
    {
        SaveStatusIsError = false;
        SaveStatus = string.Empty;

        var policy = new AutoFixPolicyDto
        {
            Mode = SelectedMode,
            RequireNetwork = RequireNetwork,
            RequireAcPower = RequireAcPower,
            MaxFixesPerDay = Math.Max(1, MaxFixesPerDay),
            CooldownHours = Math.Max(0, CooldownHours),
            ScanIntervalMinutes = Math.Max(5, ScanIntervalMinutes)
        };
        var thresholds = new RuleThresholdsDto
        {
            StorageCriticalPercentFree = StorageCriticalPercentFree,
            StorageWarningPercentFree = StorageWarningPercentFree,
            EventLogWarningCount24h = EventLogWarningCount24h,
            DefenderSignatureWarningDays = DefenderSignatureWarningDays,
            DefenderSignatureCriticalDays = DefenderSignatureCriticalDays
        };
        thresholds.Normalize();

        try
        {
            bool policySaved = await IpcClient.SetAutoFixPolicyAsync(policy);
            bool thresholdsSaved = await IpcClient.SetRuleThresholdsAsync(thresholds);
            await _setDemoModeAsync(IsDemoMode);
            _setAutomaticUpdatesEnabled(IsAutomaticUpdatesEnabled);
            RaisePropertyChanged(nameof(DemoModeHint));

            if (policySaved && thresholdsSaved)
            {
                SaveStatus = "Einstellungen gespeichert.";
                SaveStatusIsError = false;
            }
            else
            {
                SaveStatus = "Speichern teilweise fehlgeschlagen (Policy oder Schwellenwerte).";
                SaveStatusIsError = true;
            }
        }
        catch
        {
            SaveStatus = "Speichern fehlgeschlagen.";
            SaveStatusIsError = true;
        }
    }

    private async Task CreateBaselineAsync()
    {
        SaveStatusIsError = false;
        SaveStatus = string.Empty;

        CreateBaselineResultDto result = await IpcClient.CreateBaselineAsync();
        if (!result.Success)
        {
            SaveStatus = string.IsNullOrWhiteSpace(result.Message) ? "Baseline konnte nicht erstellt werden." : result.Message;
            SaveStatusIsError = true;
            return;
        }

        BaselineStatusText = result.Baseline.HasBaseline
            ? $"Baseline: {result.Baseline.BaselineLabel} ({result.Baseline.BaselineCreatedAtUtc?.ToLocalTime():g})"
            : "Keine Baseline vorhanden.";
        BaselineDriftText = $"Neu: {result.Baseline.NewFindings}, Geaendert: {result.Baseline.ChangedFindings}, Behoben: {result.Baseline.ResolvedFindings}";
        SaveStatus = string.IsNullOrWhiteSpace(result.Message) ? "Baseline erstellt." : result.Message;
        SaveStatusIsError = false;

        await IpcClient.TriggerScanAsync();
    }

    private async Task RefreshFeatureConfigAsync()
    {
        FeatureConfigDto? config = await IpcClient.GetFeatureConfigAsync();
        if (config is null)
        {
            return;
        }

        RuleThresholdsDto thresholds = config.RuleThresholds ?? new RuleThresholdsDto();
        thresholds.Normalize();
        StorageCriticalPercentFree = thresholds.StorageCriticalPercentFree;
        StorageWarningPercentFree = thresholds.StorageWarningPercentFree;
        EventLogWarningCount24h = thresholds.EventLogWarningCount24h;
        DefenderSignatureWarningDays = thresholds.DefenderSignatureWarningDays;
        DefenderSignatureCriticalDays = thresholds.DefenderSignatureCriticalDays;

        if (config.Baseline.HasBaseline)
        {
            BaselineStatusText = $"Baseline: {config.Baseline.BaselineLabel} ({config.Baseline.BaselineCreatedAtUtc?.ToLocalTime():g})";
            BaselineDriftText = $"Neu: {config.Baseline.NewFindings}, Geaendert: {config.Baseline.ChangedFindings}, Behoben: {config.Baseline.ResolvedFindings}";
        }
    }

    private void RaisePolicySummaryProperties()
    {
        RaisePropertyChanged(nameof(AutoFixModeLabel));
        RaisePropertyChanged(nameof(ExecutionGateLabel));
    }
}
