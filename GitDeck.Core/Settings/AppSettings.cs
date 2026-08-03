namespace GitDeck.Core.Settings;

public class AppSettings
{
    public string? RepositoryPath { get; set; }

    public string? GitExecutablePath { get; set; }

    /// <summary>
    /// The system-wide hotkey that opens the run window for switching branches, as a gesture such as
    /// "Ctrl+Alt+G". Null means no hotkey. Absent from settings.json means the default below.
    /// </summary>
    public string? BranchHotkey { get; set; } = DefaultBranchHotkey;

    /// <summary>
    /// The system-wide hotkey that opens the run window for committing changes.
    /// </summary>
    public string? CommitHotkey { get; set; } = DefaultCommitHotkey;

    /// <summary>Commit message generation. Disabled until the user configures a provider.</summary>
    public AiSettings Ai { get; set; } = new();

    public const string DefaultBranchHotkey = "Ctrl+Alt+G";

    public const string DefaultCommitHotkey = "Ctrl+Alt+C";

    /// <summary>
    /// When set, creating a branch also pushes it to the remote and sets it as upstream
    /// (the equivalent of <c>git push --set-upstream</c> after <c>git switch -c</c>).
    /// </summary>
    public bool PublishNewBranchesToRemote { get; set; }
}
