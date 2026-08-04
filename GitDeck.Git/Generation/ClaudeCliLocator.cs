namespace GitDeck.Git.Generation;

/// <summary>
/// Finds the Claude Code executable, so using it needs no configuration at all.
/// </summary>
public static class ClaudeCliLocator
{
    private static readonly string[] ExecutableNames =
        OperatingSystem.IsWindows() ? ["claude.exe", "claude.cmd", "claude.bat"] : ["claude"];

    /// <summary>The path to the executable, or null when Claude Code is not installed.</summary>
    // Deliberately uncached: the search is a handful of File.Exists probes, and the cache it used
    // to have was unsynchronised static state whose invalidation hook nothing ever called — so an
    // install made mid-session was never picked up.
    public static string? Find() => Search();

    private static string? Search()
    {
        foreach (var directory in SearchDirectories())
        {
            foreach (var name in ExecutableNames)
            {
                try
                {
                    var candidate = Path.Combine(directory, name);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry — skip it rather than failing the whole search.
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> SearchDirectories()
    {
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return entry;
        }

        // Claude Code's own install location. A tray app started at login can inherit a PATH that
        // predates the install, so this is checked even when PATH misses it.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (home.Length > 0)
        {
            yield return Path.Combine(home, ".local", "bin");
        }
    }
}
