using System.ComponentModel;
using System.Diagnostics;

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

public class GitExecutableService : IGitExecutableService
{
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
        var startInfo = new ProcessStartInfo(string.IsNullOrWhiteSpace(gitPath) ? "git" : gitPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Never let git stop for an interactive prompt; there is no console to answer it.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new GitCommandResult(false, string.Empty, "Could not start the git executable.");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            return new GitCommandResult(process.ExitCode == 0, await standardOutput, await standardError);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GitCommandResult(false, string.Empty, ex.Message);
        }
    }
}
