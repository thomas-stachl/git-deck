using GitDeck.App.ViewModels;
using GitDeck.App.Views.Run;

namespace GitDeck.App.Services;

public class RunWindowService(RunWindow runWindow) : IRunWindowService
{
    public void Toggle(RunMode mode, string? repositoryPathOverride = null)
    {
        if (runWindow.VisibleMode == mode)
        {
            runWindow.HideAndReset();
        }
        else
        {
            runWindow.ShowNearTop(mode, repositoryPathOverride);
        }
    }
}
