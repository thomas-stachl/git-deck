using GitDeck.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitDeck.Git;

public sealed record GitAvailability(bool IsAvailable, string? Version);

/// <param name="TimedOut">
/// The command was killed because it exceeded its time budget — most likely a credential prompt or
/// hook waiting for input that will never come.
/// </param>
public sealed record GitCommandResult(
    bool IsSuccess,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false)
{
    /// <summary>The most useful message git produced, for surfacing in the UI.</summary>
    public string? FailureMessage => IsSuccess
        ? null
        : TimedOut
            ? "git did not finish in time — a credential prompt or hook may be waiting for input."
            : ProcessText.FirstNonEmptyLine(StandardError)
              ?? ProcessText.FirstNonEmptyLine(StandardOutput)
              ?? "The git command failed.";
}

public sealed class GitExecutableService(
    IProcessRunner processRunner,
    ISettingsService settingsService,
    ILogger<GitExecutableService>? logger = null) : IGitExecutableService
{
    /// <summary>
    /// Long enough for a push over a slow network, short enough that a stuck process cannot wedge
    /// the palette forever. GIT_TERMINAL_PROMPT=0 below only suppresses terminal prompts — Git
    /// Credential Manager's window or an SSH askpass helper can still block indefinitely, and this
    /// budget is the backstop for exactly that.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Never let git stop for an interactive prompt; there is no console to answer it.</summary>
    private static readonly Dictionary<string, string> NonInteractive = new()
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    private readonly ILogger<GitExecutableService> _logger = logger ?? NullLogger<GitExecutableService>.Instance;

    public async Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default)
    {
        // The explicit parameter stays so Settings can probe a candidate path before it is saved.
        var result = await RunGitAsync(
            gitPath ?? settingsService.Settings.GitExecutablePath,
            null,
            ["--version"],
            AvailabilityTimeout,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new GitAvailability(true, result.StandardOutput.Trim())
            : new GitAvailability(false, null);
    }

    public Task<GitCommandResult> RunAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => RunGitAsync(
            settingsService.Settings.GitExecutablePath,
            workingDirectory,
            arguments,
            timeout ?? DefaultTimeout,
            cancellationToken);

    private async Task<GitCommandResult> RunGitAsync(
        string? gitPath,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var result = await processRunner.RunAsync(
                string.IsNullOrWhiteSpace(gitPath) ? "git" : gitPath,
                workingDirectory,
                arguments,
                standardInput: null,
                NonInteractive,
                timeoutSource.Token).ConfigureAwait(false);

            return new GitCommandResult(result.IsSuccess, result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The time budget ran out, not the caller: hand back a result so the failure surfaces
            // as a message instead of an exception from a fire-and-forget task.
            _logger.LogWarning(
                "git {Arguments} in {WorkingDirectory} was killed after {Timeout}s.",
                string.Join(' ', arguments), workingDirectory ?? "(none)", timeout.TotalSeconds);

            return new GitCommandResult(false, -1, string.Empty, string.Empty, TimedOut: true);
        }
    }
}
