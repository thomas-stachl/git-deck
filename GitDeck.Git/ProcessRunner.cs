using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GitDeck.Git;

public sealed record ProcessResult(bool IsSuccess, int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>The most useful message the process produced, for surfacing in the UI.</summary>
    public string? FailureMessage => IsSuccess
        ? null
        : ProcessText.FirstNonEmptyLine(StandardError)
          ?? ProcessText.FirstNonEmptyLine(StandardOutput)
          ?? $"Exited with code {ExitCode}.";
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

            // Without an explicit encoding the redirected pipes decode with the console code page,
            // which turns git's UTF-8 output into mojibake when the app is launched from a
            // non-UTF-8 console.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (standardInput is not null)
        {
            startInfo.StandardInputEncoding = Encoding.UTF8;
        }

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
            //
            // The pumps deliberately take no cancellation token. Their natural completion signal is
            // the pipes closing — which killing the process causes — and cancelling the reads
            // instead would abandon them mid-read, discarding output that is often the real error
            // message and leaving their failures to surface later as unobserved task exceptions.
            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            string? inputFailure = null;

            try
            {
                if (standardInput is not null)
                {
                    try
                    {
                        await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException ex)
                    {
                        // The child exited or closed its stdin before consuming everything — a CLI
                        // rejecting a flag does exactly this. Its own stdout/stderr explain why far
                        // better than the pipe error, so carry on and collect them.
                        inputFailure = ex.Message;
                    }
                    finally
                    {
                        TryCloseStandardInput(process);
                    }
                }

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Nothing is waiting for it any more, so don't leave it running. Reached from the
                // stdin write as well as the wait — a cancellation mid-write used to leak the child.
                TryKill(process);
                await ObserveQuietly(output, error).ConfigureAwait(false);
                throw;
            }

            var standardOutput = await output.ConfigureAwait(false);
            var standardError = await error.ConfigureAwait(false);

            if (inputFailure is not null)
            {
                var note = $"Writing to standard input failed: {inputFailure}";
                standardError = standardError.Length == 0 ? note : $"{standardError}\n{note}";
            }

            return new ProcessResult(process.ExitCode == 0, process.ExitCode, standardOutput, standardError);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new ProcessResult(false, -1, string.Empty, ex.Message);
        }
    }

    private static void TryCloseStandardInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe already broke; the write path has recorded why.
        }
    }

    /// <summary>
    /// Awaits the pumps of a killed process so their broken-pipe failures are observed here rather
    /// than crashing the finalizer thread's unobserved-exception path later.
    /// </summary>
    private static async Task ObserveQuietly(Task<string> output, Task<string> error)
    {
        try
        {
            await Task.WhenAll(output, error).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (
            // AggregateException is documented for Kill(entireProcessTree): descendants that could
            // not be killed. Everything else means the process is already gone.
            ex is InvalidOperationException or NotSupportedException or Win32Exception or AggregateException)
        {
        }
    }
}
