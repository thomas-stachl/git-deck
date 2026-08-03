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
/// The branches of a repository. <paramref name="IsRepository"/> separates "the configured path is
/// not a repository" from "it is a repository that has no branches yet".
/// </summary>
public sealed record BranchListing(bool IsRepository, IReadOnlyList<BranchInfo> Branches)
{
    public static readonly BranchListing NotARepository = new(false, []);
}
