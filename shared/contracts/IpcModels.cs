namespace PCWachter.Contracts;

public static class IpcMessageTypes
{
    public const string GetReport = "get_report";
    public const string TriggerScan = "trigger_scan";
    public const string Subscribe = "subscribe";
    public const string ExecuteAction = "execute_action";
    public const string SetFindingState = "set_finding_state";
    public const string GetFeatureConfig = "get_feature_config";
    public const string SetFeatureConfig = "set_feature_config";
    public const string CreateBaseline = "create_baseline";
    public const string RollbackAction = "rollback_action";
    public const string RunSafeMaintenance = "run_safe_maintenance";
}

public sealed class IpcRequestDto
{
    public string Type { get; set; } = string.Empty;
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string? PayloadJson { get; set; }
}

public sealed class IpcResponseDto
{
    public string RequestId { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? PayloadJson { get; set; }
}

public sealed class ExecuteActionRequestDto
{
    public string ActionId { get; set; } = string.Empty;
    public bool SimulationMode { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public sealed class SetFindingStateRequestDto
{
    public string FindingId { get; set; } = string.Empty;
    public bool Ignore { get; set; }
    public DateTimeOffset? SnoozeUntilUtc { get; set; }
}

public sealed class SetFeatureConfigRequestDto
{
    public RuleThresholdsDto? RuleThresholds { get; set; }
}

public sealed class CreateBaselineRequestDto
{
    public string? Label { get; set; }
}

public sealed class CreateBaselineResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BaselineDriftSummaryDto Baseline { get; set; } = new();
}

public sealed class RollbackActionRequestDto
{
    public string ActionExecutionId { get; set; } = string.Empty;
}

public enum MaintenanceRiskLevel
{
    Safe = 0,
    AdminRequired = 1,
    RestartPossible = 2
}

public sealed class MaintenanceStepResultDto
{
    public string StepId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MaintenanceRiskLevel RiskLevel { get; set; } = MaintenanceRiskLevel.Safe;
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class MaintenanceRunResultDto
{
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool RestartRecommended { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<MaintenanceStepResultDto> Steps { get; set; } = new();
}
