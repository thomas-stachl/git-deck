using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitDeck.App.Services;

public interface IFilePickerService
{
    Task<string?> PickFolderAsync(string title);

    Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter = null);
}
