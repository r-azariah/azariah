using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Azariah.Core.Diagnostics;
using Azariah.Core.Drive;
using Azariah.Core.Files;
using Azariah.Core.Launcher;

namespace Azariah.App.Platform;

public enum AutoLaunchState
{
    NotSupported,
    Off,
    On,
}

public sealed record AutoLaunchStatus(
    AutoLaunchState State,
    bool LocalCopyInstalled,
    bool LocalCopyUpToDate,
    bool RunningFromLocalCopy,
    string InstallFolder,
    int PairedDriveCount,
    bool WatcherRunning);

/// <summary>
/// Turns auto-launch on or off for this PC. "On" means:
/// 1. copy the running Azariah.exe to %LOCALAPPDATA%\Programs\Azariah (the copy that will run),
/// 2. remember this drive's id in %LOCALAPPDATA%\Azariah\launcher.json,
/// 3. add a per-user Startup entry that starts the watcher at sign-in, and start it now.
/// Everything is per-user and reversible; nothing needs administrator rights.
/// </summary>
public sealed class AutoLaunchManager(LauncherPaths paths, IAppLog log)
{
    private readonly LauncherConfigStore _store = new(paths);
    private (long Length, DateTime WriteUtc, bool Same)? _compareCache;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public LauncherPaths Paths => paths;

    public bool RunningFromLocalCopy =>
        Environment.ProcessPath is { } exe && File.Exists(paths.InstalledExe) && PathGuard.AreSame(exe, paths.InstalledExe);

    public async Task<AutoLaunchStatus> GetStatusAsync(Guid driveId)
    {
        if (!IsSupported)
        {
            return new AutoLaunchStatus(AutoLaunchState.NotSupported, false, false, false, paths.InstallFolder, 0, false);
        }

        var config = _store.Load();
        var paired = config.Drives.Any(d => d.DriveId == driveId);
        var registered = OperatingSystem.IsWindows() && StartupRegistration.CurrentCommand() is { } cmd
            && cmd.Contains(paths.InstalledExe, StringComparison.OrdinalIgnoreCase);
        var installed = File.Exists(paths.InstalledExe);
        var upToDate = installed && (RunningFromLocalCopy || await LocalCopyMatchesAsync());

        return new AutoLaunchStatus(
            paired && registered && installed ? AutoLaunchState.On : AutoLaunchState.Off,
            installed,
            upToDate,
            RunningFromLocalCopy,
            paths.InstallFolder,
            config.Drives.Count,
            IsWatcherRunning());
    }

    /// <summary>True while a watcher holds its single-instance mutex in this Windows session.</summary>
    public static bool IsWatcherRunning()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (Mutex.TryOpenExisting(WatcherSignals.MutexName, out var mutex))
        {
            mutex.Dispose();
            return true;
        }

