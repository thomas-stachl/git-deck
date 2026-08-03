namespace GitDeck.Git.Generation;

public enum AiProviderKind
{
    /// <summary>
    /// Anything exposing OpenAI's <c>/chat/completions</c> shape: OpenAI, Azure OpenAI, Ollama,
    /// LM Studio, OpenRouter, Groq, Mistral. One adapter covers all of them; only the base URL,
    /// model and key differ.
    /// </summary>
    OpenAiCompatible,

    /// <summary>
    /// Anthropic's Messages API, through the official SDK. Deliberately not routed through the
    /// OpenAI-compatible adapter — the wire shapes differ (system is a top-level parameter, auth is
    /// x-api-key, max_tokens is required), and a translating shim would give up thinking and effort
    /// control for nothing.
    /// </summary>
    Anthropic,
}

/// <param name="ApiKey">
/// Resolved at call time from the encrypted setting or the environment. Never persisted in the clear.
/// </param>
public sealed record AiGenerationOptions(
    AiProviderKind Provider,
    string Model,
    string? ApiKey,
    string? BaseUrl = null,
    int MaxDiffCharacters = AiGenerationOptions.DefaultMaxDiffCharacters)
{
    public const int DefaultMaxDiffCharacters = 24_000;

    public const string DefaultAnthropicModel = "claude-opus-5";

    public const string DefaultOpenAiCompatibleBaseUrl = "https://api.openai.com/v1";
}
