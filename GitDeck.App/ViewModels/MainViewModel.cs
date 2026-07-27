using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.Core;

namespace GitDeck.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    
    [ObservableProperty]
    public partial string Greeting { get; set; }
}
