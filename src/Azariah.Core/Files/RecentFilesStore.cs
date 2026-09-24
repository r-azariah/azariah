using Azariah.Core.Drive;
using Azariah.Core.Serialization;

namespace Azariah.Core.Files;

public sealed record RecentFileRecord(string RelativePath, DateTimeOffset OpenedUtc);

public sealed record RecentFilesDocument
{
    public List<RecentFileRecord> Items { get; init; } = [];
}

/// <summary>
/// Recently opened files, stored as drive-relative paths so they survive drive letter changes.
/// Items inside app-managed areas (including the Vault) are never recorded, because this file
/// is plaintext and would otherwise leak private file names.
/// </summary>
public sealed class RecentFilesStore(DriveLayout layout)
{
    public const int MaxItems = 20;

    public void Add(string fullPath)
    {
        if (!layout.Contains(fullPath) || layout.IsProtected(fullPath))
        {
            return;
        }

        var relative = layout.ToRelative(fullPath);
        var doc = Load();
        doc.Items.RemoveAll(r => string.Equals(r.RelativePath, relative, PathGuard.Comparison));
        doc.Items.Insert(0, new RecentFileRecord(relative, DateTimeOffset.UtcNow));
        if (doc.Items.Count > MaxItems)
        {
            doc.Items.RemoveRange(MaxItems, doc.Items.Count - MaxItems);
        }

        JsonFile.WriteAtomic(layout.RecentFile, doc, AzariahJsonContext.Default.RecentFilesDocument);
    }

    /// <summary>Recent files that still exist, newest first.</summary>
    public IReadOnlyList<FileEntry> GetRecent(int count = 8)
    {
        var result = new List<FileEntry>();
        foreach (var record in Load().Items)
        {
            string full;
            try
            {
                full = layout.ToFull(record.RelativePath);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!layout.IsProtected(full) && FileEntry.TryFromPath(full) is { IsDirectory: false } entry)
            {
                result.Add(entry);
                if (result.Count >= count)
                {
                    break;
                }
            }
        }

        return result;
    }

    public void Clear() =>
        JsonFile.WriteAtomic(layout.RecentFile, new RecentFilesDocument(), AzariahJsonContext.Default.RecentFilesDocument);

    private RecentFilesDocument Load() =>
        JsonFile.TryRead(layout.RecentFile, AzariahJsonContext.Default.RecentFilesDocument) ?? new RecentFilesDocument();
}
