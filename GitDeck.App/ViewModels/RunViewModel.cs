using CommunityToolkit.Mvvm.ComponentModel;

namespace GitDeck.App.ViewModels;

public partial class RunViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;
}
