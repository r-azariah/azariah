using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Azariah.Core.Diagnostics;
using Azariah.Core.Files;
using Azariah.Core.Launcher;

namespace Azariah.App.Platform;

public sealed record UpdatePackage(string ExePath, Version Version, string VersionText);

/// <summary>
/// Self-update from a dropped build (.zip or .exe). The new exe is staged in %TEMP% and started
/// in <c>--finish-update</c> mode: it waits for this app to exit, replaces the exe on the drive and
/// the auto-launch copy on this PC (hash-verified), restarts the watcher and reopens AZARIAH.
/// Nothing overwrites a running program, so it works the same on NTFS, exFAT and FAT32.
/// </summary>
public sealed class UpdateManager(LauncherPaths paths, IAppLog log)
{
    public const string ExeName = "Azariah.exe";

    /// <summary>Builds older than this don't understand <c>--finish-update</c>.</summary>
    public static readonly Version MinimumVersion = new(0, 3, 0);

    public static string StagingFolder => Path.Combine(Path.GetTempPath(), "azariah-update");

    public static Version CurrentVersion => typeof(UpdateManager).Assembly.GetName().Version ?? new Version(0, 0);

    public static string CurrentVersionText =>
        typeof(UpdateManager).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? CurrentVersion.ToString(3);

    /// <summary>Copies or extracts the build into a fresh staging folder and checks it's an AZARIAH exe.</summary>
    public static async Task<UpdatePackage> PrepareAsync(string sourcePath, CancellationToken ct = default)
    {
        var staging = Path.Combine(StagingFolder, Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(staging);
        var exe = Path.Combine(staging, ExeName);

        switch (Path.GetExtension(sourcePath).ToUpperInvariant())
        {
            case ".ZIP":
                await using (var zip = await ZipFile.OpenReadAsync(sourcePath, ct))
                {
                    var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.Name, ExeName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("That zip doesn't contain Azariah.exe.");
                    await entry.ExtractToFileAsync(exe, overwrite: true, ct);
                }

                break;
            case ".EXE":
                await using (var input = File.OpenRead(sourcePath))
                await using (var output = File.Create(exe))
                {
                    await input.CopyToAsync(output, ct);
                }

                break;
            default:
                throw new InvalidOperationException("Drop an AZARIAH .zip or Azariah.exe.");
        }

        var (version, text) = ReadVersion(exe);
        return new UpdatePackage(exe, version, text);
    }

    /// <summary>Starts the staged exe as the update helper. The caller must exit right after.</summary>
    public void StartInstall(UpdatePackage package, string driveRoot, string driveExe, bool relaunchLocal)
    {
        ArgumentNullException.ThrowIfNull(package);
        var info = new ProcessStartInfo(package.ExePath) { UseShellExecute = false };
        info.ArgumentList.Add("--finish-update");
        info.ArgumentList.Add("--wait-for-pid");
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("--root");
        info.ArgumentList.Add(driveRoot);
        info.ArgumentList.Add("--drive-exe");
        info.ArgumentList.Add(driveExe);
        if (File.Exists(paths.InstalledExe))
        {
            info.ArgumentList.Add("--local-exe");
            info.ArgumentList.Add(paths.InstalledExe);
            if (relaunchLocal)
            {
                info.ArgumentList.Add("--relaunch-local");
            }
        }

        using var _ = Process.Start(info);
        log.Info($"Update to {package.VersionText} started.");
    }

    /// <summary><c>--finish-update</c>: runs from the staging folder with no UI.</summary>
    public static int FinishUpdate(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var paths = LauncherPaths.ForCurrentUser();
        var log = new FileAppLog(paths.LogsFolder);
        var self = Environment.ProcessPath;
        if (self is null)
        {
            return 1;
        }

        try
        {
            // The watcher runs the PC copy; stop it so that file can be replaced.
            WatcherSignals.StopRunningWatcher(TimeSpan.FromSeconds(5));

            var ok = true;
            if (options.DriveExe is { } driveExe)
            {
                ok &= Install(self, driveExe, log);
            }

            if (options.LocalExe is { } localExe)
            {
                ok &= Install(self, localExe, log);
                if (new LauncherConfigStore(paths).Load().Drives.Count > 0 && File.Exists(localExe))
                {
                    Start(localExe, ["--watch"]);
                }
            }

            var relaunch = options.RelaunchLocal && options.LocalExe is { } l && File.Exists(l) ? l : options.DriveExe;
            if (relaunch is not null && File.Exists(relaunch) && options.Root is { } root)
            {
                Start(relaunch, ["--root", root, "--wait-for-pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            }

            log.Info(ok ? "Update installed." : "Update finished with errors.");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            log.Error("Update failed.", ex);
            return 1;
        }
    }

    /// <summary>Removes staging leftovers from an earlier update (best effort).</summary>
    public static void CleanupLeftovers()
    {
        try
        {
            var self = Environment.ProcessPath;
            if (Directory.Exists(StagingFolder) && (self is null || !PathGuard.IsInsideOrEqual(StagingFolder, self)))
            {
                Directory.Delete(StagingFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static (Version Version, string Text) ReadVersion(string exe)
    {
        var length = new FileInfo(exe).Length;
        Span<byte> header = stackalloc byte[2];
        using (var stream = File.OpenRead(exe))
        {
            stream.ReadExactly(header);
        }

        if (length < 1_000_000 || header[0] != (byte)'M' || header[1] != (byte)'Z')
        {
            throw new InvalidOperationException("That file isn't an AZARIAH build.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return (CurrentVersion, "unknown");
        }

        var info = FileVersionInfo.GetVersionInfo(exe);
        if (!string.Equals(info.ProductName, "Azariah", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That file isn't an AZARIAH build.");
        }

        var version = Version.TryParse(info.FileVersion, out var v) ? v : new Version(0, 0);
        var text = info.ProductVersion?.Split('+')[0] ?? version.ToString(3);
        return (version, text);
    }

    private static bool Install(string source, string target, IAppLog log)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var temp = target + ".new";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, temp, overwrite: true);
                if (!SameContent(source, temp))
                {
                    File.Delete(temp);
                    throw new IOException("Copy verification failed.");
                }

                File.Move(temp, target, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The old process may still be releasing the file; retry for ~10 s.
                Thread.Sleep(500);
                if (attempt == 19)
                {
                    log.Error("Could not replace an AZARIAH executable during update.", ex);
                }
            }
        }

        return false;
    }

    private static bool SameContent(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
        {
            return false;
        }

        using var sa = File.OpenRead(a);
        using var sb = File.OpenRead(b);
        return SHA256.HashData(sa).AsSpan().SequenceEqual(SHA256.HashData(sb));
    }

    private static void Start(string exe, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var _ = Process.Start(info);
    }
}
