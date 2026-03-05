using PCWachter.Contracts;

namespace PCWachter.Desktop.ViewModels;

public sealed class FindingCardViewModel : ObservableObject
{
    public FindingCardViewModel(
        FindingDto finding,
        Func<FindingDto, Task> openDetails,
        Func<FindingDto, Task> runBestFix,
        Func<FindingDto, int, Task> snoozeForDays,
        Func<FindingDto, Task> ignore)
    {
        Finding = finding;
        OpenDetailsCommand = new AsyncRelayCommand(() => openDetails(Finding));
        RunBestFixCommand = new AsyncRelayCommand(() => runBestFix(Finding));
        SnoozeCommand = new AsyncRelayCommand(() => snoozeForDays(Finding, 7));
        SnoozeOneDayCommand = new AsyncRelayCommand(() => snoozeForDays(Finding, 1));
        IgnoreForSevenDaysCommand = new AsyncRelayCommand(() => snoozeForDays(Finding, 7));
        IgnoreCommand = new AsyncRelayCommand(() => ignore(Finding));
    }

    public FindingDto Finding { get; }

    public string FindingId => Finding.FindingId;
    public string Title => Finding.Title;
    public string Summary => Finding.Summary;
    public FindingSeverity Severity => Finding.Severity;
    public string SeverityText => Finding.Severity.ToString();
    public int Priority => Finding.Priority;
    public string PriorityText => $"Priorität {Finding.Priority}";
    public string BadgeText => BuildBadgeText(Finding);
    public string TimelineText => BuildTimelineText(Finding);
    public string CriticalityHint => BuildCriticalityHint(Finding);
    public string WhatIsThisText => BuildWhatIsThisText(Finding);
    public string WhyImportantText => BuildWhyImportantText(Finding);
    public string RecommendedActionText => BuildRecommendedActionText(Finding);
    public string RiskEffortText => BuildRiskEffortText(Finding);
    public bool RequiresRestart => HasRestartHint(Finding);
    public string RestartHintText => RequiresRestart ? "Neustart möglich" : "Kein Neustart erwartet";
    public string UpdateGroupLabel => BuildUpdateGroupLabel(Finding);
    public string ReleaseNotesText => BuildReleaseNotesText(Finding);

    public AsyncRelayCommand OpenDetailsCommand { get; }
    public AsyncRelayCommand RunBestFixCommand { get; }
    public AsyncRelayCommand SnoozeCommand { get; }
    public AsyncRelayCommand SnoozeOneDayCommand { get; }
    public AsyncRelayCommand IgnoreForSevenDaysCommand { get; }
    public AsyncRelayCommand IgnoreCommand { get; }

    private static string BuildBadgeText(FindingDto finding)
    {
        var badges = new List<string>();
        if (finding.State.IsNew)
        {
            badges.Add("NEW");
        }

        if (finding.State.IsResolvedRecently)
        {
            badges.Add("Gerade behoben");
        }

        if (finding.State.ActiveDays > 0)
        {
            badges.Add($"Seit {finding.State.ActiveDays} Tag(en)");
        }

        if (finding.State.IsIgnored)
        {
            badges.Add("Ignoriert");
        }

        if (finding.State.SnoozedUntilUtc.HasValue)
        {
            badges.Add($"Snooze bis {finding.State.SnoozedUntilUtc.Value.ToLocalTime():g}");
        }

        return string.Join(" | ", badges);
    }

    private static string BuildTimelineText(FindingDto finding)
    {
        if (finding.State.IsResolvedRecently)
        {
            return $"Behoben: {finding.State.ResolvedAtUtc?.ToLocalTime():g}";
        }

        if (finding.State.LastSeenUtc.HasValue)
        {
            return $"Zuletzt gesehen: {finding.State.LastSeenUtc.Value.ToLocalTime():g}";
        }

        return $"Erkannt: {finding.DetectedAtUtc.ToLocalTime():g}";
    }

