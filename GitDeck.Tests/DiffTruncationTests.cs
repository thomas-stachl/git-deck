using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;
using Xunit;

namespace GitDeck.Tests;

public class DiffTruncationTests
{
    [Fact]
    public void ShortDiffIsUntouched()
    {
        var result = DiffService.Truncate("small diff", 100);

        Assert.Equal("small diff", result.Diff);
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public void LongDiffIsCutAndFlagged()
    {
        var result = DiffService.Truncate(new string('a', 200), 100);

        Assert.Equal(100, result.Diff.Length);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public void CutNeverSplitsASurrogatePair()
    {
        // "😀" is one pair of UTF-16 code units; cutting between them yields invalid UTF-16
        // that the JSON serializer sending the diff to a provider would reject.
        var diff = string.Concat(Enumerable.Repeat("😀", 100));

        var result = DiffService.Truncate(diff, 101);

        Assert.True(result.IsTruncated);
        Assert.Equal(100, result.Diff.Length);
        Assert.False(char.IsHighSurrogate(result.Diff[^1]));
    }

    [Fact]
    public void CutAtAPairBoundaryIsLeftAlone()
    {
        var diff = string.Concat(Enumerable.Repeat("😀", 100));

        var result = DiffService.Truncate(diff, 100);

        Assert.Equal(100, result.Diff.Length);
        Assert.False(char.IsHighSurrogate(result.Diff[^1]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveBudgetFallsBackToTheDefaultInsteadOfUnlimited(int budget)
    {
        var oversized = new string('a', AiSettings.DefaultMaxDiffCharacters + 1000);

        var result = DiffService.Truncate(oversized, budget);

        Assert.True(result.IsTruncated);
        Assert.Equal(AiSettings.DefaultMaxDiffCharacters, result.Diff.Length);
    }
}
