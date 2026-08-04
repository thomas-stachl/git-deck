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
    /// cannot reproduce. The executable path comes from settings, so callers never carry it around.
    /// </summary>
    /// <param name="timeout">
    /// Time budget before the process is killed and the result reports <c>TimedOut</c>. Null means
    /// the service default.
    /// </param>
    Task<GitCommandResult> RunAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
