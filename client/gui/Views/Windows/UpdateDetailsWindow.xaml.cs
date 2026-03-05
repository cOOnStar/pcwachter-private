using System.Windows;

namespace PCWachter.Desktop.Views.Windows;

public sealed class UpdateDetailsDialogModel
{
    public string Title { get; init; } = "Update";
    public string SourceLabel { get; init; } = "-";
    public string SeverityLabel { get; init; } = "-";
    public string RestartHint { get; init; } = "-";
    public string Summary { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
}

public partial class UpdateDetailsWindow : Window
{
    public UpdateDetailsWindow(UpdateDetailsDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
