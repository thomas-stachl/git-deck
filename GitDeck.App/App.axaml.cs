using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.App.Views.Run;
using GitDeck.App.Views.Settings;
using GitDeck.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App;

public partial class App : Application
{
    private readonly IServiceProvider? _services;
    private SettingsWindow? _settingsWindow;
    private RunWindow? _runWindow;
    private bool _isExitRequested;

    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public App() : this(null)
    {
    }

    public App(IServiceProvider? services)
    {
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Register the hotkeys before the settings window is built, so its view model starts
            // from the real registration state.
            RegisterGlobalHotkeys(_services);

            _settingsWindow = _services.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closing += OnSettingsWindowClosing;

            // Both windows are singletons that are shown and hidden, never recreated, so closing
            // one for real (Alt+F4 reaches the run window despite its lack of decorations) would
            // leave the next Show() throwing on a closed window.
            _runWindow = _services.GetRequiredService<RunWindow>();
            _runWindow.Closing += OnRunWindowClosing;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterGlobalHotkeys(IServiceProvider services)
    {
        var hotkeyService = services.GetRequiredService<IGlobalHotkeyService>();
        var settings = services.GetRequiredService<ISettingsService>().Settings;

        hotkeyService.Pressed += (_, e) =>
        {
            // A press can already be queued on the dispatcher while the tray exit tears windows down.
            if (_isExitRequested)
            {
                return;
            }

            services.GetRequiredService<IRunWindowService>().Toggle(ToRunMode(e.Action));
        };

        hotkeyService.Apply(HotkeyAction.Branches, Hotkeys.TryParse(settings.BranchHotkey));
        hotkeyService.Apply(HotkeyAction.Commit, Hotkeys.TryParse(settings.CommitHotkey));
    }

    private static RunMode ToRunMode(HotkeyAction action) => action switch
    {
        HotkeyAction.Commit => RunMode.Commit,
        _ => RunMode.Branches,
    };

    private void OnSettingsWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        // Keep the process alive in the tray and hide the window on close.
        e.Cancel = true;
        _settingsWindow?.Hide();
    }

    private void OnRunWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;
        _runWindow?.HideAndReset();
    }

    private void OnTrayShowClicked(object? sender, EventArgs e)
    {
        _services?.GetRequiredService<ISettingsWindowService>().Show();
    }

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        _isExitRequested = true;

        _runWindow?.Close();
        _settingsWindow?.Close();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}