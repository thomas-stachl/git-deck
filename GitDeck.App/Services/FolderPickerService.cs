using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace GitDeck.App.Services;

public class FolderPickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.Windows.FirstOrDefault();
        var topLevel = window is null ? null : TopLevel.GetTopLevel(window);
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
}
