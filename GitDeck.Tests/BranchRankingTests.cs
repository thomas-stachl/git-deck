using GitDeck.App.Services;
using GitDeck.Git.Repositories;
using Xunit;

namespace GitDeck.Tests;

public class BranchRankingTests
{
    private static BranchInfo Local(string name) => new(name, false, null, false);

    private static BranchInfo Remote(string name, string remote = "origin") => new($"{remote}/{name}", true, remote, false);

    [Fact]
    public void ExactNameBeatsEverything()
    {
        Assert.Equal(0, BranchRanking.Rank(Local("main"), "main"));
    }

    [Fact]
    public void ExactShortNameBeatsPrefixMatches()
    {
        var remote = Remote("main");

        Assert.Equal(1, BranchRanking.Rank(remote, "main"));
        Assert.Equal(2, BranchRanking.Rank(remote, "origin/m"));
    }

    [Fact]
    public void PrefixOfShortNameRanksAboveSegmentMatch()
    {
        Assert.Equal(3, BranchRanking.Rank(Remote("feature-x"), "feat"));
        Assert.Equal(4, BranchRanking.Rank(Local("feature/run-window"), "run"));
    }

    [Fact]
    public void SubstringMatchIsWeakest()
    {
        Assert.Equal(5, BranchRanking.Rank(Local("feature/run-window"), "window"));
    }

    [Fact]
    public void NoMatchReturnsNull()
    {
        Assert.Null(BranchRanking.Rank(Local("main"), "develop"));
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        Assert.Equal(0, BranchRanking.Rank(Local("Main"), "main"));
    }

    [Theory]
    [InlineData("feature/run-window-suggestions", "run-window", true)]
    [InlineData("feature/deep/nested-thing", "nested", true)]
    [InlineData("feature/run-window", "window", false)] // mid-segment is not a segment start
    [InlineData("no-separators", "no", false)] // whole-name prefixes are ranked elsewhere
    public void SegmentStartsWithMatchesSegmentBoundaries(string name, string query, bool expected)
    {
        Assert.Equal(expected, BranchRanking.SegmentStartsWith(name, query));
    }
}
