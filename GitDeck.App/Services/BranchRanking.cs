using GitDeck.Git.Repositories;
using System;

namespace GitDeck.App.Services;

/// <summary>
/// Pure matching logic for the branch palette, kept out of the view model so it can be tested
/// without constructing one.
/// </summary>
public static class BranchRanking
{
    /// <summary>
    /// Scores how well a branch matches the query; lower is better, <c>null</c> means no match.
    /// Prefers whole-name matches over the name without its remote prefix, and start-of-name or
    /// start-of-segment matches over matches buried in the middle.
    /// </summary>
    public static int? Rank(BranchInfo branch, string query)
    {
        const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

        var name = branch.Name;
        var shortName = branch.ShortName;

        if (name.Equals(query, Comparison))
        {
            return 0;
        }

        if (shortName.Equals(query, Comparison))
        {
            return 1;
        }

        if (name.StartsWith(query, Comparison))
        {
            return 2;
        }

        if (shortName.StartsWith(query, Comparison))
        {
            return 3;
        }

        if (SegmentStartsWith(name, query))
        {
            return 4;
        }

        return name.Contains(query, Comparison) ? 5 : null;
    }

    /// <summary>Matches "run-window" against "feature/run-window-suggestions".</summary>
    public static bool SegmentStartsWith(string name, string query)
    {
        var start = 0;

        while (true)
        {
            var separator = name.IndexOf('/', start);
            if (separator < 0)
            {
                return false;
            }

            start = separator + 1;

            if (name.AsSpan(start).StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }
}
