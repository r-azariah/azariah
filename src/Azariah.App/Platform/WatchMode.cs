using System.Diagnostics;
using Azariah.Core.Diagnostics;
using Azariah.Core.Drive;
using Azariah.Core.Launcher;

namespace Azariah.App.Platform;

/// <summary>
/// <c>Azariah.exe --watch</c>: the lightweight background auto-launcher. It runs from the local
/// copy in %LOCALAPPDATA%\Programs\Azariah (never from the USB), shows no window, and when a
/// paired Azariah drive is plugged in it starts that same local copy with <c>--root &lt;drive&gt;</c>.
/// It never executes anything stored on the USB, so a fake drive with a copied marker cannot run
/// code on this PC.
/// </summary>
internal static class WatchMode
{
    public static int Run()
    {
        var paths = LauncherPaths.ForCurrentUser();
        var log = new FileAppLog(paths.LogsFolder);
        var exe = Environment.ProcessPath;
        if (exe is null)
        {
            log.Error("Watcher could not determine its own path.");
            return 1;
        }

        using var mutex = new Mutex(initiallyOwned: true, WatcherSignals.MutexName, out var isFirst);
        if (!isFirst)
        {
            return 0; // Another watcher is already running for this user.
        }

        using var stop = OperatingSystem.IsWindows()
            ? new EventWaitHandle(false, EventResetMode.AutoReset, WatcherSignals.StopEventName)
            : new EventWaitHandle(false, EventResetMode.AutoReset);

        var store = new LauncherConfigStore(paths);
        var config = store.Load();
        var configStamp = store.LastWriteUtc;
        var watcher = new DriveArrivalWatcher(new SystemVolumeProvider());
        log.Info($"Watcher started for {config.Drives.Count} drive(s).");

        try
        {
            while (!stop.WaitOne(TimeSpan.FromSeconds(1)))
            {
                if (store.LastWriteUtc != configStamp)
                {
                    config = store.Load();
                    configStamp = store.LastWriteUtc;
                }

                if (config.Drives.Count == 0)
                {
                    log.Info("No paired drives left; watcher exiting.");
                    break;
                }

                var ids = config.Drives.Select(d => d.DriveId).ToHashSet();
                foreach (var arrival in watcher.Poll(ids))
                {
                    Launch(exe, arrival.Root, log);
                }
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }

        log.Info("Watcher stopped.");
        return 0;
    }

    private static void Launch(string exe, string root, IAppLog log)
    {
        try
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = false };
            info.ArgumentList.Add("--root");
            info.ArgumentList.Add(root);
            info.ArgumentList.Add("--launched-by-watcher");
            using var _ = Process.Start(info);
            log.Info("Paired drive detected; launched Azariah.");
        }
        catch (Exception ex)
        {
            log.Error("Failed to launch Azariah for a paired drive.", ex);
        }
    }
}
