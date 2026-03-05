using PCWachter.Contracts;
using PCWachter.Core;

namespace AgentService.Rules;

internal static class RuleHelpers
{
    public static bool TryGetPayload<T>(
        IReadOnlyDictionary<string, SensorResult> sensorResults,
        string sensorId,
        out T? payload,
        out string? error)
        where T : class
    {
        payload = null;
        error = null;

        if (!sensorResults.TryGetValue(sensorId, out SensorResult? result))
        {
            error = "missing sensor result";
            return false;
        }

        if (!result.Success)
        {
            error = result.Error ?? "sensor failed";
            return false;
        }

        payload = result.Payload as T;
        if (payload is null)
        {
            error = "unexpected payload type";
            return false;
        }

        return true;
    }

    public static FindingDto SensorFailureFinding(string findingId, string ruleId, string title, string summary)
    {
        return new FindingDto
        {
            FindingId = findingId,
            RuleId = ruleId,
            Category = FindingCategory.Health,
            Severity = FindingSeverity.Info,
            Title = title,
            Summary = summary,
            DetectedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static string SeverityLabel(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "Kritisch",
            FindingSeverity.Warning => "Warnung",
            _ => "Info"
        };
    }
}
