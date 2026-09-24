using System.Runtime.Versioning;
using Azariah.Core.Diagnostics;
using Azariah.Core.Launcher;
using Microsoft.Win32;

namespace Azariah.App.Platform;

/// <summary>
/// Keeps File Explorer from opening when the drive is plugged in, so a paired drive opens only
/// AZARIAH. Windows can only cancel AutoPlay for one drive through an HKLM key (admin rights), so
/// this sets this user's "Removable drive" AutoPlay choice to "Take no action", the same switch as
/// Settings > Bluetooth &amp; devices > AutoPlay. It runs once per PC while auto-launch is on; the
/// earlier choice is kept in launcher.json and put back when auto-launch is turned off for the last
/// drive. A choice made in Windows afterwards is left alone.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class AutoPlayPolicy(RegistryKey userRoot, LauncherConfigStore store, IAppLog log)
{
    public const string TakeNoAction = "MSTakeNoAction";

    private const string Handlers = @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers";
    private const string UserChosen = Handlers + @"\UserChosenExecuteHandlers\StorageOnArrival";
    private const string DefaultSelection = Handlers + @"\EventHandlersDefaultSelection\StorageOnArrival";

    public static AutoPlayPolicy ForCurrentUser(LauncherConfigStore store, IAppLog log) =>
        new(Registry.CurrentUser, store, log);

    public void Apply()
    {
        var config = store.Load();
        if (config.AutoPlayChanged)
        {
            return;
        }

        var before = Read(UserChosen);
        var defaultBefore = Read(DefaultSelection);
        Write(UserChosen, TakeNoAction);
        Write(DefaultSelection, TakeNoAction);
        store.Save(config with { AutoPlayChanged = true, AutoPlayBefore = before, AutoPlayDefaultBefore = defaultBefore });
        log.Info("Removable-drive AutoPlay set to take no action.");
    }

    public void Restore()
    {
        var config = store.Load();
        if (!config.AutoPlayChanged)
        {
            return;
        }

        // Only undo our own change; a choice made in Windows since then wins.
        if (string.Equals(Read(UserChosen), TakeNoAction, StringComparison.Ordinal))
        {
            Write(UserChosen, config.AutoPlayBefore);
            Write(DefaultSelection, config.AutoPlayDefaultBefore);
            log.Info("Removable-drive AutoPlay restored.");
        }

        store.Save(config with { AutoPlayChanged = false, AutoPlayBefore = null, AutoPlayDefaultBefore = null });
    }

    private string? Read(string path)
    {
        using var key = userRoot.OpenSubKey(path, writable: false);
        return key?.GetValue(null) as string;
    }

    private void Write(string path, string? value)
    {
        using var key = userRoot.CreateSubKey(path, writable: true);
        if (value is null)
        {
            key.DeleteValue(string.Empty, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(null, value, RegistryValueKind.String);
        }
    }
}
