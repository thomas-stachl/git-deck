using System.Net.Http;

namespace GitDeck.Git.Generation;

/// <summary>
/// The single entry point callers use. Picks the adapter for the configured provider, so nothing
/// upstream of here knows or cares which one is in use.
/// </summary>
public sealed class CommitMessageGenerator : ICommitMessageGenerator, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    private readonly HttpClient _httpClient = new() { Timeout = Timeout };
    private readonly AnthropicCommitMessageClient _anthropic = new();
    private readonly OpenAiCompatibleCommitMessageClient _openAiCompatible;
    private readonly ClaudeCliCommitMessageClient _claudeCli;

    public CommitMessageGenerator(IProcessRunner processRunner)
    {
        _openAiCompatible = new OpenAiCompatibleCommitMessageClient(_httpClient);
        _claudeCli = new ClaudeCliCommitMessageClient(processRunner);
    }

    public Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Diff.Length == 0)
        {
            return Task.FromResult(CommitMessageResult.Failed("There is no diff to describe."));
        }

        // Claude Code picks its own model when none is set, so only the API providers need one.
        if (request.Options.Provider is not AiProviderKind.ClaudeCli
            && string.IsNullOrWhiteSpace(request.Options.Model))
        {
            return Task.FromResult(CommitMessageResult.Failed("No model configured. Set one in Settings."));
        }

        return request.Options.Provider switch
        {
            AiProviderKind.ClaudeCli => _claudeCli.GenerateAsync(request, cancellationToken),
            AiProviderKind.Anthropic => _anthropic.GenerateAsync(request, cancellationToken),
            AiProviderKind.OpenAiCompatible => _openAiCompatible.GenerateAsync(request, cancellationToken),
            _ => Task.FromResult(CommitMessageResult.Failed("No provider configured. Choose one in Settings.")),
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
