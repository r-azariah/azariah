using Azariah.Core.Diagnostics;
using Azariah.Core.Files;

namespace Azariah.Core.Tests;

public sealed class FileOperationTests : IDisposable
{
    private readonly TempDrive _drive = new();
    private readonly TrashService _trash;
    private readonly FileOperationService _files;

    public FileOperationTests()
    {
        _trash = new TrashService(_drive.Layout, NullAppLog.Instance);
        _files = new FileOperationService(_drive.Layout, _trash, NullAppLog.Instance);
    }

    public void Dispose() => _drive.Dispose();

    [Fact]
    public void Listing_the_root_hides_the_system_folder()
    {
        var names = _files.List(_drive.Root, includeHidden: true).Select(e => e.Name).ToList();
        Assert.DoesNotContain(".azariah", names);
        Assert.Contains("Files", names);
    }

    [Fact]
    public void Create_and_rename_folder_including_case_only_rename()
    {
        var created = _files.CreateFolder(_drive.Layout.Files, "Stuff");
        Assert.Throws<FileOperationException>(() => _files.CreateFolder(_drive.Layout.Files, "Stuff"));
        var renamed = _files.Rename(created.FullPath, "stuff");
        Assert.Equal("stuff", Path.GetFileName(renamed));
        Assert.True(Directory.Exists(renamed));
    }

    [Fact]
    public void Invalid_names_are_refused()
    {
        Assert.Throws<FileOperationException>(() => _files.CreateFolder(_drive.Layout.Files, "CON"));
    }

    [Fact]
    public async Task Copy_with_keep_both_never_overwrites()
    {
        var src = _drive.File("Transfer/a.txt", "new");
        _drive.File("Files/a.txt", "old");
        var report = await _files.CopyAsync([src], _drive.Layout.Files, ConflictPolicy.KeepBoth);
        Assert.Equal(1, report.Succeeded);
        Assert.Equal("old", File.ReadAllText(Path.Combine(_drive.Layout.Files, "a.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_drive.Layout.Files, "a (2).txt")));
    }

    [Fact]
    public async Task Copy_with_replace_overwrites_and_merges_folders()
    {
        _drive.File("Transfer/Proj/one.lua", "v2");
        _drive.File("Files/Proj/one.lua", "v1");
        _drive.File("Files/Proj/keep.lua", "keep");
        var report = await _files.CopyAsync([Path.Combine(_drive.Layout.Transfer, "Proj")], _drive.Layout.Files, ConflictPolicy.Replace);
        Assert.False(report.HasErrors);
        Assert.Equal("v2", File.ReadAllText(Path.Combine(_drive.Layout.Files, "Proj", "one.lua")));
        Assert.True(File.Exists(Path.Combine(_drive.Layout.Files, "Proj", "keep.lua")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_drive.Layout.Files, "Proj"), "*.partial"));
    }

    [Fact]
    public async Task Copying_into_the_same_folder_duplicates()
    {
        var src = _drive.File("Files/doc.md");
        await _files.CopyAsync([src], _drive.Layout.Files, ConflictPolicy.Replace);
        Assert.True(File.Exists(Path.Combine(_drive.Layout.Files, "doc (2).md")));
    }

    [Fact]
    public async Task A_folder_cannot_be_moved_into_itself()
    {
        var folder = _drive.Folder("Files/Outer");
        var inner = _drive.Folder("Files/Outer/Inner");
        var report = await _files.MoveAsync([folder], inner, ConflictPolicy.KeepBoth);
        Assert.Single(report.Errors);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task Move_renames_within_the_drive()
    {
        var src = _drive.File("Transfer/place.rbxl");
        var report = await _files.MoveAsync([src], _drive.Layout.Roblox, ConflictPolicy.KeepBoth);
        Assert.Equal(1, report.Succeeded);
        Assert.False(File.Exists(src));
        Assert.True(File.Exists(Path.Combine(_drive.Layout.Roblox, "place.rbxl")));
    }

    [Fact]
    public async Task Writes_outside_the_drive_and_into_protected_areas_are_refused()
    {
        var src = _drive.File("Files/a.txt");
        await Assert.ThrowsAsync<FileOperationException>(() => _files.CopyAsync([src], Path.GetTempPath(), ConflictPolicy.KeepBoth));
        await Assert.ThrowsAsync<FileOperationException>(() => _files.CopyAsync([src], _drive.Layout.SystemFolder, ConflictPolicy.KeepBoth));
        Assert.Throws<FileOperationException>(() => _files.Rename(_drive.Layout.SystemFolder, "x"));
        var trashReport = await _files.MoveToTrashAsync([_drive.Layout.MarkerFile]);
        Assert.Single(trashReport.Errors);
    }

    [Fact]
    public async Task Export_copies_to_a_folder_on_the_pc()
    {
        var src = _drive.File("Transfer/out.txt", "data");
        using var pc = new TempDrive(initialize: false);
        var report = await _files.ExportAsync([src], pc.Root, ConflictPolicy.KeepBoth);
        Assert.Equal(1, report.Succeeded);
        Assert.Equal("data", File.ReadAllText(Path.Combine(pc.Root, "out.txt")));
    }

    [Fact]
    public async Task Trash_moves_restores_and_empties()
    {
        var src = _drive.File("Files/Notes/todo.txt", "x");
        var report = await _files.MoveToTrashAsync([src]);
        Assert.Equal(1, report.Succeeded);
        Assert.False(File.Exists(src));

        var entry = Assert.Single(_trash.List());
        Assert.Equal(Path.Combine("Files", "Notes", "todo.txt"), entry.Info.OriginalRelativePath);

        _drive.File("Files/Notes/todo.txt", "someone else");
        var restored = _trash.Restore(entry);
        Assert.Equal("todo (2).txt", Path.GetFileName(restored));
        Assert.Empty(_trash.List());

        await _files.MoveToTrashAsync([restored]);
        Assert.Equal(1, _trash.Empty());
        Assert.Empty(_trash.List());
    }

    [Fact]
    public async Task Search_matches_text_and_wildcards_and_skips_system_area()
    {
        _drive.File("Roblox/Places/Obby.rbxl");
        _drive.File("Roblox/Scripts/obby_spawn.luau");
        _drive.File(".azariah/secret-obby.txt");
        var search = new FileSearchService(_drive.Layout);

        var byText = await search.SearchAsync(_drive.Root, new SearchQuery("obby", IncludeHidden: true)).ToListAsync();
        Assert.Equal(2, byText.Count);

        var byPattern = await search.SearchAsync(_drive.Root, new SearchQuery("*.rbxl")).ToListAsync();
        Assert.Equal("Obby.rbxl", Assert.Single(byPattern).Name);

        var byKind = await search.SearchAsync(_drive.Layout.Roblox, new SearchQuery(string.Empty, Kinds: new HashSet<FileKind> { FileKind.LuauScript })).ToListAsync();
        Assert.Equal("obby_spawn.luau", Assert.Single(byKind).Name);
    }

    [Fact]
    public void Recent_files_are_relative_and_never_include_the_vault()
    {
        var recent = new RecentFilesStore(_drive.Layout);
        var normal = _drive.File("Files/a.txt");
        var vault = _drive.File("Vault/private.bin");
        recent.Add(normal);
        recent.Add(vault);
        var items = recent.GetRecent();
        Assert.Equal("a.txt", Assert.Single(items).Name);
        Assert.DoesNotContain(_drive.Root, File.ReadAllText(_drive.Layout.RecentFile), StringComparison.Ordinal);
    }
}
