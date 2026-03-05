using PCWachter.Contracts;

namespace AgentService.Runtime;

internal static class HealthScoreCalculator
{
    public static int Calculate(IReadOnlyCollection<FindingDto> activeFindings)
    {
        int score = 100;

        foreach (FindingDto finding in activeFindings)
        {
            score -= finding.Severity switch
            {
                FindingSeverity.Critical => 20,
                FindingSeverity.Warning => 8,
                _ => 3
            };
        }

        bool hasSecurityCritical = activeFindings.Any(f => f.Category == FindingCategory.Security && f.Severity == FindingSeverity.Critical);
        if (hasSecurityCritical)
        {
            score -= 10;
        }

        bool hasPendingReboot = activeFindings.Any(f => f.FindingId.Equals("system.reboot.pending", StringComparison.OrdinalIgnoreCase));
        if (hasPendingReboot)
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }
}
