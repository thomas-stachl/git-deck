using GitDeck.Core.Settings;
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
                request.WorkingDirectory,
                ["diff", "HEAD", "--", .. tracked],
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!diff.IsSuccess)
            {
                // An unborn HEAD, a locked index — whatever it is, generating a message from a
                // silently incomplete diff would describe the wrong change.
                return DiffResult.Failed($"Reading the diff failed: {diff.FailureMessage}");
            }

            builder.Append(diff.StandardOutput);
        }

        // Untracked files have nothing to diff against, so their content is read directly rather than
        // through git — which also avoids `git diff --no-index` and its exit code 1 for "differs".
        foreach (var file in request.Files.Where(file => file.IsUntracked))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendNewFile(request.WorkingDirectory, file.Path, builder);
        }

        return Truncate(builder.ToString(), request.MaxCharacters);
    }

    private static void AppendNewFile(string workingDirectory, string path, StringBuilder builder)
    {
        builder.Append("\n--- new file: ").Append(path).Append('\n');

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
            builder.Append(content[..CutIndex(content, MaxUntrackedFileCharacters)]).Append("\n(truncated)\n");
            return;
        }

        builder.Append(content);

        if (!content.EndsWith('\n'))
        {
            builder.Append('\n');
        }
    }

    internal static DiffResult Truncate(string diff, int maxCharacters)
    {
        // A zero or negative budget from a hand-edited settings file must not mean "unlimited" —
        // bounding what is sent to a provider is the entire point of the cap.
        if (maxCharacters <= 0)
        {
            maxCharacters = AiSettings.DefaultMaxDiffCharacters;
        }

        return diff.Length <= maxCharacters
            ? new DiffResult(diff, false)
            : new DiffResult(diff[..CutIndex(diff, maxCharacters)], true);
    }

    /// <summary>
    /// Moves a cut point off the middle of a surrogate pair. A lone surrogate is invalid UTF-16
    /// that the JSON serializer in the OpenAI client refuses to encode.
    /// </summary>
    internal static int CutIndex(string text, int index) =>
        char.IsHighSurrogate(text[index - 1]) ? index - 1 : index;
}
