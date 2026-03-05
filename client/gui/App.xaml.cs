using System.Windows;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop;

public partial class App : Application
{
    private readonly AppUiStateStore _uiStateStore = new();
    private readonly UpdaterIntegrationService _updaterService = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppUiState state = _uiStateStore.Load();
        await TryInstallPendingUpdateBeforeStartupAsync(state);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private async Task TryInstallPendingUpdateBeforeStartupAsync(AppUiState state)
    {
        if (!state.IsAutomaticUpdatesEnabled || string.IsNullOrWhiteSpace(state.PendingUpdateVersion))
        {
            return;
        }

        string pendingVersion = NormalizeVersion(state.PendingUpdateVersion);
        string currentVersion = NormalizeVersion(typeof(App).Assembly.GetName().Version?.ToString(4));
        if (IsCurrentVersionUpToDate(currentVersion, pendingVersion))
        {
            state.PendingUpdateVersion = null;
            state.PendingUpdateDetectedAtUtc = null;
            _uiStateStore.Save(state);
            return;
        }

        UpdaterExecutionResult result = await _updaterService.RunUpdateAndWaitAsync(CancellationToken.None);
        if (result.IsSuccessfulExit)
        {
            state.PendingUpdateVersion = null;
            state.PendingUpdateDetectedAtUtc = null;
            _uiStateStore.Save(state);
        }
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? string.Empty
            : version.Trim().TrimStart('v', 'V');
    }

    private static bool IsCurrentVersionUpToDate(string currentVersion, string pendingVersion)
    {
        if (Version.TryParse(currentVersion, out Version? current) &&
            Version.TryParse(pendingVersion, out Version? pending))
        {
            return current >= pending;
        }

        return string.Equals(currentVersion, pendingVersion, StringComparison.OrdinalIgnoreCase);
    }
}
