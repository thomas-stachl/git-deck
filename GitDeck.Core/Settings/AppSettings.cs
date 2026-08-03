namespace GitDeck.Core.Settings;

public class AppSettings
{
    public string? RepositoryPath { get; set; }

    public string? GitExecutablePath { get; set; }

    /// <summary>
    /// The system-wide hotkey that shows the run window, as a gesture such as "Ctrl+Alt+G".
    /// Null means no hotkey. Absent from settings.json means the default below.
    /// </summary>
    public string? Hotkey { get; set; } = DefaultHotkey;

    public const string DefaultHotkey = "Ctrl+Alt+G";

    /// <summary>
    /// When set, creating a branch also pushes it to the remote and sets it as upstream
    /// (the equivalent of <c>git push --set-upstream</c> after <c>git switch -c</c>).
    /// </summary>
    public bool PublishNewBranchesToRemote { get; set; }
}
