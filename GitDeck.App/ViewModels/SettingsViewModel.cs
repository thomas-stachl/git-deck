using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.Core;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Hello from GitDeck.App!";
}
