namespace PCWachter.Contracts;

public enum FindingSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum FindingCategory
{
    Security = 0,
    Storage = 1,
    System = 2,
    Health = 3
}

public enum ActionKind
{
    RunRemediation = 0,
    OpenExternal = 1,
    OpenDetails = 2
}

public enum DriveHealthState
{
    Good = 0,
    Warning = 1,
    Critical = 2,
    Unknown = 3
}

public sealed class ActionDto
{
    public string ActionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ActionKind Kind { get; set; }
    public string? RemediationId { get; set; }
    public string? ExternalTarget { get; set; }
    public string? DetailsMarkdown { get; set; }
    public string? ConfirmText { get; set; }
    public bool IsSafeForOneClickMaintenance { get; set; }
    public bool RequiresAdmin { get; set; }
    public bool MayRequireRestart { get; set; }
}

public sealed class FindingStateDto
{
    // Suppression flags
    public bool IsIgnored { get; set; }
    public DateTimeOffset? SnoozedUntilUtc { get; set; }

    // UX intelligence lifecycle state
    public bool IsNew { get; set; }
    public bool IsResolvedRecently { get; set; }
    public int ActiveDays { get; set; }
    public int ActiveStreakScans { get; set; }
    public DateTimeOffset? FirstSeenUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class FindingDto
{
    public string FindingId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public FindingCategory Category { get; set; }
    public FindingSeverity Severity { get; set; }
    public int Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DetailsMarkdown { get; set; }
    public string? WhatIsThis { get; set; }
    public string? WhyImportant { get; set; }
    public string? RecommendedAction { get; set; }
    public string? RiskEffort { get; set; }
    public Dictionary<string, string> Evidence { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ActionDto> Actions { get; set; } = new();
    public FindingStateDto State { get; set; } = new();
    public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DeviceContextDto
{
    public bool IsLaptop { get; set; }
    public bool IsDesktop { get; set; }
    public bool IsServer { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string OsVersion { get; set; } = Environment.OSVersion.VersionString;
    public int? MemoryGb { get; set; }
    public string? Cpu { get; set; }
}

public enum AutoFixMode
{
    Off = 0,
    RecommendOnly = 1,
    AutoSafe = 2
}

public sealed class AutoFixPolicyDto
{
    public AutoFixMode Mode { get; set; } = AutoFixMode.RecommendOnly;
    public bool RequireNetwork { get; set; } = true;
    public bool RequireAcPower { get; set; } = false;
    public int MaxFixesPerDay { get; set; } = 10;
    public int CooldownHours { get; set; } = 2;
    public int ScanIntervalMinutes { get; set; } = 30;
}

public sealed class RecommendedAutoFixDto
{
    public string FindingId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public sealed class AutoFixLogItemDto
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string FindingId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string? ActionExecutionId { get; set; }
    public bool RollbackAvailable { get; set; }
    public string? RollbackHint { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class RuleThresholdsDto
{
    public int StorageCriticalPercentFree { get; set; } = 5;
    public int StorageWarningPercentFree { get; set; } = 15;
    public int EventLogWarningCount24h { get; set; } = 10;
    public int DefenderSignatureWarningDays { get; set; } = 7;
    public int DefenderSignatureCriticalDays { get; set; } = 14;

    public void Normalize()
    {
        StorageCriticalPercentFree = Math.Clamp(StorageCriticalPercentFree, 1, 50);
        StorageWarningPercentFree = Math.Clamp(StorageWarningPercentFree, StorageCriticalPercentFree + 1, 80);
        EventLogWarningCount24h = Math.Clamp(EventLogWarningCount24h, 1, 500);
        DefenderSignatureWarningDays = Math.Clamp(DefenderSignatureWarningDays, 1, 90);
        DefenderSignatureCriticalDays = Math.Clamp(DefenderSignatureCriticalDays, DefenderSignatureWarningDays + 1, 180);
    }
}

public sealed class BaselineItemDto
{
    public string FindingId { get; set; } = string.Empty;
    public FindingSeverity Severity { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
}

public sealed class BaselineSnapshotDto
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Label { get; set; } = "Standard-Baseline";
    public List<BaselineItemDto> Findings { get; set; } = new();
}

public sealed class BaselineDriftSummaryDto
{
    public bool HasBaseline { get; set; }
    public DateTimeOffset? BaselineCreatedAtUtc { get; set; }
    public string BaselineLabel { get; set; } = string.Empty;
    public int NewFindings { get; set; }
    public int ChangedFindings { get; set; }
    public int ResolvedFindings { get; set; }
}

public sealed class TimelineEventDto
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Kind { get; set; } = "info";
    public string Level { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FindingId { get; set; }
    public string? ActionId { get; set; }
    public string? ActionExecutionId { get; set; }
}

public sealed class ActionExecutionRecordDto
{
    public string ActionExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string? FindingId { get; set; }
    public string? RemediationId { get; set; }
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public bool RestorePointAttempted { get; set; }
    public bool RestorePointCreated { get; set; }
    public string? RestorePointDescription { get; set; }

    public bool RollbackAvailable { get; set; }
    public string? RollbackPowerShellCommand { get; set; }
    public string? RollbackHint { get; set; }

    public bool RollbackExecuted { get; set; }
    public DateTimeOffset? RollbackAtUtc { get; set; }
    public bool? RollbackSuccess { get; set; }
    public string? RollbackMessage { get; set; }
}

public sealed class HealthScoreDailySnapshotDto
{
    public DateTime DateUtc { get; set; }
    public int HealthScore { get; set; }
    public int OpenFindings { get; set; }
    public int ResolvedFindings { get; set; }
}

public sealed class DriveStatusDto
{
    public string Name { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public DriveHealthState HealthState { get; set; } = DriveHealthState.Unknown;
    public string HealthBadgeText { get; set; } = "Unbekannt";
    public bool SmartAvailable { get; set; }
    public bool PredictFailure { get; set; }
    public int? TemperatureC { get; set; }
    public List<string> HealthDetails { get; set; } = new();
    public DateTimeOffset LastCheckedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PerformanceSpikeRecordDto
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Metric { get; set; } = "cpu";
    public double Value { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
}

public sealed class StartupUndoEntryDto
{
    public string ItemKey { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public DateTimeOffset DisabledAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FeatureConfigDto
{
    public RuleThresholdsDto RuleThresholds { get; set; } = new();
    public BaselineDriftSummaryDto Baseline { get; set; } = new();
}

public sealed class ScanReportDto
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string OverallStatus { get; set; } = "ok";
    public int HealthScore { get; set; } = 100;
    public DeviceContextDto DeviceContext { get; set; } = new();
    public List<FindingDto> Findings { get; set; } = new();
    public List<FindingDto> TopFindings { get; set; } = new();
    public List<FindingDto> RecentlyResolved { get; set; } = new();
    public AutoFixPolicyDto AutoFixPolicy { get; set; } = new();
    public List<RecommendedAutoFixDto> RecommendedAutoFixes { get; set; } = new();
    public List<AutoFixLogItemDto> RecentAutoFixLog { get; set; } = new();
    public List<TimelineEventDto> Timeline { get; set; } = new();
    public BaselineDriftSummaryDto BaselineDrift { get; set; } = new();
    public RuleThresholdsDto RuleThresholds { get; set; } = new();
    public Dictionary<string, string> SensorErrors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SecuritySignals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DriveStatusDto> Drives { get; set; } = new();
}

public sealed class FindingHistoryRecordDto
{
    public DateTimeOffset? FirstSeenUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public DateTimeOffset? LastChangedUtc { get; set; }
    public bool Active { get; set; }
    public FindingSeverity LastSeverity { get; set; }
    public int LastPriority { get; set; }
    public int ActiveStreakScans { get; set; }
    public string? LastEvidenceHash { get; set; }
}

public sealed class ResolvedFindingCacheItemDto
{
    public FindingDto Finding { get; set; } = new();
    public DateTimeOffset ResolvedAtUtc { get; set; }
}

public sealed class ServiceStateDto
{
    // State file version marker.
    public int Version { get; set; } = 1;

    public int SchemaVersion { get; set; } = 3;
    public bool SimulationMode { get; set; }
    public Dictionary<string, FindingStateDto> Findings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, FindingHistoryRecordDto> History { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ResolvedFindingCacheItemDto> ResolvedRecentlyQueue { get; set; } = new();
    public RuleThresholdsDto RuleThresholds { get; set; } = new();
    public BaselineSnapshotDto? Baseline { get; set; }
    public List<TimelineEventDto> Timeline { get; set; } = new();
    public List<ActionExecutionRecordDto> ActionExecutions { get; set; } = new();
    public Dictionary<string, StartupUndoEntryDto> StartupUndoEntries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PerformanceSpikeRecordDto> PerformanceSpikes { get; set; } = new();
    public List<HealthScoreDailySnapshotDto> DailyHealthSnapshots { get; set; } = new();
    public Dictionary<string, string> Capabilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ActionProgressDto
{
    public string ActionId { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ActionExecutionResultDto
{
    public string ActionExecutionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RestorePointAttempted { get; set; }
    public bool RestorePointCreated { get; set; }
    public string? RestorePointDescription { get; set; }
    public bool RollbackAvailable { get; set; }
    public string? RollbackHint { get; set; }
}

// ---------------------------------------------------------------------------
// License / Account
// ---------------------------------------------------------------------------

public sealed class LicenseStatusDto
{
    public bool Ok { get; set; }
    public string Plan { get; set; } = "none";
    public string PlanLabel { get; set; } = "Keine Lizenz";
    public string State { get; set; } = "none";   // active | grace | expired | revoked | none
    public DateTime? ExpiresAt { get; set; }
    public DateTime? GracePeriodUntil { get; set; }
    public int? DaysRemaining { get; set; }
    public int? MaxDevices { get; set; }
    public Dictionary<string, bool> Features { get; set; } = new();

    public bool IsActive => State is "active" or "grace";
    public bool HasFeature(string key) => Features.TryGetValue(key, out bool v) && v;
}

public sealed class KeycloakUserInfo
{
    public string Sub { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PreferredUsername { get; set; } = string.Empty;
}
