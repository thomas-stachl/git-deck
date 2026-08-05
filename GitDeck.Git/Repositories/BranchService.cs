using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitDeck.Git.Repositories;

/// <summary>
/// Reads run through LibGit2Sharp, which is fast and needs no configuration. Mutations — switching
/// and creating branches — run through git.exe instead: libgit2 checkouts skip hooks and
/// clean/smudge filters, which writes pointer files into an LFS working tree.
/// </summary>
public sealed class BranchService(
    IGitExecutableService gitExecutableService,
    ILogger<BranchService>? logger = null) : IBranchService
{
    private readonly ILogger<BranchService> _logger = logger ?? NullLogger<BranchService>.Instance;

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

        var preflight = await Task.Run(() => PrepareCreate(request), cancellationToken).ConfigureAwait(false);

        if (preflight.Error is not null)
        {
            return CreateBranchResult.Failed(preflight.Error);
        }

        var create = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            ["switch", "-c", request.BranchName],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!create.IsSuccess)
        {
            return CreateBranchResult.Failed(create.FailureMessage ?? "Could not create the branch.");
        }

        if (!request.PublishToRemote)
        {
            return new CreateBranchResult(true, false, null);
        }

        if (preflight.RemoteName is null)
        {
            return new CreateBranchResult(true, false, "Created locally. The repository has no remote to publish to.");
        }

        var push = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            ["push", "--set-upstream", preflight.RemoteName, request.BranchName],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return push.IsSuccess
            ? new CreateBranchResult(true, true, null)
            : new CreateBranchResult(true, false, $"Created locally, but publishing failed: {push.FailureMessage}");
    }

    public async Task<SwitchBranchResult> SwitchBranchAsync(SwitchBranchRequest request, CancellationToken cancellationToken = default)
    {
        var preflight = await Task.Run(() => PrepareSwitch(request), cancellationToken).ConfigureAwait(false);

        if (preflight.Error is not null)
        {
            return SwitchBranchResult.Failed(preflight.Error);
        }

        // --track pins the new local branch to the exact remote branch that was picked in the list,
        // instead of leaving `git switch` to guess between remotes that share the name.
        string[] arguments = preflight.CreatesLocalBranch
            ? ["switch", "--track", request.Branch.Name]
            : ["switch", preflight.TargetName!];

        var result = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            arguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new SwitchBranchResult(true, preflight.CreatesLocalBranch, null)
            : SwitchBranchResult.Failed(result.FailureMessage ?? "Could not switch branches.");
    }

    public async Task<FetchResult> FetchAsync(string? repositoryPath, CancellationToken cancellationToken = default)
    {
        var preflight = await Task.Run(
            () => PrepareWorkingDirectory(repositoryPath, "This repository has no working tree to fetch into."),
            cancellationToken).ConfigureAwait(false);

        if (preflight.Error is not null)
        {
            return FetchResult.Failed(preflight.Error);
        }

        var fetch = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            ["fetch", "--prune"],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return fetch.IsSuccess
            ? new FetchResult(true, null)
            : FetchResult.Failed(fetch.FailureMessage ?? "Fetch failed.");
    }

    public async Task<PullResult> PullCurrentBranchAsync(string? repositoryPath, CancellationToken cancellationToken = default)
    {
        var preflight = await Task.Run(
            () => PrepareWorkingDirectory(repositoryPath, "This repository has no working tree to pull into."),
            cancellationToken).ConfigureAwait(false);

        if (preflight.Error is not null)
        {
            return PullResult.Failed(preflight.Error);
        }

        // --ff-only, deliberately: a merge or rebase is a judgment call this palette should not make
        // on its own. Diverged history surfaces as a clean failure instead.
        var pull = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            ["pull", "--ff-only"],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return pull.IsSuccess
            ? new PullResult(true, null)
            : PullResult.Failed(pull.FailureMessage ?? "Could not pull the latest changes.");
    }

    public async Task<PushResult> PushCurrentBranchAsync(string? repositoryPath, CancellationToken cancellationToken = default)
    {
        var preflight = await Task.Run(() => PreparePush(repositoryPath), cancellationToken).ConfigureAwait(false);

        if (preflight.Error is not null)
        {
            return PushResult.Failed(preflight.Error);
        }

        string[] arguments = preflight.NeedsPublish
            ? ["push", "--set-upstream", preflight.RemoteName!, preflight.BranchName!]
            : ["push"];

        var push = await gitExecutableService.RunAsync(
            preflight.WorkingDirectory,
            arguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return push.IsSuccess
            ? new PushResult(true, preflight.NeedsPublish, null)
            : PushResult.Failed(push.FailureMessage ?? "Could not push the current branch.");
    }

    private sealed record WorkingDirectoryPreflight(string? Error, string? WorkingDirectory = null);

    private static WorkingDirectoryPreflight PrepareWorkingDirectory(string? repositoryPath, string noWorkingTreeMessage)
    {
        var gitDirectory = TryDiscover(repositoryPath);
        if (gitDirectory is null)
        {
            return new WorkingDirectoryPreflight("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new Repository(gitDirectory);

            return repository.Info.IsBare
                ? new WorkingDirectoryPreflight(noWorkingTreeMessage)
                : new WorkingDirectoryPreflight(null, repository.Info.WorkingDirectory);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new WorkingDirectoryPreflight(ex.Message);
        }
    }

    private sealed record SwitchPreflight(
        string? Error,
        string? WorkingDirectory = null,
        string? TargetName = null,
        bool CreatesLocalBranch = false);

    private static SwitchPreflight PrepareSwitch(SwitchBranchRequest request)
    {
        var gitDirectory = TryDiscover(request.RepositoryPath);
        if (gitDirectory is null)
        {
            return new SwitchPreflight("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new Repository(gitDirectory);

            if (repository.Info.IsBare)
            {
                return new SwitchPreflight("Cannot switch branches in a bare repository.");
            }

            var workingDirectory = repository.Info.WorkingDirectory;

            if (!request.Branch.IsRemote)
            {
                return repository.Branches[request.Branch.Name] is null
                    ? new SwitchPreflight($"Branch \"{request.Branch.Name}\" no longer exists.")
                    : new SwitchPreflight(null, workingDirectory, request.Branch.Name);
            }

            // A local branch of the same short name is switched to rather than failing, matching
            // what `git switch <name>` would do.
            var localName = request.Branch.ShortName;

            return repository.Branches[localName] is { IsRemote: false }
                ? new SwitchPreflight(null, workingDirectory, localName)
                : new SwitchPreflight(null, workingDirectory, localName, CreatesLocalBranch: true);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new SwitchPreflight(ex.Message);
        }
    }

    private sealed record PushPreflight(
        string? Error,
        string? WorkingDirectory = null,
        string? BranchName = null,
        string? RemoteName = null,
        bool NeedsPublish = false);

    private static PushPreflight PreparePush(string? repositoryPath)
    {
        var gitDirectory = TryDiscover(repositoryPath);
        if (gitDirectory is null)
        {
            return new PushPreflight("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new Repository(gitDirectory);

            if (repository.Info.IsBare)
            {
                return new PushPreflight("Cannot push from a bare repository.");
            }

            if (repository.Info.IsHeadUnborn)
            {
                return new PushPreflight("The repository has no commits yet, so there is nothing to push.");
            }

            if (repository.Info.IsHeadDetached)
            {
                return new PushPreflight("Cannot push a detached HEAD. Switch to a branch first.");
            }

            var head = repository.Head;
            var workingDirectory = repository.Info.WorkingDirectory;

            if (head.IsTracking)
            {
                return new PushPreflight(null, workingDirectory, head.FriendlyName);
            }

            // No upstream yet: publish to whichever remote CreateBranchAsync would have used.
            var remoteName = PreferredRemoteName(repository);

            return remoteName is null
                ? new PushPreflight("This repository has no remote to push to.")
                : new PushPreflight(null, workingDirectory, head.FriendlyName, remoteName, NeedsPublish: true);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new PushPreflight(ex.Message);
        }
    }

    private sealed record CreatePreflight(string? Error, string? WorkingDirectory = null, string? RemoteName = null);

    private static CreatePreflight PrepareCreate(CreateBranchRequest request)
    {
        var gitDirectory = TryDiscover(request.RepositoryPath);
        if (gitDirectory is null)
        {
            return new CreatePreflight("No repository found. Check the repository path in Settings.");
        }

        try
        {
            using var repository = new Repository(gitDirectory);

            if (repository.Info.IsBare)
            {
                return new CreatePreflight("Cannot create a branch in a bare repository.");
            }

            if (repository.Info.IsHeadUnborn)
            {
                return new CreatePreflight("The repository has no commits yet, so there is nothing to branch from.");
            }

            if (repository.Branches[request.BranchName] is not null)
            {
                return new CreatePreflight($"Branch \"{request.BranchName}\" already exists.");
            }

            return new CreatePreflight(null, repository.Info.WorkingDirectory, PreferredRemoteName(repository));
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new CreatePreflight(ex.Message);
        }
    }

    private RepositoryOverview GetOverview(string? repositoryPath)
    {
        var gitDirectory = TryDiscover(repositoryPath);
        if (gitDirectory is null)
        {
            return RepositoryOverview.NotARepository;
        }

        try
        {
            using var repository = new Repository(gitDirectory);

            var tracking = DescribeTracking(repository);

            return new RepositoryOverview(
                true,
                TrimSeparator(repository.Info.WorkingDirectory),
                DescribeHead(repository),
                GetChangedFiles(repository),
                [.. repository.Branches
                    .Where(branch => !IsRemoteHead(branch))
                    .Select(ToBranchInfo)
                    .OrderByDescending(branch => branch.IsCurrent)
                    .ThenBy(branch => branch.IsRemote)
                    .ThenBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)],
                HasUpstream: tracking.HasUpstream,
                AheadBy: tracking.AheadBy,
                BehindBy: tracking.BehindBy);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A permission problem or corrupt index is not "not a repository" — carry the reason
            // so the UI can show it instead of a misleading default.
            _logger.LogError(ex, "Could not read the repository at {Path}.", repositoryPath);
            return RepositoryOverview.Failed(ex.Message);
        }
    }

    private static string DescribeHead(Repository repository)
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

    /// <summary>
    /// How far the current branch is from its upstream. This reads the local remote-tracking ref —
    /// it reflects whatever was fetched last, not a live network check.
    /// </summary>
    private static (bool HasUpstream, int AheadBy, int BehindBy) DescribeTracking(Repository repository)
    {
        if (repository.Info.IsBare || repository.Info.IsHeadUnborn || repository.Info.IsHeadDetached)
        {
            return (false, 0, 0);
        }

        var head = repository.Head;

        return head.IsTracking
            ? (true, head.TrackingDetails.AheadBy ?? 0, head.TrackingDetails.BehindBy ?? 0)
            : (false, 0, 0);
    }

    private static IReadOnlyList<ChangedFile> GetChangedFiles(Repository repository)
    {
        if (repository.Info.IsBare)
        {
            return [];
        }

        // Rename detection stays off — it is the expensive part of a status and does not change
        // what git itself reports by default. Untracked directories are walked, though: the commit
        // stages files, so every file has to be listed and tickable individually rather than
        // hidden behind one "dir/" entry.
        var status = repository.RetrieveStatus(new StatusOptions
        {
            IncludeIgnored = false,
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
            DetectRenamesInIndex = false,
            DetectRenamesInWorkDir = false,
        });

        return [.. status
            .Where(entry => entry.State != FileStatus.Unaltered && !entry.State.HasFlag(FileStatus.Ignored))
            .Select(entry => new ChangedFile(
                entry.FilePath,
                ToChangeKind(entry.State),
                entry.State.HasFlag(FileStatus.NewInWorkdir)))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Reduces the status flags to the one that best describes the file. Order matters: a path can
    /// carry both index and working-tree flags, and the more significant one should win.
    /// </summary>
    private static FileChangeKind ToChangeKind(FileStatus state)
    {
        if (state.HasFlag(FileStatus.Conflicted))
        {
            return FileChangeKind.Conflicted;
        }

        if (state.HasFlag(FileStatus.NewInWorkdir))
        {
            return FileChangeKind.Untracked;
        }

        if (state.HasFlag(FileStatus.DeletedFromWorkdir) || state.HasFlag(FileStatus.DeletedFromIndex))
        {
            return FileChangeKind.Deleted;
        }

        if (state.HasFlag(FileStatus.RenamedInIndex) || state.HasFlag(FileStatus.RenamedInWorkdir))
        {
            return FileChangeKind.Renamed;
        }

        if (state.HasFlag(FileStatus.NewInIndex))
        {
            return FileChangeKind.Added;
        }

        if (state.HasFlag(FileStatus.TypeChangeInWorkdir) || state.HasFlag(FileStatus.TypeChangeInIndex))
        {
            return FileChangeKind.TypeChanged;
        }

        return FileChangeKind.Modified;
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
            return Repository.Discover(repositoryPath);
        }
        catch (Exception ex) when (ex is LibGit2SharpException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? PreferredRemoteName(Repository repository)
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
}
