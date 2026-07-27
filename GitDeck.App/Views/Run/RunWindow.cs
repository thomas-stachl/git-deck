using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Run;

public partial class RunWindow : Window
{
    public RunWindow(RunViewModel viewModel)
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
        Hide();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }
}
