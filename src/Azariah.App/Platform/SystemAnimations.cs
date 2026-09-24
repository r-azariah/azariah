using System.Runtime.InteropServices;

namespace Azariah.App.Platform;

/// <summary>Respects Windows' "Animation effects" setting (Settings > Accessibility > Visual effects).</summary>
internal static partial class SystemAnimations
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    public static bool Enabled
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                var value = 1;
                return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref value, 0) || value != 0;
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
            {
                return true;
            }
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);
}
