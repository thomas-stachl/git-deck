using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace GitDeck.Core.Settings;

/// <summary>
/// Protects secrets with Windows DPAPI under <see cref="DataProtectionScope.CurrentUser"/>: the
/// stored blob can only be decrypted by this Windows account on this machine, so copying
/// settings.json elsewhere yields nothing.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSecretProtector : ISecretProtector
{
    public bool IsSupported => true;

    public string? Protect(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return null;
        }

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(encrypted);
    }

    public string? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedSecret),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Written by a different user or machine, or corrupted. Treat as absent rather than
            // failing — the user can re-enter the key.
            return null;
        }
    }
}
