using Azariah.Core.Drive;
using Azariah.Core.Serialization;

namespace Azariah.Core.Devices;

/// <summary>Reads and writes the owner's device list at <c>.azariah/devices.json</c> on the drive.</summary>
public sealed class OwnerDevicesStore(DriveLayout layout)
{
    public OwnerDevices Load() =>
        JsonFile.TryRead(layout.DevicesFile, AzariahJsonContext.Default.OwnerDevices) ?? new OwnerDevices();

    public void Save(OwnerDevices devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        JsonFile.WriteAtomic(layout.DevicesFile, devices, AzariahJsonContext.Default.OwnerDevices);
    }

    /// <summary>
    /// Decides which face to show for <paramref name="pc"/>. When this PC is a name-only owner entry,
    /// records its MachineGuid so later checks use the stronger signal (best effort; never throws).
    /// </summary>
    public bool IsOwnerDevice(DeviceInfo pc)
    {
        var devices = Load();

        // An unclaimed drive (no devices listed yet) shows the full workspace, so first-time setup
        // and any drive made before this feature keep working. Once devices are listed, it's locked to them.
        if (devices.Devices.Count == 0)
        {
            return true;
        }

        if (!OwnerDeviceMatch.IsOwner(devices, pc))
        {
            return false;
        }

        if (OwnerDeviceMatch.Harden(devices, pc) is { } hardened)
        {
            try
            {
                Save(hardened);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return true;
    }
}
