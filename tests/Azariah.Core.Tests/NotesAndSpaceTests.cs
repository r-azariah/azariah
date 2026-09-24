using Azariah.Core.Diagnostics;
using Azariah.Core.Files;
using Azariah.Core.Notes;
using Azariah.Core.Space;

namespace Azariah.Core.Tests;

public sealed class NotesAndSpaceTests : IDisposable
{
    private readonly TempDrive _drive = new();
    private readonly FileOperationService _files;
    private readonly NotesService _notes;

    public NotesAndSpaceTests()
    {
        _files = new FileOperationService(_drive.Layout, new TrashService(_drive.Layout, NullAppLog.Instance), NullAppLog.Instance);
        _notes = new NotesService(_drive.Layout, _files);
    }

    public void Dispose() => _drive.Dispose();

    [Fact]
    public void Notes_are_markdown_files_listed_newest_first_with_a_preview()
    {
        Assert.Empty(_notes.List());

        var ideas = _notes.Create("Ideas");
        _notes.Write(ideas, "# Ideas\n\n- Local AI on the desktop GPU\n");
        File.SetLastWriteTimeUtc(ideas, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        var second = _notes.Create("Ideas");
        _drive.File(@"Notes\.obsidian\workspace.md", "ignored");

        var list = _notes.List();
        Assert.Equal(2, list.Count);
        Assert.Equal("Ideas (2)", list[0].Title);
        Assert.True(PathGuard.AreSame(second, list[0].Path));
        Assert.Equal("Local AI on the desktop GPU", list[1].Preview);
        Assert.Equal("Ideas", Assert.Single(_notes.Search("desktop gpu")).Title);
    }

    [Fact]
    public void Quick_capture_appends_to_todays_daily_note()
    {
        var path = _notes.AppendToDaily("buy milk", new DateTime(2026, 9, 24, 14, 3, 0));
        _notes.AppendToDaily("call Sam", new DateTime(2026, 9, 24, 15, 30, 0));

        Assert.EndsWith(Path.Combine("Notes", "Daily", "2026-09-24.md"), path, StringComparison.Ordinal);
        Assert.Equal("# 2026-09-24\n\n- 14:03 buy milk\n- 15:30 call Sam\n", File.ReadAllText(path));
        Assert.Equal("Daily", Assert.Single(_notes.List()).Folder);
    }

    [Fact]
    public async Task Notes_rename_and_go_to_trash()
    {
        var path = _notes.Create("Draft");
        var renamed = _notes.Rename(path, "Plan");
        Assert.Equal("Plan", Assert.Single(_notes.List()).Title);

        await _notes.MoveToTrashAsync(renamed);
        Assert.Empty(_notes.List());
        Assert.Throws<FileOperationException>(() => _notes.Write(Path.Combine(_drive.Root, "Files", "x.md"), "no"));
    }

    [Fact]
    public void Space_report_shows_folders_biggest_files_and_duplicates()
    {
        var copy = new string('a', 3000);
        _drive.File(@"Files\a.bin", copy);
        _drive.File(@"Files\Sub\b.bin", copy);
        _drive.File(@"Roblox\big.bin", new string('b', 10_000));
        _drive.File(@"Transfer\small.txt", "tiny");
        _drive.File(@".azariah\logs\app.log", new string('c', 50_000));

        var report = SpaceScanner.Scan(_drive.Layout, topFiles: 3, minDuplicateBytes: 1000);

        Assert.Equal("Roblox", report.Folders[0].Name);
        Assert.Equal(10_000, report.Folders[0].Bytes);
        Assert.Equal(6000, report.Folders.Single(f => f.Name == "Files").Bytes);
        Assert.DoesNotContain(report.Folders, f => f.Name == ".azariah");

        Assert.Equal(3, report.BiggestFiles.Count);
        Assert.Equal("big.bin", report.BiggestFiles[0].Name);

        var dup = Assert.Single(report.Duplicates);
        Assert.Equal(2, dup.Paths.Count);
        Assert.Equal(3000, dup.WastedBytes);
    }
}
