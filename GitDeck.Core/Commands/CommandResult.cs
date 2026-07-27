namespace GitDeck.Core.Commands;

public interface ICommandResult;

public sealed record Completed(string? Message = null) : ICommandResult;
public sealed record NeedsInput(string CommandId, string? Prefill = null) : ICommandResult;
public sealed record Failed(string Error, bool IsGitError = false) : ICommandResult;