using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitDeck.App.Services;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.CheckGitAvailabilityCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Turns the hotkey box into a capture field: while it has focus, key presses configure the
    /// hotkey instead of doing what they normally would.
    /// </summary>
    private void OnHotkeyBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        // Leave keyboard navigation intact, so the box can still be tabbed out of.
        if (e.Key is Key.Tab && e.KeyModifiers is KeyModifiers.None)
        {
            return;
        }

        e.Handled = true;

        // A modifier on its own is the start of a combination, not a combination.
        if (Hotkeys.IsModifier(e.Key))
        {
            return;
        }

        viewModel.CaptureHotkey(new KeyGesture(e.Key, e.KeyModifiers));
    }
}