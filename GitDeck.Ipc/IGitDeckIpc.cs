using GitDeck.Git.Repositories;

namespace GitDeck.Ipc;

/// <summary>
/// The facade an out-of-process client (the Stream Deck plugin) calls over the named pipe.
/// Deliberately not <see cref="IBranchService"/> exposed raw: two of these calls are "focus the
/// app's UI", not a git operation, and <see cref="GetRecentRepositoriesAsync"/> /
/// <see cref="PickRepositoryFolderAsync"/> back the plugin's Property Inspector rather than a key
/// action at all.
/// </summary>
public interface IGitDeckIpc
{
    /// <summary>Reads the repository's current status — the same read the palette footer shows.</summary>
    Task<RepositoryOverview> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Updates remote-tracking branches without touching the working tree.</summary>
    Task<FetchResult> FetchAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Fast-forwards the current branch from its upstream.</summary>
    Task<PullResult> PullAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Pushes the current branch to its remote, publishing it first if it has none yet.</summary>
    Task<PushResult> PushAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Opens (or focuses) the Branches palette, scoped to <paramref name="repositoryPath"/>.</summary>
    Task OpenBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Opens (or focuses) the Commit palette, scoped to <paramref name="repositoryPath"/>.</summary>
    Task OpenCommitAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a native folder picker and returns the chosen path, or null if the user cancelled.
    /// Backs the Property Inspector's "Browse…" button.
    /// </summary>
    Task<string?> PickRepositoryFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>Most-recently-used repository paths, newest first. Backs the Property Inspector's dropdown.</summary>
    Task<IReadOnlyList<string>> GetRecentRepositoriesAsync(CancellationToken cancellationToken = default);
}
