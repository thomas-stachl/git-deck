using Avalonia.Controls;
using GitDeck.App.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App.Services;

/// <remarks>
/// The window is resolved on demand rather than injected: the run window reaches settings, and the
/// settings window reaches the run window, so taking a constructor dependency here would close a
/// cycle the container cannot build.
/// </remarks>
public class SettingsWindowService(IServiceProvider services) : ISettingsWindowService
{
    public void Show()
    {
        var window = services.GetRequiredService<SettingsWindow>();

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }
}
