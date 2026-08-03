namespace GitDeck.App.Services;

public interface ISettingsWindowService
{
    /// <summary>Shows the settings window, restoring and focusing it if it is already open.</summary>
    void Show();
}
