using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCWachter.Desktop.ViewModels;

namespace PCWachter.Desktop.Views.Controls;

public partial class UpdateCardV2 : UserControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item),
        typeof(AppUpdateSelectionItemViewModel),
        typeof(UpdateCardV2),
        new PropertyMetadata(null));

    public static readonly DependencyProperty InstallCommandProperty = DependencyProperty.Register(
        nameof(InstallCommand),
        typeof(ICommand),
        typeof(UpdateCardV2),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleDetailsCommandProperty = DependencyProperty.Register(
        nameof(ToggleDetailsCommand),
        typeof(ICommand),
        typeof(UpdateCardV2),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ShowDetailsCommandProperty = DependencyProperty.Register(
        nameof(ShowDetailsCommand),
        typeof(ICommand),
        typeof(UpdateCardV2),
        new PropertyMetadata(null));

    public UpdateCardV2()
    {
        InitializeComponent();
    }

    public AppUpdateSelectionItemViewModel? Item
    {
        get => (AppUpdateSelectionItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public ICommand? InstallCommand
    {
        get => (ICommand?)GetValue(InstallCommandProperty);
        set => SetValue(InstallCommandProperty, value);
    }

    public ICommand? ToggleDetailsCommand
    {
        get => (ICommand?)GetValue(ToggleDetailsCommandProperty);
        set => SetValue(ToggleDetailsCommandProperty, value);
    }

    public ICommand? ShowDetailsCommand
    {
        get => (ICommand?)GetValue(ShowDetailsCommandProperty);
        set => SetValue(ShowDetailsCommandProperty, value);
    }
}
