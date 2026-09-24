using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Azariah.App.Platform;
using Azariah.App.ViewModels;
using Azariah.App.Views;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

public class UpdateAndOverlayTests
{
    [Fact]
    public void Update_options_parse()
    {
        var o = LaunchOptions.Parse(["--finish-update", "--wait-for-pid", "42", "--root", "E:\\", "--drive-exe", "E:\\Azariah.exe", "--local-exe", "C:\\x\\Azariah.exe", "--relaunch-local"]);
        Assert.True(o.FinishUpdate);
        Assert.Equal(42, o.WaitForPid);
        Assert.Equal("E:\\Azariah.exe", o.DriveExe);
        Assert.Equal("C:\\x\\Azariah.exe", o.LocalExe);
        Assert.True(o.RelaunchLocal);
    }

    [Fact]
    public async Task Prepare_extracts_the_exe_from_a_zip_and_rejects_junk()
    {
        var dir = Directory.CreateTempSubdirectory("azariah-update-test").FullName;
        var fakeExe = new byte[1_200_000];
        fakeExe[0] = (byte)'M';
        fakeExe[1] = (byte)'Z';
        var zipPath = Path.Combine(dir, "AZARIAH-win-x64.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            await using var entry = await zip.CreateEntry("Azariah.exe").OpenAsync();
            await entry.WriteAsync(fakeExe);
        }

        var package = await UpdateManager.PrepareAsync(zipPath);
        Assert.True(File.Exists(package.ExePath));
        Assert.Equal(fakeExe.Length, new FileInfo(package.ExePath).Length);

        var junk = Path.Combine(dir, "notes.txt");
        await File.WriteAllTextAsync(junk, "hi");
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateManager.PrepareAsync(junk));

        var tiny = Path.Combine(dir, "Azariah.exe");
        await File.WriteAllBytesAsync(tiny, [(byte)'M', (byte)'Z', 0, 0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateManager.PrepareAsync(tiny));
    }

    [AvaloniaFact]
    public void Overlay_frames_render()
    {
        var root = Directory.CreateTempSubdirectory("azariah-overlay").FullName;
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        var main = new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        var boot = window.FindControl<BootOverlay>("Boot")!;
        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");

        foreach (var (name, open, t) in new[] { ("open-0250", true, 250.0), ("open-0600", true, 600.0), ("open-0850", true, 850.0), ("open-1000", true, 1000.0), ("close-0300", false, 300.0), ("close-0550", false, 550.0), ("close-1100", false, 1100.0) })
        {
            boot.RenderAt(open, t, "Disconnected");
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (!string.IsNullOrEmpty(shots))
            {
                Directory.CreateDirectory(shots);
                using var stream = File.Create(Path.Combine(shots, $"boot-{name}.png"));
                frame!.Save(stream, new PngBitmapEncoderOptions());
            }
        }

        window.Close();
    }
}
