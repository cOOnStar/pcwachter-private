using System.Text.Json;
using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Runtime;

internal sealed class JsonStateStore : IStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceStateDto> LoadAsync(CancellationToken cancellationToken)
    {
        RuntimePaths.EnsureDirectories();

        if (!File.Exists(RuntimePaths.ServiceStatePath))
        {
            return new ServiceStateDto();
        }

        try
        {
            await using var stream = new FileStream(RuntimePaths.ServiceStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var loaded = await JsonSerializer.DeserializeAsync<ServiceStateDto>(stream, SerializerOptions, cancellationToken);
            if (loaded is null)
            {
                return new ServiceStateDto();
            }

            loaded.Findings ??= new Dictionary<string, FindingStateDto>(StringComparer.OrdinalIgnoreCase);
            loaded.History ??= new Dictionary<string, FindingHistoryRecordDto>(StringComparer.OrdinalIgnoreCase);
            loaded.ResolvedRecentlyQueue ??= new List<ResolvedFindingCacheItemDto>();
            loaded.RuleThresholds ??= new RuleThresholdsDto();
            loaded.RuleThresholds.Normalize();
            loaded.Timeline ??= new List<TimelineEventDto>();
            loaded.ActionExecutions ??= new List<ActionExecutionRecordDto>();
            loaded.StartupUndoEntries ??= new Dictionary<string, StartupUndoEntryDto>(StringComparer.OrdinalIgnoreCase);
            loaded.PerformanceSpikes ??= new List<PerformanceSpikeRecordDto>();
            loaded.DailyHealthSnapshots ??= new List<HealthScoreDailySnapshotDto>();
            loaded.Capabilities ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (loaded.Version <= 0)
            {
                loaded.Version = 1;
            }
            if (loaded.SchemaVersion <= 0)
            {
                loaded.SchemaVersion = 3;
            }

            return loaded;
        }
        catch
        {
            // Keep service resilient on malformed state files.
            return new ServiceStateDto();
        }
    }

    public async Task SaveAsync(ServiceStateDto state, CancellationToken cancellationToken)
    {
        RuntimePaths.EnsureDirectories();
        state.Findings ??= new Dictionary<string, FindingStateDto>(StringComparer.OrdinalIgnoreCase);
        state.History ??= new Dictionary<string, FindingHistoryRecordDto>(StringComparer.OrdinalIgnoreCase);
        state.ResolvedRecentlyQueue ??= new List<ResolvedFindingCacheItemDto>();
        state.RuleThresholds ??= new RuleThresholdsDto();
        state.RuleThresholds.Normalize();
        state.Timeline ??= new List<TimelineEventDto>();
        state.ActionExecutions ??= new List<ActionExecutionRecordDto>();
        state.StartupUndoEntries ??= new Dictionary<string, StartupUndoEntryDto>(StringComparer.OrdinalIgnoreCase);
        state.PerformanceSpikes ??= new List<PerformanceSpikeRecordDto>();
        state.DailyHealthSnapshots ??= new List<HealthScoreDailySnapshotDto>();
        state.Capabilities ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (state.Version <= 0)
        {
            state.Version = 1;
        }
        if (state.SchemaVersion <= 0)
        {
            state.SchemaVersion = 3;
        }

        string tempPath = RuntimePaths.ServiceStatePath + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        }

        if (File.Exists(RuntimePaths.ServiceStatePath))
        {
            File.Replace(tempPath, RuntimePaths.ServiceStatePath, null);
        }
        else
        {
            File.Move(tempPath, RuntimePaths.ServiceStatePath);
        }
    }
}
