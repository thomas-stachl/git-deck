using Avalonia;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.App.Views.Run;
using GitDeck.App.Views.Settings;
using GitDeck.Core.Settings;
using GitDeck.Git;
using GitDeck.Git.Repositories;
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

    // Parameterless overload used by the Avalonia XAML previewer/designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App())
            .UsePlatformDetect()
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
        services.AddSingleton<IRunWindowService, RunWindowService>();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IGitExecutableService, GitExecutableService>();
        services.AddSingleton<IBranchService, BranchService>();

        // The version is what RegisterHotKey's platform annotation asks for; any Windows that can
        // run .NET 10 satisfies it.
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
        {
            services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();
        }
        else
        {
            services.AddSingleton<IGlobalHotkeyService, UnsupportedGlobalHotkeyService>();
        }

        return services.BuildServiceProvider();
    }
}
