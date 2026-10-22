using Azariah.Core.Devices;
using Azariah.Core.Space;

namespace Azariah.Core.Tests;

public class OwnerDevicesTests
{
    [Fact]
    public void Matches_by_machine_guid_or_name_only_entry()
    {
        var devices = new OwnerDevices
        {
            Devices =
            [
                new OwnerDevice("LAPTOPBRUH", "guid-1", DateTimeOffset.UtcNow),
                new OwnerDevice("ZOTAC-FRAKENST", null, DateTimeOffset.UtcNow),
            ],
        };

        // Strong match: same MachineGuid, even if the PC was renamed.
        Assert.True(OwnerDeviceMatch.IsOwner(devices, new DeviceInfo("renamed", "guid-1")));
        // Name-only entry (desktop not seen yet) matches by name.
        Assert.True(OwnerDeviceMatch.IsOwner(devices, new DeviceInfo("ZOTAC-FRAKENST", "guid-2")));
        // A stranger's PC: different guid, different name.
        Assert.False(OwnerDeviceMatch.IsOwner(devices, new DeviceInfo("FRIEND-PC", "guid-9")));
        // A name match does NOT count once the entry has its own guid recorded.
        Assert.False(OwnerDeviceMatch.IsOwner(devices, new DeviceInfo("LAPTOPBRUH", "guid-9")));
    }

    [Fact]
    public void Harden_records_the_guid_of_a_name_only_device_once()
    {
        var devices = new OwnerDevices { Devices = [new OwnerDevice("ZOTAC-FRAKENST", null, DateTimeOffset.UtcNow)] };

        var hardened = OwnerDeviceMatch.Harden(devices, new DeviceInfo("ZOTAC-FRAKENST", "guid-2"));
        Assert.NotNull(hardened);
        Assert.Equal("guid-2", hardened.Devices[0].MachineGuid);

        // Nothing to do the second time.
        Assert.Null(OwnerDeviceMatch.Harden(hardened, new DeviceInfo("ZOTAC-FRAKENST", "guid-2")));
        // A PC with no guid never hardens anything.
        Assert.Null(OwnerDeviceMatch.Harden(devices, new DeviceInfo("ZOTAC-FRAKENST", null)));
    }

    [Theory]
    [InlineData(@"C:\Users\me\Downloads\x.jpg", ContentKind.Downloads)]
    [InlineData(@"C:\Steam\steamapps\common\game\data.pak", ContentKind.Games)]
    [InlineData(@"C:\Users\me\Pictures\trip.jpg", ContentKind.Photos)]
    [InlineData(@"C:\Users\me\Videos\clip.mp4", ContentKind.Videos)]
    [InlineData(@"C:\Users\me\Music\song.flac", ContentKind.Music)]
    [InlineData(@"C:\Users\me\Documents\report.pdf", ContentKind.Documents)]
    [InlineData(@"C:\Users\me\Desktop\setup.exe", ContentKind.Installers)]
    [InlineData(@"C:\Users\me\Desktop\backup.zip", ContentKind.Archives)]
    [InlineData(@"C:\Users\me\code\main.cs", ContentKind.AppsAndCode)]
    [InlineData(@"C:\Users\me\Desktop\mystery.xyz", ContentKind.Other)]
    public void Classify_uses_folder_keywords_then_type(string path, ContentKind expected)
    {
        // Normalize the separators for the host so the segment split matches.
        var local = path.Replace('\\', Path.DirectorySeparatorChar);
        Assert.Equal(expected, PcContent.Classify(local));
    }

    [Fact]
    public void Content_scan_groups_by_category_biggest_first()
    {
        var root = Directory.CreateTempSubdirectory("azariah-content").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "a.mp4"), new string('v', 6000));
            File.WriteAllText(Path.Combine(root, "b.mp4"), new string('v', 3000));
            File.WriteAllText(Path.Combine(root, "c.pdf"), new string('d', 1000));

            var report = PcContent.Scan([root]);
            Assert.Equal(10_000, report.TotalBytes);
            Assert.Equal(3, report.TotalFiles);
            Assert.Equal("Videos", report.Categories[0].Name);
            Assert.Equal(9000, report.Categories[0].Bytes);
            Assert.Equal(2, report.Categories[0].FileCount);
            Assert.Equal("b.mp4", report.Categories[0].TopFiles[1].Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Big_files_scanner_returns_the_largest_first()
    {
        var root = Directory.CreateTempSubdirectory("azariah-bigfiles").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "small.txt"), new string('a', 10));
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllText(Path.Combine(root, "sub", "huge.bin"), new string('b', 5000));
            File.WriteAllText(Path.Combine(root, "mid.bin"), new string('c', 2000));

            var top = BigFilesScanner.Scan([root], top: 2);
            Assert.Equal(["huge.bin", "mid.bin"], top.Select(f => f.Name));
            Assert.Equal(5000, top[0].Size);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
