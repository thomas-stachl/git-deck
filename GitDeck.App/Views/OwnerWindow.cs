using Avalonia;
using Avalonia.Controls;

namespace GitDeck.App.Views;

/// <summary>
/// Exists solely to keep <c>IClassicDesktopStyleApplicationLifetime.Windows</c> non-empty for the
/// app's whole lifetime.
/// </summary>
/// <remarks>
/// <see cref="Services.FilePickerService"/> resolves its owning <c>TopLevel</c> by scanning that
/// collection, but Avalonia only adds a window to it once <c>Show()</c>/<c>Activate()</c> has been
/// called — not at construction — and <see cref="Settings.SettingsWindow"/>/
/// <see cref="Run.RunWindow"/> are both constructed eagerly at startup but never shown until the
/// user opens one. On a cold start (tray up, nothing ever opened this session) the collection
/// would otherwise be empty, so an IPC-triggered folder pick (the Stream Deck Property Inspector's
/// "Browse…") would silently return null instead of opening a dialog. This window is shown once at
/// startup and never hidden or closed, so that scenario can't happen.
/// </remarks>
public sealed class OwnerWindow : Window
{
    public OwnerWindow()
    {
        ShowInTaskbar = false;
        WindowDecorations = WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // 1x1 and fully transparent is enough to be imperceptible without pushing it off-screen —
        // an off-screen owner risks an off-screen owned dialog too, since Windows generally centers
        // an owned dialog on the owner's monitor.
        Position = new PixelPoint(0, 0);
        Width = 1;
        Height = 1;
        Opacity = 0;
    }
}
