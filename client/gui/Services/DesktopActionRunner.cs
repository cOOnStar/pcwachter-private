using System.Diagnostics;
using System.Windows;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Services;

public sealed class DesktopActionRunner
{
    private readonly IpcClientService _ipc;

    public DesktopActionRunner(IpcClientService ipc)
    {
        _ipc = ipc;
    }

    public async Task RunBestFixAsync(FindingDto finding)
    {
        ActionDto? action = finding.Actions
            .OrderByDescending(a => a.Kind == ActionKind.RunRemediation)
            .ThenBy(a => a.Kind)
            .FirstOrDefault();

        if (action is null)
        {
            MessageBox.Show("Keine Aktion verfügbar.", "PCWächter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ExecuteActionAsync(finding, action);
    }

    public async Task ExecuteActionAsync(FindingDto finding, ActionDto action)
    {
        if (action.Kind == ActionKind.RunRemediation && finding.Severity == FindingSeverity.Critical)
        {
            string guardText =
                "Du startest eine kritische Behebung. " +
                "Empfohlen: offene Dateien speichern und einen Wiederherstellungspunkt zulassen. " +
                "Fortfahren?";
            MessageBoxResult guardResult = MessageBox.Show(
                guardText,
                "Kritische Behebung absichern",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (guardResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(action.ConfirmText))
        {
            var result = MessageBox.Show(action.ConfirmText, "Bestätigung", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        switch (action.Kind)
        {
            case ActionKind.RunRemediation:
            {
                ActionExecutionResultDto result = await _ipc.RunActionAsync(action.ActionId, finding.FindingId);
                MessageBox.Show(BuildActionResultMessage(result), "Service Action", MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                await _ipc.TriggerScanAsync();
                break;
            }

            case ActionKind.OpenExternal:
            {
                if (string.IsNullOrWhiteSpace(action.ExternalTarget))
                {
                    MessageBox.Show("Kein externes Ziel konfiguriert.", "PCWächter", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                OpenExternal(action.ExternalTarget);
                break;
            }

            case ActionKind.OpenDetails:
            {
                string text = !string.IsNullOrWhiteSpace(action.DetailsMarkdown)
                    ? action.DetailsMarkdown
                    : (!string.IsNullOrWhiteSpace(finding.DetailsMarkdown) ? finding.DetailsMarkdown : finding.Summary);

                MessageBox.Show(text, finding.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                break;
            }
        }
    }

    public Task OpenDetailsAsync(FindingDto finding)
    {
        string text = !string.IsNullOrWhiteSpace(finding.DetailsMarkdown) ? finding.DetailsMarkdown : finding.Summary;
        MessageBox.Show(text, finding.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task SnoozeAsync(FindingDto finding, int days = 7)
    {
        return _ipc.SnoozeFindingAsync(finding.FindingId, days);
    }

    public Task IgnoreAsync(FindingDto finding)
    {
        return _ipc.IgnoreFindingAsync(finding.FindingId, true);
    }

    public static void OpenExternal(string target)
    {
        string trimmed = target.Trim();
        ProcessStartInfo psi;

        if (trimmed.Contains(' ') && !trimmed.Contains("://", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == false)
        {
            int firstSpace = trimmed.IndexOf(' ');
            string fileName = trimmed[..firstSpace];
            string args = trimmed[(firstSpace + 1)..];
            psi = new ProcessStartInfo(fileName, args)
            {
                UseShellExecute = true
            };
        }
        else
        {
            psi = new ProcessStartInfo(trimmed)
            {
                UseShellExecute = true
            };
        }

        Process.Start(psi);
    }

    private static string BuildActionResultMessage(ActionExecutionResultDto result)
    {
        var lines = new List<string>
        {
            result.Message
        };

        if (result.RestorePointAttempted)
        {
            lines.Add(result.RestorePointCreated
                ? "Restore-Point wurde vor der Aktion erstellt."
                : "Restore-Point konnte vor der Aktion nicht erstellt werden.");
        }

        if (!string.IsNullOrWhiteSpace(result.RestorePointDescription))
        {
            lines.Add($"Restore-Point: {result.RestorePointDescription}");
        }

        if (result.RollbackAvailable)
        {
            lines.Add("Rollback ist in der Historie verfügbar.");
        }
        else if (!string.IsNullOrWhiteSpace(result.RollbackHint))
        {
            lines.Add(result.RollbackHint);
        }

        return string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}

