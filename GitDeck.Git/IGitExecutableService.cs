using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.Git;

public interface IGitExecutableService
{
    Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the git executable with the given arguments. Used for operations that need the user's
    /// existing git configuration — credential helpers, SSH agent, hooks — which LibGit2Sharp
    /// cannot reproduce.
    /// </summary>
    Task<GitCommandResult> RunAsync(
        string? gitPath,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
