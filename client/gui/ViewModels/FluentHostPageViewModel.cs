namespace PCWachter.Desktop.ViewModels;

public class FluentHostPageViewModel : ObservableObject
{
    public FluentHostPageViewModel(string key, string title, object hostedContent)
    {
        Key = key;
        Title = title;
        HostedContent = hostedContent;
    }

    public string Key { get; }
    public string Title { get; }
    public object HostedContent { get; }
}

public sealed class UpdatesFluentPageViewModel : FluentHostPageViewModel
{
    public UpdatesFluentPageViewModel(object hostedContent)
        : base("WindowsUpdatesFluent", "Updates (Fluent)", hostedContent)
    {
    }
}

public sealed class StorageFluentPageViewModel : FluentHostPageViewModel
{
    public StorageFluentPageViewModel(object hostedContent)
        : base("StorageFluent", "Speicher (Fluent)", hostedContent)
    {
    }
}
