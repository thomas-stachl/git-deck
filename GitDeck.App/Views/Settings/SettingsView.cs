using Avalonia.Controls;
using Avalonia.Interactivity;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.CheckGitAvailabilityCommand.ExecuteAsync(null);
        }
    }
}