using Avalonia.Platform.Storage;
using GitDeck.App.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitDeck.App.Design;

internal sealed class DesignFilePickerService : IFilePickerService
{
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

    public Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter = null)
        => Task.FromResult<string?>(null);
}
