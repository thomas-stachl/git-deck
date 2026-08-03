namespace GitDeck.Git.Repositories;

/// <param name="WorkingDirectory">The working tree root, which git is run from.</param>
/// <param name="Files">
/// Exactly the files the commit should contain. Anything staged but absent from this list stays
/// staged and uncommitted.
/// </param>
public sealed record CommitRequest(
    string WorkingDirectory,
    string Message,
    IReadOnlyList<ChangedFile> Files,
    string? GitExecutablePath = null);

public sealed record CommitResult(bool IsCommitted, string? ErrorMessage)
{
    public static CommitResult Failed(string errorMessage) => new(false, errorMessage);
}

public interface ICommitService
{
    /// <summary>
    /// Commits exactly the requested files. Runs through the git executable so hooks, signing and
    /// the user's identity configuration apply the same way they do on the command line.
    /// </summary>
    Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default);
}
