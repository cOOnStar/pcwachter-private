namespace PCWachter.Desktop.ViewModels;

public abstract class PageViewModelBase : ObservableObject
{
    protected PageViewModelBase(string title)
    {
        Title = title;
    }

    public string Title { get; }
}
