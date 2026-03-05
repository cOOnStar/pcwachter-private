using PCWachter.Contracts;

namespace AgentService.Runtime;

internal static class PriorityCalculator
{
    public static int Calculate(FindingDto finding, int activeDays, bool evidenceOrSeverityChanged)
    {
        int score = finding.Severity switch
        {
            FindingSeverity.Critical => 70,
            FindingSeverity.Warning => 40,
            _ => 15
        };

        score += Math.Min(activeDays * 2, 10);

        bool requiresAdmin = finding.Actions.Any(a => a.Kind == ActionKind.RunRemediation);
        if (requiresAdmin && finding.Severity >= FindingSeverity.Warning)
        {
            score += 5;
        }

        if (finding.Category == FindingCategory.Security && finding.Severity >= FindingSeverity.Warning)
        {
            score += 10;
        }

        if (IsOptionalInfoFinding(finding))
        {
            score -= 10;
        }

        if (evidenceOrSeverityChanged)
        {
            score += 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static bool IsOptionalInfoFinding(FindingDto finding)
    {
        if (finding.Severity != FindingSeverity.Info)
        {
            return false;
        }

        return finding.FindingId.Contains("optional", StringComparison.OrdinalIgnoreCase)
               || finding.FindingId.Contains("info", StringComparison.OrdinalIgnoreCase)
               || finding.Summary.Contains("optional", StringComparison.OrdinalIgnoreCase)
               || finding.Title.Contains("optional", StringComparison.OrdinalIgnoreCase);
    }
}
