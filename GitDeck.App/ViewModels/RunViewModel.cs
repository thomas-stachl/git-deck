using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.App.Design;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;

namespace GitDeck.App.ViewModels;

public sealed record RunResult(string Title, string Subtitle, string Icon, BranchInfo Branch);

public partial class RunViewModel(ISettingsService settingsService, IBranchService branchService) : ObservableObject
{
    private const int MaxResults = 8;

    // Parameterless constructor required by the Avalonia XAML previewer/designer;
    // wires up hand-written fakes instead of the real services.
    public RunViewModel() : this(new DesignSettingsService(), new DesignBranchService())
    {
    }

    private IReadOnlyList<BranchInfo> _branches = [];
    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResult))]
    public partial ObservableCollection<RunResult> Results { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestionArea))]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResult))]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>Hint shown in place of the result list when a query matches nothing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage), nameof(HasSuggestionArea))]
    public partial string? StatusMessage { get; set; }

    public bool HasStatusMessage => StatusMessage is not null;

    /// <summary>Whether anything is shown below the search box, and so whether to draw the divider.</summary>
    public bool HasSuggestionArea => HasResults || HasStatusMessage;

    public RunResult? SelectedResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    public void Reset()
    {
        SearchText = string.Empty;
    }

    /// <summary>
    /// Re-reads the branches of the configured repository. Called each time the window is shown so
    /// suggestions reflect branches created, deleted or fetched since the last time.
    /// </summary>
    public async Task LoadBranchesAsync()
    {
        var previous = _loadCancellation;
        _loadCancellation = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        var cancellationToken = _loadCancellation.Token;

        try
        {
            _branches = await branchService.GetBranchesAsync(settingsService.Settings.RepositoryPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            UpdateResults(SearchText);
        }
    }

    partial void OnSearchTextChanged(string value) => UpdateResults(value);

    private void UpdateResults(string searchText)
    {
        var query = searchText.Trim();

        if (query.Length == 0)
        {
            Results = [];
            HasResults = false;
            SelectedIndex = -1;
            StatusMessage = null;
            return;
        }

        var matches = _branches
            .Select(branch => (Branch: branch, Rank: Rank(branch, query)))
            .Where(match => match.Rank is not null)
            .OrderBy(match => match.Rank)
            .ThenByDescending(match => match.Branch.IsCurrent)
            .ThenBy(match => match.Branch.IsRemote)
            .ThenBy(match => match.Branch.Name.Length)
            .ThenBy(match => match.Branch.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .Select(match => ToResult(match.Branch));

        Results = new ObservableCollection<RunResult>(matches);
        HasResults = Results.Count > 0;
        SelectedIndex = HasResults ? 0 : -1;
        StatusMessage = HasResults ? null : NoMatchMessage(query);
    }

    private string NoMatchMessage(string query) => _branches.Count == 0
        ? "No branches found. Check the repository path in Settings."
        : $"No branch matches \"{query}\".";

    private static RunResult ToResult(BranchInfo branch)
    {
        var subtitle = branch switch
        {
            { IsCurrent: true } => "Local branch · current",
            { IsRemote: true, RemoteName: { } remote } => $"Remote branch · {remote}",
            { IsRemote: true } => "Remote branch",
            _ => "Local branch",
        };

        var icon = branch.IsRemote ? "☁️" : "🌿";

        return new RunResult(branch.Name, subtitle, icon, branch);
    }

    /// <summary>
    /// Scores how well a branch matches the query; lower is better, <c>null</c> means no match.
    /// Prefers whole-name matches over the name without its remote prefix, and start-of-name or
    /// start-of-segment matches over matches buried in the middle.
    /// </summary>
    private static int? Rank(BranchInfo branch, string query)
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

    // Matches "run-window" against "feature/run-window-suggestions".
    private static bool SegmentStartsWith(string name, string query)
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
