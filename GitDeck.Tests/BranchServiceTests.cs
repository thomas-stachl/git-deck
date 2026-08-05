using GitDeck.Git;
using GitDeck.Git.Repositories;
using LibGit2Sharp;
using Xunit;

namespace GitDeck.Tests;

/// <summary>
/// Covers the preflight error paths of <see cref="BranchService.PushCurrentBranchAsync"/> that
/// don't need a real remote to exercise — same scratch-repo, no-mocks style as the rest of this
/// project's tests. The success paths (plain push, publish-on-first-push) would need a real
/// network remote to verify end to end, which nothing else in this test project attempts for
/// Fetch/Pull either.
/// </summary>
public sealed class BranchServiceTests : IDisposable
{
    private readonly string _repositoryPath =
        Path.Combine(Path.GetTempPath(), "GitDeckTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        // Only the tests that actually commit hit this: LibGit2Sharp can leave a native
        // memory-mapped handle on a loose object file open slightly past Repository's own Dispose
        // on Windows, and Directory.Delete right after can lose that race. A short retry clears it
        // without masking a real failure — the first attempt succeeds the overwhelming majority of
        // the time.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_repositoryPath, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    [Fact]
    public async Task PushCurrentBranchAsync_WithNoRemote_FailsCleanly()
    {
        Repository.Init(_repositoryPath);
        Commit(_repositoryPath);

        var branchService = new BranchService(new UnusedGitExecutableService());

        var result = await branchService.PushCurrentBranchAsync(_repositoryPath, CancellationToken.None);

        Assert.False(result.IsPushed);
        Assert.False(result.DidPublish);
        Assert.Equal("This repository has no remote to push to.", result.ErrorMessage);
    }

    [Fact]
    public async Task PushCurrentBranchAsync_WithNoCommits_FailsCleanly()
    {
        Repository.Init(_repositoryPath);

        var branchService = new BranchService(new UnusedGitExecutableService());

        var result = await branchService.PushCurrentBranchAsync(_repositoryPath, CancellationToken.None);

        Assert.False(result.IsPushed);
        Assert.Equal("The repository has no commits yet, so there is nothing to push.", result.ErrorMessage);
    }

    [Fact]
    public async Task PushCurrentBranchAsync_WithNoRepository_FailsCleanly()
    {
        var branchService = new BranchService(new UnusedGitExecutableService());

        var result = await branchService.PushCurrentBranchAsync(_repositoryPath, CancellationToken.None);

        Assert.False(result.IsPushed);
        Assert.Equal("No repository found. Check the repository path in Settings.", result.ErrorMessage);
    }

    private static void Commit(string repositoryPath)
    {
        File.WriteAllText(Path.Combine(repositoryPath, "file.txt"), "content");

        using var repository = new Repository(repositoryPath);
        Commands.Stage(repository, "file.txt");

        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        repository.Commit("Initial commit", signature, signature);
    }

    // Never reached: every case above fails during preflight, before any git.exe invocation.
    private sealed class UnusedGitExecutableService : IGitExecutableService
    {
        public Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
