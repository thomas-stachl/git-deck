namespace GitDeck.Ipc;

/// <summary>Shared constants for the named-pipe transport between GitDeck.App and any IPC client.</summary>
public static class GitDeckIpcConstants
{
    /// <summary>
    /// The named pipe GitDeck.App listens on. Duplicated by hand in any non-.NET client (there is no
    /// shared codegen across a language boundary) — keep it in sync with whatever the Stream Deck
    /// plugin hardcodes.
    /// </summary>
    public const string PipeName = "GitDeck.Ipc";
}
