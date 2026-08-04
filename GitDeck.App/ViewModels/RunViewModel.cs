using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.App.Design;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

/// <summary>
/// The run window itself: owns the repository read and the footer both modes share, and hands the
/// mode-specific work to <see cref="Branches"/> or <see cref="Commit"/>.
/// </summary>
public partial class RunViewModel : ObservableObject
{
    /// <summary>
    /// Longest repository path shown before the front of it is dropped. Sized to the window, which
    /// is fixed at 640px wide.
    /// </summary>
    private const int MaxPathLength = 44;

    private readonly ISettingsService _settingsService;
    private readonly IBranchService _branchService;

    private RepositoryOverview _repository = RepositoryOverview.NotARepository;
    private CancellationTokenSource? _loadCancellation;

    public RunViewModel(
        ISettingsService settingsService,
        IBranchService branchService,
        ICommitService commitService,
        ICommitMessageService commitMessageService)
    {
        _settingsService = settingsService;
        _branchService = branchService;

        Branches = new BranchPaletteViewModel(settingsService, branchService, LoadRepositoryAsync);
        Commit = new CommitPaletteViewModel(commitService, commitMessageService);

        // IsBusy is computed over the children, so their changes have to be re-announced for any
        // binding to it to update.
        Branches.PropertyChanged += OnChildPropertyChanged;
        Commit.PropertyChanged += OnChildPropertyChanged;

        Branches.IsActive = true;
    }

    // Parameterless constructor required by the Avalonia XAML previewer/designer;
    // wires up hand-written fakes instead of the real services.
    public RunViewModel() : this(
        new DesignSettingsService(),
        new DesignBranchService(),
        new DesignCommitService(),
        new DesignCommitMessageService())
    {
    }

    public BranchPaletteViewModel Branches { get; }

    public CommitPaletteViewModel Commit { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePalette))]
    public partial RunMode Mode { get; set; } = RunMode.Branches;

    /// <summary>The palette the current mode shows; everything key-driven goes through it.</summary>
    public PaletteViewModel ActivePalette => Mode is RunMode.Commit ? Commit : Branches;

    partial void OnModeChanged(RunMode value)
    {
        Branches.IsActive = value is RunMode.Branches;
        Commit.IsActive = value is RunMode.Commit;
    }

    /// <summary>The repository path, shortened from the front when it is too long to fit.</summary>
    [ObservableProperty]
    public partial string RepositoryPathDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial string HeadDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangesDisplay { get; set; } = string.Empty;

    /// <summary>Keeps the branch icon from standing alone before the repository has been read.</summary>
    public bool HasHead => HeadDisplay.Length > 0;

    /// <summary>Whether the active mode is mid-operation, which should hold the window open.</summary>
    public bool IsBusy => ActivePalette.IsBusy;

    /// <summary>
    /// Switches to a mode and re-reads the repository. Called each time the window is shown so both
    /// the branch list and the changed files reflect the repository as it is now.
    /// </summary>
    public Task OpenAsync(RunMode mode)
    {
        Mode = mode;
        Reset();

        return LoadRepositoryAsync();
    }

    /// <summary>Cancels in-flight work in both modes and clears their transient state.</summary>
    public void Reset()
    {
        Branches.Reset();
        Commit.Reset();
    }

    /// <summary>Handles Enter. True means the window should close.</summary>
    public Task<bool> AcceptAsync() => ActivePalette.AcceptAsync();

    /// <summary>
    /// Handles Escape. Returns true when the mode consumed it by stepping back, in which case the
    /// window should stay open.
    /// </summary>
    public bool TryStepBack() => ActivePalette.TryStepBack();

    /// <summary>Handles Up/Down. True when the active palette moved its selection.</summary>
    public bool MoveSelection(int offset) => ActivePalette.MoveSelection(offset);

    /// <summary>Hands any other key to the active palette. True means handled.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers) => ActivePalette.HandleKey(key, modifiers);

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PaletteViewModel.IsBusy))
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private async Task LoadRepositoryAsync()
    {
        // Cancel-and-drop, deliberately without Dispose: a plain source holds no timer or wait
        // handle, and disposing while the superseded read is still observing the token is a race.
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();

        var cancellationToken = _loadCancellation.Token;

        ShowConfiguredRepository();

        try
        {
            _repository = await _branchService.GetOverviewAsync(_settingsService.Settings.RepositoryPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            // The service maps known failures to results itself; this is the last line of defence,
            // because callers fire-and-forget this task and a throw would be unhandled.
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _repository = RepositoryOverview.Failed(ex.Message);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        UpdateRepositoryInfo();

        Branches.OnRepositoryLoaded(_repository);
        Commit.OnRepositoryLoaded(_repository);
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

        var configuredPath = _settingsService.Settings.RepositoryPath;

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
            { IsRepository: true } => ShortenPath(_settingsService.Settings.RepositoryPath),
            // A permission problem or corrupt index is not the same as "not a repository".
            { LoadError: not null } => "Could not read the repository",
            _ when string.IsNullOrWhiteSpace(_settingsService.Settings.RepositoryPath) => "No repository configured",
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
}
