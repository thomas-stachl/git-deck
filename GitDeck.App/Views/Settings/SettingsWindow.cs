using Avalonia.Controls;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Settings;

public partial class SettingsWindow : Window
{
    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public SettingsWindow() : this(null)
    {
    }

    public SettingsWindow(SettingsViewModel? viewModel)
    {
        DataContext = viewModel;

        InitializeComponent();
    }
}