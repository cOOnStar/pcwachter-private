using System.Windows.Input;

namespace PCWachter.Desktop.ViewModels;

public sealed class NotificationItemViewModel : ObservableObject
{
    private bool _isRead;

    public NotificationItemViewModel(
        string level,
        string title,
        string message,
        DateTimeOffset timestampLocal,
        string? actionLabel = null,
        ICommand? actionCommand = null)
    {
        Level = level;
        Title = title;
        Message = message;
        TimestampLocal = timestampLocal;
        ActionLabel = actionLabel;
        ActionCommand = actionCommand;
    }

    public string Level { get; }
    public string Title { get; }
    public string Message { get; }
    public DateTimeOffset TimestampLocal { get; }
    public string TimeText => TimestampLocal.ToString("HH:mm");
    public string DateText => TimestampLocal.ToString("dd.MM.yyyy");
    public string GroupLabel => BuildGroupLabel(TimestampLocal);
    public string? ActionLabel { get; }
    public ICommand? ActionCommand { get; }
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel) && ActionCommand is not null;
    public bool IsImportant =>
        string.Equals(Level, "critical", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Level, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsRead
    {
        get => _isRead;
        set => SetProperty(ref _isRead, value);
    }

    private static string BuildGroupLabel(DateTimeOffset timestampLocal)
    {
        DateOnly date = DateOnly.FromDateTime(timestampLocal.LocalDateTime);
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        if (date == today)
        {
            return "Heute";
        }

        if (date == today.AddDays(-1))
        {
            return "Gestern";
        }

        return "Aelter";
    }
}
