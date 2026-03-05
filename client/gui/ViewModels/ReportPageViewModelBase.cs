using System.Collections.ObjectModel;
using PCWachter.Contracts;
using PCWachter.Desktop.Services;

namespace PCWachter.Desktop.ViewModels;

public abstract class ReportPageViewModelBase : PageViewModelBase
{
    protected readonly ReportStore ReportStore;
    protected readonly IpcClientService IpcClient;
    protected readonly DesktopActionRunner ActionRunner;

    protected ReportPageViewModelBase(string title, ReportStore reportStore, IpcClientService ipcClient, DesktopActionRunner actionRunner)
        : base(title)
    {
        ReportStore = reportStore;
        IpcClient = ipcClient;
        ActionRunner = actionRunner;

        ReportStore.ReportUpdated += (_, report) => OnReportUpdated(report);
    }

    protected ObservableCollection<FindingCardViewModel> BuildCards(IEnumerable<FindingDto> findings)
    {
        return new ObservableCollection<FindingCardViewModel>(findings.Select(f =>
            new FindingCardViewModel(
                f,
                ActionRunner.OpenDetailsAsync,
                ActionRunner.RunBestFixAsync,
                ActionRunner.SnoozeAsync,
                ActionRunner.IgnoreAsync)));
    }

    protected abstract void OnReportUpdated(ScanReportDto report);
}
