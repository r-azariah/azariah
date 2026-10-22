using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Azariah.App.Platform;

/// <summary>
/// Ejects the AZARIAH flash drive after too many failed unlock attempts. The app runs from the verified
/// copy in %LOCALAPPDATA%, not from the USB, so it keeps running after the drive is gone and shows the
/// locked screen. Best effort: every failure is swallowed, since the lock timestamp on the drive (written
/// first) is what actually enforces the 10-minute lock on the next plug-in.
/// </summary>
internal static partial class UsbEject
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;
    private const uint IoctlStorageMediaRemoval = 0x002D4804;
    private const uint IoctlStorageEjectMedia = 0x002D4808;

    public static bool TryEject(string driveRoot)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(driveRoot))
        {
            return false;
        }

        var letter = char.ToUpperInvariant(driveRoot[0]);
        if (letter is < 'A' or > 'Z')
        {
            return false;
        }

        try
        {
            using var handle = CreateFileW(
                $@"\\.\{letter}:", GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return false;
            }

            Control(handle, FsctlLockVolume);
            Control(handle, FsctlDismountVolume);
            PreventRemoval(handle, allow: true);
            return Control(handle, IoctlStorageEjectMedia);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool Control(SafeFileHandle handle, uint code) =>
        DeviceIoControl(handle, code, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

    private static void PreventRemoval(SafeFileHandle handle, bool allow)
    {
        // PREVENT_MEDIA_REMOVAL { BOOLEAN PreventMediaRemoval } — one byte, 0 = allow removal.
        var buffer = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(buffer, (byte)(allow ? 0 : 1));
            DeviceIoControl(handle, IoctlStorageMediaRemoval, buffer, 1, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial SafeFileHandle CreateFileW(
        string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle device, uint code, IntPtr inBuffer, uint inSize, IntPtr outBuffer, uint outSize, out uint returned, IntPtr overlapped);
}
