namespace AgentService.Runtime;

internal static class RuntimePaths
{
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PCWaechter");

    public static string AgentDirectory => Path.Combine(DataRoot, "agent");
    public static string ServiceDirectory => Path.Combine(DataRoot, "service");

    public static string SnapshotPath => Path.Combine(AgentDirectory, "security-status.json");
    public static string ScanReportPath => Path.Combine(AgentDirectory, "scan-report.json");
    public static string ServiceStatePath => Path.Combine(ServiceDirectory, "state.json");
    public static string InstallIdPath => Path.Combine(DataRoot, "device_install_id.txt");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AgentDirectory);
        Directory.CreateDirectory(ServiceDirectory);
    }
}
