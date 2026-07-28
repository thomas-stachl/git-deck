using GitDeck.Core.Settings;

namespace GitDeck.App.Design;

internal sealed class DesignSettingsService : ISettingsService
{
    public AppSettings Settings { get; } = new()
    {
        RepositoryPath = @"C:\Repos\GitDeck",
        GitExecutablePath = null,
    };

    public void Save()
    {
    }
}
