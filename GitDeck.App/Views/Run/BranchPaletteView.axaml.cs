using Avalonia.Controls;

namespace GitDeck.App.Views.Run;

public partial class BranchPaletteView : UserControl
{
    public BranchPaletteView()
    {
        InitializeComponent();
    }

    /// <summary>Puts focus in the search box, ready to type over the previous query.</summary>
    public void FocusInput()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
