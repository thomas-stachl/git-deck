namespace GitDeck.Git.Generation;

/// <summary>
/// Delegates to a locally installed Claude Code, which is already authenticated — so this provider
/// needs no API key and no configuration.
/// </summary>
/// <remarks>
/// Unlike the other adapters this drives a CLI, whose flags are not an API contract the way
/// <c>POST /v1/messages</c> is. That is the trade for zero setup.
/// </remarks>
internal sealed class ClaudeCliCommitMessageClient(IProcessRunner processRunner) : ICommitMessageProvider
{
    /// <summary>
    /// Claude Code is a coding agent, and left to itself it will go read files and run git rather than
    /// answering from the diff it was handed — slower, and it can wander off the change in question.
    /// </summary>
    private static readonly string[] DisallowedTools =
        ["Bash", "Read", "Edit", "Write", "Glob", "Grep", "WebFetch", "WebSearch", "Task", "TodoWrite"];

    public async Task<CommitMessageResult> GenerateAsync(CommitMessageRequest request, CancellationToken cancellationToken)
    {
        if (ClaudeCliLocator.Find() is not { } executable)
        {
            return CommitMessageResult.Failed(
                "Claude Code was not found. Install it, or choose a different provider in Settings.");
        }

        List<string> arguments =
        [
            "--print",
            "Write a commit message for the diff on standard input.",
            "--append-system-prompt", CommitMessagePrompt.System,
            "--disallowedTools", .. DisallowedTools,
        ];

        // Empty means "whatever Claude Code is configured to use", which keeps this zero-config.
        if (!string.IsNullOrWhiteSpace(request.Options.Model))
        {
            arguments.Add("--model");
            arguments.Add(request.Options.Model);
        }

        // Run in the repository so the CLI picks up that project's CLAUDE.md and settings —
        // inheriting the app's own working directory made the output depend on how GitDeck was
        // launched. Cancellation (including the generator's timeout) propagates to the runner,
        // which kills the process tree.
        var result = await processRunner.RunAsync(
            executable,
            request.WorkingDirectory,
            arguments,
            CommitMessagePrompt.BuildUserMessage(request),
            environment: null,
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return CommitMessageResult.Failed($"Claude Code failed: {result.FailureMessage}");
        }

        return CommitMessageResult.FromText(result.StandardOutput, "Claude Code returned an empty message.");
    }
}
