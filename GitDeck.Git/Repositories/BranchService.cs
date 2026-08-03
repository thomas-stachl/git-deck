using LibGit2Sharp;
using LibGit2SharpRepository = LibGit2Sharp.Repository;

namespace GitDeck.Git.Repositories;

public sealed class BranchService : IBranchService
{
    public Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(string? repositoryPath, CancellationToken cancellationToken = default)
        => Task.Run(() => GetBranches(repositoryPath), cancellationToken);

    private static IReadOnlyList<BranchInfo> GetBranches(string? repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            return [];
        }

        try
        {
            // Accepts a path anywhere inside the working tree, not just its root.
            var gitDirectory = LibGit2SharpRepository.Discover(repositoryPath);
            if (gitDirectory is null)
            {
                return [];
            }

            using var repository = new LibGit2SharpRepository(gitDirectory);

            return [.. repository.Branches
                .Where(branch => !IsRemoteHead(branch))
                .Select(ToBranchInfo)
                .OrderByDescending(branch => branch.IsCurrent)
                .ThenBy(branch => branch.IsRemote)
                .ThenBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static BranchInfo ToBranchInfo(Branch branch) => new(
        branch.FriendlyName,
        branch.IsRemote,
        branch.IsRemote ? branch.RemoteName : null,
        branch.IsCurrentRepositoryHead);

    // "origin/HEAD" is a symbolic alias for the remote's default branch, not a branch of its own.
    private static bool IsRemoteHead(Branch branch) =>
        branch.IsRemote && branch.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal);
}
