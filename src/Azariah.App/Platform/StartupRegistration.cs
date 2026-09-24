using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Azariah.App.Platform;

/// <summary>
/// The per-user "Run" entry that starts the watcher when you sign in to Windows. It shows up in
/// Task Manager and Settings under Startup apps, where it can be switched off like any other app.
/// No administrator rights and no Windows service are needed.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Azariah";

    public static string CommandFor(string exePath) => $"\"{exePath}\" --watch";

    public static void Register(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(ValueName, CommandFor(exePath), RegistryValueKind.String);
    }

    public static void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string? CurrentCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) as string;
    }
}
