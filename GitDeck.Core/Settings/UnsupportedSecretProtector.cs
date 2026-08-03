namespace GitDeck.Core.Settings;

/// <summary>
/// Stands in where GitDeck has no OS-backed secret store. It deliberately stores nothing rather than
/// falling back to plaintext — a key on disk in the clear is worse than a key the user has to supply
/// through the environment.
/// </summary>
public sealed class UnsupportedSecretProtector : ISecretProtector
{
    public bool IsSupported => false;

    public string? Protect(string? secret) => null;

    public string? Unprotect(string? protectedSecret) => null;
}
