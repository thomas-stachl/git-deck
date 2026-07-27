using GitDeck.Git.Repositories;

namespace GitDeck.Core.Commands;

public sealed record CommandContext(Repository Repository, IReadOnlyDictionary<string, string> Args);