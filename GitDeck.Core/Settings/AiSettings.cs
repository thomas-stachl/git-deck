using GitDeck.Git.Generation;

namespace GitDeck.Core.Settings;

public class AiSettings
{
    /// <summary>
    /// Off by default. Generating a message sends the diff of the selected files to a third party, so
    /// it stays opt-in rather than something a user discovers after the fact.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Claude Code by default: it needs no key and no model, so enabling generation on a machine that
    /// has it just works. The generator reports plainly when it is absent.
    /// </summary>
    public AiProviderKind Provider { get; set; } = AiProviderKind.ClaudeCli;

    /// <summary>Empty means the provider's own default — which is what Claude Code wants.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Only used by OpenAI-compatible providers, and only when set — it should include the version
    /// path, for example <c>https://api.openai.com/v1</c> or <c>http://localhost:11434/v1</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The API key encrypted by <see cref="ISecretProtector"/>. Never the key itself — this value goes
    /// to settings.json.
    /// </summary>
    public string? ProtectedApiKey { get; set; }

    public int MaxDiffCharacters { get; set; } = AiGenerationOptions.DefaultMaxDiffCharacters;
}
