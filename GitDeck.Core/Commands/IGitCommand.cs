namespace GitDeck.Core.Commands;

public interface IGitCommand
{
    string Id { get; }              // "git.branch.switch"
    string Title { get; }           // "Switch Branch"
    bool RequiresInput { get; }     // needs the palette to pick a branch?
    Task<ICommandResult> ExecuteAsync(CommandContext ctx, CancellationToken ct);
}