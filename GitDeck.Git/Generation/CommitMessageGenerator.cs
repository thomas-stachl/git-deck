using GitDeck.Core.Settings;
using System.Net.Http;

namespace GitDeck.Git.Generation;

/// <summary>
/// The single entry point callers use. Picks the adapter for the configured provider, so nothing
/// upstream of here knows or cares which one is in use — and owns the one timeout budget and
/// cancel-versus-timeout mapping that used to be copied into every adapter.
/// </summary>
public sealed class CommitMessageGenerator : ICommitMessageGenerator, IDisposable
{
    /// <summary>Generation is interactive; nobody watches a palette spinner longer than this.</summary>
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(120);

    // The linked token below is the single budget, so the client itself must not race it with one
    // of its own.
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly Dictionary<AiProviderKind, ICommitMessageProvider> _providers;

    public CommitMessageGenerator(IProcessRunner processRunner)
    {
        // Constructed here rather than DI-registered: the adapters are an implementation detail of
        // this dispatcher; the interface exists to give them a common shape and a test seam.
        _providers = new Dictionary<AiProviderKind, ICommitMessageProvider>
        {
            [AiProviderKind.ClaudeCli] = new ClaudeCliCommitMessageClient(processRunner),
            [AiProviderKind.Anthropic] = new AnthropicCommitMessageClient(),
            [AiProviderKind.OpenAiCompatible] = new OpenAiCompatibleCommitMessageClient(_httpClient),
        };
    }

    public async Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Diff.Length == 0)
        {
            return CommitMessageResult.Failed("There is no diff to describe.");
        }

        // Claude Code picks its own model when none is set, so only the API providers need one.
        if (request.Options.Provider is not AiProviderKind.ClaudeCli
            && string.IsNullOrWhiteSpace(request.Options.Model))
        {
            return CommitMessageResult.Failed("No model configured. Set one in Settings.");
        }

        if (!_providers.TryGetValue(request.Options.Provider, out var provider))
        {
            return CommitMessageResult.Failed("No provider configured. Choose one in Settings.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProviderTimeout);

        try
        {
            return await provider.GenerateAsync(request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CommitMessageResult.Failed("The provider did not respond in time.");
        }
    }

    // Reached because Program.Main disposes the ServiceProvider, which disposes its singletons.
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
