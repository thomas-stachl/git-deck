using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel(
    RunWindowService runWindowService,
    SettingsService settingsService,
    FolderPickerService folderPickerService) : ObservableObject
{

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Hello from GitDeck.App!";

    [ObservableProperty]
    public partial string? RepositoryPath { get; set; } = settingsService.Settings.RepositoryPath;

    [RelayCommand]
    private void ToggleRunWindow()
    {
        runWindowService.Toggle();
    }

    [RelayCommand]
    private async Task BrowseRepositoryPathAsync()
    {
        var path = await folderPickerService.PickFolderAsync("Select Repository Folder");
        if (path is not null)
        {
            RepositoryPath = path;
        }
    }

    partial void OnRepositoryPathChanged(string? value)
    {
        settingsService.Settings.RepositoryPath = value;
        settingsService.Save();
    }
}
