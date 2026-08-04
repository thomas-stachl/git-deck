using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace GitDeck.Core.Settings;

/// <summary>
/// Owns the settings file. Writes are debounced and atomic: the previous file survives as a
/// <c>.bak</c> that <see cref="Load"/> falls back to, so an interrupted write can no longer wipe
/// the configuration (including the encrypted API key).
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    /// <summary>
    /// Text boxes save on focus leave, checkboxes per click; the delay folds bursts of those into
    /// one write without holding a change long enough to lose it.
    /// </summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private readonly object _gate = new();
    private readonly Timer _saveTimer;
    private bool _isDirty;
    private bool _isDisposed;

    public SettingsService(string settingsFilePath, ILogger<SettingsService>? logger = null)
    {
        _settingsFilePath = settingsFilePath;
        _logger = logger ?? NullLogger<SettingsService>.Instance;

        Settings = Load(settingsFilePath, _logger);
        _saveTimer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Where settings live by default. An unresolvable ApplicationData folder (it comes back empty
    /// rather than throwing) falls back to the application directory instead of silently producing
    /// a relative path.
    /// </summary>
    public static string DefaultSettingsFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrEmpty(appData))
            {
                appData = AppContext.BaseDirectory;
            }

            return Path.Combine(appData, "GitDeck", "settings.json");
        }
    }

    public AppSettings Settings { get; }

    /// <summary>Marks the settings dirty and schedules the debounced write.</summary>
    public void Save()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDirty = true;
            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Writes any pending change now. Never throws: a save failure is logged, and every caller is
    /// a binding-driven property setter that has no way to handle it.
    /// </summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (!_isDirty)
            {
                return;
            }

            _isDirty = false;

            try
            {
                WriteAtomically();
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
            {
                _logger.LogError(ex, "Could not save settings to {Path}.", _settingsFilePath);
            }
        }
    }

    public void Dispose()
    {
        Flush();

        lock (_gate)
        {
            _isDisposed = true;
        }

        _saveTimer.Dispose();
    }

    private void WriteAtomically()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        var temp = _settingsFilePath + ".tmp";
        File.WriteAllText(temp, json);

        if (File.Exists(_settingsFilePath))
        {
            // A single swap syscall: a crash mid-save leaves either the old file or the new one,
            // never a truncated mix — and the old file survives as the .bak that Load falls back to.
            File.Replace(temp, _settingsFilePath, _settingsFilePath + ".bak");
        }
        else
        {
            // File.Replace requires an existing destination, so the very first save moves instead.
            File.Move(temp, _settingsFilePath);
        }
    }

    private static AppSettings Load(string path, ILogger logger)
    {
        if (TryLoad(path, logger, quarantineOnParseError: true) is { } settings)
        {
            return settings;
        }

        if (TryLoad(path + ".bak", logger, quarantineOnParseError: false) is { } backup)
        {
            logger.LogWarning("Settings were recovered from the backup copy at {Path}.bak.", path);
            return backup;
        }

        return new AppSettings();
    }

    private static AppSettings? TryLoad(string path, ILogger logger, bool quarantineOnParseError)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Settings file {Path} is corrupt.", path);

            // Set the broken file aside rather than deleting it: it may still hold the encrypted
            // API key and hotkeys, which the user can recover by hand.
            if (quarantineOnParseError)
            {
                Quarantine(path, logger);
            }

            return null;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
        {
            // The file may be perfectly fine and merely locked or inaccessible — leave it alone.
            // This must not throw: it runs during service construction, before any window exists.
            logger.LogError(ex, "Could not read settings file {Path}.", path);
            return null;
        }
    }

    private static void Quarantine(string path, ILogger logger)
    {
        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not quarantine corrupt settings file {Path}.", path);
        }
    }
}
