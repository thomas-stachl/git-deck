using Avalonia.Controls;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        
        DataContext = viewModel;

        InitializeComponent();
    }
}