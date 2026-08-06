using Avalonia.Threading;
using GitDeck.App.ViewModels;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;
using GitDeck.Ipc;

namespace GitDeck.App.Services;

/// <summary>
/// Implements <see cref="IGitDeckIpc"/> by composing the same singletons the hotkey path already
/// uses — no duplicated git logic, no second copy of "is an operation already in flight".
/// </summary>
/// <remarks>
/// <see cref="GetStatusAsync"/>/<see cref="FetchAsync"/>/<see cref="PullAsync"/>/
/// <see cref="GetRecentRepositoriesAsync"/> touch no Avalonia API and run directly on whatever
/// thread StreamJsonRpc dispatches the call on. <see cref="OpenBranchesAsync"/>,
/// <see cref="OpenCommitAsync"/> and <see cref="PickRepositoryFolderAsync"/> touch
/// <c>Window</c>/<c>StorageProvider</c> APIs and marshal onto <see cref="Dispatcher.UIThread"/>,
/// the same way <see cref="WindowsGlobalHotkeyService"/> already does for its Win32 message-loop
/// thread.
/// </remarks>
public sealed class GitDeckIpc(
    IBranchService branchService,
    IRunWindowService runWindowService,
    IFilePickerService filePickerService,
    ISettingsService settingsService) : IGitDeckIpc
{
    public async Task<RepositoryOverview> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var overview = await branchService.GetOverviewAsync(repositoryPath, cancellationToken);

        if (overview.IsRepository)
        {
            // A key polling its own status is itself "a successful path resolution" — this is what
            // keeps the MRU current for a repo a key was configured with directly (typed or pasted
            // into the Property Inspector), not only ones picked through Browse….
            RecordRecentRepositoryPath(repositoryPath);
        }

        return overview;
    }

    public Task<FetchResult> FetchAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => branchService.FetchAsync(repositoryPath, cancellationToken);

    public Task<PullResult> PullAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => branchService.PullCurrentBranchAsync(repositoryPath, cancellationToken);

    public Task<PushResult> PushAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => branchService.PushCurrentBranchAsync(repositoryPath, cancellationToken);

    public Task OpenBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        // Fire-and-forget the palette open, same as the hotkey path: RunWindow.ShowNearTop already
        // discards viewModel.OpenAsync's task, so there is nothing meaningful to await here either.
        Dispatcher.UIThread.Post(() => runWindowService.Toggle(RunMode.Branches, repositoryPath));
        return Task.CompletedTask;
    }

    public Task OpenCommitAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() => runWindowService.Toggle(RunMode.Commit, repositoryPath));
        return Task.CompletedTask;
    }

    public Task<string?> PickRepositoryFolderAsync(CancellationToken cancellationToken = default)
    {
        // Unlike the two calls above, a caller here genuinely needs the picked path back, so the
        // UI-thread work has to be awaited rather than fired-and-forgotten. Dispatcher.UIThread.Post
        // only accepts a plain Action, so a TaskCompletionSource carries the result (and any
        // exception) back to this thread once the picker's async work finishes.
        var completion = new TaskCompletionSource<string?>();

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var path = await filePickerService.PickFolderAsync("Choose a repository");

                if (path is not null)
                {
                    RecordRecentRepositoryPath(path);
                }

                completion.SetResult(path);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }

    public Task<IReadOnlyList<string>> GetRecentRepositoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(settingsService.Settings.RecentRepositoryPaths);

    private void RecordRecentRepositoryPath(string path)
    {
        settingsService.Settings.RecordRecentRepositoryPath(path);
        settingsService.Save();
    }
}
