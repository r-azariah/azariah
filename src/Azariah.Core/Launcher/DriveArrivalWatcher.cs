using Azariah.Core.Drive;
using Azariah.Core.Files;

namespace Azariah.Core.Launcher;

/// <summary>
/// Notices when a paired Azariah drive is plugged in. Call <see cref="Poll"/> about once a second.
/// A freshly mounted volume can take a moment to become readable, so volumes without a marker
/// are re-checked for a short settle window before being ignored.
/// A drive is reported once per insertion; unplugging and re-plugging reports it again.
/// </summary>
public sealed class DriveArrivalWatcher(IVolumeProvider volumes, TimeProvider? time = null)
{
    public static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(6);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Dictionary<string, DateTimeOffset> _pending = new(PathGuard.Comparer);
    private readonly HashSet<string> _settled = new(PathGuard.Comparer);

    public IReadOnlyList<DriveCandidate> Poll(IReadOnlyCollection<Guid> pairedDriveIds)
    {
        ArgumentNullException.ThrowIfNull(pairedDriveIds);
        var now = _time.GetUtcNow();
        var present = new HashSet<string>(PathGuard.Comparer);
        var arrivals = new List<DriveCandidate>();

        foreach (var volume in volumes.GetReadyVolumes())
        {
            // Never touch network shares or optical drives: they can be slow or hang.
            if (volume.DriveType is DriveType.Network or DriveType.CDRom)
            {
                continue;
            }

            var root = PathGuard.Normalize(volume.RootPath);
            present.Add(root);
            if (_settled.Contains(root))
            {
                continue;
            }

            var marker = DriveMarkerStore.TryRead(root);
            if (marker is not null)
            {
                _settled.Add(root);
                _pending.Remove(root);
                if (pairedDriveIds.Contains(marker.DriveId))
                {
                    arrivals.Add(new DriveCandidate(root, marker, volume));
                }

                continue;
            }

            if (!_pending.TryGetValue(root, out var firstSeen))
            {
                _pending[root] = now;
            }
            else if (now - firstSeen >= SettleWindow)
            {
                _pending.Remove(root);
                _settled.Add(root);
            }
        }

        _settled.RemoveWhere(r => !present.Contains(r));
        foreach (var gone in _pending.Keys.Where(r => !present.Contains(r)).ToList())
        {
            _pending.Remove(gone);
        }

        return arrivals;
    }
}
