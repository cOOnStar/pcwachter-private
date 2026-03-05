using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ReportStore _reportStore;
    private readonly IpcClientService _ipcClient;
    private readonly AppUiStateStore _uiStateStore;
    private readonly AppUiState _uiState;
    private readonly UpdaterIntegrationService _updaterService;
    private readonly string _programVersionRaw = typeof(MainViewModel).Assembly.GetName().Version?.ToString(4) ?? "1.0.0.0";
    private readonly string _programVersionDisplay = $"v{(typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0")}";

    private object? _currentPage;
    private string _currentPageKey = "Dashboard";
    private string _lastScanDisplay = $"Heute ({DateTime.Now:dd.MM.yyyy}) um --:--";
    private string _footerServiceStatusDisplay = "wird geprüft";
    private bool _isServiceConnected;
    private bool _isBusy;
    private bool _isConnectingToService;
    private string _busyMessage = string.Empty;
    private bool _isNotificationCenterOpen;
    private bool _isDemoMode;
    private int _lastCriticalCount = -1;
    private int _lastHealthScore = -1;
    private int _criticalFindingCount;
    private int _warningFindingCount;
    private bool _isAutomaticUpdatesEnabled;
    private bool _updateCheckStarted;
    private string? _lastNotifiedUpdateVersion;
    private string _selectedNotificationFilter = "all";
    private CancellationTokenSource? _connectionAttemptCts;
    private int _startupScanInProgress;

    public MainViewModel(AppUiStateStore uiStateStore, AppUiState uiState)
    {
        _uiStateStore = uiStateStore;
        _uiState = uiState;
        _isDemoMode = uiState.IsDemoMode;
        if (DesktopRuntimeOptions.ForceMockupOnly)
        {
            _isDemoMode = true;
            _uiState.IsDemoMode = true;
            _uiStateStore.Save(_uiState);
        }

        _isAutomaticUpdatesEnabled = uiState.IsAutomaticUpdatesEnabled;
        _updaterService = new UpdaterIntegrationService();

        _reportStore = new ReportStore();
        _ipcClient = new IpcClientService(_reportStore)
        {
            MockMode = _isDemoMode
        };
        DesktopActionRunner actionRunner = new(_ipcClient);

        DashboardPage = new DashboardViewModel(
            _reportStore,
            _ipcClient,
            actionRunner,
            () => Navigate("Options"),
            () => IsDemoMode,
            _uiState.HealthScoreHistory);
        RemediationQueue = new ObservableCollection<RemediationQueueItemViewModel>();
        SecurityPage = new SecurityViewModel(_reportStore, _ipcClient, actionRunner);
        WindowsPage = new WindowsViewModel(_reportStore, _ipcClient, actionRunner, _uiState.HealthScoreHistory);
        WindowsUpdatesPage = new WindowsUpdatesViewModel(_reportStore, _ipcClient, actionRunner);
        WindowsUpdatesFluentPage = new UpdatesFluentPageViewModel(WindowsUpdatesPage);
        StoragePage = new StorageViewModel(_reportStore, _ipcClient, actionRunner);
        StorageFluentPage = new StorageFluentPageViewModel(StoragePage);
        NetworkPage = new NetworkViewModel(_reportStore, _ipcClient, actionRunner);
        HistoryPage = new HistoryViewModel(_reportStore, _ipcClient, actionRunner, RemediationQueue);
        AccountPage = new AccountViewModel();
        OptionsPage = new OptionsViewModel(
            _reportStore,
            _ipcClient,
            actionRunner,
            () => IsDemoMode,
            SetDemoModeAsync,
            () => IsAutomaticUpdatesEnabled,
            SetAutomaticUpdatesEnabled);
        HelpPage = new HelpViewModel(_ipcClient);

        NavItems =
        [
            new NavItemViewModel("Dashboard", "Dashboard", "\uE80F"),
            new NavItemViewModel("Security", "Sicherheit", "\uEA18"),
            new NavItemViewModel("Windows", "Windows", "\uE80A"),
            new NavItemViewModel("WindowsUpdates", "Updates", "\uE895"),
            new NavItemViewModel("Storage", "Speicher", "\uE7F1"),
#if DEBUG
            new NavItemViewModel("StorageFluent", "Speicher (Fluent)", "\uE8A5"),
#endif
            new NavItemViewModel("Network", "Netzwerk", "\uE968"),
            new NavItemViewModel("History", "Verlauf / Historie", "\uE81C"),
            new NavItemViewModel("Account", "PCW\u00E4chter Konto", "\uE77B"),
            new NavItemViewModel("Options", "Optionen", "\uE713"),
            new NavItemViewModel("Help", "Hilfe", "\uE897")
        ];

        Notifications = new ObservableCollection<NotificationItemViewModel>();
        FilteredNotifications = new ObservableCollection<NotificationItemViewModel>();
        Notifications.CollectionChanged += NotificationsOnCollectionChanged;

        NavigateCommand = new RelayCommand(param => Navigate(param?.ToString() ?? "Dashboard"));
        RetryConnectionCommand = new AsyncRelayCommand(InitializeAsync);
        TriggerScanCommand = new AsyncRelayCommand(TriggerScanAsync);
        OpenOptionsCommand = new RelayCommand(() => Navigate("Options"));
        OpenHelpCommand = new RelayCommand(() => Navigate("Help"));
        ToggleNotificationCenterCommand = new RelayCommand(() => IsNotificationCenterOpen = !IsNotificationCenterOpen);
        ClearNotificationsCommand = new RelayCommand(ClearNotifications, () => Notifications.Count > 0);
        MarkAllNotificationsReadCommand = new RelayCommand(MarkAllNotificationsAsRead, () => Notifications.Any(n => !n.IsRead));
        SetNotificationFilterCommand = new RelayCommand(param => SetNotificationFilter(param?.ToString()));

        _reportStore.ReportUpdated += (_, report) => OnReportUpdated(report);
        _ipcClient.ConnectionChanged += (_, connected) =>
        {
            if (IsDemoMode)
            {
                return;
            }

            bool previous = IsServiceConnected;
            IsServiceConnected = connected;
            FooterServiceStatusDisplay = connected ? "verbunden" : "nicht verbunden";

            if (connected && !previous)
            {
                AddNotification("success", "Service verbunden", "Die Live-Verbindung zum lokalen Service ist aktiv.");
            }
            else if (!connected && previous)
            {
                AddNotification("critical", "Service getrennt", "Live-Daten konnten nicht aktualisiert werden.");
            }
        };
        _ipcClient.RemediationProgress += (_, progress) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateRemediationQueue(progress);

                if (progress.Percent >= 100)
                {
                    BusyMessage = string.Empty;
                    IsBusy = false;
                    bool isFailure = progress.Message.Contains("fehl", StringComparison.OrdinalIgnoreCase)
                                     || progress.Message.Contains("error", StringComparison.OrdinalIgnoreCase);
                    AddNotification(isFailure ? "critical" : "success", "Aktion abgeschlossen", progress.Message);
                    return;
                }

                BusyMessage = progress.Message;
                IsBusy = true;
            });
        };

        if (IsDemoMode)
        {
            SetDemoStatus();
            PublishDemoReport();
            AddNotification("info", "Demo-Modus", "Die App zeigt lokale Demo-Daten an.");
        }

        if (!string.IsNullOrWhiteSpace(_uiState.PendingUpdateVersion))
        {
            AddUpdateAvailableNotification(_uiState.PendingUpdateVersion!);
        }

        Navigate(_uiState.LastNavKey);
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }
    public ObservableCollection<NotificationItemViewModel> Notifications { get; }
    public ObservableCollection<NotificationItemViewModel> FilteredNotifications { get; }
    public ObservableCollection<RemediationQueueItemViewModel> RemediationQueue { get; }

    public DashboardViewModel DashboardPage { get; }
    public SecurityViewModel SecurityPage { get; }
    public WindowsViewModel WindowsPage { get; }
    public WindowsUpdatesViewModel WindowsUpdatesPage { get; }
    public UpdatesFluentPageViewModel WindowsUpdatesFluentPage { get; }
    public StorageViewModel StoragePage { get; }
    public StorageFluentPageViewModel StorageFluentPage { get; }
    public NetworkViewModel NetworkPage { get; }
    public HistoryViewModel HistoryPage { get; }
    public AccountViewModel AccountPage { get; }
    public OptionsViewModel OptionsPage { get; }
    public HelpViewModel HelpPage { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageKey
    {
        get => _currentPageKey;
        private set => SetProperty(ref _currentPageKey, value);
    }

    public string LastScanDisplay
    {
        get => _lastScanDisplay;
        set => SetProperty(ref _lastScanDisplay, value);
    }

    public string ProgramVersionDisplay => _programVersionDisplay;

    public string AccountStatusDisplay => "nicht angemeldet";

    public string FooterServiceStatusDisplay
    {
        get => _footerServiceStatusDisplay;
        set => SetProperty(ref _footerServiceStatusDisplay, value);
    }

    public bool IsServiceConnected
    {
        get => _isServiceConnected;
        set
        {
            if (SetProperty(ref _isServiceConnected, value))
            {
                RaisePropertyChanged(nameof(IsServiceDisconnected));
            }
        }
    }

    public bool IsServiceDisconnected => !IsServiceConnected;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsConnectingToService
    {
        get => _isConnectingToService;
        private set => SetProperty(ref _isConnectingToService, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public bool IsNotificationCenterOpen
    {
        get => _isNotificationCenterOpen;
        set
        {
            if (!SetProperty(ref _isNotificationCenterOpen, value))
            {
                return;
            }

            if (value)
            {
                MarkAllNotificationsAsRead();
            }
        }
    }

    public int UnreadNotificationCount => Notifications.Count(n => !n.IsRead);
    public int ImportantNotificationCount => Notifications.Count(n => n.IsImportant);
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;
    public bool HasNotifications => Notifications.Count > 0;
    public bool HasFilteredNotifications => FilteredNotifications.Count > 0;
    public bool HasCriticalFindings => CriticalFindingCount > 0;
    public bool HasWarningFindings => WarningFindingCount > 0;
    public int UnresolvedIssueCount => CriticalFindingCount + WarningFindingCount;

    public string SelectedNotificationFilter
    {
        get => _selectedNotificationFilter;
        private set
        {
            if (string.Equals(_selectedNotificationFilter, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedNotificationFilter = value;
            RaisePropertyChanged(nameof(SelectedNotificationFilter));
            RaisePropertyChanged(nameof(IsNotificationFilterAllSelected));
            RaisePropertyChanged(nameof(IsNotificationFilterUnreadSelected));
            RaisePropertyChanged(nameof(IsNotificationFilterImportantSelected));
        }
    }

    public bool IsNotificationFilterAllSelected => string.Equals(SelectedNotificationFilter, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsNotificationFilterUnreadSelected => string.Equals(SelectedNotificationFilter, "unread", StringComparison.OrdinalIgnoreCase);
    public bool IsNotificationFilterImportantSelected => string.Equals(SelectedNotificationFilter, "important", StringComparison.OrdinalIgnoreCase);
    public string NotificationFilterAllLabel => $"Alle ({Notifications.Count})";
    public string NotificationFilterUnreadLabel => $"Ungelesen ({UnreadNotificationCount})";
    public string NotificationFilterImportantLabel => $"Wichtig ({ImportantNotificationCount})";

    public bool IsDemoMode
    {
        get => _isDemoMode;
        private set
        {
            if (SetProperty(ref _isDemoMode, value))
            {
                RaisePropertyChanged(nameof(LiveModeLabel));
            }
        }
    }

    public string LiveModeLabel => IsDemoMode ? "Demo" : "Live";

    public bool IsAutomaticUpdatesEnabled => _isAutomaticUpdatesEnabled;

    public int CriticalFindingCount
    {
        get => _criticalFindingCount;
        private set
        {
            if (SetProperty(ref _criticalFindingCount, value))
            {
                RaisePropertyChanged(nameof(HasCriticalFindings));
                RaisePropertyChanged(nameof(UnresolvedIssueCount));
            }
        }
    }

    public int WarningFindingCount
    {
        get => _warningFindingCount;
        private set
        {
            if (SetProperty(ref _warningFindingCount, value))
            {
                RaisePropertyChanged(nameof(HasWarningFindings));
                RaisePropertyChanged(nameof(UnresolvedIssueCount));
            }
        }
    }

    public RelayCommand NavigateCommand { get; }
    public AsyncRelayCommand RetryConnectionCommand { get; }
    public AsyncRelayCommand TriggerScanCommand { get; }
    public RelayCommand OpenOptionsCommand { get; }
    public RelayCommand OpenHelpCommand { get; }
    public RelayCommand ToggleNotificationCenterCommand { get; }
    public RelayCommand ClearNotificationsCommand { get; }
    public RelayCommand MarkAllNotificationsReadCommand { get; }
    public RelayCommand SetNotificationFilterCommand { get; }

    public async Task InitializeAsync()
    {
        if (IsDemoMode)
        {
            _connectionAttemptCts?.Cancel();
            _connectionAttemptCts?.Dispose();
            _connectionAttemptCts = null;
            IsConnectingToService = false;
            SetDemoStatus();
            PublishDemoReport();
            StartUpdateCheckIfNeeded();
            return;
        }

        if (_connectionAttemptCts is not null)
        {
            return;
        }

        using CancellationTokenSource connectionAttemptCts = new();
        _connectionAttemptCts = connectionAttemptCts;
        CancellationToken cancellationToken = connectionAttemptCts.Token;

        IsConnectingToService = true;
        FooterServiceStatusDisplay = "wird geprüft";

        try
        {
            bool connected = await _ipcClient.ConnectAsync(cancellationToken);
            IsServiceConnected = connected;
            FooterServiceStatusDisplay = connected ? "verbunden" : "nicht verbunden";

            if (connected)
            {
                await _ipcClient.GetLatestReportAsync(cancellationToken);
            }

            await _ipcClient.SubscribeEventsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_connectionAttemptCts, connectionAttemptCts))
            {
                _connectionAttemptCts = null;
            }

            IsConnectingToService = false;
            StartUpdateCheckIfNeeded();
        }

        if (!IsDemoMode && IsServiceConnected)
        {
            StartStartupScanInBackground();
        }
    }

    public async Task SetDemoModeAsync(bool enabled)
    {
        if (DesktopRuntimeOptions.ForceMockupOnly && !enabled)
        {
            AddNotification("info", "Mockup-only aktiv", "Live-Modus ist in dieser Build-Konfiguration deaktiviert.");
            IsDemoMode = true;
            _uiState.IsDemoMode = true;
            _ipcClient.MockMode = true;
            PersistUiState();
            return;
        }

        if (IsDemoMode == enabled)
        {
            return;
        }

        IsDemoMode = enabled;
        _uiState.IsDemoMode = enabled;
        _ipcClient.MockMode = enabled;

        if (enabled)
        {
            SetDemoStatus();
            PublishDemoReport();
            AddNotification("info", "Demo-Modus aktiviert", "Es werden lokale UI-Testdaten verwendet.");
        }
        else
        {
            AddNotification("info", "Live-Modus aktiviert", "Die App verbindet sich wieder mit dem lokalen Service.");
            await InitializeAsync();
        }

        PersistUiState();
    }

    public void SetAutomaticUpdatesEnabled(bool enabled)
    {
        if (_isAutomaticUpdatesEnabled == enabled)
        {
            return;
        }

        _isAutomaticUpdatesEnabled = enabled;
        _uiState.IsAutomaticUpdatesEnabled = enabled;
        RaisePropertyChanged(nameof(IsAutomaticUpdatesEnabled));
        PersistUiState();
    }

    public void PersistUiState(double width, double height, double left, double top, bool isMaximized)
    {
        _uiState.WindowWidth = width;
        _uiState.WindowHeight = height;
        _uiState.WindowLeft = left;
        _uiState.WindowTop = top;
        _uiState.WindowMaximized = isMaximized;
        _uiState.LastNavKey = ResolveNavKey(_uiState.LastNavKey);
        PersistUiState();
    }

    public void OpenDashboard()
    {
        Navigate("Dashboard");
    }

    private void PersistUiState()
    {
        _uiStateStore.Save(_uiState);
    }

    private async Task TriggerScanAsync()
    {
        if (IsDemoMode)
        {
            PublishDemoReport();
            AddNotification("info", "Demo-Scan", "Demo-Daten wurden aktualisiert.");
            return;
        }

        IsBusy = true;
        BusyMessage = "Scan wird gestartet...";
        AddNotification("info", "Scan gestartet", "Ein neuer Systemscan wurde gestartet.");
        try
        {
            await _ipcClient.TriggerScanAsync();
            AddNotification("success", "Scan abgeschlossen", "Neue Ergebnisse wurden geladen.");
        }
        catch
        {
            AddNotification("critical", "Scan fehlgeschlagen", "Der Scan konnte nicht gestartet werden.");
            throw;
        }
        finally
        {
            BusyMessage = string.Empty;
            IsBusy = false;
        }
    }

    private void StartStartupScanInBackground()
    {
        if (Interlocked.Exchange(ref _startupScanInProgress, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _ipcClient.TriggerScanAsync(CancellationToken.None);
            }
            catch
            {
                // Startup scan is best-effort; periodic polling continues.
            }
            finally
            {
                Interlocked.Exchange(ref _startupScanInProgress, 0);
            }
        });
    }

    private void Navigate(string key)
    {
        string normalizedKey = ResolveNavKey(key);
        CurrentPage = normalizedKey switch
        {
            "Dashboard" => DashboardPage,
            "Security" => SecurityPage,
            "Windows" => WindowsPage,
            "WindowsUpdates" => WindowsUpdatesFluentPage,
            "WindowsUpdatesFluent" => WindowsUpdatesFluentPage,
            "Storage" => StoragePage,
            "StorageFluent" => StorageFluentPage,
            "Network" => NetworkPage,
            "History" => HistoryPage,
            "Account" => AccountPage,
            "Options" => OptionsPage,
            "Help" => HelpPage,
            _ => DashboardPage
        };

        foreach (NavItemViewModel item in NavItems)
        {
            item.IsSelected = string.Equals(item.Key, normalizedKey, StringComparison.OrdinalIgnoreCase);
        }

        _uiState.LastNavKey = normalizedKey;
        CurrentPageKey = normalizedKey;
        PersistUiState();
    }

    private void OnReportUpdated(ScanReportDto report)
    {
        LastScanDisplay = FormatLastScan(report.GeneratedAtUtc.ToLocalTime());

        int criticalCount = report.Findings.Count(f => f.Severity == FindingSeverity.Critical);
        int warningCount = report.Findings.Count(f => f.Severity == FindingSeverity.Warning);
        CriticalFindingCount = criticalCount;
        WarningFindingCount = warningCount;
        if (_lastCriticalCount != criticalCount)
        {
            if (criticalCount > 0)
            {
                AddNotification(
                    "warning",
                    "Kritische Probleme erkannt",
                    $"{criticalCount} kritische Probleme benötigen Aufmerksamkeit.");
            }
            else if (_lastCriticalCount > 0)
            {
                AddNotification("success", "Keine kritischen Probleme", "Alle kritischen Probleme wurden behoben.");
            }
        }

        if (_lastHealthScore >= 0)
        {
            int delta = report.HealthScore - _lastHealthScore;
            if (delta >= 10)
            {
                AddNotification("success", "Gesundheitswert verbessert", $"Der Score hat sich um +{delta} Punkte verbessert.");
            }
            else if (delta <= -10)
            {
                AddNotification("warning", "Gesundheitswert gefallen", $"Der Score ist um {delta} Punkte gefallen.");
            }
        }

        UpdateNavAttention(report);
        _lastCriticalCount = criticalCount;
        _lastHealthScore = report.HealthScore;
    }

    private void UpdateNavAttention(ScanReportDto report)
    {
        SetNavAttention("Dashboard", report.Findings);
        SetNavAttention("Security", report.Findings.Where(f =>
            f.Category == FindingCategory.Security ||
            f.FindingId.Contains("security", StringComparison.OrdinalIgnoreCase)));
        SetNavAttention("WindowsUpdates", report.Findings.Where(f =>
            f.FindingId.StartsWith("updates.", StringComparison.OrdinalIgnoreCase)));
        SetNavAttention("Storage", report.Findings.Where(f =>
            f.Category == FindingCategory.Storage ||
            f.FindingId.StartsWith("storage.", StringComparison.OrdinalIgnoreCase)));
        SetNavAttention("Network", report.Findings.Where(f =>
            f.FindingId.StartsWith("network.", StringComparison.OrdinalIgnoreCase)));
        SetNavAttention("Windows", report.Findings.Where(f =>
            f.FindingId.StartsWith("startup.", StringComparison.OrdinalIgnoreCase) ||
            f.FindingId.StartsWith("health.", StringComparison.OrdinalIgnoreCase)));
    }

    private void SetNavAttention(string navKey, IEnumerable<FindingDto> findings)
    {
        NavItemViewModel? navItem = NavItems.FirstOrDefault(x => string.Equals(x.Key, navKey, StringComparison.OrdinalIgnoreCase));
        if (navItem is null)
        {
            return;
        }

        bool hasCritical = findings.Any(f => f.Severity == FindingSeverity.Critical);
        bool hasWarning = findings.Any(f => f.Severity == FindingSeverity.Warning);

        navItem.AttentionLevel = hasCritical
            ? "critical"
            : hasWarning
                ? "warning"
                : "none";
    }

    private void StartUpdateCheckIfNeeded()
    {
        if (_updateCheckStarted)
        {
            return;
        }

        _updateCheckStarted = true;
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            UpdateCheckResult result = await _updaterService.CheckForUpdateAsync(_programVersionRaw, CancellationToken.None);
            if (!result.Success || string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                return;
            }

            string latestVersion = NormalizeVersion(result.LatestVersion);
            if (result.IsUpdateAvailable)
            {
                _uiState.PendingUpdateVersion = latestVersion;
                _uiState.PendingUpdateDetectedAtUtc = DateTimeOffset.UtcNow;
                PersistUiState();
                AddUpdateAvailableNotification(latestVersion);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_uiState.PendingUpdateVersion))
            {
                _uiState.PendingUpdateVersion = null;
                _uiState.PendingUpdateDetectedAtUtc = null;
                PersistUiState();
            }
        }
        catch
        {
        }
    }

    private void AddUpdateAvailableNotification(string version)
    {
        string normalizedVersion = NormalizeVersion(version);
        if (string.Equals(_lastNotifiedUpdateVersion, normalizedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastNotifiedUpdateVersion = normalizedVersion;
        AddNotification(
            "info",
            "Update verfügbar",
            $"Es wurde eine neue Version gefunden: Version {normalizedVersion}.",
            "Jetzt installieren",
            new AsyncRelayCommand(() => InstallPendingUpdateNowAsync(normalizedVersion)));
    }

    private async Task InstallPendingUpdateNowAsync(string version)
    {
        _uiState.PendingUpdateVersion = NormalizeVersion(version);
        _uiState.PendingUpdateDetectedAtUtc = DateTimeOffset.UtcNow;
        PersistUiState();

        UpdaterExecutionResult result = _updaterService.StartUpdateDetached();
        if (!result.Started)
        {
            AddNotification(
                "critical",
                "Update konnte nicht gestartet werden",
                string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Updater-Start fehlgeschlagen." : result.ErrorMessage);
            return;
        }

        AddNotification("info", "Update gestartet", "Installation läuft. Die App wird jetzt geschlossen.");
        await Task.Delay(250);
        Application.Current.Shutdown();
    }

    private static string ResolveNavKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Dashboard";
        }

        return key switch
        {
            "Dashboard" => "Dashboard",
            "Security" => "Security",
            "Windows" => "Windows",
            "WindowsUpdates" => "WindowsUpdates",
            "WindowsUpdatesFluent" => "WindowsUpdates",
            "StorageFluent" => "StorageFluent",
            "Storage" => "Storage",
            "Network" => "Network",
            "History" => "History",
            "Account" => "Account",
            "Options" => "Options",
            "Help" => "Help",
            _ => "Dashboard"
        };
    }

    private static string FormatLastScan(DateTimeOffset localTime)
    {
        if (localTime.Date == DateTimeOffset.Now.Date)
        {
            return $"Heute ({localTime:dd.MM.yyyy}) um {localTime:HH:mm}";
        }

        return $"{localTime:dd.MM.yyyy} um {localTime:HH:mm}";
    }

    private void PublishDemoReport()
    {
        // No demo data – report starts empty until the service connects
        _reportStore.Update(new ScanReportDto
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            HealthScore = 0,
            Findings = [],
            TopFindings = [],
            RecentlyResolved = [],
            RecentAutoFixLog = [],
            Timeline = [],
        });
    }

    private void SetDemoStatus()
    {
        IsConnectingToService = false;
        IsServiceConnected = false;
        FooterServiceStatusDisplay = "demo";
        BusyMessage = string.Empty;
        IsBusy = false;
    }

    private void AddNotification(
        string level,
        string title,
        string message,
        string? actionLabel = null,
        ICommand? actionCommand = null)
    {
        NotificationItemViewModel item = new(level, title, message, DateTimeOffset.Now, actionLabel, actionCommand)
        {
            IsRead = IsNotificationCenterOpen
        };

        Notifications.Insert(0, item);
        if (Notifications.Count > 60)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }

        RebuildFilteredNotifications();
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? string.Empty
            : version.Trim().TrimStart('v', 'V');
    }

    private void UpdateRemediationQueue(ActionProgressDto progress)
    {
        RemediationQueueItemViewModel? item = RemediationQueue
            .FirstOrDefault(x => string.Equals(x.ActionId, progress.ActionId, StringComparison.OrdinalIgnoreCase) && x.IsRunning);

        if (item is null)
        {
            item = new RemediationQueueItemViewModel(progress.ActionId);
            RemediationQueue.Insert(0, item);
        }

        item.Message = progress.Message;
        item.Percent = Math.Clamp(progress.Percent, 0, 100);
        item.UpdatedAtLocal = DateTimeOffset.Now;

        if (progress.Percent >= 100)
        {
            bool isFailure = progress.Message.Contains("fehl", StringComparison.OrdinalIgnoreCase)
                             || progress.Message.Contains("error", StringComparison.OrdinalIgnoreCase);
            item.IsRunning = false;
            item.StatusText = isFailure ? "Fehlgeschlagen" : "Abgeschlossen";
            item.StatusLevel = isFailure ? "critical" : "success";
        }
        else
        {
            item.IsRunning = true;
            item.StatusText = "Läuft";
            item.StatusLevel = "info";
        }

        if (RemediationQueue.Count > 30)
        {
            RemediationQueue.RemoveAt(RemediationQueue.Count - 1);
        }
    }

    private void ClearNotifications()
    {
        foreach (NotificationItemViewModel item in Notifications)
        {
            item.PropertyChanged -= NotificationItemOnPropertyChanged;
        }

        Notifications.Clear();
        RebuildFilteredNotifications();
    }

    private void MarkAllNotificationsAsRead()
    {
        foreach (NotificationItemViewModel item in Notifications)
        {
            item.IsRead = true;
        }

        RebuildFilteredNotifications();
        RaiseNotificationCounters();
    }

    private void SetNotificationFilter(string? filter)
    {
        string normalized = filter?.Trim().ToLowerInvariant() switch
        {
            "unread" => "unread",
            "important" => "important",
            _ => "all"
        };

        SelectedNotificationFilter = normalized;
        RebuildFilteredNotifications();
    }

    private void RebuildFilteredNotifications()
    {
        IEnumerable<NotificationItemViewModel> source = Notifications;
        source = SelectedNotificationFilter switch
        {
            "unread" => source.Where(n => !n.IsRead),
            "important" => source.Where(n => n.IsImportant),
            _ => source
        };

        FilteredNotifications.Clear();
        foreach (NotificationItemViewModel item in source)
        {
            FilteredNotifications.Add(item);
        }

        RaisePropertyChanged(nameof(HasFilteredNotifications));
    }

    private void NotificationsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (object item in e.NewItems)
            {
                if (item is NotificationItemViewModel notification)
                {
                    notification.PropertyChanged += NotificationItemOnPropertyChanged;
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (object item in e.OldItems)
            {
                if (item is NotificationItemViewModel notification)
                {
                    notification.PropertyChanged -= NotificationItemOnPropertyChanged;
                }
            }
        }

        RaiseNotificationCounters();
        ClearNotificationsCommand.RaiseCanExecuteChanged();
        MarkAllNotificationsReadCommand.RaiseCanExecuteChanged();
        RebuildFilteredNotifications();
    }

    private void NotificationItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NotificationItemViewModel.IsRead), StringComparison.Ordinal))
        {
            RaiseNotificationCounters();
            MarkAllNotificationsReadCommand.RaiseCanExecuteChanged();
            RebuildFilteredNotifications();
        }
    }

    private void RaiseNotificationCounters()
    {
        RaisePropertyChanged(nameof(UnreadNotificationCount));
        RaisePropertyChanged(nameof(ImportantNotificationCount));
        RaisePropertyChanged(nameof(HasUnreadNotifications));
        RaisePropertyChanged(nameof(HasNotifications));
        RaisePropertyChanged(nameof(NotificationFilterAllLabel));
        RaisePropertyChanged(nameof(NotificationFilterUnreadLabel));
        RaisePropertyChanged(nameof(NotificationFilterImportantLabel));
    }

    public async ValueTask DisposeAsync()
    {
        _connectionAttemptCts?.Cancel();
        _connectionAttemptCts?.Dispose();
        _connectionAttemptCts = null;
        PersistUiState();
        await _ipcClient.DisposeAsync();
    }
}


