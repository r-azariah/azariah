namespace Azariah.Core.Drive;

/// <summary>Storage numbers for the Home page.</summary>
public sealed record DriveSummary(
    string Root,
    string? VolumeLabel,
    string? FileSystem,
    DriveType DriveType,
    long TotalBytes,
    long FreeBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsedFraction => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes;

    public static DriveSummary For(string root, IVolumeProvider volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        var v = volumes.GetVolumeFor(root);
        return v is null
            ? new DriveSummary(root, null, null, DriveType.Unknown, 0, 0)
            : new DriveSummary(root, v.Label, v.FileSystem, v.DriveType, v.TotalBytes, v.FreeBytes);
    }
}
