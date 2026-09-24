using System.Globalization;
using Avalonia.Headless.XUnit;
using Azariah.App.ViewModels;

namespace Azariah.App.Tests;

public class CommandBarTests
{
    [AvaloniaFact]
    public async Task Finds_games_pages_files_and_lines_inside_scripts()
    {
        var bar = SeedData.OpenWorkspace(out _).CommandBar;

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
        var workspace = SeedData.OpenWorkspace(out _);
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

    [AvaloniaFact]
    public async Task Note_capture_adds_a_line_to_todays_note_and_notes_are_searchable()
    {
        var workspace = SeedData.OpenWorkspace(out var root);
        var bar = workspace.CommandBar;

        bar.Open();
        bar.Query = "note: call Sam";
        await bar.Pending;
        var capture = bar.Results[0];
        Assert.Equal("Add to today's note", capture.Title);
        await bar.RunAsync(capture);

        Assert.True(bar.IsOpen);
        Assert.Equal("Added to today's note.", bar.Note);
        Assert.Equal(string.Empty, bar.Query);
        var today = Path.Combine(root, "Notes", "Daily", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");
        Assert.Contains("call Sam", File.ReadAllText(today), StringComparison.Ordinal);

        bar.Query = "screenshot";
        await bar.Pending;
        Assert.Contains(bar.Results, r => r.Kind == CommandKind.Note && r.Title == "Ideas");
    }

    [AvaloniaFact]
    public void Notes_save_while_typing_and_rename_from_the_title()
    {
        var workspace = SeedData.OpenWorkspace(out var root);
        var notes = workspace.Notes;

        notes.NewNote();
        Assert.Equal("Untitled", notes.Selected?.Title);

        notes.Text = "milk, eggs";
        notes.Flush();
        Assert.Equal("milk, eggs", File.ReadAllText(Path.Combine(root, "Notes", "Untitled.md")));

        notes.Title = "Groceries";
        notes.CommitTitleCommand.Execute(null);
        Assert.Equal("Groceries", notes.Selected?.Title);
        Assert.True(File.Exists(Path.Combine(root, "Notes", "Groceries.md")));
        Assert.False(File.Exists(Path.Combine(root, "Notes", "Untitled.md")));
    }
}
