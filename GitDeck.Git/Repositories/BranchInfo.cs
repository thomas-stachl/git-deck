namespace GitDeck.Git.Repositories;

/// <summary>
/// A branch as shown in the run window. <paramref name="Name"/> is the friendly name, which for
/// remote branches includes the remote prefix (for example <c>origin/main</c>).
/// </summary>
public sealed record BranchInfo(string Name, bool IsRemote, string? RemoteName, bool IsCurrent)
{
    /// <summary>The branch name without its remote prefix.</summary>
    public string ShortName =>
        IsRemote && RemoteName is not null && Name.StartsWith($"{RemoteName}/", StringComparison.Ordinal)
            ? Name[(RemoteName.Length + 1)..]
            : Name;
}

/// <summary>
/// What the run window needs to know about the configured repository in one read.
/// <paramref name="IsRepository"/> separates "the configured path is not a repository" from "it is a
/// repository that has no branches yet".
/// </summary>
/// <param name="WorkingDirectory">The working tree root, or null for a bare repository.</param>
/// <param name="Head">
/// A description of what is checked out: a branch name, or a note about a detached or unborn HEAD.
/// </param>
/// <param name="ChangedFiles">
/// Files with staged, unstaged or untracked changes, as <c>git status</c> lists them.
/// </param>
/// <param name="LoadError">
/// Why the repository could not be read, when the cause is something other than the path not being
/// a repository — a permission problem or a corrupt index should not be reported as "not a Git
/// repository".
/// </param>
/// <param name="HasUpstream">Whether the current branch has a remote-tracking branch configured.</param>
/// <param name="AheadBy">
/// Commits on the current branch not yet on its upstream. Zero when there is no upstream.
/// </param>
/// <param name="BehindBy">
/// Commits on the upstream not yet on the current branch — as of the last fetch, not a live network
/// check. Zero when there is no upstream.
/// </param>
public sealed record RepositoryOverview(
    bool IsRepository,
    string? WorkingDirectory,
    string? Head,
    IReadOnlyList<ChangedFile> ChangedFiles,
    IReadOnlyList<BranchInfo> Branches,
    string? LoadError = null,
    bool HasUpstream = false,
    int AheadBy = 0,
    int BehindBy = 0)
{
    public static readonly RepositoryOverview NotARepository = new(false, null, null, [], []);

    public static RepositoryOverview Failed(string loadError) => new(false, null, null, [], [], loadError);

    public int ChangedFileCount => ChangedFiles.Count;
}
