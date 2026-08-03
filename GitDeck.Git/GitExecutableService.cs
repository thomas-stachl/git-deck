namespace GitDeck.Git;

public sealed record GitAvailability(bool IsAvailable, string? Version);

public sealed record GitCommandResult(bool IsSuccess, string StandardOutput, string StandardError)
{
    /// <summary>The most useful message git produced, for surfacing in the UI.</summary>
    public string? FailureMessage => IsSuccess
        ? null
        : FirstNonEmptyLine(StandardError) ?? FirstNonEmptyLine(StandardOutput) ?? "The git command failed.";

    private static string? FirstNonEmptyLine(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0);
}

public class GitExecutableService(IProcessRunner processRunner) : IGitExecutableService
{
    /// <summary>Never let git stop for an interactive prompt; there is no console to answer it.</summary>
    private static readonly Dictionary<string, string> NonInteractive = new()
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    public async Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(gitPath, null, ["--version"], cancellationToken);

        return result.IsSuccess
            ? new GitAvailability(true, result.StandardOutput.Trim())
            : new GitAvailability(false, null);
    }

    public async Task<GitCommandResult> RunAsync(
        string? gitPath,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await processRunner.RunAsync(
            string.IsNullOrWhiteSpace(gitPath) ? "git" : gitPath,
            workingDirectory,
            arguments,
            standardInput: null,
            NonInteractive,
            cancellationToken);

        return new GitCommandResult(result.IsSuccess, result.StandardOutput, result.StandardError);
    }
}
