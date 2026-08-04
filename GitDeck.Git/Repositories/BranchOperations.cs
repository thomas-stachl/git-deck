namespace GitDeck.Git.Repositories;

/// <param name="PublishToRemote">
/// Also push the new branch and set it as upstream. <c>git switch -c</c> alone never touches the
/// remote; this adds the <c>git push --set-upstream</c> that would normally follow it.
/// </param>
public sealed record CreateBranchRequest(
    string? RepositoryPath,
    string BranchName,
    bool PublishToRemote);

/// <param name="IsPublished">
/// Whether the branch reached the remote. Always <c>false</c> when publishing was not requested.
/// </param>
/// <param name="ErrorMessage">
/// Why the operation fell short. Set together with <c>IsCreated: true</c> when the local branch was
/// created but publishing it failed.
/// </param>
public sealed record CreateBranchResult(bool IsCreated, bool IsPublished, string? ErrorMessage)
{
    public static CreateBranchResult Failed(string errorMessage) => new(false, false, errorMessage);
}

public sealed record SwitchBranchRequest(string? RepositoryPath, BranchInfo Branch);

/// <param name="CreatedLocalBranch">
/// Whether a local branch had to be created to track the requested remote branch.
/// </param>
public sealed record SwitchBranchResult(bool IsSwitched, bool CreatedLocalBranch, string? ErrorMessage)
{
    public static SwitchBranchResult Failed(string errorMessage) => new(false, false, errorMessage);
}

/// <summary>Result of updating remote-tracking branches — <c>git fetch --prune</c>.</summary>
public sealed record FetchResult(bool IsDone, string? ErrorMessage)
{
    public static FetchResult Failed(string errorMessage) => new(false, errorMessage);
}

/// <summary>Result of fast-forwarding the current branch from its upstream.</summary>
public sealed record PullResult(bool IsPulled, string? ErrorMessage)
{
    public static PullResult Failed(string errorMessage) => new(false, errorMessage);
}
