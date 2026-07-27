using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Services;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel(RunWindowService runWindowService) : ObservableObject
{

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Hello from GitDeck.App!";

    [RelayCommand]
    private void ToggleRunWindow()
    {
        runWindowService.Toggle();
    }
}
