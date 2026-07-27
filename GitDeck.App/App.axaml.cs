using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitDeck.App.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App;

public partial class App(IServiceProvider services) : Application
{
    private SettingsWindow? _settingsWindow;
    private bool _isExitRequested;


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        DataTemplates.Add(services.GetRequiredService<ViewLocator>());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settingsWindow = services.GetRequiredService<SettingsWindow>();

            _settingsWindow.Closing += OnSettingsWindowClosing;
        }

        base.OnFrameworkInitializationCompleted();
    }

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
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
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