    private static string BuildCriticalityHint(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.DetailsMarkdown))
        {
            string details = finding.DetailsMarkdown;
            string[] lines = details
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string? reasonLine = lines.FirstOrDefault(x =>
                x.StartsWith("Warum kritisch?", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Warum wichtig?", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Einordnung:", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(reasonLine))
            {
                return reasonLine;
            }
        }

        return finding.Severity switch
        {
            FindingSeverity.Critical => "Warum kritisch? Das Problem kann Schutz oder Stabilität direkt beeinträchtigen.",
            FindingSeverity.Warning => "Warum wichtig? Das Problem kann die Systemqualitaet mittelfristig verschlechtern.",
            _ => "Einordnung: Hinweis zur Beobachtung und Optimierung."
        };
    }

    private static string BuildWhatIsThisText(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.WhatIsThis))
        {
            return finding.WhatIsThis;
        }

        return !string.IsNullOrWhiteSpace(finding.Summary)
            ? finding.Summary
            : "Keine Zusatzbeschreibung verfügbar.";
    }

    private static string BuildWhyImportantText(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.WhyImportant))
        {
            return finding.WhyImportant;
        }

        return finding.Severity switch
        {
            FindingSeverity.Critical => "Das Problem kann Schutz oder Stabilität direkt beeinträchtigen.",
            FindingSeverity.Warning => "Das Problem sollte zeitnah behoben werden.",
            _ => "Hinweis zur Beobachtung und Optimierung."
        };
    }

    private static string BuildRecommendedActionText(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.RecommendedAction))
        {
            return finding.RecommendedAction;
        }

        ActionDto? remediation = finding.Actions.FirstOrDefault(a => a.Kind == ActionKind.RunRemediation);
        if (remediation is not null)
        {
            return remediation.Label;
        }

        ActionDto? first = finding.Actions.FirstOrDefault();
        return first?.Label ?? "Manuell prüfen";
    }

    private static string BuildRiskEffortText(FindingDto finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.RiskEffort))
        {
            return finding.RiskEffort;
        }

        bool hasAdmin = finding.Actions.Any(a => a.RequiresAdmin);
        bool hasRestart = finding.Actions.Any(a => a.MayRequireRestart);

        if (hasAdmin && hasRestart)
        {
            return "Admin nötig, Neustart möglich.";
        }

        if (hasAdmin)
        {
            return "Admin nötig.";
        }

        if (hasRestart)
        {
            return "Neustart möglich.";
        }

        return "Geringes Risiko.";
    }

    private static bool HasRestartHint(FindingDto finding)
    {
        if (finding.Actions.Any(a => a.MayRequireRestart))
        {
            return true;
        }

        string[] restartEvidenceKeys =
        [
            "restart_required",
            "requires_restart",
            "may_require_restart",
            "reboot_required",
            "requires_reboot",
            "needs_restart"
        ];

        foreach (string key in restartEvidenceKeys)
        {
            if (finding.Evidence.TryGetValue(key, out string? raw) && IsTrueFlag(raw))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrueFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string value = raw.Trim().ToLowerInvariant();
        return value is "1" or "true" or "yes" or "ja" or "required" or "possible" ||
               value.Contains("neustart", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("reboot", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildUpdateGroupLabel(FindingDto finding)
    {
        if (finding.FindingId.StartsWith("updates.security", StringComparison.OrdinalIgnoreCase))
        {
            return "Sicherheit";
        }

        if (finding.FindingId.StartsWith("updates.drivers", StringComparison.OrdinalIgnoreCase))
        {
            return "Treiber";
        }

        if (finding.FindingId.StartsWith("updates.optional", StringComparison.OrdinalIgnoreCase))
        {
            return "Optional";
        }

        if (finding.FindingId.StartsWith("apps.outdated", StringComparison.OrdinalIgnoreCase))
        {
            return "Software";
        }

        return "Update";
    }

    private static string BuildReleaseNotesText(FindingDto finding)
    {
        string[] preferredKeys =
        [
            "release_notes",
            "release_notes_url",
            "changelog",
            "kb_article",
            "kb_url"
        ];

        foreach (string key in preferredKeys)
        {
            if (finding.Evidence.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(finding.DetailsMarkdown))
        {
            return finding.DetailsMarkdown;
        }

        return "Keine Release Notes verfügbar.";
    }
}


