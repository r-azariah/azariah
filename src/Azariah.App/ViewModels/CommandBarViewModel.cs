using System.Collections.ObjectModel;
using System.Globalization;
using Azariah.Core.Files;
using Azariah.Core.Roblox;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Azariah.App.ViewModels;

public enum CommandKind
{
    Page,
    Game,
    Place,
    File,
    Folder,
    Script,
    Search,
    Note,
}

public sealed record CommandItem(string Title, string? Detail, CommandKind Kind, Func<Task> Run, bool KeepOpen = false)
{
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public string KindText => Kind switch
    {
        CommandKind.Page => "Page",
        CommandKind.Game => "Game",
        CommandKind.Place => "Place",
        CommandKind.Folder => "Folder",
        CommandKind.Script => "Script",
        CommandKind.Search => "Search",
        CommandKind.Note => "Note",
        _ => "File",
    };
}

/// <summary>
/// Ctrl+Space: one input to reach anything. Pages and games match instantly; files on the drive
/// are found by name as you type; "Search inside scripts" looks through every Luau script.
/// </summary>
public sealed partial class CommandBarViewModel(WorkspaceViewModel workspace) : ViewModelBase
{
    private const int MaxFiles = 8;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<RobloxProject> _games = [];
    private bool _updatingQuery;

    public ObservableCollection<CommandItem> Results { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CommandItem? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    /// <summary>Finishes when the current round of results is in (used by tests).</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        _games = RobloxProjects.Load(workspace.Session.Layout);
        _updatingQuery = true;
        Query = string.Empty;
        _updatingQuery = false;
        IsOpen = true;
        Pending = UpdateAsync();
    }

    public void Close()
    {
        _cts?.Cancel();
        IsOpen = false;
        IsSearching = false;
    }

