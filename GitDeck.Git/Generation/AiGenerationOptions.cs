using GitDeck.Core.Settings;

namespace GitDeck.Git.Generation;

/// <param name="ApiKey">
/// Resolved at call time from the encrypted setting or the environment. Never persisted in the clear.
/// </param>
public sealed record AiGenerationOptions(
    AiProviderKind Provider,
    string Model,
    string? ApiKey,
    string? BaseUrl = null,
    int MaxDiffCharacters = AiSettings.DefaultMaxDiffCharacters)
{
    public const string DefaultAnthropicModel = "claude-opus-5";

    public const string DefaultOpenAiCompatibleBaseUrl = "https://api.openai.com/v1";
}
