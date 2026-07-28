using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitDeck.App.Services;

public class FilePickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = GetActiveTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter = null)
    {
        var topLevel = GetActiveTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilter,
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static TopLevel? GetActiveTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.Windows.FirstOrDefault();
        return window is null ? null : TopLevel.GetTopLevel(window);
    }
}
