using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Design;
using GitDeck.Core.Settings;
using GitDeck.Git.Generation;
using System;

namespace GitDeck.App.ViewModels;

/// <summary>The commit-message-generation section of Settings: provider, model and key handling.</summary>
public partial class AiSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISecretProtector _secretProtector;

    public AiSettingsViewModel(ISettingsService settingsService, ISecretProtector secretProtector)
    {
        _settingsService = settingsService;
        _secretProtector = secretProtector;

        var ai = settingsService.Settings.Ai;
        IsEnabled = ai.IsEnabled;
        Provider = ai.Provider;
        Model = ai.Model;
        BaseUrl = ai.BaseUrl;
        KeyStatus = DescribeKey();
    }

    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public AiSettingsViewModel() : this(new DesignSettingsService(), new UnsupportedSecretProtector())
    {
    }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBaseUrlRelevant), nameof(IsApiKeyRelevant), nameof(ProviderNote))]
    public partial AiProviderKind Provider { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? BaseUrl { get; set; }

    /// <summary>
    /// Bound to the key box. Never round-trips the stored secret: it starts empty, and saving it
    /// replaces the stored key and clears the box.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveApiKeyCommand))]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeyStatus { get; set; } = string.Empty;

    public AiProviderKind[] Providers { get; } =
        [AiProviderKind.ClaudeCli, AiProviderKind.Anthropic, AiProviderKind.OpenAiCompatible];

    /// <summary>Anthropic's endpoint is fixed; only the OpenAI-compatible adapter takes a base URL.</summary>
    public bool IsBaseUrlRelevant => Provider is AiProviderKind.OpenAiCompatible;

    /// <summary>Claude Code carries its own credentials, so it has no key to enter.</summary>
    public bool IsApiKeyRelevant => Provider is not AiProviderKind.ClaudeCli;

    /// <summary>Explains what the chosen provider needs — including whether it was actually found.</summary>
    public string ProviderNote => Provider switch
    {
        AiProviderKind.ClaudeCli => ClaudeCliLocator.Find() is { } path
            ? $"Found at {path}. Already signed in, so no key or model is needed. Usage counts against your Claude Code plan."
            : "Claude Code was not found on this machine. Install it, or pick another provider.",
        AiProviderKind.Anthropic => "Uses Anthropic's API directly. Needs a key and API credit.",
        _ => "Any OpenAI-compatible endpoint. Leave the key blank for a local provider such as Ollama.",
    };

    /// <summary>
    /// Persisting only on an explicit save lets the key be typed as well as pasted — a per-change
    /// hook would store the first character as the whole key.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveApiKey))]
    private void SaveApiKey()
    {
        if (!_secretProtector.IsSupported)
        {
            KeyStatus = "This platform has no secret store, so the key cannot be saved. Use an environment variable instead.";
            return;
        }

        _settingsService.Settings.Ai.ProtectedApiKey = _secretProtector.Protect(ApiKey);
        _settingsService.Save();

        // Drop the plaintext from the box now that it is stored, so it is not left on screen.
        ApiKey = string.Empty;
        KeyStatus = "Key saved, encrypted for this Windows account.";
    }

    private bool CanSaveApiKey() => ApiKey.Length > 0;

    [RelayCommand]
    private void ClearApiKey()
    {
        _settingsService.Settings.Ai.ProtectedApiKey = null;
        _settingsService.Save();

        ApiKey = string.Empty;
        KeyStatus = DescribeKey();
    }

    /// <summary>Fills in a local Ollama, which needs no key and keeps the diff on this machine.</summary>
    [RelayCommand]
    private void UseOllamaPreset()
    {
        Provider = AiProviderKind.OpenAiCompatible;
        BaseUrl = "http://localhost:11434/v1";
        Model = "qwen2.5-coder";

        ClearApiKey();
        KeyStatus = "Pointed at a local Ollama. No key needed — pull the model with: ollama pull qwen2.5-coder";
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _settingsService.Settings.Ai.IsEnabled = value;
        _settingsService.Save();
    }

    partial void OnProviderChanged(AiProviderKind value)
    {
        _settingsService.Settings.Ai.Provider = value;

        // Model names are provider-specific, so carry the switch through to something that works
        // rather than leaving an Anthropic model pointed at an OpenAI endpoint.
        Model = value switch
        {
            // Claude Code uses whatever it is configured for; an empty box means "don't override".
            AiProviderKind.ClaudeCli => string.Empty,
            AiProviderKind.Anthropic when !Model.StartsWith("claude", StringComparison.OrdinalIgnoreCase)
                => AiGenerationOptions.DefaultAnthropicModel,
            _ => Model,
        };

        _settingsService.Save();
        KeyStatus = DescribeKey();
    }

    partial void OnModelChanged(string value)
    {
        _settingsService.Settings.Ai.Model = value;
        _settingsService.Save();
    }

    partial void OnBaseUrlChanged(string? value)
    {
        _settingsService.Settings.Ai.BaseUrl = value;
        _settingsService.Save();
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

        var variable = AiProviderKinds.ApiKeyEnvironmentVariable(_settingsService.Settings.Ai.Provider);

        return Environment.GetEnvironmentVariable(variable) is not null
            ? $"No key stored. Using {variable} from the environment."
            : $"No key. Enter one above, or set {variable}.";
    }
}
