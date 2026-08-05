using GitDeck.App.ViewModels;

namespace GitDeck.App.Services;

public interface IRunWindowService
{
    /// <summary>
    /// Shows the run window in <paramref name="mode"/>. Pressing the same mode's hotkey again
    /// dismisses it; pressing the other mode's hotkey switches to that mode instead.
    /// </summary>
    /// <param name="repositoryPathOverride">
    /// Repository to show instead of <c>Settings.RepositoryPath</c> — used by an IPC caller (a
    /// Stream Deck key) that targets its own configured repo. Null keeps today's behavior: the
    /// hotkey path never passes this.
    /// </param>
    void Toggle(RunMode mode, string? repositoryPathOverride = null);
}
