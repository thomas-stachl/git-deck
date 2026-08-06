using System.IO.Pipes;
using Avalonia.Platform.Storage;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.Core.Settings;
using GitDeck.Git;
using GitDeck.Git.Repositories;
using GitDeck.Ipc;
using LibGit2Sharp;
using StreamJsonRpc;
using Xunit;

namespace GitDeck.Tests;

/// <summary>
/// Proves the named-pipe round trip end to end: a real <see cref="BranchService"/> against a real
/// scratch repository, served by a real <see cref="GitDeckIpcServer"/>, called by a real
/// <see cref="JsonRpc"/> client over an actual pipe — the same "spin up the real thing" style
/// <see cref="SettingsServiceTests"/> already uses, rather than a mocking library.
/// </summary>
public sealed class GitDeckIpcTests : IDisposable
{
    private readonly string _repositoryPath =
        Path.Combine(Path.GetTempPath(), "GitDeckTests", Guid.NewGuid().ToString("N"));

    public GitDeckIpcTests()
    {
        Repository.Init(_repositoryPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_repositoryPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task GetStatusAsync_RoundTripsOverNamedPipe()
    {
        var target = new GitDeckIpc(
            new BranchService(new UnusedGitExecutableService()),
            new UnusedRunWindowService(),
            new UnusedFilePickerService(),
            new InMemorySettingsService());

        var pipeName = $"GitDeckTests-{Guid.NewGuid():N}";
        using var server = new GitDeckIpcServer(target, pipeName);

        await using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientStream.ConnectAsync(5000);

        // Constructed explicitly with the same formatter GitDeckIpcServer uses, rather than the
        // static JsonRpc.Attach convenience overload's default formatter — the point of this test is
        // to prove client and server agree on wire format, not to rely on both defaulting the same way.
        var handler = new HeaderDelimitedMessageHandler(clientStream, clientStream, new SystemTextJsonFormatter());
        using var rpc = new JsonRpc(handler);
        var proxy = rpc.Attach<IGitDeckIpc>();
        rpc.StartListening();

        var overview = await proxy.GetStatusAsync(_repositoryPath, CancellationToken.None);

        Assert.True(overview.IsRepository);
        Assert.Equal(_repositoryPath.TrimEnd(Path.DirectorySeparatorChar), overview.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar));
    }

    // Plain hand-written stubs, not a mocking library — consistent with this repo's convention.
    // GetStatusAsync only reaches IBranchService.GetOverviewAsync, which touches none of these.

    private sealed class UnusedGitExecutableService : IGitExecutableService
    {
        public Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedRunWindowService : IRunWindowService
    {
        public void Toggle(RunMode mode, string? repositoryPathOverride = null) => throw new NotImplementedException();
    }

    private sealed class UnusedFilePickerService : IFilePickerService
    {
        public Task<string?> PickFolderAsync(string title) => throw new NotImplementedException();

        public Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter = null) =>
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
