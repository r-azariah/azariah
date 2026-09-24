using Azariah.Core.Files;

namespace Azariah.Core.Drive;

public enum LocateOutcome
{
    /// <summary>An initialized Azariah drive was found.</summary>
    Found,

    /// <summary>A plausible root was found but it has no marker yet (fresh drive).</summary>
    NeedsInitialization,

    /// <summary>Several initialized drives are connected; the user must choose.</summary>
    Ambiguous,

    /// <summary>Nothing found (typically a development build on the system drive).</summary>
    NotFound,
}

public sealed record DriveCandidate(string Root, DriveMarker Marker, VolumeInfo? Volume);

public sealed record LocateResult(
    LocateOutcome Outcome,
    string? Root,
    DriveMarker? Marker,
    string Source,
    IReadOnlyList<DriveCandidate> Candidates)
{
    public static LocateResult NotFound(IReadOnlyList<DriveCandidate>? candidates = null) =>
        new(LocateOutcome.NotFound, null, null, "none", candidates ?? []);
}

/// <param name="ExplicitRoot">From <c>--root</c>, e.g. passed by the Phase 4 launcher.</param>
/// <param name="EnvironmentRoot">From the <c>AZARIAH_ROOT</c> environment variable (development).</param>
/// <param name="ExecutableDirectory">Where the running app lives.</param>
public sealed record LocateRequest(string? ExplicitRoot, string? EnvironmentRoot, string ExecutableDirectory);

/// <summary>
/// Figures out which folder is the root of the Azariah drive. Drive letters are never assumed:
/// the root is found from the launch arguments, from where the executable lives, or by
/// scanning mounted volumes for the <c>.azariah/drive.json</c> marker.
/// </summary>
public sealed class DriveRootLocator(IVolumeProvider volumes)
{
    public const string EnvironmentVariable = "AZARIAH_ROOT";

    public LocateResult Locate(LocateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. An explicit root always wins. A missing marker means the user is pointing us at a
        //    fresh drive/folder that needs initializing.
        if (TryExplicit(request.ExplicitRoot, "argument") is { } fromArg)
        {
            return fromArg;
        }

        if (TryExplicit(request.EnvironmentRoot, "environment") is { } fromEnv)
        {
            return fromEnv;
        }

        // 2. Walk up from the executable. This is the normal case when running from the USB.
        if (FindMarkerAbove(request.ExecutableDirectory) is { } walked)
        {
            return new LocateResult(LocateOutcome.Found, walked, DriveMarkerStore.TryRead(walked), "executable", []);
        }

        // 3. Running from a non-system volume with no marker yet: first launch from a fresh USB.
        var exeVolume = volumes.GetVolumeFor(request.ExecutableDirectory);
        var systemRoot = volumes.SystemVolumeRoot;
        var candidates = ScanVolumes();

        if (exeVolume is not null
            && systemRoot is not null
            && !PathGuard.AreSame(exeVolume.RootPath, systemRoot)
            && candidates.Count == 0)
        {
            return new LocateResult(LocateOutcome.NeedsInitialization, PathGuard.Normalize(exeVolume.RootPath), null, "executable-volume", []);
        }

        // 4. Dev build or launched from elsewhere: look at every mounted volume.
        return candidates.Count switch
        {
            1 => new LocateResult(LocateOutcome.Found, candidates[0].Root, candidates[0].Marker, "scan", candidates),
            > 1 => new LocateResult(LocateOutcome.Ambiguous, null, null, "scan", candidates),
            _ => LocateResult.NotFound(),
        };
    }

    /// <summary>All mounted volumes whose root carries an Azariah marker.</summary>
    public IReadOnlyList<DriveCandidate> ScanVolumes()
    {
        var result = new List<DriveCandidate>();
        foreach (var volume in volumes.GetReadyVolumes())
        {
            if (DriveMarkerStore.TryRead(volume.RootPath) is { } marker)
            {
                result.Add(new DriveCandidate(PathGuard.Normalize(volume.RootPath), marker, volume));
            }
        }

        return result;
    }

    /// <summary>Finds the drive with a given id, e.g. after it was re-plugged under a new letter.</summary>
    public DriveCandidate? FindByDriveId(Guid driveId) =>
        ScanVolumes().FirstOrDefault(c => c.Marker.DriveId == driveId);

    private static LocateResult? TryExplicit(string? root, string source)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        string normalized;
        try
        {
            normalized = PathGuard.Normalize(root.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!Directory.Exists(normalized))
        {
            return null;
        }

        var marker = DriveMarkerStore.TryRead(normalized);
        return new LocateResult(
            marker is null ? LocateOutcome.NeedsInitialization : LocateOutcome.Found,
            normalized,
            marker,
            source,
            []);
    }

    private static string? FindMarkerAbove(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        var dir = new DirectoryInfo(PathGuard.Normalize(startDirectory));
        while (dir is not null)
        {
            if (DriveMarkerStore.TryRead(dir.FullName) is not null)
            {
                return PathGuard.Normalize(dir.FullName);
            }

            dir = dir.Parent;
        }

        return null;
    }
}
