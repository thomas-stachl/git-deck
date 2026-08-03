namespace GitDeck.Core.Settings;

/// <summary>
/// Encrypts secrets before they reach settings.json. An API key is a real credential, so it never
/// goes to disk in the clear.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Whether secrets can be stored at all on this platform.</summary>
    bool IsSupported { get; }

    /// <summary>Encrypts a secret for storage, or returns null when there is nothing to store.</summary>
    string? Protect(string? secret);

    /// <summary>Decrypts a stored secret, returning null when it is absent or unreadable.</summary>
    string? Unprotect(string? protectedSecret);
}
