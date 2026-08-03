using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitDeck.App.Services;
using GitDeck.Core.Settings;
using GitDeck.Git.Repositories;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

public enum CommitPhase
{
    /// <summary>Ticking the files that should go into the commit.</summary>
    Files,

    /// <summary>Typing the commit message.</summary>
    Message,
}

/// <summary>
/// Commit mode: opens on the list of changed files, then takes a message, then commits. Enter moves
/// between those steps, so the whole thing is one hotkey and two keystrokes in the common case.
/// </summary>
public partial class CommitPaletteViewModel(
    ISettingsService settingsService,
    ICommitService commitService,
    ICommitMessageService commitMessageService) : ObservableObject
{
    private const string FilesHint = "Space toggles · Ctrl+A toggles all · Enter to write a message";

    private RepositoryOverview _repository = RepositoryOverview.NotARepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilesPhase), nameof(IsMessagePhase))]
    public partial CommitPhase Phase { get; set; } = CommitPhase.Files;

    [ObservableProperty]
    public partial ObservableCollection<CommitFileViewModel> Files { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage), nameof(HasSuggestionArea))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Reads "3 of 7 files selected", and doubles as the keyboard hint line.</summary>
    [ObservableProperty]
    public partial string SelectionSummary { get; set; } = "No changes to commit";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestionArea))]
    public partial bool HasFiles { get; set; }

    public bool IsFilesPhase => Phase is CommitPhase.Files;

    public bool IsMessagePhase => Phase is CommitPhase.Message;

    public bool HasStatusMessage => StatusMessage is not null;

    public bool HasSuggestionArea => HasFiles || HasStatusMessage;

    public void Reset()
    {
        Phase = CommitPhase.Files;
        Message = string.Empty;
        StatusMessage = null;
    }

    public void OnRepositoryLoaded(RepositoryOverview repository)
    {
        _repository = repository;

        foreach (var file in Files)
        {
            file.PropertyChanged -= OnFilePropertyChanged;
        }

        Files = [.. repository.ChangedFiles.Select(file => new CommitFileViewModel(file))];

        foreach (var file in Files)
        {
            file.PropertyChanged += OnFilePropertyChanged;
        }

        HasFiles = Files.Count > 0;
        SelectedIndex = HasFiles ? 0 : -1;

        UpdateSelectionSummary();
        StatusMessage = DescribeState();
    }

    /// <summary>Ticks or unticks the highlighted file.</summary>
    [RelayCommand]
    private void ToggleSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Files.Count)
        {
            Files[SelectedIndex].IsSelected = !Files[SelectedIndex].IsSelected;
        }
    }

    /// <summary>
    /// Ticks everything, or unticks everything when all are already ticked.
    /// </summary>
    [RelayCommand]
    private void ToggleAll()
    {
        var select = !Files.All(file => file.IsSelected);

        foreach (var file in Files)
        {
            file.IsSelected = select;
        }
    }

    /// <summary>
    /// Handles Enter: moves from the file list to the message, then commits. Returns whether the
    /// window should close, which only happens once the commit has actually been made.
    /// </summary>
    public async Task<bool> AdvanceAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (Phase is CommitPhase.Files)
        {
            if (SelectedFiles.Count == 0)
            {
                StatusMessage = "Tick at least one file to commit.";
                return false;
            }

            Phase = CommitPhase.Message;
            StatusMessage = MessageHint();
            return false;
        }

        return await CommitAsync();
    }

    /// <summary>Whether Ctrl+G can generate a message right now.</summary>
    public bool CanGenerateMessage =>
        commitMessageService.IsEnabled && !IsBusy && Phase is CommitPhase.Message && SelectedFiles.Count > 0;

    /// <summary>
    /// Fills the message box from the diff of the ticked files. The result is left editable — it is a
    /// starting point, not a commit.
    /// </summary>
    [RelayCommand]
    private async Task GenerateMessageAsync()
    {
        if (!CanGenerateMessage)
        {
            return;
        }

        if (_repository.WorkingDirectory is not { } workingDirectory)
        {
            StatusMessage = "This repository has no working tree to read a diff from.";
            return;
        }

        var files = SelectedFiles;

        IsBusy = true;
        StatusMessage = "Writing a commit message from the diff...";

        GeneratedCommitMessage generated;
        try
        {
            generated = await commitMessageService.GenerateAsync(workingDirectory, files);
        }
        finally
        {
            IsBusy = false;
        }

        if (generated.Message is { } message)
        {
            Message = message;
            StatusMessage = MessageHint();
            return;
        }

        StatusMessage = generated.ErrorMessage ?? "Could not generate a message.";
    }

    /// <summary>
    /// Handles Escape: steps back to the file list. Returns false when there is nothing to step back
    /// from, which tells the window to close instead.
    /// </summary>
    public bool GoBack()
    {
        if (IsBusy || Phase is not CommitPhase.Message)
        {
            return false;
        }

        Phase = CommitPhase.Files;
        StatusMessage = DescribeState();
        return true;
    }

    private async Task<bool> CommitAsync()
    {
        var files = SelectedFiles;

        if (files.Count == 0)
        {
            StatusMessage = "Tick at least one file to commit.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            StatusMessage = "Enter a commit message.";
            return false;
        }

        if (_repository.WorkingDirectory is not { } workingDirectory)
        {
            StatusMessage = "This repository has no working tree to commit in.";
            return false;
        }

        IsBusy = true;
        StatusMessage = files.Count == 1
            ? "Committing 1 file..."
            : $"Committing {files.Count} files...";

        CommitResult result;
        try
        {
            result = await commitService.CommitAsync(new CommitRequest(
                workingDirectory,
                Message,
                files,
                settingsService.Settings.GitExecutablePath));
        }
        finally
        {
            IsBusy = false;
        }

        if (result.IsCommitted)
        {
            return true;
        }

        // Deliberately no reload here: a rejected commit — a failing pre-commit hook, say — leaves
        // the message and ticks worth keeping so the user can adjust and try again.
        StatusMessage = result.ErrorMessage ?? "The commit failed.";
        return false;
    }

    private List<ChangedFile> SelectedFiles =>
        [.. Files.Where(file => file.IsSelected).Select(file => file.File)];

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CommitFileViewModel.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private void UpdateSelectionSummary()
    {
        if (Files.Count == 0)
        {
            SelectionSummary = _repository.IsRepository
                ? "Nothing to commit"
                : "No repository configured";
            return;
        }

        var selected = Files.Count(file => file.IsSelected);

        SelectionSummary = selected == Files.Count
            ? $"All {Files.Count} files selected"
            : $"{selected} of {Files.Count} files selected";
    }

    private string MessageHint() => commitMessageService.IsEnabled
        ? "Enter to commit · Ctrl+G to write one from the diff · Esc to go back"
        : "Enter to commit · Esc to go back to the files";

    private string? DescribeState()
    {
        if (!_repository.IsRepository)
        {
            return "No repository found. Check the repository path in Settings.";
        }

        return Files.Count > 0 ? FilesHint : "There are no changes to commit.";
    }
}
