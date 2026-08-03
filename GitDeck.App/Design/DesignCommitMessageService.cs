using GitDeck.App.Services;
using GitDeck.Git.Repositories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Design;

internal sealed class DesignCommitMessageService : ICommitMessageService
{
    public bool IsEnabled => true;

    public Task<GeneratedCommitMessage> GenerateAsync(
        string workingDirectory,
        IReadOnlyList<ChangedFile> files,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GeneratedCommitMessage(
            "Add commit message generation to the run window\n\n- Collect the diff of the ticked files\n- Offer Ctrl+G in the message phase",
            null));
}
