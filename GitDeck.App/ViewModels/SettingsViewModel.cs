using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel(
    IRunWindowService runWindowService,
    ISettingsService settingsService,
    IFilePickerService filePickerService,
    IGitExecutableService gitExecutableService,
    IGlobalHotkeyService globalHotkeyService) : ObservableObject
{
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

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Hello from GitDeck.App!";

    [ObservableProperty]
    public partial string? RepositoryPath { get; set; } = settingsService.Settings.RepositoryPath;

    [ObservableProperty]
    public partial string? GitExecutablePath { get; set; } = settingsService.Settings.GitExecutablePath;

    [ObservableProperty]
    public partial bool PublishNewBranchesToRemote { get; set; } = settingsService.Settings.PublishNewBranchesToRemote;

    /// <summary>
    /// The configured hotkey. Registration is applied at startup, so this starts from what the
    /// hotkey service actually holds rather than re-reading the setting.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyDisplay))]
    public partial KeyGesture? Hotkey { get; set; } = globalHotkeyService.Current;

    [ObservableProperty]
    public partial string HotkeyStatus { get; set; } = Describe(globalHotkeyService.LastResult);

    public string HotkeyDisplay => Hotkey is { } hotkey ? Hotkeys.Format(hotkey) : string.Empty;

    [ObservableProperty]
    public partial string GitStatus { get; set; } = "Checking for Git...";

    [RelayCommand]
    private void ToggleRunWindow()
    {
        runWindowService.Toggle();
    }

    /// <summary>Called by the settings view when the user presses a combination in the capture box.</summary>
    public void CaptureHotkey(KeyGesture gesture)
    {
        if (!Hotkeys.IsValid(gesture))
        {
            HotkeyStatus = Hotkeys.ModifierRequiredMessage;
            return;
        }

        Hotkey = gesture;

        // Re-registering the same gesture leaves Hotkey unchanged, so report the state either way.
        HotkeyStatus = Describe(globalHotkeyService.LastResult);
    }

    [RelayCommand]
    private void ClearHotkey()
    {
        Hotkey = null;
    }

    [RelayCommand]
    private async Task BrowseRepositoryPathAsync()
    {
        var path = await filePickerService.PickFolderAsync("Select Repository Folder");
        if (path is not null)
        {
            RepositoryPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseGitExecutablePathAsync()
    {
        var path = await filePickerService.PickFileAsync("Select Git Executable");
        if (path is not null)
        {
            GitExecutablePath = path;
        }
    }

    [RelayCommand]
    private async Task CheckGitAvailabilityAsync()
    {
        GitStatus = "Checking for Git...";

        var availability = await gitExecutableService.CheckAvailabilityAsync(GitExecutablePath);
        GitStatus = availability.IsAvailable
            ? $"Found: {availability.Version}"
            : "Git not found. Set the path below or install Git and ensure it's on PATH.";
    }

    partial void OnRepositoryPathChanged(string? value)
    {
        settingsService.Settings.RepositoryPath = value;
        settingsService.Save();
    }

    partial void OnHotkeyChanged(KeyGesture? value)
    {
        settingsService.Settings.Hotkey = value?.ToString();
        settingsService.Save();

        HotkeyStatus = Describe(globalHotkeyService.Apply(value));
    }

    private static string Describe(HotkeyRegistration registration) => registration switch
    {
        { IsRegistered: true } => "Hotkey is active.",
        { ErrorMessage: { } error } => error,
        _ => "No hotkey set. Click the box above and press a combination.",
    };

    partial void OnPublishNewBranchesToRemoteChanged(bool value)
    {
        settingsService.Settings.PublishNewBranchesToRemote = value;
        settingsService.Save();
    }

    partial void OnGitExecutablePathChanged(string? value)
    {
        settingsService.Settings.GitExecutablePath = value;
        settingsService.Save();

        _ = CheckGitAvailabilityCommand.ExecuteAsync(null);
    }
}
