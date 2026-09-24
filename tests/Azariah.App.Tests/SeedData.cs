using Azariah.App.ViewModels;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

/// <summary>A small, realistic drive: two games (one with notes, passes and saves), scripts, files, notes.</summary>
internal static class SeedData
{
    public static void Write(string root)
    {
        var game = Directory.CreateDirectory(Path.Combine(root, "Roblox", "Games", "Obby Rush")).FullName;
        File.WriteAllText(Path.Combine(game, "CLAUDE.md"), """
            # Obby Rush! (Roblox game)

            - Place ID 107196611281898.
            - Checkpoints, the lava stage and the leaderboard work. Not published yet.

            ## Next step
            - Add a second world with moving platforms.
            """);
        File.WriteAllText(Path.Combine(game, "PASSES.md"), """
            # Passes: Obby Rush!

            ## Pass 03, 2026-09-24, laptop
            Wired up the lava stage and saved a new LATEST.
            """);
        File.WriteAllText(Path.Combine(game, "Obby Rush (LATEST 2026-09-24).rbxl"), new string('x', 48_000));
        var old = Directory.CreateDirectory(Path.Combine(game, "Old Versions")).FullName;
        File.WriteAllText(Path.Combine(old, "2026-09-20 Obby Rush.rbxl"), new string('x', 70_000));
        File.WriteAllText(Path.Combine(old, "2026-09-12 Obby Rush.rbxl"), new string('x', 30_000));
        Directory.CreateDirectory(Path.Combine(root, "Roblox", "Games", "Tycoon"));

        File.WriteAllText(Path.Combine(root, "Roblox", "Scripts", "SpawnHandler.luau"), "print('hi')");
        File.WriteAllText(Path.Combine(root, "Roblox", "Scripts", "Data.luau"), "local x = 1\nlocal store = DataStoreService:GetDataStore(\"Main\")\n");
        File.WriteAllText(Path.Combine(root, "Files", "Homework notes.md"), "notes");
        Directory.CreateDirectory(Path.Combine(root, "Files", "School"));
        File.WriteAllText(Path.Combine(root, "Transfer", "copy of place.rbxl"), new string('x', 70_000));

        var notes = Directory.CreateDirectory(Path.Combine(root, "Notes", "Daily")).Parent!.FullName;
        File.WriteAllText(Path.Combine(notes, "Ideas.md"), "# Ideas\n\n- Local AI that runs on whatever PC the drive is plugged into.\n- Screenshot hotkey.\n");
        File.WriteAllText(Path.Combine(notes, "Daily", "2026-09-23.md"), "# 2026-09-23\n\n- 21:40 Moved Games into Roblox\n");
    }

    public static WorkspaceViewModel OpenWorkspace(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "azariah-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        Write(root);
        var main = new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
        return Assert.IsType<WorkspaceViewModel>(main.Current);
    }
}
