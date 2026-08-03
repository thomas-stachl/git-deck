using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitDeck.Git.Generation;

/// <summary>
/// Talks to any endpoint exposing OpenAI's <c>/chat/completions</c> shape — OpenAI, Azure OpenAI,
/// Ollama, LM Studio, OpenRouter, Groq, Mistral. Only the base URL, model and key change.
/// </summary>
internal sealed class OpenAiCompatibleCommitMessageClient(HttpClient httpClient)
{
    private const int MaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(request.Options.BaseUrl)
            ? AiGenerationOptions.DefaultOpenAiCompatibleBaseUrl
            : request.Options.BaseUrl).TrimEnd('/');

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(
                new ChatRequest(
                    request.Options.Model,
                    MaxTokens,
                    [
                        new ChatMessage("system", CommitMessagePrompt.System),
                        new ChatMessage("user", CommitMessagePrompt.BuildUserMessage(request)),
                    ]),
                options: JsonOptions),
        };

        // Locally hosted providers such as Ollama accept requests with no key at all.
        if (!string.IsNullOrWhiteSpace(request.Options.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Options.ApiKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return CommitMessageResult.Failed(Describe(response.StatusCode, body));
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
            var text = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            return string.IsNullOrWhiteSpace(text)
                ? CommitMessageResult.Failed("The model returned an empty message.")
                : new CommitMessageResult(text.Trim(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return CommitMessageResult.Failed($"Could not reach the model: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CommitMessageResult.Failed("The model did not respond in time.");
        }
    }

    private static string Describe(System.Net.HttpStatusCode statusCode, string body) => statusCode switch
    {
        System.Net.HttpStatusCode.Unauthorized => "The API key was rejected. Check it in Settings.",
        System.Net.HttpStatusCode.NotFound =>
            "The endpoint was not found. Check the base URL — it should include the version path, such as https://api.openai.com/v1.",
        System.Net.HttpStatusCode.TooManyRequests => "The provider is rate limiting. Try again shortly.",
        _ => $"The provider returned {(int)statusCode}: {FirstLine(body)}",
    };

    private static string FirstLine(string body)
    {
        var trimmed = body.Trim();

        if (trimmed.Length == 0)
        {
            return "no details";
        }

        var line = trimmed.Split('\n')[0].Trim();

        return line.Length > 200 ? line[..200] : line;
    }

    // max_tokens is the spelling every compatible implementation accepts; OpenAI's newer reasoning
    // models want max_completion_tokens instead, which is why the field is nullable rather than fixed.
    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);
}
