namespace Azariah.App.Tests;

/// <summary>A small, realistic drive: two games (one with notes, passes and saves), scripts, files.</summary>
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
        File.WriteAllText(Path.Combine(old, "2026-09-20 Obby Rush.rbxl"), new string('x', 41_000));
        File.WriteAllText(Path.Combine(old, "2026-09-12 Obby Rush.rbxl"), new string('x', 30_000));
        Directory.CreateDirectory(Path.Combine(root, "Roblox", "Games", "Tycoon"));

        File.WriteAllText(Path.Combine(root, "Roblox", "Scripts", "SpawnHandler.luau"), "print('hi')");
        File.WriteAllText(Path.Combine(root, "Roblox", "Scripts", "Data.luau"), "local x = 1\nlocal store = DataStoreService:GetDataStore(\"Main\")\n");
        File.WriteAllText(Path.Combine(root, "Files", "Homework notes.md"), "notes");
        Directory.CreateDirectory(Path.Combine(root, "Files", "School"));
    }
}
