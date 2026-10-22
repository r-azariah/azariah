using Azariah.Core.Files;

namespace Azariah.Core.Space;

/// <summary>
/// Finds the biggest files under some roots on the computer AZARIAH is plugged into, for the guest
/// page's "what's filling this PC" view. Read-only: it only measures. Hidden, system and linked
/// folders are skipped so it stays quick and never follows junctions in circles.
/// </summary>
public static class BigFilesScanner
{
    /// <summary>The usual places a person's big files pile up. Missing folders are skipped.</summary>
    public static IReadOnlyList<string> UserRoots()
    {
        var roots = new List<string>();
        foreach (var folder in new[]
        {
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.MyPictures,
        })
        {
            var path = Environment.GetFolderPath(folder);
            if (path.Length > 0 && Directory.Exists(path))
            {
                roots.Add(path);
            }
        }

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            roots.Add(downloads);
        }

        // Keep only the top-most of any overlapping roots (the profile already covers Documents etc.).
        return roots
            .Where(r => !roots.Any(other => !PathGuard.AreSame(other, r) && PathGuard.IsInsideOrEqual(other, r)))
            .ToList();
    }

    public static Task<IReadOnlyList<FileEntry>> ScanAsync(IEnumerable<string> roots, int top = 40, CancellationToken ct = default) =>
        Task.Run(() => Scan(roots, top, ct), ct);

    public static IReadOnlyList<FileEntry> Scan(IEnumerable<string> roots, int top = 40, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var biggest = new PriorityQueue<FileEntry, long>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        foreach (var root in roots.Distinct(PathGuard.Comparer))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", options);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var path in files)
            {
                ct.ThrowIfCancellationRequested();
                long size;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                biggest.Enqueue(FileEntry.FromInfo(new FileInfo(path)), size);
                if (biggest.Count > top)
                {
                    biggest.Dequeue();
                }
            }
        }

        return biggest.UnorderedItems.Select(i => i.Element).OrderByDescending(e => e.Size).ToList();
    }
}
