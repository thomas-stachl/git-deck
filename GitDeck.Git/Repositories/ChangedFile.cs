namespace GitDeck.Git.Repositories;

public enum FileChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    TypeChanged,
    Untracked,
    Conflicted,
}

/// <param name="Path">
/// Relative to the working tree root, with forward slashes — the form git accepts as a pathspec.
/// An untracked directory appears as a single entry ending in a slash rather than one entry per file
/// inside it, matching how <c>git status</c> reports it.
/// </param>
/// <param name="IsUntracked">
/// Whether git does not know this path yet, which means it has to be added before it can take part
/// in a commit.
/// </param>
public sealed record ChangedFile(string Path, FileChangeKind Kind, bool IsUntracked);
