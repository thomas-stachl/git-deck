using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace GitDeck.Git.Generation;

/// <summary>
/// Talks to Anthropic's Messages API through the official SDK.
/// </summary>
/// <remarks>
/// Deliberately not folded into the OpenAI-compatible adapter: the system prompt is a top-level
/// parameter rather than a message, auth is <c>x-api-key</c>, and <c>max_tokens</c> is required, so a
/// translating shim would give up thinking and effort control for no benefit.
/// </remarks>
internal sealed class AnthropicCommitMessageClient : ICommitMessageProvider
{
    /// <summary>
    /// Covers thinking and the message together — max_tokens caps their sum, so a value sized only for
    /// the message itself would truncate mid-answer.
    /// </summary>
    private const int MaxTokens = 2048;

    // stop_reason values the SDK models as plain strings.
    private const string StopReasonRefusal = "refusal";
    private const string StopReasonMaxTokens = "max_tokens";

    private readonly object _clientGate = new();
    private AnthropicClient? _client;
    private string? _clientApiKey;

    public async Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Options.ApiKey))
        {
            return CommitMessageResult.Failed("No Anthropic API key. Add one in Settings or set ANTHROPIC_API_KEY.");
        }

        var client = GetClient(request.Options.ApiKey);

        var parameters = new MessageCreateParams
        {
            Model = request.Options.Model,
            MaxTokens = MaxTokens,
            System = CommitMessagePrompt.System,

            // Thinking stays on at low effort rather than being disabled. Disabling it can leak
            // <thinking> tags into the visible response — which in a commit message is a defect the
            // user pastes into history — and is rejected outright above "high" effort.
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = Effort.Low },

            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = CommitMessagePrompt.BuildUserMessage(request),
                },
            ],
        };

        Message response;
        try
        {
            response = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (AnthropicApiException ex)
        {
            return CommitMessageResult.Failed($"Anthropic rejected the request: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return CommitMessageResult.Failed($"Could not reach Anthropic: {ex.Message}");
        }

        // A safety classifier can decline the request: that arrives as a normal success with
        // stop_reason "refusal", so reading content without checking would surface an empty message.
        if (response.StopReason == StopReasonRefusal)
        {
            return CommitMessageResult.Failed("The model declined to write a message for this diff.");
        }

        // Running out of max_tokens is a normal success on the wire, but the message is cut off
        // mid-sentence — worse than no message, if it were pasted into history unnoticed.
        if (response.StopReason == StopReasonMaxTokens)
        {
            return CommitMessageResult.Failed("The model ran out of room before finishing the message.");
        }

        // Thinking blocks precede the text, so filtering by block type is required rather than
        // reading the first block.
        var text = string.Concat(response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text));

        return CommitMessageResult.FromText(text, "The model returned an empty message.");
    }

    private AnthropicClient GetClient(string apiKey)
    {
        lock (_clientGate)
        {
            // One client per key, kept for reuse. A client per request opened a fresh connection
            // pool every time, and the SDK's defaults — a 10-minute timeout with retries — could
            // hold the palette's busy state far beyond anything a user would wait for.
            if (_client is null || _clientApiKey != apiKey)
            {
                _client = new AnthropicClient
                {
                    ApiKey = apiKey,
                    Timeout = TimeSpan.FromSeconds(90),
                    MaxRetries = 1,
                };
                _clientApiKey = apiKey;
            }

            return _client;
        }
    }
}
