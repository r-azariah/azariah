namespace Azariah.Core.Devices;

/// <summary>What AZARIAH knows about the PC it is running on. Supplied by the app layer
/// (<c>Environment.MachineName</c> and the Windows MachineGuid); Core only compares it.</summary>
public sealed record DeviceInfo(string Name, string? MachineGuid);

/// <summary>One of the owner's own computers, as recorded on the drive.</summary>
public sealed record OwnerDevice(string Name, string? MachineGuid, DateTimeOffset AddedUtc);

/// <summary>
/// The owner's computers, stored on the drive in <c>.azariah/devices.json</c>. When AZARIAH opens on
/// one of these it shows the full owner workspace; on any other PC it shows the guest page.
/// This is NOT a security boundary: a machine name or MachineGuid can be copied or changed, so it only
/// decides which face to show. Real trust comes with the Phase 3 Vault pairing.
/// </summary>
public sealed record OwnerDevices
{
    public int Version { get; init; } = 1;

    public List<OwnerDevice> Devices { get; init; } = [];
}

public static class OwnerDeviceMatch
{
    /// <summary>
    /// True when this PC is one of the owner's. A device matches by MachineGuid when both are known
    /// (the strong signal), or by machine name for an entry whose MachineGuid hasn't been recorded yet
    /// (so a device the owner listed by name is recognized before its first run).
    /// </summary>
    public static bool IsOwner(OwnerDevices devices, DeviceInfo pc)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pc);
        return devices.Devices.Any(d => Matches(d, pc));
    }

    /// <summary>
    /// Fills in the MachineGuid of a name-only entry once the owner opens AZARIAH on that PC, so later
    /// checks use the stronger signal. Returns the updated list, or null when nothing changed.
    /// </summary>
    public static OwnerDevices? Harden(OwnerDevices devices, DeviceInfo pc)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pc);
        if (string.IsNullOrEmpty(pc.MachineGuid))
        {
            return null;
        }

        var changed = false;
        var updated = devices.Devices.Select(d =>
        {
            if (string.IsNullOrEmpty(d.MachineGuid) && NameMatches(d, pc))
            {
                changed = true;
                return d with { MachineGuid = pc.MachineGuid };
            }

            return d;
        }).ToList();

        return changed ? devices with { Devices = updated } : null;
    }

    private static bool Matches(OwnerDevice device, DeviceInfo pc) =>
        (!string.IsNullOrEmpty(device.MachineGuid) && !string.IsNullOrEmpty(pc.MachineGuid)
            && string.Equals(device.MachineGuid, pc.MachineGuid, StringComparison.OrdinalIgnoreCase))
        || (string.IsNullOrEmpty(device.MachineGuid) && NameMatches(device, pc));

    private static bool NameMatches(OwnerDevice device, DeviceInfo pc) =>
        !string.IsNullOrWhiteSpace(device.Name) && string.Equals(device.Name, pc.Name, StringComparison.OrdinalIgnoreCase);
}
