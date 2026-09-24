using Azariah.Core.Serialization;

namespace Azariah.Core.Drive;

public static class DriveMarkerStore
{
    public static string MarkerPathFor(string root) =>
        Path.Combine(root, DriveLayout.SystemFolderName, DriveLayout.MarkerFileName);

    /// <summary>Reads the marker for a candidate root, or null if absent or unreadable.</summary>
    public static DriveMarker? TryRead(string root)
    {
        var marker = JsonFile.TryRead(MarkerPathFor(root), AzariahJsonContext.Default.DriveMarker);
        return marker is { DriveId: var id } && id != Guid.Empty ? marker : null;
    }

    public static void Write(string root, DriveMarker marker) =>
        JsonFile.WriteAtomic(MarkerPathFor(root), marker, AzariahJsonContext.Default.DriveMarker);
}
