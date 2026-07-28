using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.Git;

public interface IGitExecutableService
{
    Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default);
}
