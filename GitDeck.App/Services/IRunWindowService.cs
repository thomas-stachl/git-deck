using GitDeck.App.ViewModels;

namespace GitDeck.App.Services;

public interface IRunWindowService
{
    /// <summary>
    /// Shows the run window in <paramref name="mode"/>. Pressing the same mode's hotkey again
    /// dismisses it; pressing the other mode's hotkey switches to that mode instead.
    /// </summary>
    void Toggle(RunMode mode);
}
