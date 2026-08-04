using Avalonia.Controls;
using GitDeck.App.ViewModels;

namespace GitDeck.App.Views.Run;

public partial class CommitPaletteView : UserControl
{
    public CommitPaletteView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Puts focus where the keyboard is expected to act: the message box while a message is being
    /// typed, the file list while files are being ticked.
    /// </summary>
    public void FocusInput()
    {
        if (DataContext is not CommitPaletteViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsMessagePhase)
        {
            MessageBox.Focus();
            MessageBox.SelectAll();
        }
        else
        {
            // Nothing to type into during file selection, but something has to hold focus for
            // key events to reach the window at all.
            FileList.Focus();
        }
    }
}
