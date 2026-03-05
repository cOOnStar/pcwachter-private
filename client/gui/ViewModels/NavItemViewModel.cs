namespace PCWachter.Desktop.ViewModels;

public sealed class NavItemViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _hasAttention;
    private string _attentionLevel = "none";

    public NavItemViewModel(string key, string title, string iconGlyph)
    {
        Key = key;
        Title = title;
        IconGlyph = iconGlyph;
    }

    public string Key { get; }
    public string Title { get; }
    public string IconGlyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasAttention
    {
        get => _hasAttention;
        private set => SetProperty(ref _hasAttention, value);
    }

    public string AttentionLevel
    {
        get => _attentionLevel;
        set
        {
            string normalized = NormalizeLevel(value);
            if (!SetProperty(ref _attentionLevel, normalized))
            {
                return;
            }

            HasAttention = normalized is "warning" or "critical";
        }
    }

    private static string NormalizeLevel(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "critical" => "critical",
            "warning" => "warning",
            _ => "none"
        };
    }
}
