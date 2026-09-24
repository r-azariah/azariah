using Azariah.Core.Files;
using Azariah.Core.Roblox;

namespace Azariah.Core.Tests;

public sealed class PlaceFinderTests : IDisposable
{
    private readonly TempDrive _pc = new(initialize: false);

    public void Dispose() => _pc.Dispose();

    [Fact]
    public void Finds_the_newest_place_within_the_depth_limit()
    {
        var old = Saved(@"Documents\old.rbxl", day: 1);
        var mid = Saved(@"Documents\ROBLOX\mid.rbxlx", day: 20);
        Saved(@"Documents\a\b\c\d\too deep.rbxl", day: 23);
        Saved(@"Documents\notes.txt", day: 24);

        var newest = PlaceFinder.FindNewest([new SearchRoot(Path.Combine(_pc.Root, "Documents"), 3)]);

        Assert.NotNull(newest);
        Assert.True(PathGuard.AreSame(mid, newest.FullPath));
        Assert.False(PathGuard.AreSame(old, newest.FullPath));
    }

    [Fact]
    public void Excluded_files_are_skipped()
    {
        var older = Saved("a.rbxl", day: 10);
        var latest = Saved("b.rbxl", day: 12);

        var newest = PlaceFinder.FindNewest([new SearchRoot(_pc.Root, 0)], path => PathGuard.AreSame(path, latest));

        Assert.NotNull(newest);
        Assert.True(PathGuard.AreSame(older, newest.FullPath));
    }

    [Fact]
    public void Nothing_found_returns_null()
    {
        Saved("readme.md", day: 5);
        Assert.Null(PlaceFinder.FindNewest([new SearchRoot(_pc.Root, 2), new SearchRoot(Path.Combine(_pc.Root, "missing"), 1)]));
    }

    private string Saved(string relative, int day)
    {
        var path = _pc.File(relative);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, day, 12, 0, 0, DateTimeKind.Utc));
        return path;
    }
}
