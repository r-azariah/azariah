using Azariah.Core.Files;

namespace Azariah.Core.Roblox;

/// <summary>A folder to look in and how many levels of subfolders to include (0 = just the folder).</summary>
public sealed record SearchRoot(string Folder, int Depth);

/// <summary>
/// Finds the newest Roblox place saved on this PC, so "Save new version" can offer it instead of a
/// blank file picker. Looks only where Studio saves usually land; hidden, system and linked folders
/// are skipped, and the search depth is capped so a big Documents folder stays quick.
/// </summary>
public static class PlaceFinder
{
    public static IReadOnlyList<SearchRoot> DefaultRoots(string? projectFolder)
    {
        var roots = new List<SearchRoot>();
        if (projectFolder is not null)
        {
            roots.Add(new SearchRoot(projectFolder, 0));
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var (folder, depth) in new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 3),
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 2),
            (Path.Combine(profile, "Downloads"), 1),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "AutoSaves"), 1),
        })
        {
            if (folder.Length > 0 && Directory.Exists(folder))
            {
                roots.Add(new SearchRoot(folder, depth));
            }
        }

        return roots;
    }

    /// <summary>The most recently modified .rbxl/.rbxlx under the roots, or null.</summary>
    public static FileEntry? FindNewest(IEnumerable<SearchRoot> roots, Func<string, bool>? exclude = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        FileEntry? newest = null;
        foreach (var root in roots)
        {
            var pending = new Queue<(string Folder, int Level)>();
            pending.Enqueue((root.Folder, 0));
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (folder, level) = pending.Dequeue();
                List<FileSystemInfo> children;
                try
                {
                    children = [.. new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options)];
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (child is DirectoryInfo)
                    {
                        if (level < root.Depth)
                        {
                            pending.Enqueue((child.FullName, level + 1));
                        }

                        continue;
                    }

                    var entry = FileEntry.FromInfo(child);
                    if (entry.Kind == FileKind.RobloxPlace
                        && exclude?.Invoke(entry.FullPath) != true
                        && (newest is null || entry.ModifiedUtc > newest.ModifiedUtc))
                    {
                        newest = entry;
                    }
                }
            }
        }

        return newest;
    }
}
