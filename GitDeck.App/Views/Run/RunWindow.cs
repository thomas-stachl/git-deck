using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Run;

public partial class RunWindow : Window
{
    private readonly ISettingsWindowService? _settingsWindowService;

    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public RunWindow() : this(null, null)
    {
    }

    public RunWindow(RunViewModel? viewModel, ISettingsWindowService? settingsWindowService)
    {
        DataContext = viewModel;
        _settingsWindowService = settingsWindowService;

        InitializeComponent();

        Activated += OnActivated;
        Deactivated += OnDeactivated;

        // Tunnelling, not bubbling: in commit mode the focused control is the file list, which
        // handles Space and the arrow keys itself and would mark them handled before they ever
        // reached the window. Keys this handler leaves unhandled still reach the focused control,
        // so typing into the search and message boxes is unaffected.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>The mode currently on screen, or null when the window is hidden.</summary>
    public RunMode? VisibleMode =>
        IsVisible && DataContext is RunViewModel viewModel ? viewModel.Mode : null;

    public void ShowNearTop(RunMode mode)
    {
        // Set the mode before activating: OnActivated decides what to focus from it.
        if (DataContext is RunViewModel viewModel)
        {
            _ = viewModel.OpenAsync(mode);
        }

        PositionNearTop();

        Show();
        Activate();
    }

    public void HideAndReset()
    {
        Hide();

        if (DataContext is RunViewModel viewModel)
        {
            viewModel.Reset();
        }
    }

    private void PositionNearTop()
    {
        var screen = Screens.ScreenFromPoint(Position) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var workingArea = screen.WorkingArea;
        var pixelWidth = (int)(Width * screen.Scaling);
        var x = workingArea.X + (workingArea.Width - pixelWidth) / 2;
        var y = workingArea.Y + (int)(workingArea.Height * 0.3);

        Position = new PixelPoint(x, y);
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        FocusForMode();
    }

    /// <summary>Hands focus to the active palette view, which knows its own focus rules.</summary>
    private void FocusForMode()
    {
        if (DataContext is not RunViewModel viewModel)
        {
            return;
        }

        if (viewModel.Mode is RunMode.Commit)
        {
            CommitView.FocusInput();
        }
        else
        {
            BranchView.FocusInput();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Losing focus mid-operation would hide the outcome; let it finish and report instead.
        if (DataContext is RunViewModel { IsBusy: true })
        {
            return;
        }

        HideAndReset();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        // Dismiss the palette first so settings comes up in front of it, not behind.
        HideAndReset();

        _settingsWindowService?.Show();
    }

    private async Task AcceptAsync(RunViewModel viewModel)
    {
        if (await viewModel.AcceptAsync())
        {
            HideAndReset();
            return;
        }

        // Staying open can mean the mode moved on a step — from files to message, say.
        FocusForMode();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not RunViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                // A mode may consume Escape to step back rather than close.
                if (!viewModel.TryStepBack())
                {
                    HideAndReset();
                }
                else
                {
                    FocusForMode();
                }

                e.Handled = true;
                break;

            // Bare Enter only: Shift+Enter falls through to the message box as a line break.
            case Key.Enter when e.KeyModifiers is KeyModifiers.None:
                _ = AcceptAsync(viewModel);
                e.Handled = true;
                break;

            case Key.Down:
                e.Handled = viewModel.MoveSelection(1);
                break;

            case Key.Up:
                e.Handled = viewModel.MoveSelection(-1);
                break;

            // Everything mode-specific (Space, Ctrl+A, Ctrl+G) is the active palette's business;
            // whatever it leaves unhandled still reaches the focused text box as ordinary typing.
            default:
                e.Handled = viewModel.HandleKey(e.Key, e.KeyModifiers);
                break;
        }
    }
}
