using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Azariah.App.ViewModels;
using Azariah.App.Views;
using Azariah.Core.Devices;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

public class GuestPageTests
{
    [AvaloniaFact]
    public void A_drive_claimed_by_other_devices_opens_the_guest_page()
    {
        var main = OpenWith([new OwnerDevice("SOME-OTHER-PC", "other-guid", DateTimeOffset.UtcNow)], out _);
        var guest = Assert.IsType<GuestViewModel>(main.Current);
        Assert.False(guest.IsPreview);
        Assert.Equal(6, guest.Tiles.Count);
        Assert.Contains(guest.Tiles, t => t.Title == "Windows makeover" && !t.HasBadge);
        Assert.Contains(guest.Tiles, t => t.Title == "Local AI" && t.Badge == "Soon");
    }

    [AvaloniaFact]
    public void This_pc_listed_as_an_owner_device_opens_the_full_workspace()
    {
        var main = OpenWith([new OwnerDevice(Environment.MachineName, null, DateTimeOffset.UtcNow)], out _);
        Assert.IsType<WorkspaceViewModel>(main.Current);
    }

    [AvaloniaFact]
    public async Task Guest_biggest_files_scan_lists_something_and_renders()
    {
        var main = OpenWith([new OwnerDevice("SOME-OTHER-PC", "other-guid", DateTimeOffset.UtcNow)], out _);
        var guest = Assert.IsType<GuestViewModel>(main.Current);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        main.AttachWindow(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var shots = Environment.GetEnvironmentVariable("AZARIAH_SCREENSHOTS");
        Save(window.CaptureRenderedFrame(), shots, "guest.png");

        guest.ShowBigFilesCommand.Execute(null);
        await guest.Pending;
        Assert.True(guest.IsBigFiles);
        Assert.NotEmpty(guest.BigFiles); // the test runner's own profile has files

        window.Close();
    }

    private static MainViewModel OpenWith(IEnumerable<OwnerDevice> devices, out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "azariah-guest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        SeedData.Write(root);
        new OwnerDevicesStore(new DriveLayout(root)).Save(new OwnerDevices { Devices = [.. devices] });
        return new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
    }

    private static void Save(Bitmap? frame, string? dir, string name)
    {
        if (frame is null || string.IsNullOrEmpty(dir))
        {
            return;
        }

        Directory.CreateDirectory(dir);
        using var stream = File.Create(Path.Combine(dir, name));
        frame.Save(stream, new PngBitmapEncoderOptions());
    }
}
