using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git;
using GitDeck.Git.Generation;
using System;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IRunWindowService _runWindowService;
    private readonly ISettingsService _settingsService;
    private readonly IFilePickerService _filePickerService;
    private readonly IGitExecutableService _gitExecutableService;
    private readonly ISecretProtector _secretProtector;

    public SettingsViewModel(
        IRunWindowService runWindowService,
        ISettingsService settingsService,
        IFilePickerService filePickerService,
        IGitExecutableService gitExecutableService,
        IGlobalHotkeyService globalHotkeyService,
        ISecretProtector secretProtector)
    {
        _runWindowService = runWindowService;
        _settingsService = settingsService;
        _filePickerService = filePickerService;
        _gitExecutableService = gitExecutableService;
        _secretProtector = secretProtector;

        RepositoryPath = settingsService.Settings.RepositoryPath;
        GitExecutablePath = settingsService.Settings.GitExecutablePath;
        PublishNewBranchesToRemote = settingsService.Settings.PublishNewBranchesToRemote;

        var ai = settingsService.Settings.Ai;
        IsAiEnabled = ai.IsEnabled;
        AiProvider = ai.Provider;
        AiModel = ai.Model;
        AiBaseUrl = ai.BaseUrl;
        AiKeyStatus = DescribeKey();

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
        new DesignGlobalHotkeyService(),
        new UnsupportedSecretProtector())
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

    [ObservableProperty]
    public partial bool IsAiEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBaseUrlRelevant))]
    public partial AiProviderKind AiProvider { get; set; }

    [ObservableProperty]
    public partial string AiModel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AiBaseUrl { get; set; }

    /// <summary>
    /// Bound to the key box. Never round-trips the stored secret: it starts empty, and typing into it
    /// replaces the stored key.
    /// </summary>
    [ObservableProperty]
    public partial string AiApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AiKeyStatus { get; set; } = string.Empty;

    public AiProviderKind[] AiProviders { get; } = [AiProviderKind.Anthropic, AiProviderKind.OpenAiCompatible];

    /// <summary>Anthropic's endpoint is fixed; only the OpenAI-compatible adapter takes a base URL.</summary>
    public bool IsBaseUrlRelevant => AiProvider is AiProviderKind.OpenAiCompatible;

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

    partial void OnIsAiEnabledChanged(bool value)
    {
        _settingsService.Settings.Ai.IsEnabled = value;
        _settingsService.Save();
    }

    partial void OnAiProviderChanged(AiProviderKind value)
    {
        var settings = _settingsService.Settings.Ai;
        settings.Provider = value;

        // The model names are provider-specific, so carry the switch through to a working default
        // rather than leaving an Anthropic model pointed at an OpenAI endpoint.
        if (value is AiProviderKind.Anthropic && !AiModel.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            AiModel = AiGenerationOptions.DefaultAnthropicModel;
        }

        _settingsService.Save();
        AiKeyStatus = DescribeKey();
    }

    partial void OnAiModelChanged(string value)
    {
        _settingsService.Settings.Ai.Model = value;
        _settingsService.Save();
    }

    partial void OnAiBaseUrlChanged(string? value)
    {
        _settingsService.Settings.Ai.BaseUrl = value;
        _settingsService.Save();
    }

    partial void OnAiApiKeyChanged(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        if (!_secretProtector.IsSupported)
        {
            AiKeyStatus = "This platform has no secret store, so the key cannot be saved. Use an environment variable instead.";
            return;
        }

        _settingsService.Settings.Ai.ProtectedApiKey = _secretProtector.Protect(value);
        _settingsService.Save();

        // Drop the plaintext from the box now that it is stored, so it is not left on screen.
        AiApiKey = string.Empty;
        AiKeyStatus = "Key saved, encrypted for this Windows account.";
    }

    [RelayCommand]
    private void ClearAiApiKey()
    {
        _settingsService.Settings.Ai.ProtectedApiKey = null;
        _settingsService.Save();

        AiApiKey = string.Empty;
        AiKeyStatus = DescribeKey();
    }

    /// <summary>
    /// Reports where the key would come from without ever showing it — a stored key, the provider's
    /// environment variable, or nothing.
    /// </summary>
    private string DescribeKey()
    {
        if (_settingsService.Settings.Ai.ProtectedApiKey is not null)
        {
            return "A key is stored, encrypted for this Windows account.";
        }

        var variable = _settingsService.Settings.Ai.Provider switch
        {
            AiProviderKind.Anthropic => "ANTHROPIC_API_KEY",
            _ => "OPENAI_API_KEY",
        };

        return Environment.GetEnvironmentVariable(variable) is not null
            ? $"No key stored. Using {variable} from the environment."
            : $"No key. Enter one above, or set {variable}.";
    }

    partial void OnGitExecutablePathChanged(string? value)
    {
        _settingsService.Settings.GitExecutablePath = value;
        _settingsService.Save();

        _ = CheckGitAvailabilityCommand.ExecuteAsync(null);
    }
}
