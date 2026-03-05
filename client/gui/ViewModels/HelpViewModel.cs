using System.IO;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public sealed class HelpViewModel : PageViewModelBase
{
    private readonly IpcClientService _ipcClient;
    private string _serviceCheckStatus = "Noch kein Check ausgeführt.";
    private string _lastCheckText = "Noch nicht geprüft";
    private bool _serviceCheckIsError;

    public HelpViewModel(IpcClientService ipcClient)
        : base("Hilfe")
    {
        _ipcClient = ipcClient;
        OpenLogsCommand = new RelayCommand(OpenLogs);
        CheckServiceStatusCommand = new AsyncRelayCommand(CheckServiceStatusAsync);
        OpenDesktopReadmeCommand = new RelayCommand(OpenDesktopReadme);
    }

    public string VersionText => $"Desktop Version: {typeof(HelpViewModel).Assembly.GetName().Version}";

    public string ServiceCheckStatus
    {
        get => _serviceCheckStatus;
        private set => SetProperty(ref _serviceCheckStatus, value);
    }

    public string LastCheckText
    {
        get => _lastCheckText;
        private set => SetProperty(ref _lastCheckText, value);
    }

    public bool ServiceCheckIsError
    {
        get => _serviceCheckIsError;
        private set => SetProperty(ref _serviceCheckIsError, value);
    }

    public string ServiceStateLabel => ServiceCheckIsError ? "Fehler" : "Bereit";

    public RelayCommand OpenLogsCommand { get; }
    public AsyncRelayCommand CheckServiceStatusCommand { get; }
    public RelayCommand OpenDesktopReadmeCommand { get; }

    private static void OpenLogs()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PCWaechter");
        DesktopActionRunner.OpenExternal($"explorer.exe \"{path}\"");
    }

    private static void OpenDesktopReadme()
    {
        string readmePath = Path.Combine(Environment.CurrentDirectory, "README.md");
        if (File.Exists(readmePath))
        {
            DesktopActionRunner.OpenExternal($"explorer.exe \"{readmePath}\"");
            return;
        }

        System.Windows.MessageBox.Show("README nicht gefunden.", "Hilfe");
    }

    private async Task CheckServiceStatusAsync()
    {
        try
        {
            ServiceCheckStatus = "Service prüfen...";
            ServiceCheckIsError = false;

            bool connected = await _ipcClient.ConnectAsync();
            if (!connected)
            {
                ServiceCheckStatus = "Service nicht erreichbar.";
                ServiceCheckIsError = true;
                LastCheckText = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                RaisePropertyChanged(nameof(ServiceStateLabel));
                return;
            }

            await _ipcClient.TriggerScanAsync();
            ServiceCheckStatus = "Service erreichbar und Scan gestartet.";
            ServiceCheckIsError = false;
        }
        catch
        {
            ServiceCheckStatus = "Service-Check fehlgeschlagen.";
            ServiceCheckIsError = true;
        }
        finally
        {
            LastCheckText = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            RaisePropertyChanged(nameof(ServiceStateLabel));
        }
    }
}


