namespace Azariah.Core.Drive;

/// <summary>Snapshot of a mounted volume. Removable/fixed is informational only: many fast USB
/// sticks report themselves as fixed disks, so detection never depends on it.</summary>
public sealed record VolumeInfo(
    string RootPath,
    string? Label,
    string? FileSystem,
    DriveType DriveType,
    long TotalBytes,
    long FreeBytes);

public interface IVolumeProvider
{
    IReadOnlyList<VolumeInfo> GetReadyVolumes();

    /// <summary>Volume info for the volume containing <paramref name="path"/>, or null.</summary>
    VolumeInfo? GetVolumeFor(string path);

    /// <summary>Root of the volume that holds the operating system (e.g. C:\).</summary>
    string? SystemVolumeRoot { get; }
}

public sealed class SystemVolumeProvider : IVolumeProvider
{
    public IReadOnlyList<VolumeInfo> GetReadyVolumes()
    {
        var list = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (TryCreate(drive) is { } info)
            {
                list.Add(info);
            }
        }

        return list;
    }

    public VolumeInfo? GetVolumeFor(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            if (OperatingSystem.IsWindows())
            {
                return TryCreate(new DriveInfo(root));
            }

            // On Linux/macOS (development only) pick the longest mount point containing the path.
            return GetReadyVolumes()
                .Where(v => Files.PathGuard.IsInsideOrEqual(v.RootPath, path))
                .OrderByDescending(v => v.RootPath.Length)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? SystemVolumeRoot
    {
        get
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return string.IsNullOrEmpty(system) ? "/" : Path.GetPathRoot(system);
        }
    }

    private static VolumeInfo? TryCreate(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady)
            {
                return null;
            }

            return new VolumeInfo(
                drive.RootDirectory.FullName,
                string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                drive.DriveFormat,
                drive.DriveType,
                drive.TotalSize,
                drive.AvailableFreeSpace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
