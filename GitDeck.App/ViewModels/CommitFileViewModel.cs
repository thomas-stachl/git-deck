using CommunityToolkit.Mvvm.ComponentModel;
using GitDeck.Git.Repositories;

namespace GitDeck.App.ViewModels;

/// <summary>One tickable row in the commit file list.</summary>
public partial class CommitFileViewModel(ChangedFile file) : ObservableObject
{
    public ChangedFile File { get; } = file;

    public string Path => File.Path;

    public string KindLabel => File.Kind switch
    {
        FileChangeKind.Untracked => "new",
        FileChangeKind.Added => "added",
        FileChangeKind.Deleted => "deleted",
        FileChangeKind.Renamed => "renamed",
        FileChangeKind.TypeChanged => "type changed",
        FileChangeKind.Conflicted => "conflicted",
        _ => "modified",
    };

    /// <summary>Starts ticked: committing everything you changed is the common case.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;
}
