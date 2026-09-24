using Azariah.Core.Drive;

namespace Azariah.Core.Tests;

/// <summary>A throwaway folder that plays the part of the USB drive.</summary>
public sealed class TempDrive : IDisposable
{
    public TempDrive(bool initialize = true)
    {
        Root = Path.Combine(Path.GetTempPath(), "azariah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Layout = new DriveLayout(Root);
        if (initialize)
        {
            Marker = new DriveInitializer(new FakeVolumes()).Initialize(Root, "TEST");
        }
    }

    public string Root { get; }
    public DriveLayout Layout { get; }
    public DriveMarker? Marker { get; }

    public string File(string relative, string content = "hello")
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public string Folder(string relative) => Directory.CreateDirectory(Path.Combine(Root, relative)).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public sealed class FakeVolumes : IVolumeProvider
{
    public List<VolumeInfo> Volumes { get; } = [];

    public string? SystemVolumeRoot { get; set; } = "/nonexistent-system-root";

    public void Add(string root, DriveType type = DriveType.Removable) =>
        Volumes.Add(new VolumeInfo(root, "USB", "exFAT", type, 256L << 30, 200L << 30));

    public IReadOnlyList<VolumeInfo> GetReadyVolumes() => Volumes;

    public VolumeInfo? GetVolumeFor(string path) =>
        Volumes.Where(v => Files.PathGuard.IsInsideOrEqual(v.RootPath, path)).OrderByDescending(v => v.RootPath.Length).FirstOrDefault();
}
