using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.App.Design;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;

namespace GitDeck.App.ViewModels;

public enum RunResultKind
{
    /// <summary>An existing local or remote branch to switch to.</summary>
    Branch,

    /// <summary>Create a branch named after the entered text and switch to it.</summary>
    CreateBranch,
}

public sealed record RunResult(RunResultKind Kind, string Title, string Subtitle, string Icon, string BranchName, BranchInfo? Branch = null);

public partial class RunViewModel(ISettingsService settingsService, IBranchService branchService) : ObservableObject
{
    private const int MaxResults = 8;

    // Parameterless constructor required by the Avalonia XAML previewer/designer;
    // wires up hand-written fakes instead of the real services.
    public RunViewModel() : this(new DesignSettingsService(), new DesignBranchService())
    {
    }

    /// <summary>
    /// Longest repository path shown before the front of it is dropped. Sized to the window, which
    /// is fixed at 640px wide.
    /// </summary>
    private const int MaxPathLength = 44;

    private RepositoryOverview _repository = RepositoryOverview.NotARepository;
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

    /// <summary>Hint or error shown in place of the result list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage), nameof(HasSuggestionArea))]
    public partial string? StatusMessage { get; set; }

    /// <summary>Set while a branch is being created, so Enter cannot start a second one.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>The repository path, shortened from the front when it is too long to fit.</summary>
    [ObservableProperty]
    public partial string RepositoryPathDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial string HeadDisplay { get; set; } = string.Empty;

    /// <summary>Keeps the branch icon from standing alone before the repository has been read.</summary>
    public bool HasHead => HeadDisplay.Length > 0;

    [ObservableProperty]
    public partial string ChangesDisplay { get; set; } = string.Empty;

    public bool HasStatusMessage => StatusMessage is not null;

    /// <summary>Whether anything is shown below the search box, and so whether to draw the divider.</summary>
    public bool HasSuggestionArea => HasResults || HasStatusMessage;

    public RunResult? SelectedResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    public void Reset()
    {
        SearchText = string.Empty;
        StatusMessage = null;
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

        ShowConfiguredRepository();

        try
        {
            _repository = await branchService.GetOverviewAsync(settingsService.Settings.RepositoryPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            UpdateRepositoryInfo();
            UpdateResults(SearchText);
        }
    }

    /// <summary>
    /// Fills the footer from the configured path before the repository has been read, so the window
    /// does not change height once the real values arrive. Only the very first load needs this;
    /// later ones keep showing the previous repository until the new read replaces it.
    /// </summary>
    private void ShowConfiguredRepository()
    {
        if (_repository.IsRepository)
        {
            return;
        }

        var configuredPath = settingsService.Settings.RepositoryPath;

        RepositoryPathDisplay = string.IsNullOrWhiteSpace(configuredPath)
            ? "No repository configured"
            : ShortenPath(configuredPath);
        HeadDisplay = string.Empty;
        ChangesDisplay = string.Empty;
    }

    private void UpdateRepositoryInfo()
    {
        RepositoryPathDisplay = _repository switch
        {
            // A bare repository is still a repository, it just has no working tree to name.
            { IsRepository: true, WorkingDirectory: { } directory } => ShortenPath(directory),
            { IsRepository: true } => ShortenPath(settingsService.Settings.RepositoryPath),
            _ when string.IsNullOrWhiteSpace(settingsService.Settings.RepositoryPath) => "No repository configured",
            _ => "Not a Git repository",
        };

        HeadDisplay = _repository.Head ?? string.Empty;

        // "no changes" would read as a fact about a repository that is not there.
        ChangesDisplay = !_repository.IsRepository
            ? string.Empty
            : _repository.ChangedFileCount switch
            {
                0 => "no changes",
                1 => "1 changed file",
                var count => $"{count} changed files",
            };
    }

    /// <summary>
    /// Drops whole leading segments from a path that is too long to fit, keeping the end — which is
    /// the part that identifies the repository.
    /// </summary>
    private static string ShortenPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        if (path.Length <= MaxPathLength)
        {
            return path;
        }

        var separator = path.Contains('\\') ? '\\' : '/';
        var segments = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var kept = 0;

        // The leading ellipsis and the separator after it are always part of the budget.
        var length = 2;

        for (var index = segments.Length - 1; index >= 0; index--)
        {
            var candidate = length + segments[index].Length + (kept > 0 ? 1 : 0);
            if (kept > 0 && candidate > MaxPathLength)
            {
                break;
            }

            length = candidate;
            kept++;
        }

        var tail = string.Join(separator, segments[^kept..]);

        // A single segment can still be longer than the budget on its own.
        return tail.Length + 2 > MaxPathLength
            ? $"…{separator}{tail[^Math.Min(tail.Length, MaxPathLength - 2)..]}"
            : $"…{separator}{tail}";
    }

    /// <summary>
    /// Runs the highlighted suggestion. Returns whether the window should close, which happens only
    /// when something was actually done — a failure stays open with the reason in
    /// <see cref="StatusMessage"/>.
    /// </summary>
    public async Task<bool> ExecuteSelectedAsync()
    {
        if (IsBusy || SelectedResult is not { } result)
        {
            return false;
        }

        return result.Kind switch
        {
            RunResultKind.CreateBranch => await CreateBranchAsync(result),
            RunResultKind.Branch => await SwitchBranchAsync(result),
            _ => false,
        };
    }

