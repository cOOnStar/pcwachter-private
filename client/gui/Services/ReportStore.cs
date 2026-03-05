using System.Text.Json;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

public sealed class ReportStore
{
    private readonly object _sync = new();
    private ScanReportDto _currentReport = new();

    public ScanReportDto CurrentReport
    {
        get
        {
            lock (_sync)
            {
                return _currentReport;
            }
        }
    }

    public event EventHandler<ScanReportDto>? ReportUpdated;

    public void Update(ScanReportDto report)
    {
        lock (_sync)
        {
            _currentReport = report;
        }

        ReportUpdated?.Invoke(this, report);
    }

    public static string ComputeReportIdentity(ScanReportDto report)
    {
        return $"{report.GeneratedAtUtc:O}|{report.HealthScore}|{report.Findings.Count}|{report.RecentlyResolved.Count}";
    }

    public static ScanReportDto Clone(ScanReportDto report)
    {
        string json = JsonSerializer.Serialize(report);
        return JsonSerializer.Deserialize<ScanReportDto>(json) ?? new ScanReportDto();
    }
}
