using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Azariah.App.Services;

/// <summary>Pickers and clipboard, which need the window (TopLevel) to exist.</summary>
public sealed class PlatformUi
{
    public TopLevel? TopLevel { get; set; }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (TopLevel?.StorageProvider is not { CanPickFolder: true } provider)
        {
            return null;
        }

        var result = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title)
    {
        if (TopLevel?.StorageProvider is not { CanOpen: true } provider)
        {
            return [];
        }

        var result = await provider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = true });
        return result.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }

    /// <summary>Puts files on the system clipboard so they can be pasted into Explorer.</summary>
    public async Task SetClipboardFilesAsync(IEnumerable<string> paths)
    {
        if (TopLevel is not { Clipboard: { } clipboard, StorageProvider: { } provider })
        {
            return;
        }

        try
        {
            var items = new List<IStorageItem>();
            foreach (var path in paths)
            {
                IStorageItem? item = Directory.Exists(path)
                    ? await provider.TryGetFolderFromPathAsync(path)
                    : await provider.TryGetFileFromPathAsync(path);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            if (items.Count > 0)
            {
                await clipboard.SetFilesAsync(items);
            }
        }
        catch (Exception)
        {
            // The in-app clipboard still works; system clipboard support is best effort.
        }
    }

    /// <summary>Files copied in Explorer (or elsewhere), as local paths.</summary>
    public async Task<IReadOnlyList<string>> GetClipboardFilesAsync()
    {
        if (TopLevel?.Clipboard is not { } clipboard)
        {
            return [];
        }

        try
        {
            var files = await clipboard.TryGetFilesAsync();
            return files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
