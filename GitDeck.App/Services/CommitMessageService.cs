using GitDeck.Core.Settings;
using GitDeck.Git.Generation;
using GitDeck.Git.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GitDeck.App.Services;

public class CommitMessageService(
    ISettingsService settingsService,
    ISecretProtector secretProtector,
    IDiffService diffService,
    ICommitMessageGenerator generator) : ICommitMessageService
{
    public bool IsEnabled => settingsService.Settings.Ai.IsEnabled;

    public async Task<GeneratedCommitMessage> GenerateAsync(
        string workingDirectory,
        IReadOnlyList<ChangedFile> files,
        CancellationToken cancellationToken = default)
    {
        var settings = settingsService.Settings.Ai;

        if (!settings.IsEnabled)
        {
            return GeneratedCommitMessage.Failed("Message generation is turned off. Enable it in Settings.");
        }

        if (files.Count == 0)
        {
            return GeneratedCommitMessage.Failed("Tick at least one file first.");
        }

        var options = new AiGenerationOptions(
            settings.Provider,
            settings.Model,
            ResolveApiKey(settings),
            settings.BaseUrl,
            settings.MaxDiffCharacters);

        var diff = await diffService.GetDiffAsync(
            new DiffRequest(workingDirectory, files, options.MaxDiffCharacters, settingsService.Settings.GitExecutablePath),
            cancellationToken);

        if (diff.IsEmpty)
        {
            return GeneratedCommitMessage.Failed("The selected files produced no diff to describe.");
        }

        var result = await generator.GenerateAsync(
            new CommitMessageRequest(options, files, diff.Diff, diff.IsTruncated),
            cancellationToken);

        return result.IsGenerated
            ? new GeneratedCommitMessage(result.Message, null)
            : GeneratedCommitMessage.Failed(result.ErrorMessage ?? "Could not generate a message.");
    }

    /// <summary>
    /// Prefers the key stored in settings, falling back to the provider's usual environment variable —
    /// which is also the only option on platforms where secrets cannot be stored.
    /// </summary>
    private string? ResolveApiKey(AiSettings settings)
    {
        var stored = secretProtector.Unprotect(settings.ProtectedApiKey);

        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        var variable = settings.Provider switch
        {
            AiProviderKind.Anthropic => "ANTHROPIC_API_KEY",
            _ => "OPENAI_API_KEY",
        };

        return Environment.GetEnvironmentVariable(variable);
    }
}
