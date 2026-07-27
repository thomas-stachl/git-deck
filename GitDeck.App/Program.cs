using Avalonia;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.App.Views.Run;
using GitDeck.App.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GitDeck.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new Program().ConfigureServices();

        BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<SettingsView>();
        services.AddSingleton<ViewLocator>();

        services.AddSingleton<RunViewModel>();
        services.AddSingleton<RunWindow>();
        services.AddSingleton<RunWindowService>();

        return services.BuildServiceProvider();
    }
}
