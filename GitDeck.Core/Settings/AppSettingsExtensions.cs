namespace GitDeck.Core.Settings;

public static class AppSettingsExtensions
{
    /// <summary>
    /// Records <paramref name="path"/> as the most recently used repository: moved to the front if
    /// already present, otherwise inserted there, and the list trimmed to
    /// <see cref="AppSettings.MaxRecentRepositoryPaths"/>. Does not save — callers decide when.
    /// </summary>
    public static void RecordRecentRepositoryPath(this AppSettings settings, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var recent = settings.RecentRepositoryPaths;

        recent.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, path);

        if (recent.Count > AppSettings.MaxRecentRepositoryPaths)
        {
            recent.RemoveRange(AppSettings.MaxRecentRepositoryPaths, recent.Count - AppSettings.MaxRecentRepositoryPaths);
        }
    }
}
