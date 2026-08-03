using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IRunWindowService _runWindowService;
    private readonly ISettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private readonly IGitExecutableService _gitExecutableService;

    public SettingsViewModel(
        IRunWindowService runWindowService,
        ISettingsService settingsService,
        IFilePickerService filePickerService,
        IGitExecutableService gitExecutableService,
        IGlobalHotkeyService globalHotkeyService)
    {
        _runWindowService = runWindowService;
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _gitExecutableService = gitExecutableService;

        RepositoryPath = settingsService.Settings.RepositoryPath;
        GitExecutablePath = settingsService.Settings.GitExecutablePath;
        PublishNewBranchesToRemote = settingsService.Settings.PublishNewBranchesToRemote;

        // The hotkeys are registered at startup, so the editors start from what the hotkey service
        // actually holds rather than re-reading the settings.
        BranchHotkey = CreateEditor(globalHotkeyService, HotkeyAction.Branches, "Switch branches");
        CommitHotkey = CreateEditor(globalHotkeyService, HotkeyAction.Commit, "Commit changes");
    }

    // Parameterless constructor required by the Avalonia XAML previewer/designer;
    // wires up hand-written fakes instead of the real services.
    public SettingsViewModel() : this(
        new DesignRunWindowService(),
        new DesignSettingsService(),
        new DesignFilePickerService(),
        new DesignGitExecutableService(),
        new DesignGlobalHotkeyService())
    {
    }

    public HotkeyEditorViewModel BranchHotkey { get; }

    public HotkeyEditorViewModel CommitHotkey { get; }

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Hello from GitDeck.App!";

    [ObservableProperty]
    public partial string? RepositoryPath { get; set; }

    [ObservableProperty]
    public partial string? GitExecutablePath { get; set; }

    [ObservableProperty]
    public partial bool PublishNewBranchesToRemote { get; set; }

    [ObservableProperty]
    public partial string GitStatus { get; set; } = "Checking for Git...";

    [RelayCommand]
    private void OpenBranchPalette()
    {
        _runWindowService.Toggle(RunMode.Branches);
    }

    [RelayCommand]
    private void OpenCommitPalette()
    {
        _runWindowService.Toggle(RunMode.Commit);
    }

    [RelayCommand]
    private async Task BrowseRepositoryPathAsync()
    {
        var path = await _filePickerService.PickFolderAsync("Select Repository Folder");
        if (path is not null)
        {
            RepositoryPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseGitExecutablePathAsync()
    {
        var path = await _filePickerService.PickFileAsync("Select Git Executable");
        if (path is not null)
        {
            GitExecutablePath = path;
        }
    }

    [RelayCommand]
    private async Task CheckGitAvailabilityAsync()
    {
        GitStatus = "Checking for Git...";

        var availability = await _gitExecutableService.CheckAvailabilityAsync(GitExecutablePath);
        GitStatus = availability.IsAvailable
            ? $"Found: {availability.Version}"
            : "Git not found. Set the path below or install Git and ensure it's on PATH.";
    }

    private HotkeyEditorViewModel CreateEditor(IGlobalHotkeyService hotkeyService, HotkeyAction action, string label) =>
        new(label,
            hotkeyService.GetHotkey(action),
            hotkeyService.GetLastResult(action),
            gesture =>
            {
                Persist(action, gesture);
                return hotkeyService.Apply(action, gesture);
            });

    private void Persist(HotkeyAction action, KeyGesture? gesture)
    {
        // The stored form is KeyGesture.ToString(), which is what Hotkeys.TryParse reads back.
        var stored = gesture?.ToString();

        switch (action)
        {
            case HotkeyAction.Branches:
                _settingsService.Settings.BranchHotkey = stored;
                break;

            case HotkeyAction.Commit:
                _settingsService.Settings.CommitHotkey = stored;
                break;
        }

        _settingsService.Save();
    }

    partial void OnRepositoryPathChanged(string? value)
    {
        _settingsService.Settings.RepositoryPath = value;
        _settingsService.Save();
    }

    partial void OnPublishNewBranchesToRemoteChanged(bool value)
    {
        _settingsService.Settings.PublishNewBranchesToRemote = value;
        _settingsService.Save();
    }

    partial void OnGitExecutablePathChanged(string? value)
    {
        _settingsService.Settings.GitExecutablePath = value;
        _settingsService.Save();

        _ = CheckGitAvailabilityCommand.ExecuteAsync(null);
    }
}
