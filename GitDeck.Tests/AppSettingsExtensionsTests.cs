using GitDeck.Core.Settings;
using Xunit;

namespace GitDeck.Tests;

public sealed class AppSettingsExtensionsTests
{
    [Fact]
    public void RecordRecentRepositoryPath_InsertsAtFront()
    {
        var settings = new AppSettings();

        settings.RecordRecentRepositoryPath(@"C:\repos\a");
        settings.RecordRecentRepositoryPath(@"C:\repos\b");

        Assert.Equal([@"C:\repos\b", @"C:\repos\a"], settings.RecentRepositoryPaths);
    }

    [Fact]
    public void RecordRecentRepositoryPath_MovesExistingEntryToFrontInsteadOfDuplicating()
    {
        var settings = new AppSettings();

        settings.RecordRecentRepositoryPath(@"C:\repos\a");
        settings.RecordRecentRepositoryPath(@"C:\repos\b");
        settings.RecordRecentRepositoryPath(@"C:\repos\a");

        Assert.Equal([@"C:\repos\a", @"C:\repos\b"], settings.RecentRepositoryPaths);
    }

    [Fact]
    public void RecordRecentRepositoryPath_DedupesCaseInsensitively()
    {
        var settings = new AppSettings();

        settings.RecordRecentRepositoryPath(@"C:\repos\a");
        settings.RecordRecentRepositoryPath(@"C:\REPOS\A");

        Assert.Equal([@"C:\REPOS\A"], settings.RecentRepositoryPaths);
    }

    [Fact]
    public void RecordRecentRepositoryPath_TrimsToMaxCount()
    {
        var settings = new AppSettings();

        for (var i = 0; i < AppSettings.MaxRecentRepositoryPaths + 5; i++)
        {
            settings.RecordRecentRepositoryPath($@"C:\repos\{i}");
        }

        Assert.Equal(AppSettings.MaxRecentRepositoryPaths, settings.RecentRepositoryPaths.Count);

        // The most recent entries survive; the oldest are trimmed off the end.
        Assert.Equal($@"C:\repos\{AppSettings.MaxRecentRepositoryPaths + 4}", settings.RecentRepositoryPaths[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordRecentRepositoryPath_IgnoresBlankPaths(string? path)
    {
        var settings = new AppSettings();

        settings.RecordRecentRepositoryPath(path);

        Assert.Empty(settings.RecentRepositoryPaths);
    }
}
