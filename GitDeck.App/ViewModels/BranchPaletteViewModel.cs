using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public enum RunResultKind
{
    /// <summary>An existing local or remote branch to switch to.</summary>
    Branch,

    /// <summary>Create a branch named after the entered text and switch to it.</summary>
    CreateBranch,

    /// <summary>Fast-forward the current branch from its upstream.</summary>
    Pull,
}

public sealed record RunResult(RunResultKind Kind, string Title, string Subtitle, string Icon, string BranchName, BranchInfo? Branch = null);

/// <summary>
/// Branch mode: filters the repository's branches as the user types, and offers to create a branch
/// when what they typed does not name one yet.
/// </summary>
public partial class BranchPaletteViewModel(
    ISettingsService settingsService,
    IBranchService branchService,
    Func<Task> reloadRepositoryAsync,
    Func<string?> getRepositoryPath) : PaletteViewModel
{
    private const int MaxResults = 8;

    private RepositoryOverview _repository = RepositoryOverview.NotARepository;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResult))]
    public partial ObservableCollection<RunResult> Results { get; set; } = [];

    public RunResult? SelectedResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    protected override int ItemCount => Results.Count;

    protected override void OnSelectionChanged() => OnPropertyChanged(nameof(SelectedResult));

    partial void OnResultsChanged(ObservableCollection<RunResult> value) => NotifyItemsChanged();

    public override void Reset()
    {
        base.Reset();
        SearchText = string.Empty;
    }

    public override void OnRepositoryLoaded(RepositoryOverview repository)
    {
        _repository = repository;
        UpdateResults(SearchText);
    }

    /// <summary>
    /// Runs the highlighted suggestion. Returns whether the window should close, which happens only
    /// when something was actually done — a failure stays open with the reason in
    /// <see cref="PaletteViewModel.StatusMessage"/>.
    /// </summary>
    public override async Task<bool> AcceptAsync()
    {
        if (IsBusy || SelectedResult is not { } result)
        {
            return false;
        }

        return result.Kind switch
        {
            RunResultKind.CreateBranch => await CreateBranchAsync(result),
            RunResultKind.Branch => await SwitchBranchAsync(result),
            RunResultKind.Pull => await PullAsync(),
            _ => false,
        };
    }

    protected override void OnOperationStarting()
    {
        Results = [];
        SelectedIndex = -1;
    }

    // Reload before the failure message is shown: it rebuilds the results, and would otherwise
    // clear the message.
    protected override Task OnOperationFailedAsync() => reloadRepositoryAsync();

    private Task<bool> CreateBranchAsync(RunResult result)
    {
        var publish = settingsService.Settings.PublishNewBranchesToRemote;

        var busyMessage = publish
            ? $"Creating and publishing \"{result.BranchName}\"..."
            : $"Creating \"{result.BranchName}\"...";

        return RunOperationAsync(busyMessage, "Could not create the branch.", async token =>
        {
            var creation = await branchService.CreateBranchAsync(
                new CreateBranchRequest(getRepositoryPath(), result.BranchName, publish),
                token);

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

        return RunOperationAsync(busyMessage, "Could not switch branches.", async token =>
        {
            var switched = await branchService.SwitchBranchAsync(
                new SwitchBranchRequest(getRepositoryPath(), branch),
                token);

            return (switched.IsSwitched, switched.ErrorMessage);
        });
    }

    /// <summary>
    /// Fast-forwards the current branch from its upstream. Offered only when the repository overview
    /// says there is something to pull, so <see cref="SelectedResult"/> is always non-null here.
    /// </summary>
    private Task<bool> PullAsync()
    {
        var busyMessage = _repository.BehindBy == 1
            ? "Pulling 1 commit..."
            : $"Pulling {_repository.BehindBy} commits...";

        return RunOperationAsync(busyMessage, "Could not pull the latest changes.", async token =>
        {
            var pull = await branchService.PullCurrentBranchAsync(getRepositoryPath(), token);
            return (pull.IsPulled, pull.ErrorMessage);
        });
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
            // The one moment this offers anything unprompted: an empty search box is the palette's
            // resting state, so a pull that's actually available surfaces right there instead of
            // waiting to be found.
            var pull = CreatePullResultOrNull();
            Results = pull is null ? [] : [pull];
            SelectedIndex = Results.Count > 0 ? 0 : -1;
            StatusMessage = null;
            return;
        }

        var canCreate = CanCreateBranch(query);
        var limit = canCreate ? MaxResults - 1 : MaxResults;

        var results = _repository.Branches
            .Select(branch => (Branch: branch, Rank: BranchRanking.Rank(branch, query)))
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
        SelectedIndex = Results.Count > 0 ? 0 : -1;
        StatusMessage = Results.Count > 0 ? null : NoResultsMessage(query);
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
        if (_repository.LoadError is { } loadError)
        {
            return loadError;
        }

        if (!_repository.IsRepository)
        {
            return "No repository found. Check the repository path in Settings.";
        }

        return branchService.IsValidBranchName(query)
            ? $"No branch matches \"{query}\"."
            : $"\"{query}\" is not a valid branch name.";
    }

    /// <summary>
    /// Offered only when there is somewhere to pull from and something to pull — no upstream, or an
    /// upstream already caught up, means nothing is shown.
    /// </summary>
    private RunResult? CreatePullResultOrNull()
    {
        if (!_repository.IsRepository || !_repository.HasUpstream || _repository.BehindBy == 0)
        {
            return null;
        }

        var branchName = _repository.Head ?? "the current branch";
        var subtitle = _repository.BehindBy == 1
            ? "Pull · 1 commit behind the upstream"
            : $"Pull · {_repository.BehindBy} commits behind the upstream";

        return new RunResult(RunResultKind.Pull, $"Pull {branchName}", subtitle, "⬇️", branchName);
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
}
