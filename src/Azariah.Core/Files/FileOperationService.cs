using Azariah.Core.Diagnostics;
using Azariah.Core.Drive;

namespace Azariah.Core.Files;

/// <summary>
/// All file-management operations for the normal (unencrypted) areas of the drive.
/// Rules enforced here rather than in the UI:
/// <list type="bullet">
/// <item>Writes only ever land inside the drive root (except explicit exports to this PC).</item>
/// <item>App-managed areas (<c>.azariah</c>, <c>Vault</c>) cannot be modified from here.</item>
/// <item>Replacing a file copies to a temp file first, so a failed copy never destroys the original.</item>
/// <item>Deletes go to the on-drive Trash; permanent deletion only happens from Trash.</item>
/// </list>
/// </summary>
public sealed class FileOperationService(DriveLayout layout, TrashService trash, IAppLog log)
{
    private const int BufferSize = 1024 * 1024;

    public DriveLayout Layout => layout;

    public IReadOnlyList<FileEntry> List(string folder, bool includeHidden)
    {
        var full = PathGuard.Normalize(folder);
        EnsureInsideRoot(full);
        if (layout.IsProtected(full))
        {
            throw new FileOperationException("This area is managed by Azariah.");
        }

        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
        var list = new List<FileEntry>();
        foreach (var info in new DirectoryInfo(full).EnumerateFileSystemInfos("*", options))
        {
            if (PathGuard.AreSame(info.FullName, layout.SystemFolder))
            {
                continue;
            }

            var entry = FileEntry.FromInfo(info);
            if (!includeHidden && entry.IsHidden)
            {
                continue;
            }

            list.Add(entry);
        }

        return list;
    }

    public FileEntry CreateFolder(string parentFolder, string name)
    {
        var reason = FileNameRules.Validate(name);
        if (reason is not null)
        {
            throw new FileOperationException(reason);
        }

        var target = Path.Combine(PathGuard.Normalize(parentFolder), name.Trim());
        EnsureWritable(target);
        if (UniqueNames.Exists(target))
        {
            throw new FileOperationException("Something with that name already exists here.");
        }

        Directory.CreateDirectory(target);
        log.Info("Created folder.");
        return FileEntry.TryFromPath(target)!;
    }

    /// <summary>Renames a file or folder in place and returns the new full path.</summary>
    public string Rename(string path, string newName)
    {
        var reason = FileNameRules.Validate(newName);
        if (reason is not null)
        {
            throw new FileOperationException(reason);
        }

        var source = PathGuard.Normalize(path);
        EnsureWritable(source);
        var isDir = Directory.Exists(source);
        if (!isDir && !File.Exists(source))
        {
            throw new FileOperationException("The item no longer exists.");
        }

        newName = newName.Trim();
        var target = Path.Combine(Path.GetDirectoryName(source)!, newName);
        if (string.Equals(Path.GetFileName(source), newName, StringComparison.Ordinal))
        {
            return source;
        }

        var caseOnly = PathGuard.AreSame(source, target);
        if (!caseOnly && UniqueNames.Exists(target))
        {
            throw new FileOperationException("Something with that name already exists here.");
        }

        if (caseOnly)
        {
            // Case-insensitive file systems need a hop through a temporary name.
            var temp = Path.Combine(Path.GetDirectoryName(source)!, $".rename-{Guid.NewGuid():N}");
            MoveEntry(source, temp, isDir);
            MoveEntry(temp, target, isDir);
        }
        else
        {
            MoveEntry(source, target, isDir);
        }

        log.Info("Renamed an item.");
        return target;
    }

    /// <summary>Returns the names that already exist in the destination folder.</summary>
    public IReadOnlyList<string> FindConflicts(IEnumerable<string> sources, string destinationFolder)
    {
        var dest = PathGuard.Normalize(destinationFolder);
        EnsureInsideRoot(dest);
        return sources
            .Select(PathGuard.Normalize)
            .Where(s => !PathGuard.AreSame(Path.GetDirectoryName(s)!, dest))
            .Select(s => Path.GetFileName(s))
            .Where(name => UniqueNames.Exists(Path.Combine(dest, name)))
            .ToList();
    }

