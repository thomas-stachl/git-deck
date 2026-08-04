using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

/// <summary>
/// The settings shell: repository and git-executable configuration, plus the AI and hotkey
/// sections as child view models with their own concerns.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IRunWindowService _runWindowService;
    private readonly ISettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private readonly IGitExecutableService _gitExecutableService;

    private int _gitProbeVersion;

    public SettingsViewModel(
        IRunWindowService runWindowService,
        ISettingsService settingsService,
        IFilePickerService filePickerService,
        IGitExecutableService gitExecutableService,
        AiSettingsViewModel ai,
        HotkeySettingsViewModel hotkeys)
    {
        _runWindowService = runWindowService;
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _gitExecutableService = gitExecutableService;

        Ai = ai;
        Hotkeys = hotkeys;

        RepositoryPath = settingsService.Settings.RepositoryPath;
        GitExecutablePath = settingsService.Settings.GitExecutablePath;
        PublishNewBranchesToRemote = settingsService.Settings.PublishNewBranchesToRemote;
    }

    // Parameterless constructor required by the Avalonia XAML previewer/designer;
    // wires up hand-written fakes instead of the real services.
    public SettingsViewModel() : this(
        new DesignRunWindowService(),
        new DesignSettingsService(),
        new DesignFilePickerService(),
        new DesignGitExecutableService(),
        new AiSettingsViewModel(),
        new HotkeySettingsViewModel())
    {
    }

    public AiSettingsViewModel Ai { get; }

    public HotkeySettingsViewModel Hotkeys { get; }

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
        // Probes are not cancelled, so an older, slower one can finish after a newer one; the
        // counter keeps its stale result from overwriting the answer for the current path.
        var probe = ++_gitProbeVersion;

        GitStatus = "Checking for Git...";

        var availability = await _gitExecutableService.CheckAvailabilityAsync(GitExecutablePath);

        if (probe != _gitProbeVersion)
        {
            return;
        }

        GitStatus = availability.IsAvailable
            ? $"Found: {availability.Version}"
            : "Git not found. Set the path below or install Git and ensure it's on PATH.";
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
