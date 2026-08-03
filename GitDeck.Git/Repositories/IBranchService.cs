namespace GitDeck.Git.Repositories;

public interface IBranchService
{
    /// <summary>
    /// Lists the local and remote branches of the repository at <paramref name="repositoryPath"/>,
    /// current branch first, then locals, then remotes. Returns
    /// <see cref="BranchListing.NotARepository"/> when the path is missing or is not a repository.
    /// </summary>
    Task<BranchListing> GetBranchesAsync(string? repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Whether git would accept <paramref name="branchName"/> as a branch name.</summary>
    bool IsValidBranchName(string branchName);

    /// <summary>
    /// Creates a branch at the current HEAD and switches to it — the equivalent of
    /// <c>git switch -c &lt;name&gt;</c> — optionally publishing it to the remote afterwards.
    /// </summary>
    Task<CreateBranchResult> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches to an existing branch. A remote branch is first checked out as a local branch that
    /// tracks it, the way <c>git switch &lt;name&gt;</c> does for a name that exists on exactly one
    /// remote; an already existing local branch of that name is switched to instead.
    /// </summary>
    Task<SwitchBranchResult> SwitchBranchAsync(SwitchBranchRequest request, CancellationToken cancellationToken = default);
}
