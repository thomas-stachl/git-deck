using GitDeck.Git.Repositories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Services;

public sealed record GeneratedCommitMessage(string? Message, string? ErrorMessage)
{
    public static GeneratedCommitMessage Failed(string errorMessage) => new(null, errorMessage);

    public bool IsGenerated => Message is not null;
}

/// <summary>
/// Assembles everything commit message generation needs — settings, the resolved API key, and the
/// diff of the chosen files — so the palette view model depends on one thing rather than four.
/// </summary>
public interface ICommitMessageService
{
    /// <summary>Whether generation is configured and turned on. False keeps the affordance hidden.</summary>
    bool IsEnabled { get; }

    Task<GeneratedCommitMessage> GenerateAsync(
        string workingDirectory,
        IReadOnlyList<ChangedFile> files,
        CancellationToken cancellationToken = default);
}