    public void Move(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(index + delta, 0, Results.Count - 1)];
    }

    public async Task RunAsync(CommandItem? item = null)
    {
        item ??= Selected ?? Results.FirstOrDefault();
        if (item is null)
        {
            return;
        }

        if (!item.KeepOpen)
        {
            Close();
        }

        await item.Run();
    }

    partial void OnQueryChanged(string value)
    {
        if (IsOpen && !_updatingQuery)
        {
            Pending = UpdateAsync();
        }
    }

    partial void OnNoteChanged(string? value) => OnPropertyChanged(nameof(HasNote));

    private async Task UpdateAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var ct = cts.Token;
        var query = Query.Trim();
        Note = null;

        var items = new List<CommandItem>();
        if (CaptureText(query) is { } capture)
        {
            items.Add(new CommandItem("Add to today's note", capture, CommandKind.Note, () => CaptureAsync(capture), KeepOpen: true));
        }

        AddGames(items, query);
        AddPages(items, query);
        if (query.Length == 0)
        {
            AddRecent(items);
        }
        else if (query.Length >= 2)
        {
            AddNotes(items, query);
            items.Add(new CommandItem(
                $"Search inside scripts for “{query}”",
                "Every Luau script on the drive",
                CommandKind.Search,
                () => SearchScriptsAsync(query),
                KeepOpen: true));
        }

        Show(items);
        if (query.Length < 2)
        {
            return;
        }

        try
        {
            await Task.Delay(120, ct);
            IsSearching = true;
            var root = workspace.Session.Layout.Root;
            await foreach (var entry in workspace.Session.Search.SearchAsync(root, new SearchQuery(query, MaxResults: MaxFiles), ct))
            {
                items.Add(ForEntry(entry));
            }

            Show(items);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(cts, _cts))
            {
                IsSearching = false;
            }
        }
    }

    private async Task SearchScriptsAsync(string phrase)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var items = new List<CommandItem>();
        Show(items);
        Note = $"Searching scripts for “{phrase}”…";
        IsSearching = true;
        try
        {
            var layout = workspace.Session.Layout;
            var search = new ContentSearchService(layout);
            await foreach (var match in search.SearchAsync(layout.Root, phrase, ct: cts.Token))
            {
                var path = match.Path;
                items.Add(new CommandItem(
                    string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(path)}:{match.Line}"),
                    match.Text,
                    CommandKind.Script,
                    () => workspace.OpenPathAsync(path)));
                if (items.Count % 20 == 0)
                {
                    Show(items);
                }
            }

            Show(items);
            Note = items.Count switch
            {
                0 => $"No script mentions “{phrase}”.",
                1 => "1 line",
                _ => string.Create(CultureInfo.InvariantCulture, $"{items.Count} lines"),
            };
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(cts, _cts))
            {
                IsSearching = false;
            }
        }
    }

    private void AddGames(List<CommandItem> items, string query)
    {
        foreach (var game in _games.Where(g => Matches(g.Title, query) || Matches(Path.GetFileName(g.Folder), query)))
        {
            var folder = game.Folder;
            items.Add(new CommandItem(game.Title, "Roblox game", CommandKind.Game, () =>
            {
                workspace.OpenProject(folder);
                return Task.CompletedTask;
            }));

            items.Add(new CommandItem($"Open {game.Title} in Studio", game.Latest?.Name ?? "Roblox Studio", CommandKind.Place, () => workspace.OpenGameAsync(game)));
        }
    }

    private void AddPages(List<CommandItem> items, string query)
    {
        foreach (var nav in workspace.NavItems.Where(n => Matches(n.Title, query)))
        {
            var key = nav.Key;
            items.Add(new CommandItem(nav.Title, null, CommandKind.Page, () =>
            {
                workspace.Navigate(key);
                return Task.CompletedTask;
            }));
        }

        if (Matches("Trash", query))
        {
            items.Add(new CommandItem("Trash", null, CommandKind.Page, () =>
            {
                workspace.OpenTrash();
                return Task.CompletedTask;
            }));
        }

        if (Matches("Space", query) || Matches("Storage", query))
        {
            items.Add(new CommandItem("Space", "What's using the drive", CommandKind.Page, () =>
            {
                workspace.OpenSpace();
                return Task.CompletedTask;
            }));
        }

        if (Matches("New note", query))
        {
            items.Add(new CommandItem("New note", null, CommandKind.Note, () =>
            {
                workspace.Navigate("notes");
                workspace.Notes.NewNote();
                return Task.CompletedTask;
            }));
        }

        if (Matches("Today's note", query) || Matches("Daily note", query))
        {
            items.Add(new CommandItem("Today's note", null, CommandKind.Note, () =>
            {
                workspace.Navigate("notes");
                workspace.Notes.Today();
                return Task.CompletedTask;
            }));
        }
    }

    private void AddNotes(List<CommandItem> items, string query)
    {
        if (CaptureText(query) is not null)
        {
            return;
        }

        foreach (var note in workspace.Session.Notes.Search(query).Take(5))
        {
            var path = note.Path;
            items.Add(new CommandItem(note.Title, note.Preview.Length > 0 ? note.Preview : note.Folder, CommandKind.Note, () =>
            {
                workspace.OpenNote(path);
                return Task.CompletedTask;
            }));
        }
    }

    private Task CaptureAsync(string text)
    {
        try
        {
            workspace.CaptureNote(text);
            _updatingQuery = true;
            Query = string.Empty;
            _updatingQuery = false;
            Pending = UpdateAsync();
            Note = "Added to today's note.";
        }
        catch (Exception ex)
        {
            Note = $"Couldn't add it: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    /// <summary>"note: call Sam" or "note call Sam" captures "call Sam".</summary>
    private static string? CaptureText(string query)
    {
        if (query.Length > 5 && query.StartsWith("note", StringComparison.OrdinalIgnoreCase) && query[4] is (':' or ' '))
        {
            var text = query[5..].Trim();
            return text.Length > 0 ? text : null;
        }

        return null;
    }

    private void AddRecent(List<CommandItem> items)
    {
        foreach (var entry in workspace.Session.Recent.GetRecent(6))
        {
            items.Add(ForEntry(entry));
        }
    }

    private CommandItem ForEntry(FileEntry entry)
    {
        var path = entry.FullPath;
        var layout = workspace.Session.Layout;
        var parent = Path.GetDirectoryName(path);
        var where = parent is not null && layout.Contains(parent) ? layout.ToRelative(parent) : parent;
        return new CommandItem(
            entry.Name,
            where == "." ? "Drive" : where,
            entry.IsDirectory ? CommandKind.Folder : entry.Kind == FileKind.RobloxPlace ? CommandKind.Place : CommandKind.File,
            () => workspace.OpenPathAsync(path));
    }

    private void Show(List<CommandItem> items)
    {
        Results.Clear();
        foreach (var item in items)
        {
            Results.Add(item);
        }

        Selected = Results.FirstOrDefault();
    }

    private static bool Matches(string text, string query) =>
        query.Length == 0 || text.Contains(query, StringComparison.OrdinalIgnoreCase);
}
