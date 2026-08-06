using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.Core.Settings;
using GitDeck.Git;
using GitDeck.Git.Repositories;
using LibGit2Sharp;
using Xunit;

namespace GitDeck.Tests;

/// <summary>
/// Regression coverage for the repo-override plumbing: a Stream Deck key opening the palette for
/// its own configured repo must not silently fall back to <c>Settings.RepositoryPath</c>.
/// <see cref="RunViewModel"/>, <see cref="ViewModels.BranchPaletteViewModel"/> and
/// <see cref="ViewModels.CommitPaletteViewModel"/> have no Avalonia dependency (only the
/// <c>RunWindow</c> view does), so they can be constructed directly against real scratch repos.
/// </summary>
public sealed class RunViewModelRepositoryOverrideTests : IDisposable
{
    private readonly string _repositoryA =
        Path.Combine(Path.GetTempPath(), "GitDeckTests", Guid.NewGuid().ToString("N"));

    private readonly string _repositoryB =
        Path.Combine(Path.GetTempPath(), "GitDeckTests", Guid.NewGuid().ToString("N"));

    public RunViewModelRepositoryOverrideTests()
    {
        Repository.Init(_repositoryA);
        Repository.Init(_repositoryB);
    }

    public void Dispose()
    {
        foreach (var path in new[] { _repositoryA, _repositoryB })
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task OpenAsync_WithRepositoryPathOverride_ReadsOverrideInsteadOfConfiguredRepository()
    {
        var settingsService = new InMemorySettingsService();
        settingsService.Settings.RepositoryPath = _repositoryA;

        var viewModel = new RunViewModel(
            settingsService,
            new BranchService(new SilentlyFailingGitExecutableService()),
            new UnusedCommitService(),
            new DisabledCommitMessageService());

        await viewModel.OpenAsync(RunMode.Branches, repositoryPathOverride: _repositoryB);

        // ShortenPath only ever trims from the front of a path, so the leaf folder name (the part
        // that actually distinguishes repo A from repo B here) always survives intact at the end of
        // the displayed value, shortened or not.
        Assert.EndsWith(Path.GetFileName(_repositoryB), viewModel.RepositoryPathDisplay);
        Assert.DoesNotContain(Path.GetFileName(_repositoryA), viewModel.RepositoryPathDisplay);
    }

    [Fact]
    public async Task OpenAsync_WithoutRepositoryPathOverride_FallsBackToConfiguredRepository()
    {
        var settingsService = new InMemorySettingsService();
        settingsService.Settings.RepositoryPath = _repositoryA;

        var viewModel = new RunViewModel(
            settingsService,
            new BranchService(new SilentlyFailingGitExecutableService()),
            new UnusedCommitService(),
            new DisabledCommitMessageService());

        await viewModel.OpenAsync(RunMode.Branches);

        Assert.EndsWith(Path.GetFileName(_repositoryA), viewModel.RepositoryPathDisplay);
    }

    // Plain hand-written stubs, not a mocking library — consistent with this repo's convention.

    /// <summary>
    /// Returns ordinary failed results instead of throwing: LoadRepositoryAsync fires a background
    /// fetch (RefreshAfterFetchAsync) that this repo has nothing real to fetch from, and a graceful
    /// failure keeps that fire-and-forget path quiet instead of surfacing an unobserved exception.
    /// </summary>
    private sealed class SilentlyFailingGitExecutableService : IGitExecutableService
    {
        public Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GitAvailability(false, null));

        public Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GitCommandResult(false, 1, string.Empty, "not available in tests"));
    }

    private sealed class UnusedCommitService : ICommitService
    {
        public Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class DisabledCommitMessageService : ICommitMessageService
    {
        public bool IsEnabled => false;

        public Task<GeneratedCommitMessage> GenerateAsync(string workingDirectory, IReadOnlyList<ChangedFile> files, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();

        public void Save()
        {
        }
    }
}
