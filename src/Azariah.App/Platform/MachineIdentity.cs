using System.Runtime.Versioning;
using Azariah.Core.Devices;
using Microsoft.Win32;

namespace Azariah.App.Platform;

/// <summary>Describes the PC AZARIAH is running on: machine name plus the Windows MachineGuid
/// (stable per Windows install). Used only to pick the owner workspace vs the guest page.</summary>
public static class MachineIdentity
{
    public static DeviceInfo Current => new(Environment.MachineName, ReadMachineGuid());

    [SupportedOSPlatform("windows")]
    private static string? ReadMachineGuidWindows()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
        return key?.GetValue("MachineGuid") as string;
    }

    private static string? ReadMachineGuid()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return ReadMachineGuidWindows();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
