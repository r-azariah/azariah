using System.Collections.ObjectModel;
using Avalonia;
using Azariah.App.Services;
using Azariah.Core.Files;
using Azariah.Core.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Azariah.App.ViewModels;

public sealed partial class NoteItemViewModel(NoteInfo info) : ObservableObject
{
    public NoteInfo Info { get; private set; } = info;
    public string Path => Info.Path;
    public string Title => Info.Title;
    public string Preview => Info.Preview;
    public bool HasPreview => Preview.Length > 0;
    public string Detail => Info.Folder.Length > 0 ? $"{Info.Folder}  ·  {Format.Ago(Info.ModifiedUtc)}" : Format.Ago(Info.ModifiedUtc);

    public void Update(NoteInfo info)
    {
        Info = info;
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>A <c>[[link]]</c> shown as a chip. Missing targets are drawn faint and create the note when clicked.</summary>
public sealed record NoteLinkItem(string Title, string? Path, bool Exists);

/// <summary>One line of the local graph, in canvas coordinates.</summary>
public sealed record GraphEdge(Point Start, Point End);

/// <summary>One dot of the local graph. <see cref="Left"/>/<see cref="Top"/> place its fixed-width cell on the canvas.</summary>
public sealed record GraphNode(string? Path, string Title, double Left, double Top, double Diameter, bool IsCenter);

/// <summary>
/// Notes: Markdown files in <c>Notes\</c> (Obsidian's layout). Typing saves on its own after a short
/// pause, and whenever you switch notes or pages. The file name is the title, as in Obsidian.
/// <c>[[wiki links]]</c> connect notes; the right rail shows outgoing links, linked mentions, tags and a
/// small graph of the current note's neighbourhood.
/// </summary>
public sealed partial class NotesPageViewModel : ViewModelBase
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    // Local-graph canvas. The center note sits in the middle; direct neighbours ring it.
    private const double GraphWidth = 280;
    private const double GraphHeight = 232;
    private const double GraphRadius = 74;
    private const double CellWidth = 84;
    private const double CenterDiameter = 16;
    private const double NodeDiameter = 11;

    private readonly WorkspaceSession _s;
    private readonly WorkspaceViewModel _workspace;
    private CancellationTokenSource? _saveCts;
    private string? _currentPath;
    private string _savedText = string.Empty;
    private bool _loading;

    public NotesPageViewModel(WorkspaceSession session, WorkspaceViewModel workspace)
    {
        _s = session;
        _workspace = workspace;
        Refresh();
    }

    public ObservableCollection<NoteItemViewModel> Notes { get; } = [];

    /// <summary>Outgoing <c>[[links]]</c> from the current note.</summary>
    public ObservableCollection<NoteLinkItem> Links { get; } = [];

    /// <summary>Notes that link to the current note.</summary>
    public ObservableCollection<NoteItemViewModel> Backlinks { get; } = [];

    /// <summary><c>#tags</c> in the current note.</summary>
    public ObservableCollection<string> Tags { get; } = [];

    public ObservableCollection<GraphNode> GraphNodes { get; } = [];
    public ObservableCollection<GraphEdge> GraphEdges { get; } = [];

    public bool HasNotes => Notes.Count > 0;

    public bool HasSelection => Selected is not null;

    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public bool HasLinks => Links.Count > 0;

    public bool HasBacklinks => Backlinks.Count > 0;

    public bool HasTags => Tags.Count > 0;

    /// <summary>A graph is worth showing only once the current note actually connects to something.</summary>
    public bool HasGraph => GraphNodes.Count > 1;

    public bool HasConnections => HasLinks || HasBacklinks || HasTags;

    public bool HasActiveTag => !string.IsNullOrEmpty(ActiveTag);

    [ObservableProperty]
    public partial NoteItemViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial string WordCountText { get; set; } = string.Empty;

    /// <summary>When set, the note list is filtered to notes carrying this tag.</summary>
    [ObservableProperty]
    public partial string? ActiveTag { get; set; }

    /// <summary>A new note wants its title typed; a daily note wants the cursor at the end.</summary>
    public event EventHandler? TitleFocusRequested;

    public event EventHandler? EditorFocusRequested;

    public void Refresh()
    {
        Flush();
        var keep = _currentPath;
        IReadOnlyList<NoteInfo> list;
        try
        {
            if (ActiveTag is { } tag)
            {
                list = _s.Notes.ByTag(tag);
            }
            else
            {
                list = Search.Trim().Length > 0 ? _s.Notes.Search(Search) : _s.Notes.List();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            list = [];
        }

        Notes.Clear();
        foreach (var note in list)
        {
            Notes.Add(new NoteItemViewModel(note));
        }

        Selected = Notes.FirstOrDefault(n => keep is not null && PathGuard.AreSame(n.Path, keep)) ?? Notes.FirstOrDefault();
        OnPropertyChanged(nameof(HasNotes));
        RefreshConnections();
    }

    public void Select(string path)
    {
        var item = Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path));
        if (item is null)
        {
            _loading = true;
            Search = string.Empty;
            ActiveTag = null;
            _loading = false;
            Refresh();
            item = Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path));
        }

        Selected = item ?? Selected;
    }

    /// <summary>Writes unsaved typing now (switching notes or pages, closing the editor).</summary>
    public void Flush()
    {
        _saveCts?.Cancel();
        if (_currentPath is null || string.Equals(Text, _savedText, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _s.Notes.Write(_currentPath, Text);
            _savedText = Text;
            var path = _currentPath;
            if (Notes.FirstOrDefault(n => PathGuard.AreSame(n.Path, path)) is { } item && _s.Notes.Describe(path) is { } info)
            {
                item.Update(info);
            }
        }
        catch (Exception ex)
        {
            Status = $"Couldn't save: {OperationReport.Describe(ex)}";
        }
    }

    partial void OnSelectedChanged(NoteItemViewModel? value)
    {
        Flush();
        _loading = true;
        try
        {
            _currentPath = value?.Path;
            Title = value?.Title ?? string.Empty;
            Text = value is null ? string.Empty : ReadSafe(value.Path);
            _savedText = Text;
            Status = null;
        }
        finally
        {
            _loading = false;
        }

        UpdateWordCount();
        RefreshConnections();
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnTextChanged(string value)
    {
        UpdateWordCount();
        if (_loading || _currentPath is null)
        {
            return;
        }

        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = SaveLaterAsync(cts.Token);
    }

    partial void OnSearchChanged(string value)
    {
        if (!_loading)
        {
            Refresh();
        }
    }

    partial void OnActiveTagChanged(string? value)
    {
        OnPropertyChanged(nameof(HasActiveTag));
        if (!_loading)
        {
            Refresh();
        }
    }

    partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [RelayCommand]
    private void CommitTitle()
    {
        if (_currentPath is null || Selected is not { } note)
        {
            return;
        }

        var title = Title.Trim();
        if (title.Length == 0 || string.Equals(title, note.Title, StringComparison.Ordinal))
        {
            Title = note.Title;
            return;
        }

        Flush();
        try
        {
            _currentPath = _s.Notes.Rename(_currentPath, title);
            if (_s.Notes.Describe(_currentPath) is { } info)
            {
                note.Update(info);
            }

            Title = note.Title;
            Status = null;
            RefreshConnections();
        }
        catch (Exception ex)
        {
            Title = note.Title;
            Status = OperationReport.Describe(ex);
        }
    }

    [RelayCommand]
    public void NewNote()
    {
        try
        {
            Flush();
            var path = _s.Notes.Create();
            Select(path);
            TitleFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = OperationReport.Describe(ex);
        }
    }

    [RelayCommand]
    public void Today()
    {
        try
        {
            Flush();
            var path = _s.Notes.OpenDaily(DateOnly.FromDateTime(DateTime.Now));
            Select(path);
            EditorFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = OperationReport.Describe(ex);
        }
    }

    /// <summary>Opens a linked note, creating it first if it doesn't exist yet (Obsidian's click-to-create).</summary>
    [RelayCommand]
    private void OpenLink(NoteLinkItem? link)
    {
        if (link is null)
        {
            return;
        }

        try
        {
            Flush();
            var path = link.Path ?? _s.Notes.Create(link.Title);
            Select(path);
            if (!link.Exists)
            {
                EditorFocusRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Status = OperationReport.Describe(ex);
        }
    }

    /// <summary>Opens a note by view model (linked mentions and the note list use this).</summary>
    [RelayCommand]
    private void OpenNote(NoteItemViewModel? note)
    {
        if (note is not null)
        {
            Select(note.Path);
        }
    }

    /// <summary>Clicking a graph dot jumps to that note.</summary>
    [RelayCommand]
    private void OpenGraphNode(GraphNode? node)
    {
        if (node?.Path is { } path)
        {
            Select(path);
        }
    }

    /// <summary>Clicking a tag filters the list to it; clicking the same tag again clears the filter.</summary>
    [RelayCommand]
    private void ToggleTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        ActiveTag = string.Equals(ActiveTag, tag, StringComparison.OrdinalIgnoreCase) ? null : tag;
    }

    [RelayCommand]
    private void ClearTag() => ActiveTag = null;

    [RelayCommand]
    private async Task DeleteNote()
    {
        if (Selected is not { } note)
        {
            return;
        }

        var ok = await _s.Dialogs.ConfirmAsync(
            "Move to Trash?",
            $"Move \"{note.Title}\" to Trash? You can restore it from Settings > Trash.",
            "Move to Trash",
            danger: true);
        if (!ok)
        {
            return;
        }

        Flush();
        var report = await _s.Notes.MoveToTrashAsync(note.Path);
        _currentPath = null;
        Refresh();
        if (report.HasErrors)
        {
            Status = report.Errors[0].Message;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Flush();
        if (Directory.Exists(_s.Notes.Folder))
        {
            _workspace.OpenInFiles(_s.Notes.Folder);
        }
        else
        {
            Status = "No notes yet.";
        }
    }

    private async Task SaveLaterAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SaveDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Flush();
        RefreshConnections();
    }

    private void UpdateWordCount()
    {
        var text = Text;
        var words = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }

        WordCountText = _currentPath is null
            ? string.Empty
            : $"{words} {(words == 1 ? "word" : "words")}  ·  {text.Length} chars";
    }

    /// <summary>Recomputes the right rail (links, mentions, tags) and the local graph for the current note.</summary>
    private void RefreshConnections()
    {
        Links.Clear();
        Backlinks.Clear();
        Tags.Clear();
        GraphNodes.Clear();
        GraphEdges.Clear();

        if (_currentPath is { } path)
        {
            try
            {
                var outgoing = _s.Notes.OutgoingLinks(path);
                foreach (var link in outgoing)
                {
                    Links.Add(new NoteLinkItem(link.Title, link.Path, link.Exists));
                }

                foreach (var note in _s.Notes.Backlinks(path))
                {
                    Backlinks.Add(new NoteItemViewModel(note));
                }

                foreach (var tag in NotesService.ParseTags(Text))
                {
                    Tags.Add("#" + tag);
                }

                BuildGraph(path, outgoing);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A note that vanished mid-edit just shows no connections.
            }
        }

        OnPropertyChanged(nameof(HasLinks));
        OnPropertyChanged(nameof(HasBacklinks));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasGraph));
        OnPropertyChanged(nameof(HasConnections));
    }

    private void BuildGraph(string path, IReadOnlyList<NoteLink> outgoing)
    {
        const double cx = GraphWidth / 2;
        const double cy = GraphHeight / 2;

        var neighbours = new List<(string Path, string Title)>();
        var seen = new HashSet<string>(PathGuard.Comparer);
        foreach (var link in outgoing)
        {
            if (link.Path is { } p && seen.Add(p))
            {
                neighbours.Add((p, link.Title));
            }
        }

        foreach (var note in Backlinks)
        {
            if (seen.Add(note.Path))
            {
                neighbours.Add((note.Path, note.Title));
            }
        }

        GraphNodes.Add(new GraphNode(path, Selected?.Title ?? Path.GetFileNameWithoutExtension(path),
            cx - CellWidth / 2, cy - CenterDiameter / 2, CenterDiameter, IsCenter: true));

        var count = neighbours.Count;
        for (var i = 0; i < count; i++)
        {
            var angle = (-Math.PI / 2) + (2 * Math.PI * i / count);
            var x = cx + (GraphRadius * Math.Cos(angle));
            var y = cy + (GraphRadius * Math.Sin(angle));
            GraphEdges.Add(new GraphEdge(new Point(cx, cy), new Point(x, y)));
            GraphNodes.Add(new GraphNode(neighbours[i].Path, neighbours[i].Title,
                x - (CellWidth / 2), y - (NodeDiameter / 2), NodeDiameter, IsCenter: false));
        }
    }

    private string ReadSafe(string path)
    {
        try
        {
            return _s.Notes.Read(path);
        }
        catch (Exception ex)
        {
            Status = $"Couldn't open: {OperationReport.Describe(ex)}";
            return string.Empty;
        }
    }
}
