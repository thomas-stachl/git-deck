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

        // Probe git on every open — the window is a hidden singleton, so a Loaded handler on the
        // view would only ever run once, and an async void one at that.
        Opened += (_, _) =>
        {
            if (DataContext is SettingsViewModel settings)
            {
                _ = settings.CheckGitAvailabilityCommand.ExecuteAsync(null);
            }
        };
    }
}
