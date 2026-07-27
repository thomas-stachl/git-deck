using GitDeck.App.Views.Run;

namespace GitDeck.App.Services;

public class RunWindowService(RunWindow runWindow)
{
    public void Toggle()
    {
        if (runWindow.IsVisible)
        {
            runWindow.Hide();
        }
        else
        {
            runWindow.ShowNearTop();
        }
    }
}
