using GitDeck.Git.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Design;

internal sealed class DesignBranchService : IBranchService
{
    private static readonly BranchInfo[] Branches =
    [
        new("main", false, null, true),
        new("develop", false, null, false),
        new("feature/run-window-suggestions", false, null, false),
        new("origin/main", true, "origin", false),
        new("origin/develop", true, "origin", false),
        new("origin/feature/settings-di", true, "origin", false),
    ];

    private static readonly ChangedFile[] ChangedFiles =
    [
        new("GitDeck.App/ViewModels/RunViewModel.cs", FileChangeKind.Modified, false),
        new("GitDeck.App/Views/Run/RunWindow.axaml", FileChangeKind.Modified, false),
        new("GitDeck.App/ViewModels/CommitPaletteViewModel.cs", FileChangeKind.Untracked, true),
    ];

    public Task<RepositoryOverview> GetOverviewAsync(string? repositoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult(new RepositoryOverview(true, @"C:\Repos\GitDeck", "main", ChangedFiles, Branches));

    public bool IsValidBranchName(string branchName) => !string.IsNullOrWhiteSpace(branchName);

    public Task<CreateBranchResult> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CreateBranchResult(true, request.PublishToRemote, null));

    public Task<SwitchBranchResult> SwitchBranchAsync(SwitchBranchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SwitchBranchResult(true, request.Branch.IsRemote, null));
}
