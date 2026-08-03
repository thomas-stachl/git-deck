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
/// <param name="ChangedFileCount">
/// Files with staged, unstaged or untracked changes, counted the way <c>git status</c> lists them —
/// an untracked directory counts once rather than per file inside it.
/// </param>
public sealed record RepositoryOverview(
    bool IsRepository,
    string? WorkingDirectory,
    string? Head,
    int ChangedFileCount,
    IReadOnlyList<BranchInfo> Branches)
{
    public static readonly RepositoryOverview NotARepository = new(false, null, null, 0, []);
}
