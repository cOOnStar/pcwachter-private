using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Windows;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class NetworkViewModel : ReportPageViewModelBase
{
    private string _networkStatus = "Unbekannt";
    private int _activeAdapterCount;
    private int _totalNetworkFindings;
    private int _criticalNetworkFindings;
    private int _warningNetworkFindings;
    private string _adapterSummary = "-";
    private string _gatewayLatencyText = "-";
    private string _publicDnsLatencyText = "-";
    private string _proxyStateText = "-";
    private bool _hasInternet;

    public NetworkViewModel(ReportStore reportStore, IpcClientService ipcClient, DesktopActionRunner actionRunner)
        : base("Netzwerk", reportStore, ipcClient, actionRunner)
    {
        FlushDnsCommand = new AsyncRelayCommand(() => RunNetworkFixAsync("action.network.flush_dns"));
        DisableProxyCommand = new AsyncRelayCommand(() => RunNetworkFixAsync("action.network.disable_proxy"));
        ResetAdaptersCommand = new AsyncRelayCommand(() => RunNetworkFixAsync("action.network.reset_adapters"));
        OnReportUpdated(reportStore.CurrentReport);
        UpdateNetworkStatus();
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        set
        {
            if (SetProperty(ref _networkStatus, value))
            {
                RaisePropertyChanged(nameof(NetworkStatusHint));
                RaisePropertyChanged(nameof(NetworkOverallLabel));
            }
        }
    }

    public ObservableCollection<FindingCardViewModel> NetworkFindings { get; private set; } = new();
    public AsyncRelayCommand FlushDnsCommand { get; }
    public AsyncRelayCommand DisableProxyCommand { get; }
    public AsyncRelayCommand ResetAdaptersCommand { get; }

    public int ActiveAdapterCount
    {
        get => _activeAdapterCount;
        private set => SetProperty(ref _activeAdapterCount, value);
    }

    public int TotalNetworkFindings
    {
        get => _totalNetworkFindings;
        private set
        {
            if (SetProperty(ref _totalNetworkFindings, value))
            {
                RaisePropertyChanged(nameof(HasNetworkFindings));
                RaisePropertyChanged(nameof(NetworkOverallLabel));
            }
        }
    }

    public int CriticalNetworkFindings
    {
        get => _criticalNetworkFindings;
        private set
        {
            if (SetProperty(ref _criticalNetworkFindings, value))
            {
                RaisePropertyChanged(nameof(NetworkOverallLabel));
            }
        }
    }

    public int WarningNetworkFindings
    {
        get => _warningNetworkFindings;
        private set
        {
            if (SetProperty(ref _warningNetworkFindings, value))
            {
                RaisePropertyChanged(nameof(NetworkOverallLabel));
            }
        }
    }

    public bool HasNetworkFindings => TotalNetworkFindings > 0;
    public bool HasInternet
    {
        get => _hasInternet;
        private set => SetProperty(ref _hasInternet, value);
    }

    public string AdapterSummary
    {
        get => _adapterSummary;
        private set => SetProperty(ref _adapterSummary, value);
    }

    public string GatewayLatencyText
    {
        get => _gatewayLatencyText;
        private set => SetProperty(ref _gatewayLatencyText, value);
    }

    public string PublicDnsLatencyText
    {
        get => _publicDnsLatencyText;
        private set => SetProperty(ref _publicDnsLatencyText, value);
    }

    public string ProxyStateText
    {
        get => _proxyStateText;
        private set => SetProperty(ref _proxyStateText, value);
    }

    public string NetworkStatusHint => string.Equals(NetworkStatus, "Online", StringComparison.OrdinalIgnoreCase)
        ? "Mindestens ein aktiver Adapter erkannt"
        : "Keine aktive Verbindung erkannt";

    public string NetworkOverallLabel => CriticalNetworkFindings > 0
        ? "Kritisch"
        : WarningNetworkFindings > 0
            ? "Achtung"
            : HasNetworkFindings
                ? "Hinweise"
                : "Stabil";

    protected override void OnReportUpdated(ScanReportDto report)
    {
        List<FindingDto> findings = report.Findings
            .Where(f => f.FindingId.StartsWith("network.", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Priority)
            .ToList();
        FindingDto? summary = findings.FirstOrDefault(f =>
            f.FindingId.Equals("network.diagnostics.summary", StringComparison.OrdinalIgnoreCase));

        AdapterSummary = ReadEvidence(summary, "adapter_summary", "-");
        string gatewayMs = ReadEvidence(summary, "gateway_latency_ms", "-");
        string dnsMs = ReadEvidence(summary, "public_dns_latency_ms", "-");
        GatewayLatencyText = gatewayMs == "-" ? "-" : $"{gatewayMs} ms";
        PublicDnsLatencyText = dnsMs == "-" ? "-" : $"{dnsMs} ms";
        bool proxyEnabled = bool.TryParse(ReadEvidence(summary, "proxy_enabled", "false"), out bool proxy) && proxy;
        ProxyStateText = proxyEnabled ? "Aktiv" : "Deaktiviert";
        HasInternet = bool.TryParse(ReadEvidence(summary, "has_internet", "false"), out bool internet) && internet;

        NetworkFindings = BuildCards(findings);
        RaisePropertyChanged(nameof(NetworkFindings));
        TotalNetworkFindings = findings.Count;
        CriticalNetworkFindings = findings.Count(f => f.Severity == FindingSeverity.Critical);
        WarningNetworkFindings = findings.Count(f => f.Severity == FindingSeverity.Warning);

        UpdateNetworkStatus();
    }

    private void UpdateNetworkStatus()
    {
        if (HasInternet)
        {
            NetworkStatus = "Online";
        }
        else
        {
            NetworkStatus = NetworkInterface.GetIsNetworkAvailable() ? "Online" : "Offline";
        }

        ActiveAdapterCount = NetworkInterface
            .GetAllNetworkInterfaces()
            .Count(i => i.OperationalStatus == OperationalStatus.Up &&
                        i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        i.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
    }

    private async Task RunNetworkFixAsync(string actionId)
    {
        ActionExecutionResultDto result = await IpcClient.RunActionAsync(actionId);
        if (!result.Success)
        {
            MessageBox.Show(
                result.Message,
                "Netzwerkaktion fehlgeschlagen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        await IpcClient.TriggerScanAsync();
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
}
