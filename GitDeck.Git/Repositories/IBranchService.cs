namespace GitDeck.Git.Repositories;

public interface IBranchService
{
    /// <summary>
    /// Lists the local and remote branches of the repository at <paramref name="repositoryPath"/>,
    /// current branch first, then locals, then remotes. Returns an empty list when the path is
    /// missing or is not a repository.
    /// </summary>
    Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(string? repositoryPath, CancellationToken cancellationToken = default);
}
