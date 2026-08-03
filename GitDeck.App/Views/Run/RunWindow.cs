using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Run;

public partial class RunWindow : Window
{
    // Parameterless constructor required by the Avalonia XAML previewer/designer.
    public RunWindow() : this(null)
    {
    }

    public RunWindow(RunViewModel? viewModel)
    {
        DataContext = viewModel;

        InitializeComponent();

        Activated += OnActivated;
        Deactivated += OnDeactivated;
        KeyDown += OnKeyDown;
    }

    public void ShowNearTop()
    {
        PositionNearTop();

        Show();
        Activate();

        if (DataContext is RunViewModel viewModel)
        {
            _ = viewModel.LoadBranchesAsync();
        }
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
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        HideAndReset();
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
                HideAndReset();
                e.Handled = true;
                break;

            case Key.Down when viewModel.Results.Count > 0:
                viewModel.SelectedIndex = (viewModel.SelectedIndex + 1) % viewModel.Results.Count;
                e.Handled = true;
                break;

            case Key.Up when viewModel.Results.Count > 0:
                viewModel.SelectedIndex = (viewModel.SelectedIndex - 1 + viewModel.Results.Count) % viewModel.Results.Count;
                e.Handled = true;
                break;
        }
    }
}
