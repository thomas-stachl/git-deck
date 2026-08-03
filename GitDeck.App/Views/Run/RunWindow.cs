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

    private async Task ExecuteSelectedAsync(RunViewModel viewModel)
    {
        if (await viewModel.ExecuteSelectedAsync())
        {
            HideAndReset();
        }
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

            case Key.Enter:
                _ = ExecuteSelectedAsync(viewModel);
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
