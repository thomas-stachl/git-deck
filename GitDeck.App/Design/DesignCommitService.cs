using GitDeck.Git.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Design;

internal sealed class DesignCommitService : ICommitService
{
    public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CommitResult(true, null));
}
