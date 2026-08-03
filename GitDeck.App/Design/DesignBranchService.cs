using GitDeck.Git.Repositories;
using System.Collections.Generic;
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

    public Task<BranchListing> GetBranchesAsync(string? repositoryPath, CancellationToken cancellationToken = default)
        => Task.FromResult(new BranchListing(true, Branches));

    public bool IsValidBranchName(string branchName) => !string.IsNullOrWhiteSpace(branchName);

    public Task<CreateBranchResult> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CreateBranchResult(true, request.PublishToRemote, null));

    public Task<SwitchBranchResult> SwitchBranchAsync(SwitchBranchRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SwitchBranchResult(true, request.Branch.IsRemote, null));
}
