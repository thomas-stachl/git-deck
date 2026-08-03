using System.ComponentModel;
using System.Diagnostics;

namespace GitDeck.Git;

public sealed record ProcessResult(bool IsSuccess, int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>The most useful message the process produced, for surfacing in the UI.</summary>
    public string? FailureMessage => IsSuccess
        ? null
        : FirstNonEmptyLine(StandardError) ?? FirstNonEmptyLine(StandardOutput) ?? $"Exited with code {ExitCode}.";

    private static string? FirstNonEmptyLine(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0);
}

public interface IProcessRunner
{
    /// <summary>
    /// Runs an executable and collects its output. <paramref name="standardInput"/> is written to the
    /// process and the pipe then closed, which is how input too large for a command line is passed —
    /// on Windows the whole command line is capped near 32K characters.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string fileName,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
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

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessResult(false, -1, string.Empty, $"Could not start {fileName}.");
            }

            // Start draining the output pipes before writing input. A process that fills its output
            // buffer while we are still writing would otherwise deadlock: it blocks on a write we are
            // not reading, and we block on a write it is not reading.
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
                process.StandardInput.Close();
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Nothing is waiting for it any more, so don't leave it running.
                TryKill(process);
                throw;
            }

            return new ProcessResult(process.ExitCode == 0, process.ExitCode, await output, await error);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new ProcessResult(false, -1, string.Empty, ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // Already gone.
        }
    }
}
