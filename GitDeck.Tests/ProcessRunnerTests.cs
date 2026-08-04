using GitDeck.Git;
using Xunit;

namespace GitDeck.Tests;

/// <summary>
/// Exercises the runner against cmd.exe, which is present on every Windows machine this app
/// targets, so no fixture executables are needed.
/// </summary>
public class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task CapturesStandardOutputAndExitCode()
    {
        var result = await _runner.RunAsync("cmd.exe", null, ["/c", "echo hello"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task ReportsNonZeroExitAsFailure()
    {
        var result = await _runner.RunAsync("cmd.exe", null, ["/c", "echo broken 1>&2 & exit 3"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("broken", result.FailureMessage);
    }

    [Fact]
    public async Task WritesStandardInputToTheProcess()
    {
        // `more` copies stdin to stdout and exits on EOF, which closing the pipe provides.
        var result = await _runner.RunAsync("cmd.exe", null, ["/c", "more"], standardInput: "alpha\r\nbeta\r\n");

        Assert.True(result.IsSuccess);
        Assert.Contains("alpha", result.StandardOutput);
        Assert.Contains("beta", result.StandardOutput);
    }

    [Fact]
    public async Task AppliesEnvironmentOverrides()
    {
        var result = await _runner.RunAsync(
            "cmd.exe",
            null,
            ["/c", "echo %GITDECK_TEST_VALUE%"],
            environment: new Dictionary<string, string> { ["GITDECK_TEST_VALUE"] = "42" });

        Assert.Equal("42", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task MissingExecutableReturnsFailureResultInsteadOfThrowing()
    {
        var result = await _runner.RunAsync("gitdeck-no-such-executable", null, []);

        Assert.False(result.IsSuccess);
        Assert.Equal(-1, result.ExitCode);
        Assert.NotNull(result.FailureMessage);
    }

    [Fact]
    public async Task CancellationKillsTheProcessAndThrows()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _runner.RunAsync(
            "cmd.exe",
            null,
            ["/c", "ping -n 30 localhost >nul"],
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task EarlyExitingChildStillReportsItsOwnOutput()
    {
        // The child never reads stdin and exits at once, so the large write breaks the pipe.
        // The result must carry the child's actual output, not just the pipe error.
        var largeInput = new string('x', 1 << 20);

        var result = await _runner.RunAsync(
            "cmd.exe",
            null,
            ["/c", "echo real-error & exit 2"],
            standardInput: largeInput);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("real-error", result.StandardOutput);
    }
}
