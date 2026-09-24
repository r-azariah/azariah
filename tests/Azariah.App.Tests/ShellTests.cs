using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Azariah.App.ViewModels;
using Azariah.App.Views;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

public class ShellTests
{
    [Fact]
    public void Launch_options_parse_root_and_watch()
    {
        var o = LaunchOptions.Parse(["--root", @"E:\", "--launched-by-watcher"]);
        Assert.Equal(@"E:\", o.Root);
        Assert.True(o.LaunchedByWatcher);
        Assert.False(o.Watch);
        Assert.True(LaunchOptions.Parse(["--watch"]).Watch);
    }

    [AvaloniaFact]
    public void Every_page_renders_for_an_initialized_drive()
    {
        var root = Path.Combine(Path.GetTempPath(), "azariah-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        Directory.CreateDirectory(Path.Combine(root, "Roblox", "Games", "Obby Rush"));
        File.WriteAllText(Path.Combine(root, "Roblox", "Games", "Obby Rush", "Obby Rush (LATEST 2026-09-24).rbxl"), "x");
        File.WriteAllText(Path.Combine(root, "Roblox", "Scripts", "SpawnHandler.luau"), "print('hi')");
        File.WriteAllText(Path.Combine(root, "Files", "Homework notes.md"), "notes");
        Directory.CreateDirectory(Path.Combine(root, "Files", "School"));

        var located = new LocateResult(LocateOutcome.Found, root, marker, "test", []);
        var main = new MainViewModel(located, volumes, new DriveRootLocator(volumes));
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        main.AttachWindow(window);
        window.Show();

        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");
        var workspace = Assert.IsType<WorkspaceViewModel>(main.Current);
        foreach (var nav in workspace.NavItems)
        {
            workspace.SelectedNav = nav;
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(150);
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (!string.IsNullOrEmpty(shots))
            {
                Directory.CreateDirectory(shots);
                SavePng(frame!, Path.Combine(shots, $"{nav.Key}.png"));
            }
        }

        window.Close();
    }

    [AvaloniaFact]
    public void Welcome_screen_renders_when_no_drive_is_found()
    {
        var volumes = new SystemVolumeProvider();
        var main = new MainViewModel(LocateResult.NotFound(), volumes, new DriveRootLocator(volumes));
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<WelcomeViewModel>(main.Current);
        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");
        if (!string.IsNullOrEmpty(shots))
        {
            Directory.CreateDirectory(shots);
            SavePng(window.CaptureRenderedFrame()!, Path.Combine(shots, "welcome.png"));
        }

        window.Close();
    }

    private static void SavePng(Avalonia.Media.Imaging.Bitmap bitmap, string path)
    {
        using var stream = File.Create(path);
        bitmap.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
