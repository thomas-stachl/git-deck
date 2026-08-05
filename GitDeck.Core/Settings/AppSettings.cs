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

    private AiSettings _ai = new();

    /// <summary>Commit message generation. Disabled until the user configures a provider.</summary>
    /// <remarks>
    /// The setter tolerates null because the deserializer is not bound by the nullability
    /// annotation: a hand-edited <c>"Ai": null</c> in settings.json would otherwise put a null
    /// behind a non-nullable property and crash every consumer.
    /// </remarks>
    public AiSettings Ai
    {
        get => _ai;
        set => _ai = value ?? new AiSettings();
    }

    public const string DefaultBranchHotkey = "Ctrl+Alt+G";

    public const string DefaultCommitHotkey = "Ctrl+Alt+C";

    /// <summary>
    /// When set, creating a branch also pushes it to the remote and sets it as upstream
    /// (the equivalent of <c>git push --set-upstream</c> after <c>git switch -c</c>).
    /// </summary>
    public bool PublishNewBranchesToRemote { get; set; }

    private List<string> _recentRepositoryPaths = [];

    /// <summary>
    /// Repository paths used before, newest first, capped at <see cref="MaxRecentRepositoryPaths"/>.
    /// Backs the Stream Deck Property Inspector's repo dropdown.
    /// </summary>
    /// <remarks>
    /// The setter tolerates null for the same reason <see cref="Ai"/>'s does: the deserializer is not
    /// bound by the nullability annotation, so a hand-edited <c>"RecentRepositoryPaths": null</c> in
    /// settings.json would otherwise put a null behind every consumer that expects a list.
    /// </remarks>
    public List<string> RecentRepositoryPaths
    {
        get => _recentRepositoryPaths;
        set => _recentRepositoryPaths = value ?? [];
    }

    public const int MaxRecentRepositoryPaths = 10;
}
