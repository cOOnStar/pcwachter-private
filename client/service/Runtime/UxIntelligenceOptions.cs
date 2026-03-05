namespace AgentService.Runtime;

internal sealed class UxIntelligenceOptions
{
    public int ResolvedRecentlyHours { get; set; } = 24;
    public int NewFindingHours { get; set; } = 24;
    public int TopFindingsCount { get; set; } = 3;
    public int SensorTimeoutSeconds { get; set; } = 8;
    public int SlowSensorWarningMs { get; set; } = 1500;
}
