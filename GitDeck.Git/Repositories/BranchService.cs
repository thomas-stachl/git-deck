using LibGit2Sharp;
using LibGit2SharpRepository = LibGit2Sharp.Repository;

namespace GitDeck.Git.Repositories;

public sealed class BranchService(IGitExecutableService gitExecutableService) : IBranchService
{
    public Task<RepositoryOverview> GetOverviewAsync(string? repositoryPath, CancellationToken cancellationToken = default)
        => Task.Run(() => GetOverview(repositoryPath), cancellationToken);

    public bool IsValidBranchName(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName) || branchName != branchName.Trim())
        {
            return false;
        }

        return Reference.IsValidName($"refs/heads/{branchName}");
    }

    public async Task<CreateBranchResult> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValidBranchName(request.BranchName))
        {
            return CreateBranchResult.Failed($"\"{request.BranchName}\" is not a valid branch name.");
        }

        var creation = await Task.Run(() => CreateLocalBranch(request), cancellationToken);

        if (!creation.Result.IsCreated || !request.PublishToRemote)
        {
            return creation.Result;
        }

        if (creation.RemoteName is null)
        {
            return new CreateBranchResult(true, false, "Created locally. The repository has no remote to publish to.");
        }

        var push = await gitExecutableService.RunAsync(
            request.GitExecutablePath,
            creation.WorkingDirectory,
            ["push", "--set-upstream", creation.RemoteName, request.BranchName],
            cancellationToken);

        return push.IsSuccess
            ? new CreateBranchResult(true, true, null)
            : new CreateBranchResult(true, false, $"Created locally, but publishing failed: {push.FailureMessage}");
    }

    public Task<SwitchBranchResult> SwitchBranchAsync(SwitchBranchRequest request, CancellationToken cancellationToken = default)
        => Task.Run(() => SwitchBranch(request), cancellationToken);

    private static SwitchBranchResult SwitchBranch(SwitchBranchRequest request)
    {
        var gitDirectory = TryDiscover(request.RepositoryPath);
        if (gitDirectory is null)
        {
            return SwitchBranchResult.Failed("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new LibGit2SharpRepository(gitDirectory);

            if (repository.Info.IsBare)
            {
                return SwitchBranchResult.Failed("Cannot switch branches in a bare repository.");
            }

            return request.Branch.IsRemote
                ? SwitchToRemoteBranch(repository, request.Branch)
                : SwitchToLocalBranch(repository, request.Branch);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return SwitchBranchResult.Failed(ex.Message);
        }
    }

    private static SwitchBranchResult SwitchToLocalBranch(LibGit2SharpRepository repository, BranchInfo branchInfo)
    {
        if (repository.Branches[branchInfo.Name] is not { } branch)
        {
            return SwitchBranchResult.Failed($"Branch \"{branchInfo.Name}\" no longer exists.");
        }

        Commands.Checkout(repository, branch);

        return new SwitchBranchResult(true, false, null);
    }

    private static SwitchBranchResult SwitchToRemoteBranch(LibGit2SharpRepository repository, BranchInfo branchInfo)
    {
        if (repository.Branches[branchInfo.Name] is not { } remoteBranch)
        {
            return SwitchBranchResult.Failed($"Remote branch \"{branchInfo.Name}\" no longer exists.");
        }

        var localName = branchInfo.ShortName;
        var existingLocal = repository.Branches[localName];

        if (existingLocal is not null)
        {
            // A local branch of that name already exists; switch to it rather than fail, matching
            // what `git switch <name>` would do.
            Commands.Checkout(repository, existingLocal);
            return new SwitchBranchResult(true, false, null);
        }

        var localBranch = repository.CreateBranch(localName, remoteBranch.Tip);
        localBranch = repository.Branches.Update(localBranch, branch => branch.TrackedBranch = remoteBranch.CanonicalName);

        Commands.Checkout(repository, localBranch);

        return new SwitchBranchResult(true, true, null);
    }

    private static LocalCreation CreateLocalBranch(CreateBranchRequest request)
    {
        var gitDirectory = TryDiscover(request.RepositoryPath);
        if (gitDirectory is null)
        {
            return LocalCreation.Failed("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new LibGit2SharpRepository(gitDirectory);

            if (repository.Info.IsBare)
            {
                return LocalCreation.Failed("Cannot switch branches in a bare repository.");
            }

            if (repository.Info.IsHeadUnborn)
            {
                return LocalCreation.Failed("The repository has no commits yet, so there is nothing to branch from.");
            }

            if (repository.Branches[request.BranchName] is not null)
            {
                return LocalCreation.Failed($"Branch \"{request.BranchName}\" already exists.");
            }

            var branch = repository.CreateBranch(request.BranchName);
            Commands.Checkout(repository, branch);

            return new LocalCreation(
                new CreateBranchResult(true, false, null),
                repository.Info.WorkingDirectory,
                PreferredRemoteName(repository));
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return LocalCreation.Failed(ex.Message);
        }
    }

    private static RepositoryOverview GetOverview(string? repositoryPath)
    {
        var gitDirectory = TryDiscover(repositoryPath);
        if (gitDirectory is null)
        {
            return RepositoryOverview.NotARepository;
        }

        try
        {
            using var repository = new LibGit2SharpRepository(gitDirectory);

            return new RepositoryOverview(
                true,
                TrimSeparator(repository.Info.WorkingDirectory),
                DescribeHead(repository),
                CountChangedFiles(repository),
                [.. repository.Branches
                    .Where(branch => !IsRemoteHead(branch))
                    .Select(ToBranchInfo)
                    .OrderByDescending(branch => branch.IsCurrent)
                    .ThenBy(branch => branch.IsRemote)
                    .ThenBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)]);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return RepositoryOverview.NotARepository;
        }
    }

    private static string DescribeHead(LibGit2SharpRepository repository)
    {
        var head = repository.Head;

        if (repository.Info.IsHeadUnborn)
        {
            return $"{head.FriendlyName} (no commits yet)";
        }

        return repository.Info.IsHeadDetached
            ? $"detached at {head.Tip.Sha[..7]}"
            : head.FriendlyName;
    }

    private static int CountChangedFiles(LibGit2SharpRepository repository)
    {
        if (repository.Info.IsBare)
        {
            return 0;
        }

        // Kept deliberately cheap: rename detection and walking into untracked directories are the
        // expensive parts of a status, and neither changes the count the way git reports it.
        var status = repository.RetrieveStatus(new StatusOptions
        {
            IncludeIgnored = false,
            IncludeUntracked = true,
            RecurseUntrackedDirs = false,
            DetectRenamesInIndex = false,
            DetectRenamesInWorkDir = false,
        });

        return status.Count(entry =>
            entry.State != FileStatus.Unaltered && !entry.State.HasFlag(FileStatus.Ignored));
    }

    private static string? TrimSeparator(string? path) =>
        path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // Accepts a path anywhere inside the working tree, not just its root.
    private static string? TryDiscover(string? repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            return null;
        }

        try
        {
            return LibGit2SharpRepository.Discover(repositoryPath);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? PreferredRemoteName(LibGit2SharpRepository repository)
    {
        var remotes = repository.Network.Remotes.ToList();

        return remotes.FirstOrDefault(remote => remote.Name == "origin")?.Name
            ?? remotes.FirstOrDefault()?.Name;
    }

    private static BranchInfo ToBranchInfo(Branch branch) => new(
        branch.FriendlyName,
        branch.IsRemote,
        branch.IsRemote ? branch.RemoteName : null,
        branch.IsCurrentRepositoryHead);

    // "origin/HEAD" is a symbolic alias for the remote's default branch, not a branch of its own.
    private static bool IsRemoteHead(Branch branch) =>
        branch.IsRemote && branch.FriendlyName.EndsWith("/HEAD", StringComparison.Ordinal);

    private sealed record LocalCreation(CreateBranchResult Result, string? WorkingDirectory, string? RemoteName)
    {
        public static LocalCreation Failed(string errorMessage) => new(CreateBranchResult.Failed(errorMessage), null, null);
    }
}
