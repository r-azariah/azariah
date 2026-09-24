using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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
                new LaunchOptions(null, false, false),
                LocateResult.NotFound(),
                volumes,
                new DriveRootLocator(volumes),
                null);

            var main = new MainViewModel(context.Located, context.Volumes, context.Locator);
            var window = new MainWindow { DataContext = main };
            main.AttachWindow(window);
            context.Instance?.Listen(() => Avalonia.Threading.Dispatcher.UIThread.Post(window.BringToFront));
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => main.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
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
