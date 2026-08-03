using GitDeck.Git.Repositories;

namespace GitDeck.Git.Generation;

public sealed record CommitMessageRequest(
    AiGenerationOptions Options,
    IReadOnlyList<ChangedFile> Files,
    string Diff,
    bool IsDiffTruncated);

public sealed record CommitMessageResult(string? Message, string? ErrorMessage)
{
    public static CommitMessageResult Failed(string errorMessage) => new(null, errorMessage);

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
