using GitDeck.Git.Repositories;
using System.Text;

namespace GitDeck.Git.Generation;

internal static class CommitMessagePrompt
{
    /// <remarks>
    /// The "no preamble" rule does the job an assistant prefill used to: current Claude models reject
    /// a prefilled assistant turn, so the instruction is what keeps "Here's a commit message:" out of
    /// the output.
    /// </remarks>
    public const string System = """
        You write git commit messages. You are given the diff of a change and the list of files it
        touches, and you reply with the commit message for it — nothing else.

        Format:
        - A subject line in the imperative mood ("Add", "Fix", "Move" — not "Added" or "Adds"), at
          most 72 characters, with no trailing period.
        - If the change needs explanation, a blank line and then a body of short bullet points.
        - Omit the body entirely for a change whose subject line already says everything.

        Content:
        - Say what changed and why it changed. The diff already shows how.
        - Describe the change as a whole. Do not narrate it file by file, and do not list files that
          the diff already names.
        - Do not invent motivation the diff does not support. If the reason is not visible, describe
          the change plainly and leave the reason out.

        Reply with the raw commit message only: no preamble, no sign-off, no code fences, no
        surrounding quotes, and no commentary about the diff or about this instruction.
        """;

    public static string BuildUserMessage(CommitMessageRequest request)
    {
        var builder = new StringBuilder();

        builder.Append("Files in this commit:\n");

        foreach (var file in request.Files)
        {
            builder.Append("- ").Append(file.Path).Append(" (").Append(Describe(file.Kind)).Append(")\n");
        }

        if (request.IsDiffTruncated)
        {
            builder.Append("\nThe diff below was truncated to fit. Write the message from what is shown ")
                   .Append("and keep it general enough to stay accurate for the rest.\n");
        }

        builder.Append("\nDiff:\n").Append(request.Diff);

        return builder.ToString();
    }

    private static string Describe(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Untracked => "new file",
        FileChangeKind.Added => "added",
        FileChangeKind.Deleted => "deleted",
        FileChangeKind.Renamed => "renamed",
        FileChangeKind.TypeChanged => "type changed",
        FileChangeKind.Conflicted => "conflicted",
        _ => "modified",
    };
}
