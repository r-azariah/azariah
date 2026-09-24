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

        // Background auto-launcher: no UI framework is loaded in this mode.
        if (options.Watch)
        {
            return WatchMode.Run();
        }

        var volumes = new SystemVolumeProvider();
        var locator = new DriveRootLocator(volumes);
        var located = locator.Locate(new LocateRequest(
            options.Root,
            Environment.GetEnvironmentVariable(DriveRootLocator.EnvironmentVariable),
            AppContext.BaseDirectory));

        SingleInstance? instance = null;
        if (located is { Outcome: LocateOutcome.Found, Marker: { } marker })
        {
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
}

internal sealed record StartupContext(
    LaunchOptions Options,
    LocateResult Located,
    IVolumeProvider Volumes,
    DriveRootLocator Locator,
    SingleInstance? Instance);
