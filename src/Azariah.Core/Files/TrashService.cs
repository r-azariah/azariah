using System.Globalization;
using Azariah.Core.Diagnostics;
using Azariah.Core.Drive;
using Azariah.Core.Serialization;

namespace Azariah.Core.Files;

public sealed record TrashInfo
{
    public string OriginalRelativePath { get; init; } = string.Empty;
    public DateTimeOffset DeletedUtc { get; init; }
    public bool IsDirectory { get; init; }
    public long SizeBytes { get; init; }
}

public sealed record TrashEntry(string EntryFolder, string ItemPath, TrashInfo Info)
{
    public string Name => Path.GetFileName(ItemPath);
}

/// <summary>
/// A recycle bin that lives on the drive itself. Windows has no Recycle Bin for removable
/// drives, so without this every delete on the USB would be permanent.
/// Items are moved (renamed) into <c>.azariah/Trash</c>, which is instant on the same volume.
/// This is only for normal, unencrypted files; the Vault will manage its own deletions.
/// </summary>
public sealed class TrashService(DriveLayout layout, IAppLog log)
{
    private const string InfoFileName = "trashinfo.json";

    public TrashEntry MoveToTrash(string path)
    {
        var full = PathGuard.Normalize(path);
        if (!layout.Contains(full) || PathGuard.AreSame(full, layout.Root) || layout.IsProtected(full))
        {
            throw new FileOperationException("That item can't be moved to Trash.");
        }

        var isDir = Directory.Exists(full);
        if (!isDir && !File.Exists(full))
        {
            throw new FileOperationException("The item no longer exists.");
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var entryFolder = Path.Combine(layout.TrashFolder, $"{stamp}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(entryFolder);
        var target = Path.Combine(entryFolder, Path.GetFileName(full));

        var info = new TrashInfo
        {
            OriginalRelativePath = layout.ToRelative(full),
            DeletedUtc = DateTimeOffset.UtcNow,
            IsDirectory = isDir,
            SizeBytes = isDir ? DirectorySize.Measure(full) : new FileInfo(full).Length,
        };

        try
        {
            JsonFile.WriteAtomic(Path.Combine(entryFolder, InfoFileName), info, AzariahJsonContext.Default.TrashInfo);
            if (isDir)
            {
                Directory.Move(full, target);
            }
            else
            {
                File.Move(full, target);
            }
        }
        catch
        {
            TryDeleteFolder(entryFolder);
            throw;
        }

        log.Info($"Moved {(isDir ? "folder" : "file")} to Trash.");
        return new TrashEntry(entryFolder, target, info);
    }

    public IReadOnlyList<TrashEntry> List()
    {
        if (!Directory.Exists(layout.TrashFolder))
        {
            return [];
        }

        var entries = new List<TrashEntry>();
        foreach (var folder in Directory.EnumerateDirectories(layout.TrashFolder))
        {
            var info = JsonFile.TryRead(Path.Combine(folder, InfoFileName), AzariahJsonContext.Default.TrashInfo);
            var item = Directory.EnumerateFileSystemEntries(folder)
                .FirstOrDefault(p => !string.Equals(Path.GetFileName(p), InfoFileName, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                continue;
            }

            info ??= new TrashInfo
            {
                OriginalRelativePath = Path.GetFileName(item),
                DeletedUtc = Directory.GetCreationTimeUtc(folder),
                IsDirectory = Directory.Exists(item),
            };
            entries.Add(new TrashEntry(folder, item, info));
        }

        return entries.OrderByDescending(e => e.Info.DeletedUtc).ToList();
    }

    /// <summary>Moves the item back. If something now occupies the original spot, restores as "name (2)".</summary>
    public string Restore(TrashEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureInTrash(entry);

        string destination;
        try
        {
            destination = layout.ToFull(entry.Info.OriginalRelativePath);
        }
        catch (ArgumentException)
        {
            destination = Path.Combine(layout.Files, entry.Name);
        }

        if (layout.IsProtected(destination))
        {
            destination = Path.Combine(layout.Files, entry.Name);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        destination = UniqueNames.Next(destination);

        if (Directory.Exists(entry.ItemPath))
        {
            Directory.Move(entry.ItemPath, destination);
        }
        else
        {
            File.Move(entry.ItemPath, destination);
        }

        TryDeleteFolder(entry.EntryFolder);
        log.Info("Restored an item from Trash.");
        return destination;
    }

    public void DeletePermanently(TrashEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureInTrash(entry);
        Directory.Delete(entry.EntryFolder, recursive: true);
    }

    public int Empty()
    {
        var count = 0;
        foreach (var entry in List())
        {
            try
            {
                DeletePermanently(entry);
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Could not permanently delete a Trash item: {ex.GetType().Name}");
            }
        }

        return count;
    }

    private void EnsureInTrash(TrashEntry entry)
    {
        if (!PathGuard.IsStrictlyInside(layout.TrashFolder, entry.EntryFolder)
            || !PathGuard.IsStrictlyInside(entry.EntryFolder, entry.ItemPath))
        {
            throw new FileOperationException("That item is not in Trash.");
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public static class DirectorySize
{
    public static long Measure(string folder, CancellationToken ct = default)
    {
        long total = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", options))
        {
            ct.ThrowIfCancellationRequested();
            total += file.Length;
        }

        return total;
    }

    public static (long Bytes, int Files) MeasureWithCount(string path, CancellationToken ct = default)
    {
        if (File.Exists(path))
        {
            return (new FileInfo(path).Length, 1);
        }

        long total = 0;
        var count = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", options))
        {
            ct.ThrowIfCancellationRequested();
            total += file.Length;
            count++;
        }

        return (total, count);
    }
}
