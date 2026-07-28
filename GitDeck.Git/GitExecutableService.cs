using System.ComponentModel;
using System.Diagnostics;

namespace GitDeck.Git;

public sealed record GitAvailability(bool IsAvailable, string? Version);

public class GitExecutableService
{
    public async Task<GitAvailability> CheckAvailabilityAsync(string? gitPath = null, CancellationToken cancellationToken = default)
    {
        var fileName = string.IsNullOrWhiteSpace(gitPath) ? "git" : gitPath;

        var startInfo = new ProcessStartInfo(fileName, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new GitAvailability(false, null);
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0
                ? new GitAvailability(true, output.Trim())
                : new GitAvailability(false, null);
        }
        catch (Win32Exception)
        {
            return new GitAvailability(false, null);
        }
    }
}
