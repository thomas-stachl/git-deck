using GitDeck.Git.Repositories;

namespace GitDeck.Git.Generation;

/// <param name="WorkingDirectory">
/// The repository's working tree root. The Claude Code provider runs there, so it picks up that
/// repository's CLAUDE.md rather than whatever directory the app happened to start from.
/// </param>
public sealed record CommitMessageRequest(
    AiGenerationOptions Options,
    string WorkingDirectory,
    IReadOnlyList<ChangedFile> Files,
    string Diff,
    bool IsDiffTruncated);

public sealed record CommitMessageResult(string? Message, string? ErrorMessage)
{
    public static CommitMessageResult Failed(string errorMessage) => new(null, errorMessage);

    /// <summary>The shared tail of every provider: trim, and treat an empty reply as a failure.</summary>
    public static CommitMessageResult FromText(string? text, string emptyErrorMessage) =>
        string.IsNullOrWhiteSpace(text)
            ? Failed(emptyErrorMessage)
            : new CommitMessageResult(text.Trim(), null);

    public bool IsGenerated => Message is not null;
}

public interface ICommitMessageGenerator
{
    /// <summary>
    /// Writes a commit message for the given diff. The provider is chosen from
    /// <see cref="AiGenerationOptions.Provider"/>, so callers never depend on which one is configured.
    /// </summary>
    Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken = default);
}
