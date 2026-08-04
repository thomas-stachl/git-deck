namespace GitDeck.Git.Generation;

/// <summary>
/// One provider adapter behind <see cref="CommitMessageGenerator"/>. Implementations map their own
/// protocol failures to failed results, but let cancellation propagate — the generator owns the
/// single timeout budget and the cancel-versus-timeout distinction for all of them.
/// </summary>
internal interface ICommitMessageProvider
{
    Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken);
}
