namespace GitDeck.Core.Settings;

/// <remarks>
/// Persisted in settings.json by numeric value, so the member order here is a serialization
/// contract: never reorder or insert members, only append.
/// </remarks>
public enum AiProviderKind
{
    /// <summary>
    /// A locally installed Claude Code, driven headlessly. Already authenticated, so it needs no API
    /// key and no configuration — the default when it is present on the machine.
    /// </summary>
    ClaudeCli,

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

public static class AiProviderKinds
{
    /// <summary>
    /// The environment variable a key is read from when none is stored. The single source of truth
    /// for both resolving the key and telling the user which variable would be used.
    /// </summary>
    public static string ApiKeyEnvironmentVariable(AiProviderKind provider) =>
        provider is AiProviderKind.Anthropic ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY";
}
