namespace GitDeck.Git.Repositories;

/// <param name="MaxCharacters">
/// Budget for the whole diff. Anything beyond it is cut and flagged, so a large change set becomes a
/// smaller prompt rather than a rejected request.
/// </param>
public sealed record DiffRequest(
    string WorkingDirectory,
    IReadOnlyList<ChangedFile> Files,
    int MaxCharacters,
    string? GitExecutablePath = null);

public sealed record DiffResult(string Diff, bool IsTruncated)
{
    public static readonly DiffResult Empty = new(string.Empty, false);

    public bool IsEmpty => Diff.Length == 0;
}

public interface IDiffService
{
    /// <summary>
    /// Collects the diff for exactly the given files — the same span of change that
    /// <c>git commit --only</c> would record.
    /// </summary>
    Task<DiffResult> GetDiffAsync(DiffRequest request, CancellationToken cancellationToken = default);
}
