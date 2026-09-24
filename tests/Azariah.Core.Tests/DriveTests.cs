using Azariah.Core.Drive;
using Azariah.Core.Launcher;

namespace Azariah.Core.Tests;

public class DriveTests
{
    [Fact]
    public void Initialize_creates_layout_and_is_idempotent()
    {
        using var drive = new TempDrive(initialize: false);
        var init = new DriveInitializer(new FakeVolumes());
        var first = init.Initialize(drive.Root, "AZARIAH");
        var second = init.Initialize(drive.Root, "ignored");

        Assert.Equal(first.DriveId, second.DriveId);
        Assert.Equal("AZARIAH", second.DisplayName);
        Assert.True(Directory.Exists(drive.Layout.Files));
        Assert.True(Directory.Exists(Path.Combine(drive.Layout.Roblox, "Scripts")));
        Assert.True(Directory.Exists(Path.Combine(drive.Layout.SetupKit, "Installers", "AI Apps")));
        Assert.True(File.Exists(Path.Combine(drive.Layout.SetupKit, "README.md")));
    }

    [Fact]
    public void Initialize_refuses_the_system_drive_root()
    {
        using var drive = new TempDrive(initialize: false);
        var volumes = new FakeVolumes { SystemVolumeRoot = drive.Root };
        Assert.Throws<InvalidOperationException>(() => new DriveInitializer(volumes).Initialize(drive.Root));
    }

    [Fact]
    public void Locator_prefers_explicit_root()
    {
        using var drive = new TempDrive();
        var result = new DriveRootLocator(new FakeVolumes()).Locate(new LocateRequest(drive.Root, null, "/somewhere/else"));
        Assert.Equal(LocateOutcome.Found, result.Outcome);
        Assert.Equal(drive.Marker!.DriveId, result.Marker!.DriveId);
    }

    [Fact]
    public void Locator_walks_up_from_the_executable()
    {
        using var drive = new TempDrive();
        var appFolder = drive.Folder(Path.Combine("Tools", "App"));
        var result = new DriveRootLocator(new FakeVolumes()).Locate(new LocateRequest(null, null, appFolder));
        Assert.Equal(LocateOutcome.Found, result.Outcome);
        Assert.Equal(drive.Layout.Root, result.Root);
    }

    [Fact]
    public void Locator_finds_marked_volume_by_scanning()
    {
        using var drive = new TempDrive();
        var volumes = new FakeVolumes();
        volumes.Add(drive.Root);
        var result = new DriveRootLocator(volumes).Locate(new LocateRequest(null, null, Path.GetTempPath()));
        Assert.Equal(LocateOutcome.Found, result.Outcome);
        Assert.Equal("scan", result.Source);
    }

    [Fact]
    public void Fresh_non_system_volume_needs_initialization()
    {
        using var drive = new TempDrive(initialize: false);
        var volumes = new FakeVolumes();
        volumes.Add(drive.Root);
        var result = new DriveRootLocator(volumes).Locate(new LocateRequest(null, null, drive.Root));
        Assert.Equal(LocateOutcome.NeedsInitialization, result.Outcome);
    }

    [Fact]
    public void Monitor_reports_removal_and_return_under_a_new_path()
    {
        using var drive = new TempDrive();
        using var moved = new TempDrive(initialize: false);
        var volumes = new FakeVolumes();
        var monitor = new DriveMonitor(new DriveRootLocator(volumes), drive.Root, drive.Marker!.DriveId);
        var disconnected = false;
        string? reconnectedAt = null;
        monitor.Disconnected += (_, _) => disconnected = true;
        monitor.Reconnected += (_, root) => reconnectedAt = root;

        // "Unplug": marker disappears. "Replug" under a new letter: marker shows up elsewhere.
        File.Move(drive.Layout.MarkerFile, drive.Layout.MarkerFile + ".away");
        monitor.Poll();
        Assert.False(disconnected); // one miss is tolerated
        monitor.Poll();
        Assert.True(disconnected);

        DriveMarkerStore.Write(moved.Root, drive.Marker);
        volumes.Add(moved.Root);
        monitor.Poll();
        Assert.Equal(moved.Layout.Root, reconnectedAt);
    }

    [Fact]
    public void Arrival_watcher_reports_paired_drive_once_per_insertion()
    {
        using var drive = new TempDrive();
        using var other = new TempDrive();
        var volumes = new FakeVolumes();
        var watcher = new DriveArrivalWatcher(volumes);
        var paired = new[] { drive.Marker!.DriveId };

        Assert.Empty(watcher.Poll(paired));
        volumes.Add(drive.Root);
        volumes.Add(other.Root);
        var arrivals = watcher.Poll(paired);
        Assert.Single(arrivals);
        Assert.Equal(drive.Layout.Root, arrivals[0].Root);
        Assert.Empty(watcher.Poll(paired));

        volumes.Volumes.Clear();
        Assert.Empty(watcher.Poll(paired));
        volumes.Add(drive.Root);
        Assert.Single(watcher.Poll(paired));
    }
}