    /// <summary>Copies items (from anywhere, e.g. an Explorer drop) into a folder on the drive.</summary>
    public Task<OperationReport> CopyAsync(
        IReadOnlyList<string> sources,
        string destinationFolder,
        ConflictPolicy policy,
        IProgress<FileProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dest = PathGuard.Normalize(destinationFolder);
        EnsureWritableFolder(dest);
        return CopyCoreAsync(sources, dest, policy, progress, ct);
    }

    /// <summary>Copies items from the drive to a folder on this PC chosen by the user.</summary>
    public Task<OperationReport> ExportAsync(
        IReadOnlyList<string> sources,
        string destinationFolder,
        ConflictPolicy policy,
        IProgress<FileProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            EnsureInsideRoot(source);
            EnsureNotProtected(source);
        }

        var dest = PathGuard.Normalize(destinationFolder);
        if (!Directory.Exists(dest))
        {
            throw new FileOperationException("The destination folder doesn't exist.");
        }

        return CopyCoreAsync(sources, dest, policy, progress, ct);
    }

    public async Task<OperationReport> MoveAsync(
        IReadOnlyList<string> sources,
        string destinationFolder,
        ConflictPolicy policy,
        IProgress<FileProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dest = PathGuard.Normalize(destinationFolder);
        EnsureWritableFolder(dest);
        var report = new OperationReport();
        var items = RemoveNested(sources);
        var done = 0;

        try
        {
            foreach (var source in items)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new FileProgress(Path.GetFileName(source), 0, 0, done, items.Count));
                try
                {
                    EnsureWritable(source);
                    var isDir = Directory.Exists(source);
                    if (!isDir && !File.Exists(source))
                    {
                        throw new FileOperationException("The item no longer exists.");
                    }

                    if (PathGuard.AreSame(Path.GetDirectoryName(source)!, dest))
                    {
                        report.Skipped++;
                        continue;
                    }

                    if (isDir && PathGuard.IsInsideOrEqual(source, dest))
                    {
                        throw new FileOperationException("A folder can't be moved into itself.");
                    }

                    var target = Path.Combine(dest, Path.GetFileName(source));
                    if (UniqueNames.Exists(target))
                    {
                        switch (policy)
                        {
                            case ConflictPolicy.Skip:
                                report.Skipped++;
                                continue;
                            case ConflictPolicy.KeepBoth:
                                target = UniqueNames.Next(target);
                                break;
                            case ConflictPolicy.Replace:
                                await ReplaceByMoveAsync(source, target, isDir, report, ct);
                                report.AddCreated(target);
                                report.Succeeded++;
                                continue;
                        }
                    }

                    if (PathGuard.IsOnSameVolume(source, target))
                    {
                        MoveEntry(source, target, isDir);
                    }
                    else
                    {
                        await CopyItemAsync(source, target, ConflictPolicy.Replace, report, null, ct);
                        DeleteEntry(source, isDir);
                    }

                    report.AddCreated(target);
                    report.Succeeded++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    report.AddError(source, ex);
                }
                finally
                {
                    done++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            report.Cancelled = true;
        }

        progress?.Report(new FileProgress(string.Empty, 0, 0, done, items.Count));
        log.Info($"Move finished: {report.Succeeded} ok, {report.Skipped} skipped, {report.Errors.Count} failed.");
        return report;
    }

    public Task<OperationReport> MoveToTrashAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        var report = new OperationReport();
        foreach (var path in RemoveNested(paths))
        {
            if (ct.IsCancellationRequested)
            {
                report.Cancelled = true;
                break;
            }

            try
            {
                EnsureWritable(path);
                trash.MoveToTrash(path);
                report.Succeeded++;
            }
            catch (Exception ex)
            {
                report.AddError(path, ex);
            }
        }

        return Task.FromResult(report);
    }

    private async Task<OperationReport> CopyCoreAsync(
        IReadOnlyList<string> sources,
        string dest,
        ConflictPolicy policy,
        IProgress<FileProgress>? progress,
        CancellationToken ct)
    {
        var report = new OperationReport();
        var items = RemoveNested(sources);
        var tracker = new ProgressTracker(progress);

        try
        {
            foreach (var source in items)
            {
                var (bytes, files) = await Task.Run(() => DirectorySize.MeasureWithCount(source, ct), ct);
                tracker.BytesTotal += bytes;
                tracker.ItemsTotal += files;
            }

            foreach (var source in items)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var isDir = Directory.Exists(source);
                    if (!isDir && !File.Exists(source))
                    {
                        throw new FileOperationException("The item no longer exists.");
                    }

                    if (layout.Contains(source))
                    {
                        EnsureNotProtected(source);
                    }

                    if (isDir && PathGuard.IsInsideOrEqual(source, dest))
                    {
                        throw new FileOperationException("A folder can't be copied into itself.");
                    }

                    var target = Path.Combine(dest, Path.GetFileName(source));
                    var effective = policy;
                    if (PathGuard.AreSame(source, target))
                    {
                        // Copying into the same folder means "duplicate".
                        target = UniqueNames.Next(target);
                    }
                    else if (UniqueNames.Exists(target))
                    {
                        if (policy == ConflictPolicy.Skip)
                        {
                            report.Skipped++;
                            continue;
                        }

                        if (policy == ConflictPolicy.KeepBoth)
                        {
                            target = UniqueNames.Next(target);
                        }
                        else if (Directory.Exists(target) != isDir)
                        {
                            throw new FileOperationException("Can't replace a folder with a file (or the other way round).");
                        }
                    }

                    var errorsBefore = report.Errors.Count;
                    await CopyItemAsync(source, target, effective, report, tracker, ct);
                    report.AddCreated(target);
                    if (report.Errors.Count == errorsBefore)
                    {
                        report.Succeeded++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    report.AddError(source, ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            report.Cancelled = true;
        }

        log.Info($"Copy finished: {report.Succeeded} ok, {report.Skipped} skipped, {report.Errors.Count} failed{(report.Cancelled ? ", cancelled" : string.Empty)}.");
        return report;
    }

    private static async Task CopyItemAsync(
        string source,
        string target,
        ConflictPolicy policy,
        OperationReport report,
        ProgressTracker? tracker,
        CancellationToken ct)
    {
        if (File.Exists(source))
        {
            await CopyFileSafeAsync(source, target, tracker, ct);
            return;
        }

        Directory.CreateDirectory(target);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos("*", options))
        {
            ct.ThrowIfCancellationRequested();
            var childTarget = Path.Combine(target, entry.Name);
            try
            {
                if (entry is DirectoryInfo)
                {
                    if (File.Exists(childTarget))
                    {
                        throw new FileOperationException("A file with the same name as this folder already exists.");
                    }

                    await CopyItemAsync(entry.FullName, childTarget, policy, report, tracker, ct);
                }
                else
                {
                    if (Directory.Exists(childTarget))
                    {
                        throw new FileOperationException("A folder with the same name as this file already exists.");
                    }

                    await CopyFileSafeAsync(entry.FullName, childTarget, tracker, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.AddError(entry.FullName, ex);
            }
        }

        TryCopyTimestamps(source, target, isDirectory: true);
    }

    /// <summary>Copies to a temp file next to the target and swaps it in only when complete.</summary>
    private static async Task CopyFileSafeAsync(string source, string target, ProgressTracker? tracker, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(target)!;
        var temp = Path.Combine(folder, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.partial");
        tracker?.StartItem(Path.GetFileName(source));
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                if (input.Length > 0)
                {
                    output.SetLength(input.Length);
                }

                var buffer = new byte[BufferSize];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    tracker?.AddBytes(read);
                }

                await output.FlushAsync(ct);
            }

            File.Move(temp, target, overwrite: true);
            TryCopyTimestamps(source, target, isDirectory: false);
            tracker?.CompleteItem();
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static async Task ReplaceByMoveAsync(string source, string target, bool isDir, OperationReport report, CancellationToken ct)
    {
        if (Directory.Exists(target) != isDir)
        {
            throw new FileOperationException("Can't replace a folder with a file (or the other way round).");
        }

        if (!isDir)
        {
            File.Move(source, target, overwrite: true);
            return;
        }

        // Merge folders: move each child across, then remove the (now empty) source.
        foreach (var child in Directory.EnumerateFileSystemEntries(source).ToList())
        {
            ct.ThrowIfCancellationRequested();
            var childTarget = Path.Combine(target, Path.GetFileName(child));
            var childIsDir = Directory.Exists(child);
            try
            {
                if (UniqueNames.Exists(childTarget))
                {
                    await ReplaceByMoveAsync(child, childTarget, childIsDir, report, ct);
                }
                else
                {
                    MoveEntry(child, childTarget, childIsDir);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                report.AddError(child, ex);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(source).Any())
        {
            Directory.Delete(source);
        }
    }

    private static void MoveEntry(string source, string target, bool isDir)
    {
        if (isDir)
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target);
        }
    }

    private static void DeleteEntry(string path, bool isDir)
    {
        if (isDir)
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static void TryCopyTimestamps(string source, string target, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.SetLastWriteTimeUtc(target, Directory.GetLastWriteTimeUtc(source));
            }
            else
            {
                File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Drops items whose parent folder is also in the list, so nothing is processed twice.</summary>
    private static List<string> RemoveNested(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var normalized = paths.Select(PathGuard.Normalize).Distinct(PathGuard.Comparer).ToList();
        return normalized
            .Where(p => !normalized.Any(other => !PathGuard.AreSame(other, p) && PathGuard.IsStrictlyInside(other, p)))
            .ToList();
    }

    private void EnsureInsideRoot(string path)
    {
        if (!layout.Contains(path))
        {
            throw new FileOperationException("That location is outside your drive.");
        }
    }

    private void EnsureNotProtected(string path)
    {
        if (layout.IsProtected(path))
        {
            throw new FileOperationException("This area is managed by Azariah and can't be changed here.");
        }
    }

    /// <summary>The path may be changed: inside the root, not the root itself, not protected.</summary>
    private void EnsureWritable(string path)
    {
        EnsureInsideRoot(path);
        if (PathGuard.AreSame(path, layout.Root))
        {
            throw new FileOperationException("The drive itself can't be changed.");
        }

        EnsureNotProtected(path);
    }

    private void EnsureWritableFolder(string folder)
    {
        EnsureInsideRoot(folder);
        EnsureNotProtected(folder);
        if (!Directory.Exists(folder))
        {
            throw new FileOperationException("The destination folder doesn't exist.");
        }
    }

    private sealed class ProgressTracker(IProgress<FileProgress>? sink)
    {
        private long _lastReportedBytes;
        private string _current = string.Empty;

        public long BytesTotal { get; set; }
        public int ItemsTotal { get; set; }
        public long BytesDone { get; private set; }
        public int ItemsDone { get; private set; }

        public void StartItem(string name)
        {
            _current = name;
            Report();
        }

        public void AddBytes(int count)
        {
            BytesDone += count;
            if (BytesDone - _lastReportedBytes >= 4 * BufferSize)
            {
                Report();
            }
        }

        public void CompleteItem()
        {
            ItemsDone++;
            Report();
        }

        private void Report()
        {
            _lastReportedBytes = BytesDone;
            sink?.Report(new FileProgress(_current, BytesDone, BytesTotal, ItemsDone, ItemsTotal));
        }
    }
}
