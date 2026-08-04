using GitDeck.Git;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Design;

internal sealed class DesignGitExecutableService : IGitExecutableService
{
    public Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new GitAvailability(true, "git version 2.44.0.windows.1"));

    public Task<GitCommandResult> RunAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GitCommandResult(true, 0, string.Empty, string.Empty));
}
