using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.App.Services;
using GitDeck.Git.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
    ICommitService commitService,
    ICommitMessageService commitMessageService) : PaletteViewModel
{
    private const string FilesHint = "Space toggles · Ctrl+A toggles all · Enter to write a message";

    private RepositoryOverview _repository = RepositoryOverview.NotARepository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilesPhase), nameof(IsMessagePhase))]
    public partial CommitPhase Phase { get; set; } = CommitPhase.Files;

    [ObservableProperty]
    public partial ObservableCollection<CommitFileViewModel> Files { get; set; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Reads "3 of 7 files selected", and doubles as the keyboard hint line.</summary>
    [ObservableProperty]
    public partial string SelectionSummary { get; set; } = "No changes to commit";

    public bool IsFilesPhase => Phase is CommitPhase.Files;

    public bool IsMessagePhase => Phase is CommitPhase.Message;

    protected override int ItemCount => Files.Count;

    partial void OnFilesChanged(ObservableCollection<CommitFileViewModel> value) => NotifyItemsChanged();

    public override void Reset()
    {
        base.Reset();
        Phase = CommitPhase.Files;
        Message = string.Empty;
    }

    public override void OnRepositoryLoaded(RepositoryOverview repository)
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

        SelectedIndex = Files.Count > 0 ? 0 : -1;

        UpdateSelectionSummary();
        StatusMessage = DescribeState();
    }

    /// <summary>The list is only navigable while files are being ticked; in the message phase the
    /// arrow keys belong to the text box.</summary>
    public override bool MoveSelection(int offset) => IsFilesPhase && base.MoveSelection(offset);

    /// <summary>The commit palette's own keys, phase-aware so typing is never intercepted.</summary>
    public override bool HandleKey(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            // Only while ticking files: in the message box these belong to the text.
            case Key.Space when IsFilesPhase:
                ToggleSelected();
                return true;

            case Key.A when IsFilesPhase && modifiers is KeyModifiers.Control:
                ToggleAll();
                return true;

            // Writing a message from the diff — only offered while the message is being typed.
            case Key.G when modifiers is KeyModifiers.Control && CanGenerateMessage:
                _ = GenerateMessageAsync();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Ticks or unticks the highlighted file.</summary>
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
    public override async Task<bool> AcceptAsync()
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

    /// <summary>
    /// Handles Escape: steps back to the file list. Returns false when there is nothing to step back
    /// from, which tells the window to close instead.
    /// </summary>
    public override bool TryStepBack()
    {
        if (IsBusy || Phase is not CommitPhase.Message)
        {
            return false;
        }

        Phase = CommitPhase.Files;
        StatusMessage = DescribeState();
        return true;
    }

    /// <summary>Whether Ctrl+G can generate a message right now.</summary>
    public bool CanGenerateMessage =>
        commitMessageService.IsEnabled && !IsBusy && Phase is CommitPhase.Message && SelectedFiles.Count > 0;

    /// <summary>
    /// Fills the message box from the diff of the ticked files. The result is left editable — it is a
    /// starting point, not a commit. Hand-rolled rather than through RunOperationAsync because
    /// success writes into the message box instead of closing the window.
    /// </summary>
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
        var token = BeginOperation();

        IsBusy = true;
        StatusMessage = "Writing a commit message from the diff...";

        try
        {
            var generated = await commitMessageService.GenerateAsync(workingDirectory, files, token);

            // Escape hid the window mid-generation; the result belongs to a dismissed session.
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (generated.Message is { } message)
            {
                Message = message;
                StatusMessage = MessageHint();
            }
            else
            {
                StatusMessage = generated.ErrorMessage ?? "Could not generate a message.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                StatusMessage = $"Could not generate a message. {ex.Message}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<bool> CommitAsync()
    {
        var files = SelectedFiles;

        if (files.Count == 0)
        {
            StatusMessage = "Tick at least one file to commit.";
            return Task.FromResult(false);
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            StatusMessage = "Enter a commit message.";
            return Task.FromResult(false);
        }

        if (_repository.WorkingDirectory is not { } workingDirectory)
        {
            StatusMessage = "This repository has no working tree to commit in.";
            return Task.FromResult(false);
        }

        var busyMessage = files.Count == 1
            ? "Committing 1 file..."
            : $"Committing {files.Count} files...";

        // No OnOperationFailedAsync reload here, deliberately: a rejected commit — a failing
        // pre-commit hook, say — leaves the message and ticks worth keeping so the user can adjust
        // and try again. The commit itself gets no cancellation token either: killing git
        // mid-commit risks a locked index and half-run hooks, so Escape merely discards the result.
        return RunOperationAsync(busyMessage, "The commit failed.", async _ =>
        {
            var result = await commitService.CommitAsync(
                new CommitRequest(workingDirectory, Message, files),
                CancellationToken.None);

            return (result.IsCommitted, result.ErrorMessage);
        });
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
        if (_repository.LoadError is { } loadError)
        {
            return loadError;
        }

        if (!_repository.IsRepository)
        {
            return "No repository found. Check the repository path in Settings.";
        }

        return Files.Count > 0 ? FilesHint : "There are no changes to commit.";
    }
}