    private Task<bool> CreateBranchAsync(RunResult result)
    {
        var publish = settingsService.Settings.PublishNewBranchesToRemote;

        var busyMessage = publish
            ? $"Creating and publishing \"{result.BranchName}\"..."
            : $"Creating \"{result.BranchName}\"...";

        return RunOperationAsync(busyMessage, "Could not create the branch.", async () =>
        {
            var creation = await branchService.CreateBranchAsync(new CreateBranchRequest(
                settingsService.Settings.RepositoryPath,
                result.BranchName,
                publish,
                settingsService.Settings.GitExecutablePath));

            return (creation.IsCreated, creation.ErrorMessage);
        });
    }

    private Task<bool> SwitchBranchAsync(RunResult result)
    {
        if (result.Branch is not { } branch)
        {
            return Task.FromResult(false);
        }

        var busyMessage = branch.IsRemote
            ? $"Checking out \"{branch.ShortName}\" from {branch.RemoteName ?? "the remote"}..."
            : $"Switching to \"{branch.Name}\"...";

        return RunOperationAsync(busyMessage, "Could not switch branches.", async () =>
        {
            var switched = await branchService.SwitchBranchAsync(
                new SwitchBranchRequest(settingsService.Settings.RepositoryPath, branch));

            return (switched.IsSwitched, switched.ErrorMessage);
        });
    }

    /// <summary>
    /// Shows <paramref name="busyMessage"/> while <paramref name="operation"/> runs, then either
    /// reports success to the caller or leaves the window open with the reason it fell short.
    /// </summary>
    private async Task<bool> RunOperationAsync(
        string busyMessage,
        string fallbackErrorMessage,
        Func<Task<(bool IsDone, string? ErrorMessage)>> operation)
    {
        IsBusy = true;
        StatusMessage = busyMessage;
        Results = [];
        HasResults = false;
        SelectedIndex = -1;

        (bool IsDone, string? ErrorMessage) outcome;
        try
        {
            outcome = await operation();
        }
        finally
        {
            IsBusy = false;
        }

        // Partial success — such as a branch created locally but not published — keeps the window
        // open so the reason is visible.
        if (outcome is { IsDone: true, ErrorMessage: null })
        {
            return true;
        }

        // Reload first: it rebuilds the results, and would otherwise clear the message below.
        await LoadBranchesAsync();
        StatusMessage = outcome.ErrorMessage ?? fallbackErrorMessage;
        return false;
    }

    partial void OnSearchTextChanged(string value) => UpdateResults(value);

    private void UpdateResults(string searchText)
    {
        if (IsBusy)
        {
            return;
        }

        var query = searchText.Trim();

        if (query.Length == 0)
        {
            Results = [];
            HasResults = false;
            SelectedIndex = -1;
            StatusMessage = null;
            return;
        }

        var canCreate = CanCreateBranch(query);
        var limit = canCreate ? MaxResults - 1 : MaxResults;

        var results = _repository.Branches
            .Select(branch => (Branch: branch, Rank: Rank(branch, query)))
            .Where(match => match.Rank is not null)
            .OrderBy(match => match.Rank)
            .ThenByDescending(match => match.Branch.IsCurrent)
            .ThenBy(match => match.Branch.IsRemote)
            .ThenBy(match => match.Branch.Name.Length)
            .ThenBy(match => match.Branch.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(match => ToResult(match.Branch))
            .ToList();

        if (canCreate)
        {
            results.Add(CreateBranchResultEntry(query));
        }

        Results = new ObservableCollection<RunResult>(results);
        HasResults = Results.Count > 0;
        SelectedIndex = HasResults ? 0 : -1;
        StatusMessage = HasResults ? null : NoResultsMessage(query);
    }

    /// <summary>
    /// Offer branch creation only when it could actually succeed: a real repository, a name git
    /// accepts, and no local branch already using it.
    /// </summary>
    private bool CanCreateBranch(string query)
    {
        if (!_repository.IsRepository || !branchService.IsValidBranchName(query))
        {
            return false;
        }

        return !_repository.Branches.Any(branch =>
            !branch.IsRemote && branch.Name.Equals(query, StringComparison.Ordinal));
    }

    private string NoResultsMessage(string query)
    {
        if (!_repository.IsRepository)
        {
            return "No repository found. Check the repository path in Settings.";
        }

        return branchService.IsValidBranchName(query)
            ? $"No branch matches \"{query}\"."
            : $"\"{query}\" is not a valid branch name.";
    }

    private RunResult CreateBranchResultEntry(string branchName)
    {
        var subtitle = settingsService.Settings.PublishNewBranchesToRemote
            ? "Create branch · switch to it and publish to the remote"
            : "Create branch · switch to it";

        return new RunResult(RunResultKind.CreateBranch, branchName, subtitle, "✨", branchName);
    }

    private static RunResult ToResult(BranchInfo branch)
    {
        var subtitle = branch switch
        {
            { IsCurrent: true } => "Local branch · already checked out",
            { IsRemote: true, RemoteName: { } remote } => $"Remote branch · check out locally from {remote}",
            { IsRemote: true } => "Remote branch · check out locally",
            _ => "Local branch · switch to it",
        };

        var icon = branch.IsRemote ? "☁️" : "🌿";

        return new RunResult(RunResultKind.Branch, branch.Name, subtitle, icon, branch.Name, branch);
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
