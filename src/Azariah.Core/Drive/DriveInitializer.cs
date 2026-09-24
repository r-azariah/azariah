using Azariah.Core.Files;
using Azariah.Core.SetupKit;

namespace Azariah.Core.Drive;

/// <summary>
/// Turns a folder (normally the USB root) into an Azariah drive: writes the marker and creates
/// the standard folders. Safe to run again; it only adds what is missing and never overwrites
/// or deletes anything.
/// </summary>
public sealed class DriveInitializer(IVolumeProvider volumes)
{
    /// <summary>True for the root of the Windows system volume (e.g. C:\). Folders on it are allowed.</summary>
    public bool IsSystemDrive(string root) =>
        volumes.SystemVolumeRoot is { } system && PathGuard.AreSame(root, system);

    public DriveMarker Initialize(string root, string? displayName = null, bool allowSystemDrive = false)
    {
        var normalized = PathGuard.Normalize(root);
        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException("The selected location doesn't exist.");
        }

        if (!allowSystemDrive && IsSystemDrive(normalized))
        {
            throw new InvalidOperationException("Refusing to set up the Windows system drive root as an Azariah drive.");
        }

        var marker = DriveMarkerStore.TryRead(normalized);
        if (marker is null)
        {
            marker = DriveMarker.CreateNew(displayName);
            DriveMarkerStore.Write(normalized, marker);
        }

        EnsureFolders(new DriveLayout(normalized));
        return marker;
    }

    /// <summary>Recreates any standard folder that is missing.</summary>
    public static void EnsureFolders(DriveLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Directory.CreateDirectory(layout.SystemFolder);
        foreach (var folder in layout.StandardFolders())
        {
            Directory.CreateDirectory(folder);
        }

        SetupKitLayout.EnsureCreated(layout.SetupKit);
    }
}
