using Avalonia;
using GitDeck.App.Logging;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;
using GitDeck.App.Views;
using GitDeck.App.Views.Run;
using GitDeck.App.Views.Settings;
using GitDeck.Core.Settings;
using GitDeck.Git;
using GitDeck.Git.Generation;
using GitDeck.Git.Repositories;
using GitDeck.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace GitDeck.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Disposing the provider on the way out is what runs the singletons' Dispose methods:
        // the hotkey thread unregisters its Win32 registrations, and the settings service flushes
        // any pending debounced save.
        using var services = new Program().ConfigureServices();

        RegisterGlobalExceptionLogging(services.GetRequiredService<ILogger<Program>>());

        BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Last-resort logging for the paths nothing else observes. Neither handler can prevent the
    /// failure — but without them a crash of a tray app leaves no trace at all.
    /// </summary>
    private static void RegisterGlobalExceptionLogging(ILogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception,
                "Unhandled exception (terminating: {IsTerminating}).", e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
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

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultLogFilePath));
        });

        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AiSettingsViewModel>();
        services.AddSingleton<HotkeySettingsViewModel>();
        services.AddSingleton<SettingsWindow>();

        services.AddSingleton<RunViewModel>();
        services.AddSingleton<RunWindow>();
        services.AddSingleton<OwnerWindow>();
        services.AddSingleton<IRunWindowService, RunWindowService>();
        services.AddSingleton<ISettingsWindowService, SettingsWindowService>();

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ISettingsService>(provider => new SettingsService(
            SettingsService.DefaultSettingsFilePath,
            provider.GetRequiredService<ILogger<SettingsService>>()));
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IGitExecutableService, GitExecutableService>();
        services.AddSingleton<IBranchService, BranchService>();
        services.AddSingleton<ICommitService, CommitService>();
        services.AddSingleton<IDiffService, DiffService>();
        services.AddSingleton<ICommitMessageGenerator, CommitMessageGenerator>();
        services.AddSingleton<ICommitMessageService, CommitMessageService>();
        services.AddSingleton<IGitDeckIpc, GitDeckIpc>();
        services.AddSingleton<GitDeckIpcServer>();

        // DPAPI is Windows-only; elsewhere nothing is stored and the API key comes from the
        // environment instead of being written to disk in the clear.
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISecretProtector, WindowsSecretProtector>();
        }
        else
        {
            services.AddSingleton<ISecretProtector, UnsupportedSecretProtector>();
        }

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
