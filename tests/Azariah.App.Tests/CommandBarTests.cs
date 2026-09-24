using Avalonia.Headless.XUnit;
using Azariah.App.ViewModels;
using Azariah.Core.Drive;

namespace Azariah.App.Tests;

public class CommandBarTests
{
    [AvaloniaFact]
    public async Task Finds_games_pages_files_and_lines_inside_scripts()
    {
        var workspace = OpenDrive();
        var bar = workspace.CommandBar;

        bar.Open();
        await bar.Pending;
        Assert.Contains(bar.Results, r => r.Kind == CommandKind.Game && r.Title == "Obby Rush!");
        Assert.Contains(bar.Results, r => r.Kind == CommandKind.Page && r.Title == "Settings");

        bar.Query = "spawn";
        await bar.Pending;
        Assert.Contains(bar.Results, r => r.Title == "SpawnHandler.luau");

        bar.Query = "DataStore";
        await bar.Pending;
        var search = Assert.Single(bar.Results, r => r.Kind == CommandKind.Search);
        await bar.RunAsync(search);
        Assert.True(bar.IsOpen);
        Assert.Contains(bar.Results, r => r.Kind == CommandKind.Script && r.Title == "Data.luau:2");
    }

    [AvaloniaFact]
    public async Task Picking_a_game_shows_it_on_the_roblox_page()
    {
        var workspace = OpenDrive();
        var bar = workspace.CommandBar;

        bar.Open();
        bar.Query = "obby";
        await bar.Pending;
        Assert.Contains(bar.Results, r => r.Kind == CommandKind.Place && r.Title == "Open Obby Rush! in Studio");

        await bar.RunAsync(bar.Results.First(r => r.Kind == CommandKind.Game));
        Assert.False(bar.IsOpen);
        Assert.Equal("roblox", workspace.SelectedNav?.Key);
        Assert.Equal("Obby Rush!", workspace.Roblox.Selected?.Title);
        Assert.Equal("Place ID 107196611281898.", workspace.Roblox.Selected?.Summary[0]);
    }

    private static WorkspaceViewModel OpenDrive()
    {
        var root = Path.Combine(Path.GetTempPath(), "azariah-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var volumes = new SystemVolumeProvider();
        var marker = new DriveInitializer(volumes).Initialize(root, "AZARIAH");
        SeedData.Write(root);
        var main = new MainViewModel(new LocateResult(LocateOutcome.Found, root, marker, "test", []), volumes, new DriveRootLocator(volumes));
        return Assert.IsType<WorkspaceViewModel>(main.Current);
    }
}
