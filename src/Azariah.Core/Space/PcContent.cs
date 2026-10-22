using Azariah.Core.Files;

namespace Azariah.Core.Space;

/// <summary>A friendly bucket the guest storage view groups files into.</summary>
public enum ContentKind
{
    Photos,
    Videos,
    Music,
    Documents,
    Downloads,
    Installers,
    Archives,
    Games,
    AppsAndCode,
    Other,
}

public sealed record PcCategory(ContentKind Kind, string Name, long Bytes, int FileCount, IReadOnlyList<FileEntry> TopFiles);

public sealed record PcContentReport(IReadOnlyList<PcCategory> Categories, long TotalBytes, int TotalFiles)
{
    public long LargestCategoryBytes => Categories.Count > 0 ? Categories.Max(c => c.Bytes) : 0;
}

/// <summary>
/// Groups the biggest files on this PC into friendly categories for the guest page, using the file's
/// extension and where it lives (folder keywords). Rules only, no AI: a photo is a photo because of its
/// type, Downloads and Games are buckets because of where they sit. Read-only.
/// </summary>
public static class PcContent
{
    private const int TopPerCategory = 12;

    private static readonly string[] GameFolders = ["steamapps", "steam", "epic games", "riot games", "games", "ubisoft", "ea games", "gog galaxy"];

    public static string DisplayName(ContentKind kind) => kind switch
    {
        ContentKind.Photos => "Photos",
        ContentKind.Videos => "Videos",
        ContentKind.Music => "Music",
        ContentKind.Documents => "Documents",
        ContentKind.Downloads => "Downloads",
        ContentKind.Installers => "Installers",
        ContentKind.Archives => "Archives",
        ContentKind.Games => "Games",
        ContentKind.AppsAndCode => "Apps and code",
        _ => "Other",
    };

    public static Task<PcContentReport> ScanAsync(IEnumerable<string> roots, CancellationToken ct = default) =>
        Task.Run(() => Scan(roots, ct), ct);

    public static PcContentReport Scan(IEnumerable<string> roots, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var bytes = new Dictionary<ContentKind, long>();
        var counts = new Dictionary<ContentKind, int>();
        var tops = new Dictionary<ContentKind, PriorityQueue<FileEntry, long>>();
        long total = 0;
        var totalFiles = 0;

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

                var kind = Classify(path);
                bytes[kind] = bytes.GetValueOrDefault(kind) + size;
                counts[kind] = counts.GetValueOrDefault(kind) + 1;
                total += size;
                totalFiles++;

                if (!tops.TryGetValue(kind, out var queue))
                {
                    tops[kind] = queue = new PriorityQueue<FileEntry, long>();
                }

                queue.Enqueue(FileEntry.FromInfo(new FileInfo(path)), size);
                if (queue.Count > TopPerCategory)
                {
                    queue.Dequeue();
                }
            }
        }

        var categories = bytes.Keys
            .Select(kind => new PcCategory(
                kind,
                DisplayName(kind),
                bytes[kind],
                counts[kind],
                tops[kind].UnorderedItems.Select(i => i.Element).OrderByDescending(e => e.Size).ToList()))
            .OrderByDescending(c => c.Bytes)
            .ToList();

        return new PcContentReport(categories, total, totalFiles);
    }

    /// <summary>Where a file belongs: folder keyword first (Downloads, Games), then its type.</summary>
    public static ContentKind Classify(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (segment.Contains("download", StringComparison.OrdinalIgnoreCase))
            {
                return ContentKind.Downloads;
            }

            if (GameFolders.Any(g => segment.Equals(g, StringComparison.OrdinalIgnoreCase)))
            {
                return ContentKind.Games;
            }
        }

        return FileKinds.FromPath(path) switch
        {
            FileKind.Image => ContentKind.Photos,
            FileKind.Video => ContentKind.Videos,
            FileKind.Audio => ContentKind.Music,
            FileKind.Document or FileKind.Pdf or FileKind.Text or FileKind.Spreadsheet or FileKind.Presentation => ContentKind.Documents,
            FileKind.Installer or FileKind.Executable => ContentKind.Installers,
            FileKind.Archive => ContentKind.Archives,
            FileKind.Code or FileKind.LuauScript or FileKind.RobloxPlace or FileKind.RobloxModel => ContentKind.AppsAndCode,
            _ => ContentKind.Other,
        };
    }
}
