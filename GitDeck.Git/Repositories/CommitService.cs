namespace GitDeck.Git.Repositories;

public sealed class CommitService(IGitExecutableService gitExecutableService) : ICommitService
{
    public async Task<CommitResult> CommitAsync(CommitRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return CommitResult.Failed("A commit needs a message.");
        }

        if (request.Files.Count == 0)
        {
            return CommitResult.Failed("Select at least one file to commit.");
        }

        // Untracked paths cannot take part in a partial commit until git knows about them, so they
        // are added first. Tracked files are left alone: --only commits them straight from the
        // working tree without disturbing what is already staged.
        var untracked = request.Files
            .Where(file => file.IsUntracked)
            .Select(file => file.Path)
            .ToList();

        if (untracked.Count > 0)
        {
            var add = await RunAsync(request, ["add", "--", .. untracked], cancellationToken);

            if (!add.IsSuccess)
            {
                return CommitResult.Failed($"Adding the new files failed: {add.FailureMessage}");
            }
        }

        var paths = request.Files.Select(file => file.Path);
        var commit = await RunAsync(
            request,
            ["commit", "--only", "--message", request.Message, "--", .. paths],
            cancellationToken);

        return commit.IsSuccess
            ? new CommitResult(true, null)
            : CommitResult.Failed(commit.FailureMessage ?? "The commit failed.");
    }

    private Task<GitCommandResult> RunAsync(CommitRequest request, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => gitExecutableService.RunAsync(request.GitExecutablePath, request.WorkingDirectory, arguments, cancellationToken);
}
