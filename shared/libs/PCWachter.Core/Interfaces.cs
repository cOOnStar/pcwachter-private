using PCWachter.Contracts;

namespace PCWachter.Core;

public sealed class SensorResult
{
    public string SensorId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? Payload { get; set; }
}

public sealed class RuleContext
{
    public DateTimeOffset NowUtc { get; set; } = DateTimeOffset.UtcNow;
    public string MachineName { get; set; } = Environment.MachineName;
    public DeviceContextDto DeviceContext { get; set; } = new();
    public RuleThresholdsDto Thresholds { get; set; } = new();
}

public sealed class RemediationRequest
{
    public bool SimulationMode { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string? FindingId { get; set; }
    public string? ExternalTarget { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RemediationResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface ISensor
{
    string SensorId { get; }
    Task<SensorResult> CollectAsync(CancellationToken cancellationToken);
}

public interface IRule
{
    string RuleId { get; }
    IReadOnlyCollection<FindingDto> Evaluate(IReadOnlyDictionary<string, SensorResult> sensorResults, RuleContext context);
}

public interface IRemediation
{
    string RemediationId { get; }
    Task<RemediationResult> ExecuteAsync(RemediationRequest request, IProgress<ActionProgressDto>? progress, CancellationToken cancellationToken);
}

public interface IStateStore
{
    Task<ServiceStateDto> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ServiceStateDto state, CancellationToken cancellationToken);
}
