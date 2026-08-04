using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace GitDeck.App.Logging;

/// <summary>
/// A deliberately small file sink: one log file, truncated when it grows too large. A tray app
/// needs a trail for the paths that would otherwise fail silently (settings IO, process timeouts,
/// hotkey registration), not a log archive.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxLengthBytes = 1_000_000;

    private readonly object _gate = new();
    private readonly string _path;

    public FileLoggerProvider(string path)
    {
        _path = path;
    }

    /// <summary>The log file under the same root as settings.json.</summary>
    public static string DefaultLogFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrEmpty(appData))
            {
                appData = AppContext.BaseDirectory;
            }

            return Path.Combine(appData, "GitDeck", "logs", "gitdeck.log");
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string categoryName, LogLevel level, Exception? exception, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {categoryName}: {message}"
                   + (exception is null ? string.Empty : Environment.NewLine + exception);

        lock (_gate)
        {
            // Logging must never take the app down; a failure here has nowhere better to go.
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(_path) && new FileInfo(_path).Length > MaxLengthBytes)
                {
                    File.Delete(_path);
                }

                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(categoryName, logLevel, exception, formatter(state, exception));
            }
        }
    }
}
