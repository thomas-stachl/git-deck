namespace GitDeck.Core.Settings;

public class AppSettings
{
    public string? RepositoryPath { get; set; }

    public string? GitExecutablePath { get; set; }

    /// <summary>
    /// When set, creating a branch also pushes it to the remote and sets it as upstream
    /// (the equivalent of <c>git push --set-upstream</c> after <c>git switch -c</c>).
    /// </summary>
    public bool PublishNewBranchesToRemote { get; set; }
}
