using System.Security.Cryptography;
using Azariah.Core.Drive;
using Azariah.Core.Files;

namespace Azariah.Core.Space;

/// <summary>A top-level folder on the drive (or the loose files at the root) and what it holds.</summary>
public sealed record FolderSize(string Path, string Name, long Bytes, int FileCount);

/// <summary>Files with the same size and the same SHA-256: every copy after the first is wasted space.</summary>
public sealed record DuplicateGroup(long Size, IReadOnlyList<string> Paths)
{
    public long WastedBytes => Size * (Paths.Count - 1);
}

public sealed record SpaceReport(
    IReadOnlyList<FolderSize> Folders,
    IReadOnlyList<FileEntry> BiggestFiles,
    IReadOnlyList<DuplicateGroup> Duplicates,
    long TotalBytes,
    int TotalFiles)
{
    public long WastedBytes => Duplicates.Sum(d => d.WastedBytes);
}

/// <summary>
/// Where the drive's space goes: size per top-level folder, the biggest files, and duplicates.
/// App-managed areas (<c>.azariah</c>, the Vault) are left out. Only files of the same size are
/// hashed, so duplicate detection stays cheap.
/// </summary>
public static class SpaceScanner
{
    public const string LooseFilesName = "Loose files";

    public static Task<SpaceReport> ScanAsync(DriveLayout layout, int topFiles = 25, long minDuplicateBytes = 64 * 1024, CancellationToken ct = default) =>
        Task.Run(() => Scan(layout, topFiles, minDuplicateBytes, ct), ct);

    public static SpaceReport Scan(DriveLayout layout, int topFiles = 25, long minDuplicateBytes = 64 * 1024, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var folders = new Dictionary<string, (long Bytes, int Count)>(PathGuard.Comparer);
        var biggest = new PriorityQueue<FileEntry, long>();
        var bySize = new Dictionary<long, List<string>>();
        long total = 0;
        var count = 0;

        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var pending = new Stack<string>();
        pending.Push(layout.Root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var info in children)
            {
                if (layout.IsProtected(info.FullName))
                {
                    continue;
                }

                if (info is DirectoryInfo)
                {
                    pending.Push(info.FullName);
                    continue;
                }

                var size = ((FileInfo)info).Length;
                total += size;
                count++;

                var key = TopLevel(layout.Root, info.FullName);
                folders[key] = folders.TryGetValue(key, out var sum) ? (sum.Bytes + size, sum.Count + 1) : (size, 1);

                biggest.Enqueue(FileEntry.FromInfo(info), size);
                if (biggest.Count > topFiles)
                {
                    biggest.Dequeue();
                }

                if (size >= minDuplicateBytes)
                {
                    if (!bySize.TryGetValue(size, out var sameSize))
                    {
                        bySize[size] = sameSize = [];
                    }

                    sameSize.Add(info.FullName);
                }
            }
        }

        var duplicates = new List<DuplicateGroup>();
        foreach (var (size, paths) in bySize)
        {
            if (paths.Count < 2)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();
            foreach (var same in paths.GroupBy(Hash, StringComparer.Ordinal))
            {
                var copies = same.Order(StringComparer.OrdinalIgnoreCase).ToList();
                if (same.Key.Length > 0 && copies.Count > 1)
                {
                    duplicates.Add(new DuplicateGroup(size, copies));
                }
            }
        }

        var root = PathGuard.Normalize(layout.Root);
        return new SpaceReport(
            folders
                .Select(f => new FolderSize(f.Key, PathGuard.AreSame(f.Key, root) ? LooseFilesName : Path.GetFileName(f.Key), f.Value.Bytes, f.Value.Count))
                .OrderByDescending(f => f.Bytes)
                .ToList(),
            [.. biggest.UnorderedItems.Select(i => i.Element).OrderByDescending(e => e.Size)],
            [.. duplicates.OrderByDescending(d => d.WastedBytes)],
            total,
            count);
    }

    /// <summary>The top-level folder under the root that holds <paramref name="path"/>, or the root for loose files.</summary>
    private static string TopLevel(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var slash = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return slash < 0 ? PathGuard.Normalize(root) : PathGuard.Normalize(Path.Combine(root, relative[..slash]));
    }

    private static string Hash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
