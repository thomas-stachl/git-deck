using System.Text;

namespace GitDeck.Git.Repositories;

public sealed class DiffService(IGitExecutableService gitExecutableService) : IDiffService
{
    /// <summary>Per-file budget for a new file, so one large addition cannot crowd out everything else.</summary>
    private const int MaxUntrackedFileCharacters = 4000;

    public async Task<DiffResult> GetDiffAsync(DiffRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Files.Count == 0)
        {
            return DiffResult.Empty;
        }

        var builder = new StringBuilder();

        // Tracked files: HEAD against the working tree, which spans staged and unstaged changes the
        // same way the commit will.
        var tracked = request.Files
            .Where(file => !file.IsUntracked)
            .Select(file => file.Path)
            .ToList();

        if (tracked.Count > 0)
        {
            var diff = await gitExecutableService.RunAsync(
                request.GitExecutablePath,
                request.WorkingDirectory,
                ["diff", "HEAD", "--", .. tracked],
                cancellationToken);

            if (diff.IsSuccess)
            {
                builder.Append(diff.StandardOutput);
            }
        }

        // Untracked files have nothing to diff against, so their content is read directly rather than
        // through git — which also avoids `git diff --no-index` and its exit code 1 for "differs".
        foreach (var file in request.Files.Where(file => file.IsUntracked))
        {
            AppendNewFile(request.WorkingDirectory, file.Path, builder);
        }

        return Truncate(builder.ToString(), request.MaxCharacters);
    }

    private static void AppendNewFile(string workingDirectory, string path, StringBuilder builder)
    {
        builder.Append("\n--- new file: ").Append(path).Append('\n');

        // An untracked directory is reported by git as a single trailing-slash entry.
        if (path.EndsWith('/'))
        {
            builder.Append("(new directory)\n");
            return;
        }

        string content;
        try
        {
            content = File.ReadAllText(Path.Combine(workingDirectory, path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            builder.Append("(could not be read)\n");
            return;
        }

        // A NUL byte is the same heuristic git uses to call a file binary.
        if (content.Contains('\0'))
        {
            builder.Append("(binary file)\n");
            return;
        }

        if (content.Length > MaxUntrackedFileCharacters)
        {
            builder.Append(content[..MaxUntrackedFileCharacters]).Append("\n(truncated)\n");
            return;
        }

        builder.Append(content);

        if (!content.EndsWith('\n'))
        {
            builder.Append('\n');
        }
    }

    private static DiffResult Truncate(string diff, int maxCharacters)
    {
        if (maxCharacters <= 0 || diff.Length <= maxCharacters)
        {
            return new DiffResult(diff, false);
        }

        return new DiffResult(diff[..maxCharacters], true);
    }
}
