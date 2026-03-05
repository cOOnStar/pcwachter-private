namespace PCWachter.Desktop.ViewModels;

public sealed class RemediationQueueItemViewModel : ObservableObject
{
    private string _message = string.Empty;
    private int _percent;
    private bool _isRunning;
    private string _statusText = "Wartend";
    private string _statusLevel = "info";
    private DateTimeOffset _updatedAtLocal = DateTimeOffset.Now;

    public RemediationQueueItemViewModel(string actionId)
    {
        ActionId = actionId;
        StartedAtLocal = DateTimeOffset.Now;
        _updatedAtLocal = StartedAtLocal;
        _isRunning = true;
    }

    public string ActionId { get; }
    public DateTimeOffset StartedAtLocal { get; }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public int Percent
    {
        get => _percent;
        set => SetProperty(ref _percent, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string StatusLevel
    {
        get => _statusLevel;
        set => SetProperty(ref _statusLevel, value);
    }

    public DateTimeOffset UpdatedAtLocal
    {
        get => _updatedAtLocal;
        set
        {
            if (SetProperty(ref _updatedAtLocal, value))
            {
                RaisePropertyChanged(nameof(UpdatedAtText));
            }
        }
    }

    public string UpdatedAtText => UpdatedAtLocal.ToString("HH:mm:ss");
}
