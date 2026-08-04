namespace GitDeck.Git;

/// <summary>Shared helper for turning raw process output into the single line the UI can show.</summary>
internal static class ProcessText
{
    public static string? FirstNonEmptyLine(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .FirstOrDefault(line => line.Length > 0);
}