        return false;
    }

    public bool IsPaired(Guid driveId) => _store.Load().Drives.Any(d => d.DriveId == driveId);

    /// <summary>Restarts the watcher if auto-launch is on for this drive but the watcher died.</summary>
    public void EnsureWatcherRunning(Guid driveId)
    {
        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(paths.InstalledExe) && IsPaired(driveId) && !IsWatcherRunning())
            {
                StartWatcher();
                log.Info("Watcher was not running; started it.");
            }
        }
        catch (Exception ex)
        {
            log.Error("Could not start the watcher.", ex);
        }
    }

    /// <summary>
    /// When AZARIAH is started from the drive on a PC where auto-launch is on and the PC copy is the
    /// same build, returns the PC copy so the app runs from local disk (unplugging can't crash it).
    /// </summary>
    public static string? TryGetHandoffTarget(string driveRoot, Guid driveId)
    {
        try
        {
            var self = Environment.ProcessPath;
            if (!OperatingSystem.IsWindows() || self is null || !PathGuard.IsInsideOrEqual(driveRoot, self))
            {
                return null;
            }

            var paths = LauncherPaths.ForCurrentUser();
            if (!File.Exists(paths.InstalledExe)
                || !new LauncherConfigStore(paths).Load().Drives.Any(d => d.DriveId == driveId))
            {
                return null;
            }

            var mine = FileVersionInfo.GetVersionInfo(self);
            var local = FileVersionInfo.GetVersionInfo(paths.InstalledExe);
            return new FileInfo(self).Length == new FileInfo(paths.InstalledExe).Length
                && string.Equals(mine.ProductVersion, local.ProductVersion, StringComparison.Ordinal)
                ? paths.InstalledExe
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task EnableAsync(DriveMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Auto-launch is only available on Windows.");
        }

        await InstallLocalCopyAsync();

        var config = _store.Load();
        config.Drives.RemoveAll(d => d.DriveId == marker.DriveId);
        config.Drives.Add(new PairedDrive(marker.DriveId, marker.DisplayName, DateTimeOffset.UtcNow));
        _store.Save(config);

        StartupRegistration.Register(paths.InstalledExe);
        ChangeAutoPlay(apply: true);
        StartWatcher();
        log.Info("Auto-launch enabled on this PC.");
    }

    public Task DisableAsync(Guid driveId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        var config = _store.Load();
        config.Drives.RemoveAll(d => d.DriveId == driveId);
        _store.Save(config);

        if (config.Drives.Count == 0)
        {
            StartupRegistration.Unregister();
            WatcherSignals.StopRunningWatcher(TimeSpan.FromSeconds(3));
            ChangeAutoPlay(apply: false);
        }

        log.Info("Auto-launch disabled for this drive on this PC.");
        return Task.CompletedTask;
    }

    /// <summary>Plugging a paired drive in should open AZARIAH, not File Explorer too.</summary>
    [SupportedOSPlatform("windows")]
    private void ChangeAutoPlay(bool apply)
    {
        try
        {
            var policy = AutoPlayPolicy.ForCurrentUser(_store, log);
            if (apply)
            {
                policy.Apply();
            }
            else
            {
                policy.Restore();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.Error("Could not change AutoPlay for removable drives.", ex);
        }
    }

    /// <summary>Replaces the PC's copy with the version currently running from the USB.</summary>
    public async Task UpdateLocalCopyAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await InstallLocalCopyAsync();
        if (_store.Load().Drives.Count > 0)
        {
            StartWatcher();
        }
    }

    private async Task InstallLocalCopyAsync()
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException("Can't find the running Azariah.exe.");
        if (RunningFromLocalCopy)
        {
            return;
        }

        // The watcher runs the local exe, which locks it; stop it before replacing the file.
        WatcherSignals.StopRunningWatcher(TimeSpan.FromSeconds(3));

        Directory.CreateDirectory(paths.InstallFolder);
        var temp = paths.InstalledExe + ".new";
        await using (var input = File.OpenRead(source))
        await using (var output = File.Create(temp))
        {
            await input.CopyToAsync(output);
        }

        if (!await SameContentAsync(source, temp))
        {
            File.Delete(temp);
            throw new IOException("The copied program didn't match the original. Nothing was changed.");
        }

        File.Move(temp, paths.InstalledExe, overwrite: true);
    }

    public void StartWatcher()
    {
        var info = new ProcessStartInfo(paths.InstalledExe) { UseShellExecute = false };
        info.ArgumentList.Add("--watch");
        using var _ = Process.Start(info);
    }

    /// <summary>Compares the running exe with the PC copy, hashing only when the PC copy changed.</summary>
    private async Task<bool> LocalCopyMatchesAsync()
    {
        var info = new FileInfo(paths.InstalledExe);
        if (_compareCache is { } c && c.Length == info.Length && c.WriteUtc == info.LastWriteTimeUtc)
        {
            return c.Same;
        }

        var same = await SameContentAsync(Environment.ProcessPath, paths.InstalledExe);
        _compareCache = (info.Length, info.LastWriteTimeUtc, same);
        return same;
    }

    private static async Task<bool> SameContentAsync(string? a, string? b)
    {
        if (a is null || b is null || !File.Exists(a) || !File.Exists(b))
        {
            return false;
        }

        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }

        var hashA = await HashAsync(a);
        var hashB = await HashAsync(b);
        return hashA.AsSpan().SequenceEqual(hashB);
    }

    private static async Task<byte[]> HashAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return await SHA256.HashDataAsync(stream);
    }
}
