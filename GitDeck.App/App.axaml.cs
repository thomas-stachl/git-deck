using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.App.Views.Settings;
using GitDeck.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App;

public partial class App : Application
{
    private readonly IServiceProvider? _services;
    private SettingsWindow? _settingsWindow;
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

        if (_services is not null)
        {
            DataTemplates.Add(_services.GetRequiredService<ViewLocator>());
        }
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
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterGlobalHotkeys(IServiceProvider services)
    {
        var hotkeyService = services.GetRequiredService<IGlobalHotkeyService>();
        var settings = services.GetRequiredService<ISettingsService>().Settings;

        hotkeyService.Pressed += (_, e) => services
            .GetRequiredService<IRunWindowService>()
            .Toggle(ToRunMode(e.Action));

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

    private void OnTrayShowClicked(object? sender, EventArgs e)
    {
        _services?.GetRequiredService<ISettingsWindowService>().Show();
    }

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        _isExitRequested = true;

        _settingsWindow?.Close();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}