using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitDeck.App.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App;

public partial class App(IServiceProvider services) : Application
{
    private SettingsWindow? _mainWindow;
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

            _mainWindow = services.GetRequiredService<SettingsWindow>();

            _mainWindow.Closing += OnMainWindowClosing;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        // Keep the process alive in the tray and hide the window on close.
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void OnTrayShowClicked(object? sender, EventArgs e)
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        _isExitRequested = true;

        _mainWindow?.Close();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}