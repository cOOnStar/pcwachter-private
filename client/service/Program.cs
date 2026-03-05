using AgentService;
using AgentService.Interop;
using AgentService.Remediations;
using AgentService.Rules;
using AgentService.Runtime;
using AgentService.Sensors;
using PCWachter.Core;
using System.Globalization;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PCWaechterService";
});

var apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
if (string.IsNullOrWhiteSpace(apiOptions.AgentApiKey))
{
    apiOptions.AgentApiKey = Environment.GetEnvironmentVariable("PCWAECHTER_AGENT_API_KEY") ?? string.Empty;
}
apiOptions.BaseUrl = NormalizeApiBaseUrl(apiOptions.BaseUrl);

builder.Services.AddSingleton(apiOptions);
builder.Services.AddHttpClient("PcWaechterApi", client =>
{
    client.BaseAddress = new Uri(apiOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrWhiteSpace(apiOptions.AgentApiKey))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Agent-Api-Key", apiOptions.AgentApiKey);
    }
});

var uxOptions = builder.Configuration.GetSection("UxIntelligence").Get<UxIntelligenceOptions>() ?? new UxIntelligenceOptions();
builder.Services.AddSingleton(uxOptions);
builder.Services.AddSingleton<DeviceContextProvider>();
builder.Services.AddSingleton<IStateStore, JsonStateStore>();
builder.Services.AddSingleton<ScanCoordinator>();

builder.Services.AddSingleton<ISensor, DefenderSensor>();
builder.Services.AddSingleton<ISensor, StorageSensor>();
builder.Services.AddSingleton<ISensor, BitLockerSensor>();
builder.Services.AddSingleton<ISensor, PendingRebootSensor>();
builder.Services.AddSingleton<ISensor, FirewallSensor>();
builder.Services.AddSingleton<ISensor, EventLogSensor>();
builder.Services.AddSingleton<ISensor, SecurityHardeningSensor>();
builder.Services.AddSingleton<ISensor, AppUpdatesSensor>();
builder.Services.AddSingleton<ISensor, StartupAppsSensor>();
builder.Services.AddSingleton<ISensor, NetworkDiagnosticsSensor>();
builder.Services.AddSingleton<ISensor, WindowsUpdatesSensor>();
builder.Services.AddSingleton<ISensor, PerformanceWatchSensor>();

builder.Services.AddSingleton<IRule, DefenderRule>();
builder.Services.AddSingleton<IRule, StorageRule>();
builder.Services.AddSingleton<IRule, BitLockerRule>();
builder.Services.AddSingleton<IRule, PendingRebootRule>();
builder.Services.AddSingleton<IRule, FirewallRule>();
builder.Services.AddSingleton<IRule, EventLogRule>();
builder.Services.AddSingleton<IRule, AppUpdatesRule>();
builder.Services.AddSingleton<IRule, StartupRule>();
builder.Services.AddSingleton<IRule, NetworkDiagnosticsRule>();
builder.Services.AddSingleton<IRule, WindowsUpdatesRule>();
builder.Services.AddSingleton<IRule, PerformanceWatchRule>();

builder.Services.AddSingleton<IRemediation, DefenderUpdateSignaturesRemediation>();
builder.Services.AddSingleton<IRemediation, DefenderEnableRealtimeRemediation>();
builder.Services.AddSingleton<IRemediation, FirewallEnableAllRemediation>();
builder.Services.AddSingleton<IRemediation, AppsUpdateSelectedRemediation>();
builder.Services.AddSingleton<IRemediation, StartupDisableRemediation>();
builder.Services.AddSingleton<IRemediation, StartupUndoRemediation>();
builder.Services.AddSingleton<IRemediation, NetworkFlushDnsRemediation>();
builder.Services.AddSingleton<IRemediation, NetworkDisableProxyRemediation>();
builder.Services.AddSingleton<IRemediation, NetworkResetAdaptersRemediation>();
builder.Services.AddSingleton<IRemediation, WindowsInstallAllUpdatesRemediation>();
builder.Services.AddSingleton<IRemediation, WindowsInstallSecurityUpdatesRemediation>();
builder.Services.AddSingleton<IRemediation, WindowsInstallOptionalUpdatesRemediation>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<IpcServerHostedService>();

var host = builder.Build();
host.Run();

static string NormalizeApiBaseUrl(string? rawBaseUrl)
{
    const string fallback = "https://api.xn--pcwchter-2za.de";

    if (string.IsNullOrWhiteSpace(rawBaseUrl))
    {
        return fallback;
    }

    string normalized = rawBaseUrl.Trim();
    normalized = normalized
        .Replace("pcw�chter", "pcwächter", StringComparison.OrdinalIgnoreCase)
        .Replace("pcwÃ¤chter", "pcwächter", StringComparison.OrdinalIgnoreCase);

    if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        normalized = "https://" + normalized;
    }

    try
    {
        var builder = new UriBuilder(normalized);
        var idn = new IdnMapping();
        builder.Host = idn.GetAscii(builder.Host);
        return builder.Uri.ToString().TrimEnd('/');
    }
    catch
    {
        return fallback;
    }
}
