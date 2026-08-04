using GitDeck.Core.Settings;
using Xunit;

namespace GitDeck.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "GitDeckTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        using var service = new SettingsService(SettingsPath);

        Assert.Null(service.Settings.RepositoryPath);
        Assert.Equal(AppSettings.DefaultBranchHotkey, service.Settings.BranchHotkey);
        Assert.NotNull(service.Settings.Ai);
    }

    [Fact]
    public void RoundTripsSettings()
    {
        using (var service = new SettingsService(SettingsPath))
        {
            service.Settings.RepositoryPath = @"C:\repos\demo";
            service.Settings.Ai.Model = "claude-opus-5";
            service.Save();
        }

        using var reloaded = new SettingsService(SettingsPath);

        Assert.Equal(@"C:\repos\demo", reloaded.Settings.RepositoryPath);
        Assert.Equal("claude-opus-5", reloaded.Settings.Ai.Model);
    }

    [Fact]
    public void DisposeFlushesPendingDebouncedSave()
    {
        // Save() alone only schedules the write; disposing must not lose it.
        using (var service = new SettingsService(SettingsPath))
        {
            service.Settings.RepositoryPath = "pending";
            service.Save();
        }

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void ReplaceKeepsBackupOfPreviousFile()
    {
        using var service = new SettingsService(SettingsPath);

        service.Settings.RepositoryPath = "first";
        service.Save();
        service.Flush();

        service.Settings.RepositoryPath = "second";
        service.Save();
        service.Flush();

        Assert.True(File.Exists(SettingsPath + ".bak"));
        Assert.Contains("first", File.ReadAllText(SettingsPath + ".bak"));
        Assert.Contains("second", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void CorruptFileIsQuarantinedAndBackupRecovered()
    {
        using (var service = new SettingsService(SettingsPath))
        {
            service.Settings.RepositoryPath = "first";
            service.Save();
            service.Flush();

            service.Settings.RepositoryPath = "second";
            service.Save();
            service.Flush();
        }

        File.WriteAllText(SettingsPath, "{ this is not json");

        using var recovered = new SettingsService(SettingsPath);

        Assert.True(File.Exists(SettingsPath + ".corrupt"));
        Assert.Equal("first", recovered.Settings.RepositoryPath);
    }

    [Fact]
    public void CorruptFileWithoutBackupYieldsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not json");

        using var service = new SettingsService(SettingsPath);

        Assert.Null(service.Settings.RepositoryPath);
        Assert.True(File.Exists(SettingsPath + ".corrupt"));
    }

    [Fact]
    public void NullAiSectionFallsBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "Ai": null }""");

        using var service = new SettingsService(SettingsPath);

        Assert.NotNull(service.Settings.Ai);
        Assert.False(service.Settings.Ai.IsEnabled);
    }

    [Fact]
    public void ExplicitNullHotkeySurvivesRoundTrip()
    {
        // Absent key means default; an explicit null means the user cleared the hotkey.
        using (var service = new SettingsService(SettingsPath))
        {
            service.Settings.BranchHotkey = null;
            service.Save();
        }

        using var reloaded = new SettingsService(SettingsPath);

        Assert.Null(reloaded.Settings.BranchHotkey);
    }
}
