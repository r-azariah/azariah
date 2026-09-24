using System.Diagnostics;
using Avalonia;
using Azariah.App.Platform;
using Azariah.Core.Drive;

namespace Azariah.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);

        // After an update the previous process may still be closing; let it finish first.
        if (options.WaitForPid is { } pid)
        {
            WaitForExit(pid);
        }

        // Background auto-launcher and update helper: no UI framework is loaded in these modes.
        if (options.Watch)
        {
            return WatchMode.Run();
        }

        if (options.FinishUpdate)
        {
            return UpdateManager.FinishUpdate(options);
        }

        UpdateManager.CleanupLeftovers();

        var volumes = new SystemVolumeProvider();
        var locator = new DriveRootLocator(volumes);
        var located = locator.Locate(new LocateRequest(
            options.Root,
            Environment.GetEnvironmentVariable(DriveRootLocator.EnvironmentVariable),
            AppContext.BaseDirectory));

        SingleInstance? instance = null;
        if (located is { Outcome: LocateOutcome.Found, Root: { } root, Marker: { } marker })
        {
            // Started from the drive on a PC with auto-launch: run the identical PC copy instead,
            // so unplugging the drive can't pull the program out from under itself.
            if (AutoLaunchManager.TryGetHandoffTarget(root, marker.DriveId) is { } local)
            {
                var info = new ProcessStartInfo(local) { UseShellExecute = false };
                info.ArgumentList.Add("--root");
                info.ArgumentList.Add(root);
                using var _ = Process.Start(info);
                return 0;
            }

            instance = SingleInstance.TryAcquire(marker.DriveId, out var alreadyRunning);
            if (alreadyRunning)
            {
                return 0; // The running window was asked to come to the front.
            }
        }

        App.Startup = new StartupContext(options, located, volumes, locator, instance);
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            instance?.Dispose();
        }
    }

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void WaitForExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal sealed record StartupContext(
    LaunchOptions Options,
    LocateResult Located,
    IVolumeProvider Volumes,
    DriveRootLocator Locator,
    SingleInstance? Instance);
