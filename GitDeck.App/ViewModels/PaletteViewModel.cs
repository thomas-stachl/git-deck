using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.Git.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.ViewModels;

/// <summary>
/// What the two palette modes share: the selection cursor, the status line, the busy flag, and the
/// run-an-operation shape (busy message → work → success closes the window, failure stays open
/// with the reason). The window talks to whichever palette is active only through this type.
/// </summary>
public abstract partial class PaletteViewModel : ObservableObject
{
    private CancellationTokenSource? _operationCancellation;

    /// <summary>Set by <see cref="RunViewModel"/> when the mode changes; views bind IsVisible to it.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>Hint or error shown in place of the item list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage), nameof(HasSuggestionArea))]
    public partial string? StatusMessage { get; set; }

    /// <summary>Set while an operation runs, so Enter cannot start a second one.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool HasStatusMessage => StatusMessage is not null;

    public bool HasItems => ItemCount > 0;

    /// <summary>Whether anything is shown below the input row, and so whether to draw the divider.</summary>
    public bool HasSuggestionArea => HasItems || HasStatusMessage;

    /// <summary>Number of navigable rows currently shown.</summary>
    protected abstract int ItemCount { get; }

    /// <summary>
    /// Derived classes call this after swapping their item collection — <see cref="HasItems"/> is
    /// computed, so nothing raises it otherwise.
    /// </summary>
    protected void NotifyItemsChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasSuggestionArea));
    }

    /// <summary>Up/Down. Wraps around. False when there is nothing to move over, so the key falls through.</summary>
    public virtual bool MoveSelection(int offset)
    {
        if (ItemCount == 0)
        {
            return false;
        }

        SelectedIndex = Wrap(SelectedIndex + offset, ItemCount);
        return true;
    }

    /// <summary>Enter. True means the window should close.</summary>
    public abstract Task<bool> AcceptAsync();

    /// <summary>
    /// Escape. True means the palette consumed it by stepping back and the window stays open.
    /// </summary>
    public virtual bool TryStepBack() => false;

    /// <summary>Mode-specific keys (Space, Ctrl+A, Ctrl+G). True means handled.</summary>
    public virtual bool HandleKey(Key key, KeyModifiers modifiers) => false;

    public abstract void OnRepositoryLoaded(RepositoryOverview repository);

    /// <summary>Cancels in-flight work and clears transient state. Called on hide and on open.</summary>
    public virtual void Reset()
    {
        _operationCancellation?.Cancel();
        StatusMessage = null;
    }

    /// <summary>
    /// Cancels any previous operation and issues the token for a new one. Cancel-and-drop,
    /// deliberately without Dispose: a plain source holds no timer or wait handle, and disposing
    /// while the superseded operation still observes the token is a race.
    /// </summary>
    protected CancellationToken BeginOperation()
    {
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        return _operationCancellation.Token;
    }

    /// <summary>Hook for clearing mode state (such as the result list) when an operation starts.</summary>
    protected virtual void OnOperationStarting()
    {
    }

    /// <summary>Hook that runs before a failure message is shown — branch mode reloads its list here.</summary>
    protected virtual Task OnOperationFailedAsync() => Task.CompletedTask;

    /// <summary>
    /// Shows <paramref name="busyMessage"/> while <paramref name="operation"/> runs, then either
    /// reports success to the caller or leaves the window open with the reason it fell short.
    /// Exceptions land in <see cref="StatusMessage"/> too — callers fire-and-forget this, so a
    /// throw would otherwise vanish and leave the busy message frozen on screen.
    /// </summary>
    protected async Task<bool> RunOperationAsync(
        string busyMessage,
        string fallbackErrorMessage,
        Func<CancellationToken, Task<(bool IsDone, string? ErrorMessage)>> operation)
    {
        if (IsBusy)
        {
            return false;
        }

        var token = BeginOperation();

        IsBusy = true;
        StatusMessage = busyMessage;
        OnOperationStarting();

        try
        {
            var outcome = await operation(token);

            // Reset() ran while the operation was in flight: the window is hidden and the result
            // belongs to a session the user already dismissed.
            if (token.IsCancellationRequested)
            {
                return false;
            }

            // Partial success — such as a branch created locally but not published — keeps the
            // window open so the reason is visible.
            if (outcome is { IsDone: true, ErrorMessage: null })
            {
                return true;
            }

            await OnOperationFailedAsync();
            StatusMessage = outcome.ErrorMessage ?? fallbackErrorMessage;
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                await OnOperationFailedAsync();
                StatusMessage = $"{fallbackErrorMessage} {ex.Message}";
            }

            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedIndexChanged(int value) => OnSelectionChanged();

    /// <summary>Runs when <see cref="SelectedIndex"/> changes, for derived properties that read it.</summary>
    protected virtual void OnSelectionChanged()
    {
    }

    private static int Wrap(int index, int count) => (index % count + count) % count;
}
