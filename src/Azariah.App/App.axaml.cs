using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Azariah.App.Platform;
using Azariah.App.ViewModels;
using Azariah.App.Views;
using Azariah.Core.Drive;
using Azariah.Core.Settings;

namespace Azariah.App;

public partial class App : Application
{
    internal static StartupContext? Startup { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var volumes = Startup?.Volumes ?? new SystemVolumeProvider();
            var context = Startup ?? new StartupContext(
                new LaunchOptions(),
                LocateResult.NotFound(),
                volumes,
                new DriveRootLocator(volumes),
                null);

            var main = new MainViewModel(context.Located, context.Volumes, context.Locator);
            var window = new MainWindow { DataContext = main };
            main.AttachWindow(window);

            var animate = SystemAnimations.Enabled && (main.Workspace?.Session.Settings.StartupAnimation ?? true);
            if (animate)
            {
                window.PrepareOpenAnimation();
                window.Opened += (_, _) => _ = window.PlayOpenAnimationAsync();
            }

            context.Instance?.Listen(() => Dispatcher.UIThread.Invoke(window.BringToFront));
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => main.Shutdown();

            // Auto-launch is on for this drive but the background watcher died: bring it back.
            if (main.Workspace?.Session is { } session)
            {
                _ = Task.Run(() => session.AutoLaunch.EnsureWatcherRunning(session.Marker.DriveId));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Closes the whole app (used after the drive is removed and after starting an update).</summary>
    public static void Exit()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public static void ApplyTheme(ThemePreference theme)
    {
        if (Current is { } app)
        {
            app.RequestedThemeVariant = theme switch
            {
                ThemePreference.Light => ThemeVariant.Light,
                ThemePreference.System => ThemeVariant.Default,
                _ => ThemeVariant.Dark,
            };
        }
    }
}
