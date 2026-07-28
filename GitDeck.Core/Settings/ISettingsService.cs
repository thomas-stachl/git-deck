namespace GitDeck.Core.Settings;

public interface ISettingsService
{
    AppSettings Settings { get; }

    void Save();
}
