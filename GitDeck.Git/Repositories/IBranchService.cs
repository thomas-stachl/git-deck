namespace GitDeck.Git.Repositories;

public interface IBranchService
{
    /// <summary>
    /// Reads the repository at <paramref name="repositoryPath"/>: its local and remote branches
    /// (current branch first, then locals, then remotes) along with what is checked out and how many
    /// files have changed. Returns <see cref="RepositoryOverview.NotARepository"/> when the path is
    /// missing or is not a repository.
    /// </summary>
    Task<RepositoryOverview> GetOverviewAsync(string? repositoryPath, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Updates remote-tracking branches without touching the working tree — the equivalent of
    /// <c>git fetch --prune</c>. Meant for a background refresh: being offline or having no cached
    /// credentials are ordinary outcomes here, not failures worth interrupting anyone for.
    /// </summary>
    Task<FetchResult> FetchAsync(string? repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast-forwards the current branch from its upstream — <c>git pull --ff-only</c>. Fails cleanly,
    /// rather than merging or rebasing, when history has diverged: that is a judgment call for a real
    /// git client, not something a quick-launch palette should attempt on its own.
    /// </summary>
    Task<PullResult> PullCurrentBranchAsync(string? repositoryPath, CancellationToken cancellationToken = default);
}
