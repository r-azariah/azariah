using Azariah.Core.Diagnostics;
using Azariah.Core.Files;
using Azariah.Core.Roblox;

namespace Azariah.Core.Tests;

public sealed class RobloxProjectsTests : IDisposable
{
    private readonly TempDrive _drive = new();
    private readonly FileOperationService _files;

    public RobloxProjectsTests()
    {
        _files = new FileOperationService(_drive.Layout, new TrashService(_drive.Layout, NullAppLog.Instance), NullAppLog.Instance);
    }

    public void Dispose() => _drive.Dispose();

    [Fact]
    public void A_game_folder_is_read_from_its_notes_passes_and_saves()
    {
        var game = SeedGame("Obby Rush");

        var project = Assert.Single(RobloxProjects.Load(_drive.Layout));
        Assert.Equal(game, project.Folder);
        Assert.Equal("Obby Rush!", project.Title);
        Assert.Equal("107196611281898", project.PlaceId);
        Assert.Equal("10765819725", project.UniverseId);
        Assert.Equal(new Uri("https://www.roblox.com/games/107196611281898"), project.PlaceUri);
        Assert.Equal(new[] { "Called Obby in Studio.", "Place ID 107196611281898, universe 10765819725.", "Checkpoints work." }, project.Summary);
        Assert.Equal(new[] { "Add the lava stage." }, project.NextStep);

        Assert.NotNull(project.Latest);
        Assert.Equal(new DateOnly(2026, 9, 22), project.Latest.Date);
        Assert.Equal(new[] { "2026-09-21 Obby Rush.rbxl", "2026-09-10 Laptop Checkpoint", "2026-09-09 Obby Rush (start).rbxl" },
            project.OldVersions.Select(v => v.Name));

        Assert.NotNull(project.LastPass);
        Assert.Equal(new DateOnly(2026, 9, 23), project.LastPass.Date);
        Assert.Equal("Pass 02, 2026-09-23, laptop", project.LastPass.Heading);
        Assert.Equal("Fixed the spawn.", project.LastPass.FirstLine);
    }

    [Fact]
    public void An_empty_game_folder_still_shows_up()
    {
        Directory.CreateDirectory(Path.Combine(_drive.Layout.Games, "Next Game"));

        var project = Assert.Single(RobloxProjects.Load(_drive.Layout));
        Assert.Equal("Next Game", project.Title);
        Assert.Null(project.Latest);
        Assert.Null(project.LastPass);
        Assert.Empty(project.Summary);
        Assert.Empty(project.OldVersions);
    }

    [Fact]
    public async Task Save_new_version_archives_the_old_latest_and_keeps_the_original()
    {
        var game = SeedGame("Obby Rush");
        var project = RobloxProjects.LoadProject(game);
        using var pc = new TempDrive(initialize: false);
        var source = pc.File("Obby Rush.rbxl", "new build");

        var latest = await RobloxProjects.SaveNewVersionAsync(_files, project, source, new DateOnly(2026, 9, 24));

        Assert.Equal(Path.Combine(game, "Obby Rush (LATEST 2026-09-24).rbxl"), latest);
        Assert.Equal("new build", File.ReadAllText(latest));
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(game, "Obby Rush (LATEST 2026-09-22).rbxl")));
        Assert.Equal("old build", File.ReadAllText(Path.Combine(game, "Old Versions", "2026-09-22 Obby Rush.rbxl")));

        var reloaded = RobloxProjects.LoadProject(game);
        Assert.Equal(new DateOnly(2026, 9, 24), reloaded.Latest!.Date);
        Assert.Equal("2026-09-22 Obby Rush.rbxl", reloaded.OldVersions[0].Name);
    }

    [Fact]
    public async Task First_save_of_a_new_game_uses_the_folder_name()
    {
        var game = Directory.CreateDirectory(Path.Combine(_drive.Layout.Games, "Next Game")).FullName;
        using var pc = new TempDrive(initialize: false);
        var source = pc.File("place.rbxl", "first");

        var latest = await RobloxProjects.SaveNewVersionAsync(_files, RobloxProjects.LoadProject(game), source, new DateOnly(2026, 9, 24));

        Assert.Equal(Path.Combine(game, "Next Game (LATEST 2026-09-24).rbxl"), latest);
        Assert.False(Directory.Exists(Path.Combine(game, "Old Versions")));
    }

    [Fact]
    public async Task Save_new_version_refuses_non_places()
    {
        var game = SeedGame("Obby Rush");
        using var pc = new TempDrive(initialize: false);
        var notAPlace = pc.File("notes.txt");
        await Assert.ThrowsAsync<FileOperationException>(() =>
            RobloxProjects.SaveNewVersionAsync(_files, RobloxProjects.LoadProject(game), notAPlace, new DateOnly(2026, 9, 24)));
    }

    [Fact]
    public async Task Content_search_finds_lines_in_luau_scripts_only()
    {
        _drive.File(@"Roblox\Scripts\Data.luau", "local x = 1\nlocal store = DataStoreService:GetDataStore(\"Main\")\n");
        _drive.File(@"Roblox\Scripts\Other.luau", "print('hi')");
        _drive.File(@"Files\notes.txt", "DataStore notes");

        var matches = await new ContentSearchService(_drive.Layout).SearchAsync(_drive.Root, "datastore").ToListAsync();

        var match = Assert.Single(matches);
        Assert.EndsWith("Data.luau", match.Path, StringComparison.Ordinal);
        Assert.Equal(2, match.Line);
        Assert.StartsWith("local store", match.Text, StringComparison.Ordinal);
    }

    private string SeedGame(string folderName)
    {
        var game = Directory.CreateDirectory(Path.Combine(_drive.Layout.Games, folderName)).FullName;
        File.WriteAllText(Path.Combine(game, "CLAUDE.md"), """
            # Obby Rush! (Roblox game)

            - Called `Obby` in Studio.
            - Place ID 107196611281898, universe 10765819725.
            - **Checkpoints** work.

            ## Next step
            - Add the lava stage.

            ## Where things are
            - Old Versions\
            """);
        File.WriteAllText(Path.Combine(game, "PASSES.md"), """
            # Passes: Obby Rush!

            ## Pass 02, 2026-09-23, laptop
            Fixed the spawn.

            ## Pass 01, 2026-09-20, desktop
            First pass.
            """);
        File.WriteAllText(Path.Combine(game, $"{folderName} (LATEST 2026-09-22).rbxl"), "old build");
        var old = Directory.CreateDirectory(Path.Combine(game, "Old Versions")).FullName;
        File.WriteAllText(Path.Combine(old, "2026-09-21 Obby Rush.rbxl"), "x");
        File.WriteAllText(Path.Combine(old, "2026-09-09 Obby Rush (start).rbxl"), "x");
        Directory.CreateDirectory(Path.Combine(old, "2026-09-10 Laptop Checkpoint"));
        return game;
    }
}
